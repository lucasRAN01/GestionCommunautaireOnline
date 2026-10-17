using GestionUtilisateurs.Shared.Models;
using GestionUtilisateurs.Shared.Services;

namespace GestionUtilisateurs.Blazor.Tests;

public class DataStoreApercuTests
{
    private static DataStore AdminConnecte()
    {
        var store = new DataStore(new MemoryStorageProvider());
        store.EnsureInitialized();
        Assert.True(store.Login("admin", "admin123").Success);
        return store;
    }

    [Fact]
    public void Apercu_Utilisateur_RetireLesDroitsAdministrateur()
    {
        var store = AdminConnecte();
        Assert.True(store.CommencerApercu(Roles.Utilisateur).Success);

        Assert.True(store.EnApercu);
        Assert.False(store.IsAdmin);
        Assert.False(store.IsManager);
        Assert.False(store.Can(Permissions.UsersView));
        Assert.True(store.Can(Permissions.CalendarUse));
        Assert.Equal(Roles.Utilisateur, store.CurrentRole);
        Assert.True(store.EstAdministrateurReel);
    }

    [Fact]
    public void Apercu_Gestionnaire_DonneLesDroitsGestionnaire()
    {
        var store = AdminConnecte();
        Assert.True(store.CommencerApercu(Roles.Gestionnaire).Success);

        Assert.False(store.IsAdmin);
        Assert.True(store.IsManager);
        Assert.True(store.Can(Permissions.UsersView));
        Assert.False(store.Can(Permissions.RolesManage));
    }

    [Fact]
    public void TerminerApercu_RendLeRoleDuCompte()
    {
        var store = AdminConnecte();
        store.CommencerApercu(Roles.Utilisateur);
        store.TerminerApercu();

        Assert.False(store.EnApercu);
        Assert.True(store.IsAdmin);
    }

    [Fact]
    public void Apercu_SArreteALaDeconnexion()
    {
        var store = AdminConnecte();
        store.CommencerApercu(Roles.Utilisateur);
        store.Logout();

        Assert.Null(store.RoleApercu);
    }

    [Fact]
    public void Apercu_RefuseAuxNonAdministrateurs_EtPendantUnApercu()
    {
        var store = AdminConnecte();
        Assert.False(store.CommencerApercu(Roles.Administrateur).Success);
        store.CommencerApercu(Roles.Utilisateur);
        // Pendant l'aperçu, l'administrateur peut changer de rôle prévisualisé.
        Assert.True(store.CommencerApercu(Roles.Gestionnaire).Success);
        store.Logout();
        Assert.False(store.CommencerApercu(Roles.Gestionnaire).Success);
    }
}
