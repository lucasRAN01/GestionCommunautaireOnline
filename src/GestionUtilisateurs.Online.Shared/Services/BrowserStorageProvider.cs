using Microsoft.JSInterop;

namespace GestionUtilisateurs.Shared.Services;

/// <summary>
/// IStorageProvider basé sur le localStorage du navigateur (hors ligne).
/// Utilise l'interopérabilité JS synchrone (IJSInProcessRuntime) disponible
/// en WebAssembly lorsque AllowInProcessInterop=true (voir .csproj).
/// </summary>
public class BrowserStorageProvider : IStorageProvider
{
    private readonly IJSInProcessRuntime _js;

    public BrowserStorageProvider(IJSRuntime js)
    {
        _js = (IJSInProcessRuntime)js;
    }

    public string? Get(string key) => _js.Invoke<string?>("guStorage.get", key);

    public void Set(string key, string value) => _js.InvokeVoid("guStorage.set", key, value);

    public void Remove(string key) => _js.InvokeVoid("guStorage.remove", key);
}