using GestionUtilisateurs.Shared.Models;
using GestionUtilisateurs.Shared.Services;

namespace GestionUtilisateurs.Online.Web.Services;

public enum OnlineSessionState
{
    Starting,
    NotConfigured,
    SignedOut,
    Loading,
    Pending,
    Refused,
    Disabled,
    NoMemberRecord,
    Ready,
    Error
}

/// <summary>Résumé pour les administrateurs : demandes de compte et alertes de capacité.</summary>
public sealed class AdminStatus
{
    public int OpenAlerts { get; set; }
    public string Level { get; set; } = "ok";
    public int PendingAccounts { get; set; }
}

/// <summary>
/// Cycle de vie de la connexion en ligne : compte Supabase, profil validé,
/// chargement des données partagées, puis ouverture de la session DataStore.
/// </summary>
public sealed class OnlineSession : IDisposable
{
    private static readonly TimeSpan RefreshEvery = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan AdminStatusEvery = TimeSpan.FromMinutes(5);

    private readonly OnlineApi _api;
    private readonly OnlineStorageProvider _storage;
    private readonly DataStore _store;
    private readonly AppState _state;
    private System.Threading.Timer? _timer;
    private DateTime _lastAdminStatus = DateTime.MinValue;
    private DateTime _lastNetworkError = DateTime.MinValue;
    private bool _storeInitialized;
    private bool _signingOut;
    private bool _refreshing;

    public OnlineSession(OnlineApi api, OnlineStorageProvider storage, DataStore store, AppState state)
    {
        _api = api;
        _storage = storage;
        _store = store;
        _state = state;
        _storage.ConflictResolved += OnConflict;
        _storage.Merged += OnMerged;
        _storage.SaveRejected += OnRejected;
        _storage.SaveFailed += OnSaveFailed;
        _store.SessionChanged += OnStoreSessionChanged;
    }

    public OnlineSessionState State { get; private set; } = OnlineSessionState.Starting;
    public OnlineProfile? Profile { get; private set; }
    public string? Error { get; private set; }
    public AdminStatus Admin { get; private set; } = new();

    /// <summary>Changement d'état (connexion, chargement, alertes…).</summary>
    public event Action? Changed;

    public bool IsAdmin => Profile != null && Roles.IsAdmin(Profile.Role) && State == OnlineSessionState.Ready;

    public async Task InitializeAsync()
    {
        if (!_api.IsConfigured || !await _api.InitAsync())
        {
            SetState(OnlineSessionState.NotConfigured);
            return;
        }
        var user = await _api.GetSessionAsync();
        if (user == null) SetState(OnlineSessionState.SignedOut);
        else await EnterAsync(user);
    }

    public async Task<string?> SignInAsync(string email, string password)
    {
        var res = await _api.SignInAsync(email, password);
        if (!res.Ok) return res.Error ?? "Connexion impossible.";
        var user = await _api.GetSessionAsync();
        if (user == null) return "Connexion impossible.";
        await EnterAsync(user);
        return null;
    }

    /// <summary>Inscription : le compte reste en attente jusqu'à la validation par un administrateur.</summary>
    public async Task<string?> SignUpAsync(string email, string password, string prenom, string nom, string telephone)
    {
        var res = await _api.SignUpAsync(email, password, prenom, nom, telephone);
        if (!res.Ok) return res.Error ?? "Inscription impossible.";
        if (!res.HasSession)
            return "Inscription enregistrée, mais la confirmation par e-mail est activée dans Supabase. "
                + "Désactivez-la (voir le guide de mise en ligne) pour que la validation se fasse par l'administrateur.";
        var user = await _api.GetSessionAsync();
        if (user != null) await EnterAsync(user);
        return null;
    }

    /// <summary>Vérifie à nouveau le statut du compte (bouton « Actualiser » de l'écran d'attente).</summary>
    public async Task RecheckAsync()
    {
        var user = await _api.GetSessionAsync();
        if (user == null) SetState(OnlineSessionState.SignedOut);
        else await EnterAsync(user);
    }

    public async Task SignOutAsync()
    {
        if (_signingOut) return;
        _signingOut = true;
        try
        {
            StopTimer();
            if (_store.IsAuthenticated) _store.Logout();
            await _storage.FlushAsync();
            await _api.SignOutAsync();
            _storage.Clear();
            Profile = null;
            Admin = new AdminStatus();
            _state.ResetNavigation();
            SetState(OnlineSessionState.SignedOut);
        }
        finally
        {
            _signingOut = false;
        }
    }

    public async Task<string?> ChangePasswordAsync(string password)
    {
        var res = await _api.UpdatePasswordAsync(password);
        return res.Ok ? null : res.Error ?? "Modification impossible.";
    }

    /// <summary>Met à jour le résumé administrateur (badges et bandeau de saturation).</summary>
    public async Task RefreshAdminStatusAsync()
    {
        if (State != OnlineSessionState.Ready || Profile == null || !Roles.IsAdmin(Profile.Role) && Profile.Role != Roles.Gestionnaire)
            return;
        var res = await _api.RpcAsync<AdminStatusDto>("capacity_status");
        _lastAdminStatus = DateTime.UtcNow;
        if (res.Data == null) return;
        Admin = new AdminStatus
        {
            OpenAlerts = res.Data.open_alerts,
            Level = res.Data.level ?? "ok",
            PendingAccounts = res.Data.pending_accounts
        };
        Changed?.Invoke();
    }

