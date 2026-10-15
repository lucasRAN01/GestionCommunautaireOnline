using Microsoft.JSInterop;
using GestionUtilisateurs.Shared.Models;

namespace GestionUtilisateurs.Shared.Services;

/// <summary>Pont JS : thème, téléchargement de fichiers, stockage fichiers (IndexedDB).</summary>
public class JavascriptBridge
{
    private readonly IJSRuntime _js;

    public JavascriptBridge(IJSRuntime js) => _js = js;

    public async Task ApplySettingsAsync(AppSettings settings)
    {
        try
        {
            await _js.InvokeVoidAsync("guTheme.apply", settings.Theme, settings.Color, settings.AppName, settings.Logo);
        }
        catch { /* hors navigateur (tests) : sans effet */ }
    }

    public Task EnableSwipeBackAsync<T>(DotNetObjectReference<T> reference) where T : class
        => InvokeAsync("guNav.enable", reference);

    public Task DisableSwipeBackAsync()
        => InvokeAsync("guNav.disable");

    public Task EnableActivityTrackingAsync<T>(DotNetObjectReference<T> reference) where T : class
        => InvokeAsync("guSession.enable", reference);

    public Task DisableActivityTrackingAsync()
        => InvokeAsync("guSession.disable");

    public Task LockBodyScrollAsync()
        => InvokeAsync("guModal.lock");

    public Task UnlockBodyScrollAsync()
        => InvokeAsync("guModal.unlock");

    public Task DownloadTextAsync(string filename, string content, string mime)
        => InvokeAsync("guDownload.text", filename, content, mime);

    public Task DownloadDataUrlAsync(string filename, string dataUrl)
        => InvokeAsync("guDownload.dataUrl", filename, dataUrl);

    public Task IdbPutAsync(string key, string base64, string mime, string fileName, long size)
        => InvokeAsync("guFile.put", key, base64, mime, fileName, size);

    public Task<string?> IdbGetDataUrlAsync(string key)
        => _js.InvokeAsync<string?>("guFile.getDataUrl", key).AsTask();

    public Task IdbDeleteAsync(string key)
        => InvokeAsync("guFile.remove", key);

    public Task IdbDeleteManyAsync(string[] keys)
        => InvokeAsync("guFile.removeMany", keys);

    public Task<long> IdbSizeAsync()
    {
        try { return _js.InvokeAsync<long>("guFile.size").AsTask(); }
        catch { return Task.FromResult(0L); }
    }

    public async Task<long> LocalStorageBytesAsync()
    {
        try { return await _js.InvokeAsync<long>("guInfo.localBytes"); }
        catch { return 0; }
    }

    public Task ClearFileInputAsync(string id)
        => InvokeAsync("guUi.clearInput", id);

    private Task InvokeAsync(string ident, params object?[] args)
    {
        try { return _js.InvokeVoidAsync(ident, args).AsTask(); }
        catch { return Task.CompletedTask; }
    }
}
