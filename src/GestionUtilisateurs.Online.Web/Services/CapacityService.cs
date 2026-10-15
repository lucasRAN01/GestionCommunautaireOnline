using System.Text.Json.Serialization;
using GestionUtilisateurs.Shared.Services;

namespace GestionUtilisateurs.Online.Web.Services;

/// <summary>Rapport de capacité Supabase (fonction SQL get_capacity).</summary>
public sealed class CapacityReport
{
    [JsonPropertyName("db_bytes")] public long DbBytes { get; set; }
    [JsonPropertyName("storage_bytes")] public long StorageBytes { get; set; }
    [JsonPropertyName("storage_files")] public int StorageFiles { get; set; }
    [JsonPropertyName("members_active")] public int MembersActive { get; set; }
    [JsonPropertyName("members_pending")] public int MembersPending { get; set; }
    [JsonPropertyName("settings")] public CapacitySettings Settings { get; set; } = new();
    [JsonPropertyName("collections")] public List<CapacityCollection> Collections { get; set; } = new();
    [JsonPropertyName("history")] public List<CapacityPoint> History { get; set; } = new();
    [JsonPropertyName("alerts")] public List<CapacityAlert> Alerts { get; set; } = new();

    public double DbPercent => Percent(DbBytes, Settings.DbLimitBytes);
    public double StoragePercent => Percent(StorageBytes, Settings.StorageLimitBytes);

    public string LevelOf(double percent) =>
        percent >= Settings.CriticalPct ? "critique" : percent >= Settings.WarningPct ? "attention" : "ok";

    private static double Percent(long used, long limit) => limit <= 0 ? 0 : Math.Round(100.0 * used / limit, 1);
}

public sealed class CapacitySettings
{
    [JsonPropertyName("warning_pct")] public int WarningPct { get; set; } = 70;
    [JsonPropertyName("critical_pct")] public int CriticalPct { get; set; } = 90;
    [JsonPropertyName("db_limit_bytes")] public long DbLimitBytes { get; set; } = 500L * 1024 * 1024;
    [JsonPropertyName("storage_limit_bytes")] public long StorageLimitBytes { get; set; } = 1024L * 1024 * 1024;
}

public sealed class CapacityCollection
{
    [JsonPropertyName("key")] public string Key { get; set; } = "";
    [JsonPropertyName("bytes")] public long Bytes { get; set; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class CapacityPoint
{
    [JsonPropertyName("day")] public DateTime Day { get; set; }
    [JsonPropertyName("db_bytes")] public long DbBytes { get; set; }
    [JsonPropertyName("storage_bytes")] public long StorageBytes { get; set; }
}

public sealed class CapacityAlert
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; set; }
    [JsonPropertyName("level")] public string Level { get; set; } = "";
    [JsonPropertyName("metric")] public string Metric { get; set; } = "";
    [JsonPropertyName("percent")] public double Percent { get; set; }
    [JsonPropertyName("message")] public string Message { get; set; } = "";
    [JsonPropertyName("acknowledged_at")] public DateTimeOffset? AcknowledgedAt { get; set; }
}

public sealed class UnusedFile
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("size")] public long Size { get; set; }
}

/// <summary>
/// Surveillance de l'espace utilisé dans l'offre gratuite Supabase et
/// alertes de saturation pour les administrateurs.
/// </summary>
public class CapacityService
{
    private readonly OnlineApi _api;

    public CapacityService(OnlineApi api) => _api = api;

    /// <summary>Prend une mesure (crée une alerte si un seuil est franchi) puis renvoie le rapport.</summary>
    public async Task<RpcResult<CapacityReport>> MeasureAsync()
    {
        var snap = await _api.RpcAsync<object>("record_capacity_snapshot", new { p_source = "app" });
        if (!snap.Ok) return new RpcResult<CapacityReport> { Error = snap.Error };
        return await _api.RpcAsync<CapacityReport>("get_capacity");
    }

    public Task<RpcResult<object>> AcknowledgeAsync(long alertId)
        => _api.RpcAsync<object>("acknowledge_alert", new { p_id = alertId });

