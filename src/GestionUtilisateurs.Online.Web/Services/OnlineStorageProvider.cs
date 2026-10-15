using System.Text.Json.Nodes;
using Microsoft.JSInterop;
using GestionUtilisateurs.Shared.Services;

namespace GestionUtilisateurs.Online.Web.Services;

/// <summary>
/// Stockage de la version en ligne : DataStore lit et écrit en mémoire, puis
/// chaque collection modifiée est envoyée à Supabase en arrière-plan.
/// Si un autre membre a modifié la même collection entre-temps, la version
/// du serveur est conservée et l'application est rechargée.
/// </summary>
public class OnlineStorageProvider : IStorageProvider
{
    /// <summary>Clés propres à l'appareil, jamais envoyées au serveur.</summary>
    private static readonly HashSet<string> LocalOnlyKeys = new(StringComparer.Ordinal)
    {
        "gu2:session", "gu2:sync", "gu2:device"
    };

    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(400);

    private readonly OnlineApi _api;
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _versions = new(StringComparer.Ordinal);
    /// <summary>Dernière valeur confirmée par le serveur, pour annuler une modification refusée.</summary>
    private readonly Dictionary<string, string> _synced = new(StringComparer.Ordinal);
    private readonly HashSet<string> _dirty = new(StringComparer.Ordinal);
    /// <summary>
    /// Collections que ce compte ne peut pas lire (le serveur ne renvoie pas leur contenu) :
    /// les envoyer écraserait celles des autres avec la seule copie locale.
    /// </summary>
    private readonly HashSet<string> _nonLisibles = new(StringComparer.Ordinal);

    /// <summary>Journaux où l'on ne fait qu'ajouter : en cas de conflit, on fusionne au lieu d'avertir.</summary>
    private static readonly HashSet<string> AjoutSeulKeys = new(StringComparer.Ordinal) { "gu2:activity" };
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private CancellationTokenSource? _flushDelay;
    private Task _flushTask = Task.CompletedTask;

    private readonly IJSInProcessRuntime? _js;

    public OnlineStorageProvider(OnlineApi api, IJSRuntime js)
    {
        _api = api;
        _js = js as IJSInProcessRuntime;
    }

    /// <summary>Préférences personnelles : gardées dans le navigateur (localStorage), jamais envoyées.</summary>
    private static bool IsBrowserKey(string key) => key.StartsWith(DataStore.K_PREFS_PREFIX, StringComparison.Ordinal);

    /// <summary>Une collection a été refusée par le serveur (droits insuffisants, taille…).</summary>
    public event Action<string, string>? SaveRejected;

    /// <summary>Un autre membre a modifié une collection : les données ont été mises à jour.</summary>
    public event Action<string>? ConflictResolved;
    /// <summary>Journal fusionné avec celui du serveur : à recharger sans alerte.</summary>
    public event Action<string>? Merged;

    /// <summary>L'envoi a échoué (réseau) : il sera retenté.</summary>
    public event Action<string>? SaveFailed;

    /// <summary>Pendant le chargement initial, les écritures automatiques refusées sont ignorées sans message.</summary>
    public bool Quiet { get; set; }

    public bool HasPendingChanges { get { lock (_dirty) return _dirty.Count > 0; } }

    public string? Get(string key)
    {
        if (IsBrowserKey(key))
        {
            try { return _js?.Invoke<string?>("localStorage.getItem", key); }
            catch (JSException) { return null; }
        }
        return _values.TryGetValue(key, out var v) ? v : null;
    }

    public void Set(string key, string value)
    {
        if (IsBrowserKey(key))
        {
            try { _js?.InvokeVoid("localStorage.setItem", key, value); }
            catch (JSException) { }
            return;
        }
        if (_values.TryGetValue(key, out var previous) && SameJson(previous, value)) return;
        _values[key] = value;
        if (LocalOnlyKeys.Contains(key) || _nonLisibles.Contains(key)) return;
        lock (_dirty) _dirty.Add(key);
        ScheduleFlush();
    }

    public void Remove(string key)
    {
        _values.Remove(key);
    }

    /// <summary>Remplace tout le contenu par les données du serveur (connexion).</summary>
    public async Task<string?> LoadAllAsync()
    {
        var result = await _api.LoadAsync(all: true);
        if (result.Error != null) return result.Error;
        _values.Clear();
        _versions.Clear();
        _synced.Clear();
        _nonLisibles.Clear();
        lock (_dirty) _dirty.Clear();
        foreach (var row in result.Rows)
        {
            _values[row.Key] = row.Value;
            _synced[row.Key] = row.Value;
            _versions[row.Key] = row.Version;
        }
        return null;
    }

