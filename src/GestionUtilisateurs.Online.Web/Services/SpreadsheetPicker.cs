namespace GestionUtilisateurs.Online.Web.Services;

/// <summary>Sélection d'un classeur Excel dans le navigateur.</summary>
public class SpreadsheetPicker
{
    private readonly WebFiles _files;

    public SpreadsheetPicker(WebFiles files) => _files = files;

    public async Task<byte[]?> PickXlsxAsync()
    {
        var file = await _files.PickAsync(".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", null, 0, "Fichier Excel", false);
        if (file == null || !string.IsNullOrEmpty(file.Error) || string.IsNullOrEmpty(file.DataUrl)) return null;
        return WebFiles.DataUrlBytes(file.DataUrl);
    }
}
