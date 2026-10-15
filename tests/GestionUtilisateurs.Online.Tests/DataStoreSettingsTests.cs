using GestionUtilisateurs.Shared.Models;
using GestionUtilisateurs.Shared.Services;

namespace GestionUtilisateurs.Blazor.Tests;

public class DataStoreSettingsTests
{
    private static DataStore NewStore()
    {
        var store = new DataStore(new MemoryStorageProvider());
        store.EnsureInitialized();
        return store;
    }

    [Fact]
    public void Settings_ParDefaut()
    {
        var store = NewStore();

        Assert.Equal("Gestion des utilisateurs", store.Settings.AppName);
        Assert.Equal("light", store.Settings.Theme);
        Assert.Equal(10, store.Settings.PageSize);
    }

    [Fact]
    public void SaveSettings_Persiste()
    {
        var provider = new MemoryStorageProvider();
        var store = new DataStore(provider);
        store.EnsureInitialized();

        store.SaveSettings(new AppSettings { AppName = "Mon App", Theme = "dark", Color = "#ff0000", PageSize = 25 });

        var reloaded = new DataStore(provider);
        reloaded.EnsureInitialized();

        Assert.Equal("Mon App", reloaded.Settings.AppName);
        Assert.Equal("dark", reloaded.Settings.Theme);
        Assert.Equal("#ff0000", reloaded.Settings.Color);
        Assert.Equal(25, reloaded.Settings.PageSize);
    }

    [Fact]
    public void StorageUsedBytes_RefleteLaTaille()
    {
        var provider = new MemoryStorageProvider();
        var store = new DataStore(provider);
        store.EnsureInitialized();
        var before = store.StorageUsedBytes();

        store.AddUser(new UserAccount
        {
            Prenom = "Nora",
            Nom = "Personne",
            Identifiant = "npersonne",
            Email = "npersonne@gestionlocal.fr",
            Role = "Utilisateur",
            Actif = true,
            MotDePasse = "secret123"
        });

        Assert.True(store.StorageUsedBytes() > before);
    }

    [Fact]
    public void Theme_Individuel_Prioritaire_SansToucherGlobal()
    {
        var store = NewStore();

        Assert.Equal("light", store.GetEffectiveTheme("jdubois"));
        store.SetUserTheme("jdubois", "dark");
        Assert.Equal("dark", store.GetEffectiveTheme("jdubois"));
        Assert.Equal("light", store.GetEffectiveTheme("cmoreau"));
        Assert.Equal("light", store.Settings.Theme);
    }

    [Fact]
    public void Conseils_Individuels_SansToucherLesAutres()
    {
        var store = NewStore();

        Assert.True(store.GetEffectiveConseils("jdubois"));
        store.SetUserConseils("jdubois", false);
        Assert.False(store.GetEffectiveConseils("jdubois"));
        Assert.True(store.GetEffectiveConseils("cmoreau"));
        Assert.True(store.Settings.AidesContextuelles);
    }

    [Fact]
    public void FormatDateHeure_AfficheDateEtHeure()
    {
        Assert.Equal("—", DataStore.FormatDateHeure(null));
        Assert.Equal("05/09/2026 à 14:30", DataStore.FormatDateHeure("2026-09-05T14:30:00+03:00"));
    }
}