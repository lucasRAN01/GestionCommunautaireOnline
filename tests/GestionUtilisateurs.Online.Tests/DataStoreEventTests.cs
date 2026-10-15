using GestionUtilisateurs.Shared.Models;
using GestionUtilisateurs.Shared.Services;

namespace GestionUtilisateurs.Blazor.Tests;

public class DataStoreEventTests
{
    private static DataStore NewStore()
    {
        var store = new DataStore(new MemoryStorageProvider());
        store.EnsureInitialized();
        return store;
    }

    private static CalendarEvent SampleEvent() => new()
    {
        Type = CalendarEventTypes.Reunion,
        Titre = "Réunion équipe",
        Date = "2026-09-12",
        HeureDebut = "09:00",
        HeureFin = "10:00",
        Lieu = "Salle de réunion",
        Notes = "Réunion hebdomadaire"
    };

    [Fact]
    public void AddEvent_Simple_Success()
    {
        var store = NewStore();

        var result = store.AddEvent(SampleEvent());

        Assert.True(result.Success);
        var evt = Assert.Single(store.Events);
        Assert.Equal("Réunion équipe", evt.Titre);
        Assert.False(string.IsNullOrWhiteSpace(evt.Id));
        Assert.False(string.IsNullOrWhiteSpace(evt.DateCreation));
    }

    [Fact]
    public void AddEvent_SansTitre_Echec()
    {
        var store = NewStore();
        var draft = SampleEvent();
        draft.Titre = "";

        var result = store.AddEvent(draft);

        Assert.False(result.Success);
        Assert.Empty(store.Events);
    }

    [Fact]
    public void AddEvent_SansDate_Echec()
    {
        var store = NewStore();
        var draft = SampleEvent();
        draft.Date = "";

        var result = store.AddEvent(draft);

        Assert.False(result.Success);
        Assert.Empty(store.Events);
    }

    [Fact]
    public void UpdateEvent_Success()
    {
        var store = NewStore();
        var added = store.AddEvent(SampleEvent());
        Assert.True(added.Success);
        var id = store.Events[0].Id;

        var draft = store.Events[0];
        draft.Titre = "Réunion reportée";
        draft.Lieu = "Salle B";

        var result = store.UpdateEvent(id, draft);

        Assert.True(result.Success);
        Assert.Equal("Réunion reportée", store.Events[0].Titre);
        Assert.Equal("Salle B", store.Events[0].Lieu);
    }

    [Fact]
    public void UpdateEvent_Introuvable_Echec()
    {
        var store = NewStore();

        var result = store.UpdateEvent("inexistant", SampleEvent());

        Assert.False(result.Success);
    }

    [Fact]
    public void DeleteEvent_Success()
    {
        var store = NewStore();
        store.AddEvent(SampleEvent());
        var id = store.Events[0].Id;

        var result = store.DeleteEvent(id);

        Assert.True(result.Success);
        Assert.Empty(store.Events);
    }

    [Fact]
    public void DeleteEvent_Introuvable_Echec()
    {
        var store = NewStore();

        var result = store.DeleteEvent("inexistant");

        Assert.False(result.Success);
    }

    [Fact]
    public void Events_TriesParDatePuisHeure()
    {
        var store = NewStore();
        store.AddEvent(new CalendarEvent { Type = CalendarEventTypes.Rappel, Titre = "Plus tard", Date = "2026-09-12", HeureDebut = "14:30" });
        store.AddEvent(new CalendarEvent { Type = CalendarEventTypes.Reunion, Titre = "Tôt", Date = "2026-09-12", HeureDebut = "08:00" });
        store.AddEvent(new CalendarEvent { Type = CalendarEventTypes.Conge, Titre = "Journée", Date = "2026-09-12", TouteLaJournee = true });

        Assert.Equal(new[] { "Journée", "Tôt", "Plus tard" }, store.Events.Select(e => e.Titre).ToArray());
    }

    [Fact]
    public void Events_PersistentApresRechargement()
    {
        var provider = new MemoryStorageProvider();
        var store = new DataStore(provider);
        store.EnsureInitialized();
        store.AddEvent(SampleEvent());

        var reloaded = new DataStore(provider);
        reloaded.EnsureInitialized();

        var evt = Assert.Single(reloaded.Events);
        Assert.Equal("Réunion équipe", evt.Titre);
    }
}