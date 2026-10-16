using GestionUtilisateurs.Shared.Models;
using GestionUtilisateurs.Shared.Services;

namespace GestionUtilisateurs.Blazor.Tests;

public class DataStoreIdentifiantsTests
{
    private static DataStore NewAdminStore()
    {
        var store = new DataStore(new MemoryStorageProvider());
        store.EnsureInitialized();
        Assert.True(store.Login("admin", "admin123").Success);
        return store;
    }

    private static UserAccount Ajouter(DataStore store, string prenom, string nom, string identifiant = "")
    {
        var res = store.AddUser(new UserAccount { Prenom = prenom, Nom = nom, Identifiant = identifiant, Role = Roles.Utilisateur, Actif = true });
        Assert.True(res.Success, res.Error);
        return store.FindUser(res.UserId!)!;
    }

    [Theory]
    [InlineData("Lucas", "RAN", "lucas")]
    [InlineData("Jean  Pierre", "Rakoto", "jean.pierre")]
    [InlineData("Hérizo", "Rabe", "herizo")]
    [InlineData("", "Rabe", "rabe")]
    public void Identifiant_EstLePrenom(string prenom, string nom, string attendu)
        => Assert.Equal(attendu, DataStore.GenerateIdentifiant(prenom, nom));

    [Fact]
    public void Homonymes_RecoiventUnNumero_DansLOrdreDInscription()
    {
        var store = NewAdminStore();
        Assert.Equal("lucas", Ajouter(store, "Lucas", "Ran").Identifiant);
        Assert.Equal("lucas1", Ajouter(store, "Lucas", "Rabe").Identifiant);
        Assert.Equal("lucas2", Ajouter(store, "lucas", "Rakoto").Identifiant);
    }

    [Fact]
    public void Import_HomonymesDePrenom_NeSontPasIgnores()
    {
        var store = NewAdminStore();
        var parsed = store.ParseImport("[{ \"prenom\": \"Jean\", \"nom\": \"Rakoto\", \"role\": \"Utilisateur\" }, { \"prenom\": \"Jean\", \"nom\": \"Rabe\", \"role\": \"Utilisateur\" }]");
        var (result, count) = store.ApplyImport(parsed.Users!);
        Assert.True(result.Success, result.Error);
        Assert.Equal(2, count);
        Assert.Contains(store.Users, u => u.Identifiant == "jean" && u.Nom == "RAKOTO");
        Assert.Contains(store.Users, u => u.Identifiant == "jean1" && u.Nom == "RABE");
    }

    [Fact]
    public void MiseALaRegle_RenommeEtReporteLesReferences()
    {
        var store = NewAdminStore();
        // Anciens identifiants (règle prénom.nom), dont un échange : l'actuel « lucas »
        // est inscrit après « lucas.ran » et devient donc « lucas1 ».
        var premier = Ajouter(store, "Lucas", "Ran", "lucas.ran");
        var second = Ajouter(store, "Lucas", "Rabe", "lucas");
        var marie = Ajouter(store, "Marie", "Rabe", "marie.rabe");
        premier.DateCreation = "2026-01-01T00:00:00+00:00";
        second.DateCreation = "2026-02-01T00:00:00+00:00";
        marie.Famille = new List<LienFamille> { new() { Identifiant = "lucas.ran", Type = "parent" } };

        Assert.True(store.AddTache(new Tache { Titre = "Préparer", Assignes = new List<string> { "lucas.ran", "lucas" } }).Success);
        Assert.True(store.AddCotisationsEnAttente(new[] { "lucas" }, 1000, "Annuelle", "2026").Result.Success);

        var plan = store.IdentifiantsARenommer();
        Assert.Contains(plan, p => p.Membre.Id == premier.Id && p.Nouveau == "lucas");
        Assert.Contains(plan, p => p.Membre.Id == second.Id && p.Nouveau == "lucas1");
        Assert.Contains(plan, p => p.Membre.Id == marie.Id && p.Nouveau == "marie");

        Assert.True(store.AppliquerIdentifiantsParPrenom().Success);
        Assert.Empty(store.IdentifiantsARenommer());
        Assert.Equal("Lucas", store.FindUser(premier.Id)!.Prenom); // le prénom n'est pas un identifiant

        Assert.Equal("lucas", store.FindUser(premier.Id)!.Identifiant);
        Assert.Equal("lucas1", store.FindUser(second.Id)!.Identifiant);
        Assert.Contains("lucas.ran", store.FindUser(premier.Id)!.AnciensIdentifiants);
        Assert.Equal("lucas", store.FindUser(marie.Id)!.Famille.Single().Identifiant);
        var tache = store.Taches.Single(t => t.Titre == "Préparer");
        Assert.Equal(new[] { "lucas", "lucas1" }, tache.Assignes.OrderBy(a => a));
        Assert.Contains(store.ImpayesCotisations("2026"), u => u.Id == second.Id);
        Assert.DoesNotContain(store.ImpayesCotisations("2026"), u => u.Id == premier.Id);
    }
}
