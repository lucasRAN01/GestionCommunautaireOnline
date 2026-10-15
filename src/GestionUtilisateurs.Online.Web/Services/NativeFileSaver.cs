namespace GestionUtilisateurs.Online.Web.Services;

/// <summary>Téléchargement et ouverture de fichiers dans le navigateur (même API que le service Android).</summary>
public class NativeFileSaver
{
    private readonly WebFiles _files;

    public NativeFileSaver(WebFiles files) => _files = files;

    public Task<bool> OpenExternalAsync(string fileName, byte[] content, string mime)
        => _files.OpenAsync(fileName, content, string.IsNullOrWhiteSpace(mime) ? "application/octet-stream" : mime);

    public Task<bool> OpenWithChooserAsync(string fileName, byte[] content, string mime)
        => _files.SaveAsync(fileName, content, mime);

    /// <summary>Télécharge le fichier ; renvoie le nom affiché (dossier Téléchargements du navigateur).</summary>
    public async Task<string?> SaveAsync(string fileName, byte[] content, string mime)
        => await _files.SaveAsync(fileName, content, mime) ? "Téléchargements/" + fileName : null;
}
