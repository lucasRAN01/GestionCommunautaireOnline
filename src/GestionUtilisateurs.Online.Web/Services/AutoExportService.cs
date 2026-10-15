namespace GestionUtilisateurs.Online.Web.Services;

/// <summary>
/// Export automatique mensuel de toutes les données locales (100 % hors ligne).
/// Vérifie au démarrage si le mois courant a déjà été sauvegardé, puis écrit
/// la sauvegarde complète (JSON) et la liste des utilisateurs (Excel) dans
/// le dossier Téléchargements de l'appareil.
/// </summary>
public class AutoExportService
{
    private readonly Shared.Services.DataStore _store;
    private readonly NativeFileSaver _saver;
    private readonly Shared.Services.AppState _state;

    public AutoExportService(
        Shared.Services.DataStore store,
        NativeFileSaver saver,
        Shared.Services.AppState state)
    {
        _store = store;
        _saver = saver;
        _state = state;
    }

    /// <summary>Dernier résultat (chemin affiché ou erreur) pour l'interface.</summary>
    public string? LastResult { get; private set; }

    /// <summary>
    /// Exécute l'export mensuel si dû. Retourne true si un export a été réalisé.
    /// Ne lève jamais d'exception (application hors ligne).
    /// </summary>
    /// <summary>
    /// Version en ligne : pas d'export automatique dans le navigateur (il
    /// déclencherait des téléchargements inattendus). Les données sont
    /// sauvegardées chaque semaine côté serveur (workflow « Sauvegarde ») ;
    /// l'export manuel reste disponible dans les paramètres.
    /// </summary>
    public Task<bool> ExportIfDueAsync() => Task.FromResult(false);

    public async Task<bool> ExportNowAsync(bool silent = false)
    {
        try
        {
            var month = DateTime.Now.ToString("yyyy-MM");
            var stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

            // 1) Sauvegarde complète JSON (toutes les collections).
            var json = _store.ExportFullBackupJson();
            var jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);
            var jsonName = "sauvegarde-complete-" + stamp + ".json";
            var jsonPath = await _saver.SaveAsync(jsonName, jsonBytes, "application/json");

            // 2) Liste des utilisateurs (Excel, format d'import).
            var excelBytes = _store.ExportUsersExcel();
            var excelName = "utilisateurs-" + stamp + ".xlsx";
            var excelPath = await _saver.SaveAsync(
                excelName, excelBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

            if (jsonPath == null && excelPath == null)
            {
                LastResult = null;
                if (!silent) _state.NotifyError("Export automatique impossible : stockage inaccessible.");
                return false;
            }

            _store.MarkAutoExportDone();
            var label = "Export automatique mensuel (" + month + ") : "
                + (jsonPath ?? jsonName) + " + " + (excelPath ?? excelName);
            LastResult = label;
            if (!silent) _state.NotifySuccess("Sauvegarde mensuelle enregistrée dans Téléchargements.");
            return true;
        }
        catch (Exception ex)
        {
            LastResult = null;
            if (!silent) _state.NotifyError("Export automatique impossible : " + ex.Message);
            return false;
        }
    }
}