    private sealed class AdminStatusDto
    {
        public int open_alerts { get; set; }
        public string? level { get; set; }
        public int pending_accounts { get; set; }
    }

    private async Task EnterAsync(OnlineAuthUser user)
    {
        SetState(OnlineSessionState.Loading);
        var profile = await _api.RpcAsync<OnlineProfile>("my_profile");
        if (!profile.Ok || profile.Data == null)
        {
            Error = profile.Error ?? "Profil introuvable. Le schéma Supabase est-il installé ?";
            SetState(OnlineSessionState.Error);
            return;
        }
        Profile = profile.Data;
        switch (Profile.Statut)
        {
            case OnlineStatuts.EnAttente: SetState(OnlineSessionState.Pending); return;
            case OnlineStatuts.Refuse: SetState(OnlineSessionState.Refused); return;
            case OnlineStatuts.Desactive: SetState(OnlineSessionState.Disabled); return;
        }

        await _api.OpenCacheAsync(user.Id);
        var loadError = await _storage.LoadAllAsync();
        if (loadError != null)
        {
            Error = loadError;
            SetState(OnlineSessionState.Error);
            return;
        }

        // Les écritures automatiques du chargement (normalisations) ne
        // doivent pas afficher d'erreur à un membre sans droit d'écriture.
        _storage.Quiet = true;
        try
        {
            if (!_storeInitialized)
            {
                _store.EnsureInitialized();
                _storeInitialized = true;
            }
            else
            {
                _store.Reload();
            }

            var record = _store.Users.FirstOrDefault(u => u.Id == user.Id);
            if (record == null && Roles.IsAdmin(Profile.Role))
            {
                // Premier administrateur : sa fiche membre est créée automatiquement.
                _store.UpsertOnlineMember(user.Id, Profile.Prenom, Profile.Nom, Profile.Email, Profile.Telephone, Profile.Role);
                record = _store.Users.FirstOrDefault(u => u.Id == user.Id);
            }
            if (record == null)
            {
                SetState(OnlineSessionState.NoMemberRecord);
                return;
            }
            // Le rôle du serveur fait foi (droits appliqués côté Supabase).
            record.Role = Profile.Role;

            var started = _store.StartOnlineSession(user.Id);
            if (!started.Success)
            {
                Error = started.Error;
                SetState(OnlineSessionState.Error);
                return;
            }
            await _storage.FlushAsync();
        }
        finally
        {
            _storage.Quiet = false;
        }

        SetState(OnlineSessionState.Ready);
        StartTimer();
        await RefreshAdminStatusAsync();
    }

    private void StartTimer()
    {
        StopTimer();
        _timer = new System.Threading.Timer(_ => _ = TickAsync(), null, RefreshEvery, RefreshEvery);
    }

    private void StopTimer()
    {
        _timer?.Dispose();
        _timer = null;
    }

    /// <summary>Récupère régulièrement les modifications faites par les autres membres.</summary>
    private async Task TickAsync()
    {
        if (State != OnlineSessionState.Ready || _refreshing) return;
        _refreshing = true;
        try
        {
            if (await _storage.RefreshAsync()) ReloadStore();
            if (DateTime.UtcNow - _lastAdminStatus > AdminStatusEvery) await RefreshAdminStatusAsync();
        }
        catch
        {
            // Réseau indisponible : nouvel essai au prochain passage.
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void ReloadStore()
    {
        _storage.Quiet = true;
        try { _store.Reload(); }
        finally { _storage.Quiet = false; }
        Changed?.Invoke();
    }

    private void OnConflict(string key)
    {
        ReloadStore();
        _state.NotifyWarning("Un autre membre vient de modifier ces données. Elles ont été mises à jour : vérifiez et refaites votre dernière modification si besoin.");
    }

    private void OnMerged(string key) => ReloadStore();

    private void OnRejected(string key, string message)
    {
        ReloadStore();
        if (!string.IsNullOrEmpty(message)) _state.NotifyError(message);
    }

    private void OnSaveFailed(string message)
    {
        if (DateTime.UtcNow - _lastNetworkError < TimeSpan.FromMinutes(1)) return;
        _lastNetworkError = DateTime.UtcNow;
        _state.NotifyWarning(message + " Nouvel essai automatique dans quelques secondes.");
    }

    private void OnStoreSessionChanged()
    {
        // Déconnexion depuis le menu ou après inactivité : on ferme aussi la session Supabase.
        if (State == OnlineSessionState.Ready && !_signingOut && !_store.IsAuthenticated)
            _ = SignOutAsync();
    }

    private void SetState(OnlineSessionState state)
    {
        State = state;
        Changed?.Invoke();
    }

    public void Dispose()
    {
        StopTimer();
        _storage.ConflictResolved -= OnConflict;
        _storage.Merged -= OnMerged;
        _storage.SaveRejected -= OnRejected;
        _storage.SaveFailed -= OnSaveFailed;
        _store.SessionChanged -= OnStoreSessionChanged;
    }
}
