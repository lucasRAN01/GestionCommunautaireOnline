namespace GestionUtilisateurs.Online.Web.Services;

/// <summary>
/// Choix / capture de photo dans le navigateur, renvoyée en data URL.
/// Même API que le service Android ; les photos sont compressées avant l'envoi.
/// </summary>
public class PhotoService
{
    /// <summary>Taille maximale d'une image ou d'une pièce jointe (5 Mo).</summary>
    public const long MaxFileBytes = 5L * 1024L * 1024L;
    /// <summary>Taille maximale d'une vidéo du journal multimédia (20 Mo).</summary>
    public const long MaxVideoBytes = 20L * 1024L * 1024L;
    /// <summary>Taille maximale d'un enregistrement audio importé (10 Mo).</summary>
    public const long MaxAudioBytes = 10L * 1024L * 1024L;

    private readonly WebFiles _files;

    public PhotoService(WebFiles files) => _files = files;

    /// <summary>Dernière erreur rencontrée (fichier trop volumineux, etc.).</summary>
    public string? LastError { get; private set; }

    public Task<string?> PickPhotoAsync() => PickImageAsync(null);

    public Task<string?> CapturePhotoAsync() => PickImageAsync("environment");

    public Task<PickResult?> PickAnyFileAsync() => PickAsync("", null, MaxFileBytes, "Pièce jointe", true, null);

    public Task<PickResult?> PickVideoAsync() => PickAsync("video/*", null, MaxVideoBytes, "Vidéo", false, null);

    public Task<PickResult?> CaptureVideoAsync() => PickAsync("video/*", "environment", MaxVideoBytes, "Vidéo", false, "video/mp4");

    public Task<PickResult?> PickAudioAsync() => PickAsync("audio/*", null, MaxAudioBytes, "Audio", false, "audio/mpeg");

    private async Task<string?> PickImageAsync(string? capture)
    {
        var pick = await PickAsync("image/*", capture, MaxFileBytes, "Image", true, "image/jpeg");
        return pick?.DataUrl;
    }

    private async Task<PickResult?> PickAsync(string accept, string? capture, long maxBytes, string label, bool compress, string? fallbackMime)
    {
        LastError = null;
        var file = await _files.PickAsync(accept, capture, maxBytes, label, compress);
        if (file == null) return null;
        if (!string.IsNullOrEmpty(file.Error)) { LastError = file.Error; return null; }
        if (string.IsNullOrEmpty(file.DataUrl)) return null;

        var mime = string.IsNullOrWhiteSpace(file.Type) ? fallbackMime ?? "application/octet-stream" : file.Type;
        var dataUrl = file.DataUrl;
        if (!dataUrl.StartsWith("data:" + mime, StringComparison.OrdinalIgnoreCase))
        {
            var b64 = dataUrl[(dataUrl.IndexOf(',', StringComparison.Ordinal) + 1)..];
            dataUrl = "data:" + mime + ";base64," + b64;
        }
        return new PickResult { FileName = file.Name ?? "fichier", Mime = mime, Size = file.Size, DataUrl = dataUrl };
    }

    public record PickResult
    {
        public string FileName { get; init; } = "";
        public string Mime { get; init; } = "";
        public long Size { get; init; }
        public string DataUrl { get; init; } = "";
    }
}