    public Task<RpcResult<object>> SaveThresholdsAsync(int warning, int critical)
        => _api.RpcAsync<object>("set_capacity_settings", new { p_warning = warning, p_critical = critical });

    public Task<RpcResult<List<UnusedFile>>> UnusedFilesAsync()
        => _api.RpcAsync<List<UnusedFile>>("unused_files");

    public async Task<string?> DeleteFilesAsync(IEnumerable<UnusedFile> files)
    {
        var names = files.Select(f => f.Name).ToList();
        for (var i = 0; i < names.Count; i += 100)
        {
            var res = await _api.RemoveFilesAsync(names.Skip(i).Take(100));
            if (!res.Ok) return res.Error ?? "Suppression impossible.";
        }
        return null;
    }

    /// <summary>
    /// Nombre de jours avant d'atteindre la limite, d'après l'évolution des
    /// 30 derniers jours (null si l'usage ne progresse pas ou manque d'historique).
    /// </summary>
    public static int? DaysUntilFull(IReadOnlyList<CapacityPoint> history, Func<CapacityPoint, long> value, long current, long limit)
    {
        var recent = history.Where(p => p.Day >= DateTime.Today.AddDays(-30)).OrderBy(p => p.Day).ToList();
        if (recent.Count < 2) return null;
        var first = recent[0];
        var last = recent[^1];
        var days = (last.Day - first.Day).TotalDays;
        if (days < 3) return null;
        var perDay = (value(last) - value(first)) / days;
        if (perDay <= 0) return null;
        var remaining = limit - current;
        if (remaining <= 0) return 0;
        return (int)Math.Ceiling(remaining / perDay);
    }

    public static string CollectionLabel(string key)
    {
        var name = key.StartsWith("gu2:", StringComparison.Ordinal) ? key[4..] : key;
        if (name == "archive") name = "archives";
        var set = DataStore.ManagedDataSets.FirstOrDefault(d => d.Key == name);
        return set?.Label ?? name;
    }

    public static string FormatSize(long bytes)
    {
        if (bytes < 1024) return bytes + " o";
        if (bytes < 1024L * 1024) return Math.Round(bytes / 1024.0, 0) + " Ko";
        if (bytes < 1024L * 1024 * 1024) return Math.Round(bytes / (1024.0 * 1024), 1).ToString("0.#") + " Mo";
        return Math.Round(bytes / (1024.0 * 1024 * 1024), 2).ToString("0.##") + " Go";
    }
}

/// <summary>Validation des comptes en ligne par les administrateurs.</summary>
public class AccountService
{
    private readonly OnlineApi _api;
    private readonly DataStore _store;

    public AccountService(OnlineApi api, DataStore store)
    {
        _api = api;
        _store = store;
    }

    public Task<RpcResult<List<OnlineProfile>>> ListAsync() => _api.RpcAsync<List<OnlineProfile>>("list_accounts");

    /// <summary>Valide le compte sur le serveur puis crée (ou réactive) sa fiche membre.</summary>
    public async Task<string?> ApproveAsync(OnlineProfile profile, string role)
    {
        var res = await _api.RpcAsync<OnlineProfile>("approve_account", new { p_id = profile.Id, p_role = role });
        if (!res.Ok) return res.Error;
        var upsert = _store.UpsertOnlineMember(profile.Id, profile.Prenom, profile.Nom, profile.Email, profile.Telephone, role);
        return upsert.Success ? null : upsert.Error;
    }

    public async Task<string?> RejectAsync(OnlineProfile profile)
        => (await _api.RpcAsync<object>("reject_account", new { p_id = profile.Id })).Error;

    public async Task<string?> DeleteRequestAsync(OnlineProfile profile)
        => (await _api.RpcAsync<object>("delete_account_request", new { p_id = profile.Id })).Error;

    public async Task<string?> SetPasswordAsync(OnlineProfile profile, string password)
        => (await _api.RpcAsync<object>("admin_set_password", new { p_id = profile.Id, p_password = password })).Error;
}