    /// <summary>
    /// Récupère les collections modifiées par les autres membres.
    /// Renvoie true si quelque chose a changé.
    /// </summary>
    public async Task<bool> RefreshAsync()
    {
        if (HasPendingChanges) return false;
        await _saveLock.WaitAsync();
        try
        {
            var result = await _api.LoadAsync(all: false);
            if (result.Error != null) return false;
            var changed = false;
            foreach (var row in result.Rows)
            {
                if (HasPendingChanges) break;
                _values[row.Key] = row.Value;
                _synced[row.Key] = row.Value;
                _versions[row.Key] = row.Version;
                changed = true;
            }
            foreach (var key in result.Removed)
            {
                changed |= _values.Remove(key);
                _synced.Remove(key);
                _versions.Remove(key);
            }
            return changed;
        }
        finally
        {
            _saveLock.Release();
        }
    }

    /// <summary>Attend la fin des envois en cours (avant une déconnexion).</summary>
    public async Task FlushAsync()
    {
        _flushDelay?.Cancel();
        await SaveDirtyAsync();
        await _flushTask;
    }

    public void Clear()
    {
        _flushDelay?.Cancel();
        _values.Clear();
        _versions.Clear();
        _synced.Clear();
        lock (_dirty) _dirty.Clear();
    }

    private void ScheduleFlush()
    {
        _flushDelay?.Cancel();
        var cts = new CancellationTokenSource();
        _flushDelay = cts;
        _flushTask = Task.Delay(SaveDelay, cts.Token).ContinueWith(async t =>
        {
            if (t.IsCanceled) return;
            await SaveDirtyAsync();
        }, TaskScheduler.Current).Unwrap();
    }

    private async Task SaveDirtyAsync()
    {
        await _saveLock.WaitAsync();
        try
        {
            while (true)
            {
                string key;
                lock (_dirty)
                {
                    if (_dirty.Count == 0) return;
                    key = _dirty.First();
                    _dirty.Remove(key);
                }
                if (!_values.TryGetValue(key, out var json)) continue;
                _versions.TryGetValue(key, out var version);
                var res = await _api.SaveAsync(key, json, version);
                switch (res.Status)
                {
                    case "ok":
                        _versions[key] = res.Version;
                        _synced[key] = json;
                        break;
                    case "conflict":
                        _versions[key] = res.Version;
                        if (res.Value == null)
                        {
                            // Collection illisible pour ce compte : ne plus jamais l'envoyer.
                            _nonLisibles.Add(key);
                            _values.Remove(key);
                            _synced.Remove(key);
                            break;
                        }
                        if (AjoutSeulKeys.Contains(key) && Fusionner(res.Value, json, _synced.GetValueOrDefault(key)) is { } fusion)
                        {
                            // Journal : on ajoute nos nouvelles lignes à celles du serveur, sans alerte.
                            _synced[key] = res.Value;
                            _values[key] = fusion;
                            lock (_dirty) _dirty.Add(key);
                            Merged?.Invoke(key);
                            break;
                        }
                        // Un autre membre a enregistré avant nous : sa version gagne.
                        _values[key] = res.Value;
                        _synced[key] = res.Value;
                        ConflictResolved?.Invoke(key);
                        break;
                    case "denied":
                    case "too_large":
                        // Modification refusée : on revient à la version du serveur.
                        if (_synced.TryGetValue(key, out var server)) _values[key] = server;
                        else _values.Remove(key);
                        SaveRejected?.Invoke(key, Quiet ? "" : res.Message ?? "Modification refusée par le serveur.");
                        break;
                    default:
                        // Erreur réseau : on garde la modification et on réessaie plus tard.
                        lock (_dirty) _dirty.Add(key);
                        SaveFailed?.Invoke(res.Message ?? "Enregistrement impossible.");
                        _ = RetryLaterAsync();
                        return;
                }
            }
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private async Task RetryLaterAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(15));
        await SaveDirtyAsync();
    }

    /// <summary>
    /// Fusion d'un journal (tableau d'objets avec « id », plus récents en tête) :
    /// lignes du serveur + nos lignes qui n'avaient pas encore été envoyées.
    /// </summary>
    private static string? Fusionner(string serveur, string local, string? dejaEnvoye)
    {
        try
        {
            if (JsonNode.Parse(serveur) is not JsonArray srv || JsonNode.Parse(local) is not JsonArray loc) return null;
            static string? Id(JsonNode? n) => n?["id"]?.GetValue<string>();
            var connus = new HashSet<string>(srv.Select(Id).OfType<string>(), StringComparer.Ordinal);
            if (dejaEnvoye != null && JsonNode.Parse(dejaEnvoye) is JsonArray env)
                foreach (var id in env.Select(Id).OfType<string>()) connus.Add(id);
            var nouveaux = loc.Where(n => Id(n) is { } id && !connus.Contains(id)).Select(n => n!.DeepClone()).ToList();
            var resultat = new JsonArray();
            foreach (var n in nouveaux) resultat.Add(n);
            foreach (var n in srv) resultat.Add(n?.DeepClone());
            while (resultat.Count > 500) resultat.RemoveAt(resultat.Count - 1);
            return resultat.ToJsonString();
        }
        catch
        {
            return null;
        }
    }

    private static bool SameJson(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.Ordinal)) return true;
        try
        {
            return JsonNode.DeepEquals(JsonNode.Parse(a), JsonNode.Parse(b));
        }
        catch
        {
            return false;
        }
    }
}
