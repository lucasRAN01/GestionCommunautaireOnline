using GestionUtilisateurs.Shared.Models;
using GestionUtilisateurs.Shared.Services;

namespace GestionUtilisateurs.Blazor.Tests;

public class DataStoreImportExportTests
{
    private static DataStore NewStore()
    {
        var store = new DataStore(new MemoryStorageProvider());
        store.EnsureInitialized();
        return store;
    }

    [Fact]
    public void ExportJson_ContientLesUtilisateurs()
    {
        var store = NewStore();
        var json = store.ExportJson();

        Assert.Contains("admin", json);
        Assert.Contains("version", json);
        Assert.DoesNotContain("Matricule", json);
    }

    [Fact]
    public void ExportCsv_ContientEnTetesEtLignes()
    {
        var store = NewStore();
        var csv = store.ExportCsv();

        var lines = csv.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(store.Users.Count + 1, lines.Length);
        Assert.StartsWith("nom;prenom;identifiant;email;telephone;role;statut", lines[0]);
        Assert.Contains("admin", lines[1]);
    }

    [Fact]
    public void ExportCsv_EchappeLesValeursAVirgules()
    {
        var store = NewStore();
        var draft = new UserAccount
        {
            Prenom = "Nora",
            Nom = "Personne;Chef",
            Identifiant = "npersonne",
            Email = "npersonne@gestionlocal.fr",
            Role = "Utilisateur",
            Actif = true,
            MotDePasse = "secret123"
        };
        store.AddUser(draft);

        var csv = store.ExportCsv();
        Assert.Contains("\"PERSONNE;CHEF\"", csv);
    }

    [Fact]
    public void RoundTrip_ExportPuisImport()
    {
        var store = NewStore();
        var json = store.ExportJson();

        var fresh = new DataStore(new MemoryStorageProvider());
        fresh.EnsureInitialized();
        var parsed = fresh.ParseImport(json);

        Assert.True(parsed.Success);
        Assert.Equal(2, parsed.Count);
        Assert.Contains(parsed.Users!, u => u.Identifiant == "admin");
    }

    [Fact]
    public void ParseImport_JsonInvalide_Refuse()
    {
        var store = NewStore();
        var result = store.ParseImport("pas du json {{{");

        Assert.False(result.Success);
        Assert.Null(result.Users);
    }

    [Fact]
    public void ParseImport_ManqueUsers_Refuse()
    {
        var store = NewStore();
        var result = store.ParseImport("{ \"app\": \"test\" }");

        Assert.False(result.Success);
        Assert.Contains("users", result.Error);
    }

    [Fact]
    public void ParseImport_UtilisateurSansNom_Refuse()
    {
        var store = NewStore();
        var json = "[{ \"prenom\": \"SeulPrenom\", \"identifiant\": \"x\" }]";
        var result = store.ParseImport(json);

        Assert.False(result.Success);
        Assert.Contains("obligatoires", result.Error);
    }

    [Fact]
    public void ApplyImport_UtilisateurSimpleSansIdentifiant_GenereUnIdentifiant()
    {
        var store = NewStore();
        var parsed = store.ParseImport("[{ \"prenom\": \"A\", \"nom\": \"B\", \"role\": \"Utilisateur\" }]");
        var (result, count) = store.ApplyImport(parsed.Users!);

        Assert.True(result.Success);
        Assert.Equal(1, count);
        Assert.Equal(3, store.Users.Count);
        Assert.Contains(store.Users, u => u.Identifiant == "a.b");
    }

    [Fact]
    public void ApplyImport_FusionSkipsLesDoublons()
    {
        var store = NewStore();
        var json = store.ExportJson();
        var parsed = store.ParseImport(json); // mêmes identifiants que le store

        var (result, count) = store.ApplyImport(parsed.Users!);

        Assert.False(result.Success); // rien de nouveau à ajouter → refus
        Assert.Equal(0, count);
        Assert.Equal(2, store.Users.Count);
        Assert.Contains(store.Users, u => u.Role == "Administrateur" && u.Actif);
    }

    [Fact]
    public void ApplyImport_FusionAjouteLesNouveaux()
    {
        var store = NewStore();
        var parsed = store.ParseImport(
            "[{ \"prenom\": \"Nouveau\", \"nom\": \"Employé\", \"identifiant\": \"nouvel\", \"role\": \"Utilisateur\", \"email\": \"nouvel@gestionlocal.fr\" }]");

        var (result, count) = store.ApplyImport(parsed.Users!);

        Assert.True(result.Success);
        Assert.Equal(1, count);
        Assert.Equal(3, store.Users.Count);
    }

    [Fact]
    public void BackupJson_ContientJournalEtUtilisateurs()
    {
        var store = NewStore();
        var backup = store.BackupJson();

        Assert.Contains("users", backup);
        Assert.Contains("activity", backup);
    }

    [Fact]
    public void RoundTrip_ExcelExportPuisImport_ReconstruitLesDonnees()
    {
        var store = NewStore();
        var bytes = store.ExportUsersExcel();
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);

        var fresh = NewStore();
        Assert.True(fresh.Login("dba", "admindba").Success);
        Assert.True(fresh.SaveManagedDataJson("users", "[]").Success);

        var parsed = fresh.ParseExcelImport(bytes);

        Assert.True(parsed.Success, "L'import Excel a échoué : " + parsed.Error);
        Assert.Equal(2, parsed.Count);
        Assert.All(parsed.Users!, u => Assert.False(string.IsNullOrWhiteSpace(u.Nom)));
        Assert.All(parsed.Users!, u => Assert.False(string.IsNullOrWhiteSpace(u.Prenom)));
        Assert.All(parsed.Users!, u => Assert.True(string.IsNullOrEmpty(u.Telephone) || u.Telephone.StartsWith("+261")));
        Assert.DoesNotContain(parsed.Users!, u => u.Identifiant == null);

        var (res, count) = fresh.ApplyImport(parsed.Users!);
        Assert.True(res.Success);
        Assert.Equal(2, count);
        Assert.Equal(2, fresh.Users.Count);

        var (res2, count2) = fresh.ApplyImport(parsed.Users!); // re-import : plus rien de nouveau
        Assert.False(res2.Success);
        Assert.Equal(0, count2);
        Assert.Equal(2, fresh.Users.Count);
        Assert.Contains(fresh.Users, u => u.Role == "Administrateur" && u.Actif);
    }
}
