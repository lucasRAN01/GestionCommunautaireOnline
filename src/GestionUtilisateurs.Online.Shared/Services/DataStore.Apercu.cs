using GestionUtilisateurs.Shared.Models;

namespace GestionUtilisateurs.Shared.Services;

/// <summary>
/// Aperçu par rôle : un administrateur voit l'application comme un Gestionnaire
/// ou un Utilisateur (menus, pages, boutons, permissions de la matrice des rôles),
/// avec son propre compte. Rien n'est enregistré : l'aperçu s'arrête au
/// rechargement de la page ou à la déconnexion.
/// </summary>
public partial class DataStore
{
    /// <summary>Rôle affiché pendant l'aperçu, ou null hors aperçu.</summary>
    public string? RoleApercu { get; private set; }

    public bool EnApercu => RoleApercu != null;

    /// <summary>Le compte connecté est réellement administrateur (aperçu ou non).</summary>
    public bool EstAdministrateurReel => Roles.IsAdmin(GetCurrentUser()?.Role) && GetCurrentUser()?.Role != Roles.BaseAdmin;

    /// <summary>Rôles que l'administrateur peut prévisualiser.</summary>
    public static IReadOnlyList<string> RolesApercu { get; } = new[] { Roles.Gestionnaire, Roles.Utilisateur };

    public OperationResult CommencerApercu(string role)
    {
        if (!EstAdministrateurReel) return OperationResult.Fail(L.T("Action réservée aux administrateurs."));
        if (!RolesApercu.Contains(role)) return OperationResult.Fail(L.T("Action impossible."));
        RoleApercu = role;
        DataChanged?.Invoke();
        return OperationResult.Ok();
    }

    public void TerminerApercu()
    {
        if (RoleApercu == null) return;
        RoleApercu = null;
        DataChanged?.Invoke();
    }
}
