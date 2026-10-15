using GestionUtilisateurs.Shared.Services;

namespace GestionUtilisateurs.Online.Web.Services;

/// <summary>Fichier importé dans la gestion documentaire.</summary>
public sealed record DocImport(string FileName, string Mime, long Size, byte[] Bytes);

/// <summary>Import, ouverture et partage de documents dans le navigateur (même API que le service Android).</summary>
public class DocumentService
{
    public const long MaxDocBytes = 10L * 1024L * 1024L;

    private readonly WebFiles _files;

    public DocumentService(WebFiles files) => _files = files;

    public string? LastError { get; private set; }

    public Task<DocImport?> PickDocumentAsync() => PickAsync("", null, "Fichier");

    public Task<DocImport?> PickImageAsync() => PickAsync("image/*", null, "Image");

    public async Task<DocImport?> CapturePageAsync()
    {
        var doc = await PickAsync("image/*", "environment", "Image");
        return doc == null ? null : doc with { FileName = "scan-" + DateTime.Now.ToString("dd-MM-yyyy_HH-mm-ss") + ".jpg" };
    }

    public async Task<bool> OpenAsync(string fileName, byte[] content, string mime, NativeFileSaver saver)
    {
        LastError = null;
        if (await saver.OpenExternalAsync(fileName, content, mime)) return true;
        LastError = L.T("Impossible d'ouvrir ce fichier.");
        return false;
    }

    public async Task<bool> ShareAsync(IReadOnlyList<DocImport> files, string title, NativeFileSaver _)
    {
        LastError = null;
        if (files.Count == 0) return false;
        var ok = await _files.ShareAsync(files.Select(f => (f.FileName, f.Bytes, f.Mime)), title);
        if (!ok) LastError = L.T("Le partage a échoué.");
        return ok;
    }

    private async Task<DocImport?> PickAsync(string accept, string? capture, string label)
    {
        LastError = null;
        var file = await _files.PickAsync(accept, capture, MaxDocBytes, label, true);
        if (file == null) return null;
        if (!string.IsNullOrEmpty(file.Error)) { LastError = file.Error; return null; }
        if (string.IsNullOrEmpty(file.DataUrl)) return null;
        var bytes = WebFiles.DataUrlBytes(file.DataUrl);
        var mime = string.IsNullOrWhiteSpace(file.Type) ? "application/octet-stream" : file.Type;
        return new DocImport(file.Name ?? "document", mime, bytes.Length, bytes);
    }
}
