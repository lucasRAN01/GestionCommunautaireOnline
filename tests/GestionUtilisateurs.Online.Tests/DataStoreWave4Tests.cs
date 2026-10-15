using GestionUtilisateurs.Shared.Models;
using GestionUtilisateurs.Shared.Services;

namespace GestionUtilisateurs.Blazor.Tests;

public class DataStoreWave4Tests
{
    private static DataStore NewStore()
    {
        var store = new DataStore(new MemoryStorageProvider());
        store.EnsureInitialized();
        return store;
    }

    private static void LoginAdmin(DataStore store)
    {
        Assert.True(store.Login("admin", "admin123").Success);
    }

    private static void ApplyAllIncoming(DataStore to, SyncPackage package)
    {
        var preview = to.PreviewSyncImport(package);
        var resolutions = preview.Conflicts
            .Select(c => new SyncResolution { Collection = c.Collection, Key = c.Key, UseIncoming = true })
            .ToList();
        var result = to.ApplySyncImport(package, resolutions);
        Assert.True(result.Success, result.Error);
    }

    [Fact]
    public void Situations_CycleDeSuivi()
    {
        var store = NewStore();
        LoginAdmin(store);
        Assert.True(store.AddSituation(new Situation
        {
            MembreIdentifiant = "admin", Type = SituationTypes.Visite,
            Titre = "Visite à domicile", Priorite = CalendarEventTypes.PrioriteImportante
        }).Success);

        var sit = Assert.Single(store.Situations);
        Assert.Equal(SituationStatuts.Ouvert, sit.Statut);
        Assert.Single(store.GetSituationsForMember("admin"));

        Assert.True(store.SetSituationStatut(sit.Id, SituationStatuts.EnCours).Success);
        Assert.True(store.SetSituationStatut(sit.Id, SituationStatuts.Resolu).Success);
        Assert.Equal(SituationStatuts.Resolu, store.FindSituation(sit.Id)!.Statut);
        Assert.False(string.IsNullOrWhiteSpace(store.FindSituation(sit.Id)!.DateResolution));

        Assert.False(store.SetSituationStatut(sit.Id, "inconnu").Success);
        Assert.False(store.AddSituation(new Situation { MembreIdentifiant = "fantome", Titre = "X" }).Success);
        Assert.True(store.DeleteSituation(sit.Id).Success);
        Assert.Empty(store.Situations);
    }

    [Fact]
    public void CarteMembre_CodeStableEtValide()
    {
        var store = NewStore();
        var admin = store.FindUserByIdentifier("admin")!;
        var code = DataStore.CodeMembre(admin);
        Assert.StartsWith("GU-", code);
        Assert.Equal(code, DataStore.CodeMembre(store.FindUserByIdentifier("admin")!));
        Assert.True(admin.Actif);
    }

    [Fact]
    public void Votes_UnBulletinParMembre_Resultats()
    {
        var store = NewStore();
        LoginAdmin(store);
        Assert.True(store.AddVote(new Vote
        {
            Titre = "Date de la fête",
            Options = new List<string> { "Samedi", "Dimanche" }
        }).Success);
        var vote = Assert.Single(store.Votes);
        Assert.Equal(VoteStatuts.Ouvert, vote.Statut);

        Assert.False(store.AddVote(new Vote { Titre = "Invalide", Options = new List<string> { "Seule" } }).Success);
        Assert.True(store.CastVote(vote.Id, "admin", "Samedi").Success);
        // Modification du bulletin autorisée tant qu'ouvert.
        Assert.True(store.CastVote(vote.Id, "admin", "Dimanche").Success);
        Assert.False(store.CastVote(vote.Id, "fantome", "Samedi").Success);
        Assert.False(store.CastVote(vote.Id, "admin", "Lundi").Success);

        var results = store.VoteResults(vote.Id);
        Assert.Equal(2, results.Count);
        Assert.Equal("Dimanche", results[0].Option);
        Assert.Equal(1, results[0].Voix);
        Assert.Equal(100.0, results[0].Pourcent);

        Assert.True(store.SetVoteStatut(vote.Id, VoteStatuts.Clos).Success);
        Assert.False(store.CastVote(vote.Id, "dba", "Samedi").Success);

        // Vote clos : modification refusée, résultats figés.
        Assert.False(store.UpdateVote(vote.Id, new Vote { Titre = "X", Options = new List<string> { "A", "B" } }).Success);
        Assert.True(store.DeleteVote(vote.Id).Success);
        Assert.Empty(store.Votes);
    }

    [Fact]
    public void JournalMedia_AjoutValidation()
    {
        var store = NewStore();
        LoginAdmin(store);
        Assert.True(store.AddMedia(new MediaItem
        {
            Titre = "Photo de groupe", Type = MediaTypes.Photo,
            Mime = "image/jpeg", Taille = 1234, Data = "data:image/jpeg;base64,AAA",
            Date = "2026-09-18"
        }).Success);
        Assert.Single(store.JournalMedia);
        Assert.False(store.AddMedia(new MediaItem { Titre = "", Type = MediaTypes.Photo, Data = "x", Taille = 1 }).Success);
        Assert.False(store.AddMedia(new MediaItem { Titre = "Sans contenu", Type = MediaTypes.Audio }).Success);

        var item = store.JournalMedia[0];
        Assert.True(store.UpdateMedia(item.Id, new MediaItem { Titre = "Renommée" }).Success);
        Assert.Equal("Renommée", store.FindMedia(item.Id)!.Titre);
        Assert.True(store.DeleteMedia(item.Id).Success);
        Assert.Empty(store.JournalMedia);
    }

    [Fact]
    public void Sync_Vague4_Fusionne()
    {
        var a = NewStore();
        var b = NewStore();
        LoginAdmin(a);
        Assert.True(a.AddSituation(new Situation { MembreIdentifiant = "admin", Titre = "Suivi A" }).Success);
        Assert.True(a.AddVote(new Vote { Titre = "Choix", Options = new List<string> { "Oui", "Non" } }).Success);
        Assert.True(a.AddMedia(new MediaItem
        {
            Titre = "Photo A", Type = MediaTypes.Photo,
            Mime = "image/jpeg", Taille = 10, Data = "data:image/jpeg;base64,AAA", Date = "2026-09-18"
        }).Success);

        var json = a.BuildSyncPackageJson(new SyncOptions(), "A");
        var (package, error) = b.ParseSyncPackage(json);
        Assert.Null(error);
        ApplyAllIncoming(b, package!);
        Assert.Single(b.Situations);
        Assert.Single(b.Votes);
        Assert.Single(b.JournalMedia);
        Assert.Equal(0, b.PreviewSyncImport(package!).TotalChanges);
    }
}
