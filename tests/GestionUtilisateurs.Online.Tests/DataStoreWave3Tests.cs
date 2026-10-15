using GestionUtilisateurs.Shared.Models;
using GestionUtilisateurs.Shared.Services;

namespace GestionUtilisateurs.Blazor.Tests;

public class DataStoreWave3Tests
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
    public void Finances_CotisationEtSolde()
    {
        var store = NewStore();
        LoginAdmin(store);
        Assert.True(store.AddCotisation(new Cotisation
        {
            MembreIdentifiant = "admin", Montant = 5000, Motif = "Annuelle",
            Periode = "2026", Date = "2026-01-15", Statut = CotisationStatuts.Payee
        }).Success);
        Assert.Equal(5000, store.TotalCotisations("2026"));
        Assert.DoesNotContain(store.ImpayesCotisations("2026"), u => u.Identifiant == "admin");
        Assert.Contains(store.ImpayesCotisations("2026"), u => u.Identifiant == "dba");

        Assert.False(store.AddCotisation(new Cotisation { MembreIdentifiant = "fantome", Montant = 100 }).Success);
        Assert.False(store.AddCotisation(new Cotisation { MembreIdentifiant = "admin", Montant = 0 }).Success);

        Assert.True(store.AddTransaction(new TransactionTresorerie
        {
            Type = TresorerieTypes.Entree, Montant = 20000, Motif = "Subvention", Date = "2026-02-01"
        }).Success);
        Assert.True(store.AddTransaction(new TransactionTresorerie
        {
            Type = TresorerieTypes.Sortie, Montant = 7500, Motif = "Location salle", Date = "2026-02-10"
        }).Success);
        Assert.Equal(12500, store.SoldeTresorerie());
    }

    [Fact]
    public void Materiel_PretEtRetour_Quantites()
    {
        var store = NewStore();
        LoginAdmin(store);
        Assert.True(store.AddMateriel(new Materiel { Nom = "Enceinte", QuantiteTotale = 2 }).Success);
        var mat = Assert.Single(store.Materiels);
        Assert.Equal(2, store.DisponibleMateriel(mat.Id));

        Assert.True(store.AddPret(mat.Id, "admin", 1).Success);
        Assert.Equal(1, store.DisponibleMateriel(mat.Id));
        Assert.False(store.AddPret(mat.Id, "dba", 2).Success);
        Assert.True(store.AddPret(mat.Id, "dba", 1).Success);
        Assert.Equal(0, store.DisponibleMateriel(mat.Id));

        var pret = store.PretsActifs(mat.Id).First(p => p.MembreIdentifiant == "admin");
        Assert.True(store.ReturnPret(pret.Id).Success);
        Assert.Equal(1, store.DisponibleMateriel(mat.Id));
        Assert.False(store.ReturnPret(pret.Id).Success);

        // Suppression refusée tant qu'un prêt est actif.
        Assert.False(store.DeleteMateriel(mat.Id).Success);
    }

    [Fact]
    public void Reunions_Presence_Statistiques()
    {
        var store = NewStore();
        LoginAdmin(store);
        Assert.True(store.AddReunion(new Reunion
        {
            Titre = "AG", Date = "2026-10-03", Lieu = "Salle A",
            Participants = new List<string> { "admin", "dba" }
        }).Success);
        var reunion = Assert.Single(store.Reunions);

        Assert.True(store.MarkPresence(reunion.Id, "admin", PresenceStatuts.Present).Success);
        Assert.True(store.MarkPresence(reunion.Id, "dba", PresenceStatuts.Excuse).Success);
        var stats = store.ReunionStats(reunion.Id);
        Assert.Equal((1, 0, 1, 0, 2), stats);
        Assert.False(store.MarkPresence(reunion.Id, "fantome", PresenceStatuts.Present).Success);
    }

    [Fact]
    public void Rappels_DetectentRetardsEtAnniversaires()
    {
        var store = NewStore();
        LoginAdmin(store);
        Assert.True(store.AddTache(new Tache { Titre = "Dossier en retard", Echeance = "2000-01-01" }).Success);
        Assert.True(store.AddUser(new UserAccount
        {
            Prenom = "Futur", Nom = "BIRTH", Identifiant = "fbirth",
            Role = Roles.Utilisateur, Actif = true, MotDePasse = "secret123",
            DateNaissance = DateTime.Today.AddDays(5).ToString("yyyy-MM-dd")
        }).Success);

        var rappels = store.GetRappels();
        Assert.Contains(rappels, r => r.Categorie == "Tâches" && r.Priorite == CalendarEventTypes.PrioriteUrgente);
        Assert.Contains(rappels, r => r.Categorie == "Anniversaires");
    }

    [Fact]
    public void Sync_Vague3_Fusionne()
    {
        var a = NewStore();
        var b = NewStore();
        LoginAdmin(a);
        Assert.True(a.AddCotisation(new Cotisation { MembreIdentifiant = "admin", Montant = 1000, Periode = "2026", Date = "2026-03-01" }).Success);
        Assert.True(a.AddMateriel(new Materiel { Nom = "Projecteur", QuantiteTotale = 1 }).Success);
        Assert.True(a.AddReunion(new Reunion { Titre = "Bureau", Date = "2026-11-01", Participants = new List<string> { "admin" } }).Success);

        var json = a.BuildSyncPackageJson(new SyncOptions(), "A");
        var (package, error) = b.ParseSyncPackage(json);
        Assert.Null(error);
        ApplyAllIncoming(b, package!);
        Assert.Single(b.Cotisations);
        Assert.Single(b.Materiels);
        Assert.Single(b.Reunions);
        Assert.Equal(0, b.PreviewSyncImport(package!).TotalChanges);
    }
}
