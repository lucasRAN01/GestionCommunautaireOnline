using Microsoft.JSInterop;

namespace GestionUtilisateurs.Online.Web.Services;

/// <summary>Téléchargement et ouverture de fichiers dans le navigateur (même API que le service Android).</summary>
public class NativeFileSaver
{
    private readonly WebFiles _files;
    private readonly IJSRuntime _js;

    public NativeFileSaver(WebFiles files, IJSRuntime js)
    {
        _files = files;
        _js = js;
    }

    public Task<bool> OpenExternalAsync(string fileName, byte[] content, string mime)
        => _files.OpenAsync(fileName, content, string.IsNullOrWhiteSpace(mime) ? "application/octet-stream" : mime);

    public Task<bool> OpenWithChooserAsync(string fileName, byte[] content, string mime)
        => _files.SaveAsync(fileName, content, mime);

    /// <summary>Télécharge le fichier ; renvoie le nom affiché (dossier Téléchargements du navigateur).</summary>
    public async Task<string?> SaveAsync(string fileName, byte[] content, string mime)
        => await _files.SaveAsync(fileName, content, mime) ? "Téléchargements/" + fileName : null;

    /// <summary>Ouvre un média stocké en data URL (photo, vidéo, audio du journal).</summary>
    public Task<bool> OpenMediaFromDataUrlAsync(string dataUrl, string mime, string fileName)
    {
        if (string.IsNullOrWhiteSpace(dataUrl)) return Task.FromResult(false);
        var comma = dataUrl.IndexOf(',');
        if (comma < 0 || !dataUrl[..comma].Contains(";base64", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(false);
        try
        {
            var bytes = Convert.FromBase64String(dataUrl[(comma + 1)..]);
            return OpenExternalAsync(fileName, bytes, mime);
        }
        catch (FormatException) { return Task.FromResult(false); }
    }

    /// <summary>
    /// Exporte des pages HTML en PDF par la fenêtre d'impression du navigateur
    /// (choisir « Enregistrer au format PDF »), à la taille exacte de la page.
    /// </summary>
    public async Task<string?> SaveHtmlAsPdfAsync(string fileName, IReadOnlyList<string> pagesHtml, double pageWidthPt, double pageHeightPt)
    {
        if (pagesHtml == null || pagesHtml.Count == 0) return null;
        try
        {
            var ok = await _js.InvokeAsync<bool>("guWebPrint.printHtml", pagesHtml.ToArray(), fileName, pageWidthPt, pageHeightPt);
            return ok ? fileName + " (impression du navigateur > Enregistrer au format PDF)" : null;
        }
        catch (JSException) { return null; }
    }

    /// <summary>Pas de dossier à ouvrir dans un navigateur : le fichier est déjà dans Téléchargements.</summary>
    public bool OpenDownloadsFolder() => true;
}
