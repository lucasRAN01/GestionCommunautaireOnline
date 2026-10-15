using GestionUtilisateurs.Shared.Services;

namespace GestionUtilisateurs.Blazor.Tests;

public class DataStoreAuthTests
{
    private static DataStore NewStore()
    {
        var store = new DataStore(new MemoryStorageProvider());
        store.EnsureInitialized();
        return store;
    }

    [Fact]
    public void SeedDemoData_ChargeUniquementLesComptesInitiauxEtLeChampSysteme()
    {
        var store = NewStore();
        Assert.Equal(2, store.Users.Count);
        Assert.Single(store.Fields);
        Assert.Contains(store.Users, u => u.Identifiant == "admin" && u.Nom == "ADMIN" && u.Prenom == "admin");
        Assert.Contains(store.Users, u => u.Identifiant == "dba" && u.Role == Shared.Models.Roles.BaseAdmin);
        Assert.False(store.Fields.Single().Requis);
    }

    [Theory]
    [InlineData("admin", "admin123")]
    [InlineData("ADMIN", "admin123")]
    [InlineData(" admin ", "admin123")]
    public void Login_IdentifiantsValides(string username, string password)
    {
        var store = NewStore();
        var result = store.Login(username, password);

        Assert.True(result.Success);
        Assert.True(store.IsAuthenticated);
        Assert.True(store.IsAdmin);
        Assert.NotNull(store.GetCurrentUser());
    }

    [Fact]
    public void Login_MauvaisMotDePasse_Echoue()
    {
        var store = NewStore();
        var result = store.Login("admin", "mauvais");

        Assert.False(result.Success);
        Assert.False(store.IsAuthenticated);
    }

    [Fact]
    public void Login_CompteInactif_Refuse()
    {
        var store = NewStore();
        store.AddUser(new Shared.Models.UserAccount
        {
            Prenom = "Compte", Nom = "Inactif", Identifiant = "inactif", MotDePasse = "utilisateur123",
            Role = Shared.Models.Roles.Utilisateur, Actif = false
        });
        var result = store.Login("inactif", "utilisateur123");

        Assert.False(result.Success);
        Assert.Contains("inactif", result.Error);
        Assert.False(store.IsAuthenticated);
    }

    [Fact]
    public void Login_EnregistreDerniereConnexionEtJournal()
    {
        var store = NewStore();
        store.Login("admin", "admin123");

        var user = store.FindUser(store.Users.First(u => u.Identifiant == "admin").Id);
        Assert.NotNull(user!.DerniereConnexion);
        Assert.Contains(store.Activity, a => a.Type == Shared.Models.ActivityTypes.Login);
    }

    [Fact]
    public void Logout_CleLaSession()
    {
        var store = NewStore();
        store.Login("admin", "admin123");
        store.Logout();

        Assert.False(store.IsAuthenticated);
        Assert.Null(store.GetCurrentUser());
    }

    [Fact]
    public void Session_expiree_CleLaSession()
    {
        var provider = new MemoryStorageProvider();
        var store = new DataStore(provider);
        store.EnsureInitialized();
        store.Login("admin", "admin123");
        Assert.True(store.IsAuthenticated);

        var expiredTicks = DateTime.UtcNow.AddMinutes(-1).Ticks;
        var sessionJson = "{\"userId\":\"" + store.GetCurrentUser()!.Id + "\",\"expiryUtcTicks\":" + expiredTicks + "}";
        provider.Set("gu2:session", sessionJson);

        var store2 = new DataStore(provider);
        store2.EnsureInitialized();
        Assert.False(store2.IsAuthenticated);
    }
}
