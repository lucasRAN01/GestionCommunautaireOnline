using System.Text.RegularExpressions;
using GestionUtilisateurs.Shared.Services;
using Microsoft.JSInterop;

namespace GestionUtilisateurs.Online.Web.Services;

/// <summary>Résultat brut d'une sélection de fichier dans le navigateur.</summary>
public sealed class WebPickedFile
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public long Size { get; set; }
    public string? DataUrl { get; set; }
    public string? Error { get; set; }
}

/// <summary>Accès aux fichiers du navigateur (wwwroot/js/web-files.js).</summary>
public class WebFiles
{
    private readonly IJSRuntime _js;

    public WebFiles(IJSRuntime js) => _js = js;

    public async Task<WebPickedFile?> PickAsync(string accept, string? capture, long maxBytes, string label, bool compressImages)
    {
        try
        {
            var file = await _js.InvokeAsync<WebPickedFile?>("guWebFiles.pick", accept, capture, maxBytes, label, compressImages);
            if (file != null && !string.IsNullOrEmpty(file.Error)) file.Error = TraduireErreur(file.Error);
            return file;
        }
        catch (JSException)
        {
            return new WebPickedFile { Error = L.T("Impossible de lire le fichier sélectionné.") };
        }
    }

    /// <summary>Les messages de web-files.js sont écrits en français : traduits ici pour l'affichage.</summary>
    private static string TraduireErreur(string message)
    {
        if (message == "Le fichier est vide.") return L.T("Le fichier est vide.");
        var m = Regex.Match(message, @"^(.+) refusé \((.+)\) : taille maximale de (.+)\.$");
        if (!m.Success) return message;
        return L.F("{0} refusé ({1}) : taille maximale de {2}.", L.T(m.Groups[1].Value), TraduireTaille(m.Groups[2].Value), TraduireTaille(m.Groups[3].Value));
    }

    /// <summary>« 5 Mo » → « 5 MB » selon la langue (unités o, Ko, Mo, Go).</summary>
    private static string TraduireTaille(string taille)
    {
        var i = taille.LastIndexOf(' ');
        return i < 0 ? taille : taille[..(i + 1)] + L.T(taille[(i + 1)..]);
    }

    public async Task<bool> SaveAsync(string fileName, byte[] content, string mime)
    {
        try { return await _js.InvokeAsync<bool>("guWebFiles.save", fileName, Convert.ToBase64String(content), mime); }
        catch (JSException) { return false; }
    }

    public async Task<bool> OpenAsync(string fileName, byte[] content, string mime)
    {
        try { return await _js.InvokeAsync<bool>("guWebFiles.open", fileName, Convert.ToBase64String(content), mime); }
        catch (JSException) { return false; }
    }

    public async Task<bool> ShareAsync(IEnumerable<(string Name, byte[] Bytes, string Mime)> files, string title)
    {
        var payload = files.Select(f => new { name = f.Name, base64 = Convert.ToBase64String(f.Bytes), mime = f.Mime }).ToArray();
        try { return await _js.InvokeAsync<bool>("guWebFiles.share", payload, title); }
        catch (JSException) { return false; }
    }

    public static byte[] DataUrlBytes(string dataUrl)
    {
        var comma = dataUrl.IndexOf(',', StringComparison.Ordinal);
        return Convert.FromBase64String(comma >= 0 ? dataUrl[(comma + 1)..] : dataUrl);
    }
}
