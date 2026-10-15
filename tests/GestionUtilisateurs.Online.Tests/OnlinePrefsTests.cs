using GestionUtilisateurs.Shared.Services;

namespace GestionUtilisateurs.Blazor.Tests;

/// <summary>
/// En ligne, les préférences personnelles (guide vu, aides, thème, conseils)
/// ne doivent jamais modifier les Paramètres partagés : seuls les
/// gestionnaires ont le droit de les enregistrer sur le serveur.
/// </summary>
public class OnlinePrefsTests
{
    private static (DataStore Store, MemoryStorageProvider Storage) NewOnlineStore()
    {
        var storage = new MemoryStorageProvider();
        var store = new DataStore(storage);
        store.EnsureInitialized();
        store.OnlineMode = true;
        return (store, storage);
    }

    [Fact]
    public void GuideEtAides_RestentHorsDesParametresPartages()
    {
        var (store, storage) = NewOnlineStore();
        var settingsAvant = storage.Get("gu2:settings");

        Assert.False(store.HasSeenOnboarding("marie"));
        store.MarkOnboardingSeen("marie");
        store.MarkModuleHelpSeen("marie", "finances");

        Assert.True(store.HasSeenOnboarding("Marie"));
        Assert.False(store.HasSeenOnboarding("paul"));
        Assert.True(store.HasSeenModuleHelp("marie", "finances"));
        Assert.False(store.HasSeenModuleHelp("marie", "votes"));
        Assert.Empty(store.Settings.GuideVu);
        Assert.Empty(store.Settings.AideVu);
        Assert.Equal(settingsAvant, storage.Get("gu2:settings"));
        Assert.NotNull(storage.Get(DataStore.K_PREFS_PREFIX + "marie"));
    }

    [Fact]
    public void ThemeEtConseils_SontPersonnelsEtPrioritaires()
    {
        var (store, storage) = NewOnlineStore();
        var settingsAvant = storage.Get("gu2:settings");
        var conseilsGlobal = store.Settings.AidesContextuelles;

        store.SetUserTheme("marie", "dark");
        store.SetUserConseils("marie", !conseilsGlobal);

        Assert.Equal("dark", store.GetEffectiveTheme("marie"));
        Assert.Equal(store.Settings.Theme == "dark" ? "dark" : "light", store.GetEffectiveTheme("paul"));
        Assert.Equal(!conseilsGlobal, store.GetEffectiveConseils("marie"));
        Assert.Equal(conseilsGlobal, store.GetEffectiveConseils("paul"));
        Assert.Empty(store.Settings.ThemesUtilisateurs);
        Assert.Empty(store.Settings.ConseilsUtilisateurs);
        Assert.Equal(settingsAvant, storage.Get("gu2:settings"));
    }
}
