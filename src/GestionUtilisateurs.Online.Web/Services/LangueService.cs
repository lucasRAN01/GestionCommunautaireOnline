using GestionUtilisateurs.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace GestionUtilisateurs.Online.Web.Services;

/// <summary>
/// Choix de la langue : personnel à chaque membre (gardé dans son navigateur),
/// appliqué en rechargeant l'application pour que tout l'écran soit traduit.
/// </summary>
public sealed class LangueService
{
    private readonly IJSRuntime _js;
    private readonly NavigationManager _nav;
    private readonly DataStore _store;

    public LangueService(IJSRuntime js, NavigationManager nav, DataStore store)
    {
        _js = js;
        _nav = nav;
        _store = store;
    }

    /// <summary>Change la langue (et la retient pour le membre connecté), puis recharge l'écran.</summary>
    public async Task ChoisirAsync(string code)
    {
        code = L.Normaliser(code);
        var me = _store.GetCurrentUser()?.Identifiant;
        if (!string.IsNullOrWhiteSpace(me)) _store.SetLangue(me, code);
        if (code == L.Langue) return;
        try { await _js.InvokeVoidAsync("guLangue.recharger", code); } catch { }
        _nav.Refresh(forceReload: true);
    }

    /// <summary>
    /// Après la connexion : applique la langue du membre, ou lui attribue celle
    /// choisie sur l'écran de connexion s'il n'en avait pas encore.
    /// </summary>
    public async Task AppliquerChoixDuMembreAsync()
    {
        var me = _store.GetCurrentUser()?.Identifiant;
        if (string.IsNullOrWhiteSpace(me)) return;
        var pref = _store.GetLangue(me);
        if (pref == null)
        {
            if (L.Langue != L.Fr) _store.SetLangue(me, L.Langue);
            return;
        }
        if (L.Normaliser(pref) != L.Langue)
        {
            try { await _js.InvokeVoidAsync("guLangue.recharger", L.Normaliser(pref)); } catch { }
            _nav.Refresh(forceReload: true);
        }
    }
}
