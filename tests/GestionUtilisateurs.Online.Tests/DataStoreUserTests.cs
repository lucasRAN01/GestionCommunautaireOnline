using GestionUtilisateurs.Shared.Models;
using GestionUtilisateurs.Shared.Services;

namespace GestionUtilisateurs.Blazor.Tests;

public class DataStoreUserTests
{
    private static DataStore NewStore()
    {
        var store = new DataStore(new MemoryStorageProvider());
        store.EnsureInitialized();
        return store;
    }

    private static UserAccount ValidDraft(string identifiant = "npersonne")
        => new()
        {
            Prenom = "Nora",
            Nom = "Personne",
            Identifiant = identifiant,
            Email = identifiant + "@gestionlocal.fr",
            Role = "Utilisateur",
            Actif = true,
            MotDePasse = "secret123",
            Notes = ""
        };

    private static UserAccount AddSimpleUser(DataStore store, string identifiant = "npersonne", bool actif = true)
    {
        var draft = ValidDraft(identifiant);
        draft.Actif = actif;
        Assert.True(store.AddUser(draft).Success);
        return store.FindUser(draft.Id)!;
    }

    [Fact]
    public void AddUser_CreeEtJournalise()
    {
        var store = NewStore();
        var result = store.AddUser(ValidDraft());

        Assert.True(result.Success);
        Assert.NotNull(result.UserId);
        Assert.Equal(3, store.Users.Count);
        Assert.Contains(store.Activity, a => a.Type == ActivityTypes.Create);
    }

[Fact]
    public void UpdateUser_ModifieLesChamps()
    {
        var store = NewStore();
        var u = AddSimpleUser(store);
        var draft = new UserAccount
        {
            Prenom = "Nora",
            Nom = "Personne",
            Identifiant = "nouvelid",
            Email = "nouvelid@gestionlocal.fr",
            Role = u.Role,
            Actif = true,
            MotDePasse = u.MotDePasse,
            Notes = u.Notes
        };
        var result = store.UpdateUser(u.Id, draft, passwordChanged: false);

        Assert.True(result.Success);
        Assert.Equal("Nora", u.Prenom);
        Assert.Equal("nouvelid@gestionlocal.fr", u.Email);
        Assert.Equal("npersonne", u.Identifiant); // l'identifiant reste immuable à l'édition
    }

    [Fact]
    public void UpdateUser_BaseAdmin_ModifieSansChangerLeRole()
    {
        var store = NewStore();
        Assert.True(store.Login("dba", "admindba").Success); // seul le dba peut modifier son compte technique
        var dba = store.Users.First(u => u.Role == "Administrateur de la base");
        var draft = new UserAccount
        {
            Prenom = "DBA2",
            Nom = dba.Nom,
            Identifiant = dba.Identifiant,
            Email = "dba@gestionlocal.fr",
            Role = "Utilisateur", // sera ignoré : seul le dernier compte de base garde son rôle
            Actif = true,
            MotDePasse = dba.MotDePasse,
            Notes = dba.Notes
        };

        var result = store.UpdateUser(dba.Id, draft, passwordChanged: false);

        Assert.True(result.Success);
        Assert.Equal("Administrateur de la base", store.FindUser(dba.Id)!.Role);
        Assert.Equal("DBA2", store.FindUser(dba.Id)!.Prenom);
    }

    [Fact]
    public void UpdateUser_Inchangeable_RetourneErreur()
    {
        var store = NewStore();
        var admin = store.Users.First(u => u.Role == "Administrateur");
        var draft = ValidDraft();
        draft.Prenom = admin.Prenom;
        draft.Nom = admin.Nom;
        draft.Email = admin.Email;
        draft.Role = "Utilisateur"; // retirer le rôle admin
        draft.Actif = admin.Actif;

        var result = store.UpdateUser(admin.Id, draft, passwordChanged: false);

        Assert.False(result.Success);
        Assert.Contains("rôle", result.Error);
        Assert.Equal("Administrateur", store.FindUser(admin.Id)!.Role);
    }

    [Fact]
    public void UpdateUser_DesactivationDernierAdmin_Refuse()
    {
        var store = NewStore();
        var admin = store.Users.First(u => u.Role == "Administrateur");
        var draft = ValidDraft();
        draft.Prenom = admin.Prenom;
        draft.Nom = admin.Nom;
        draft.Email = admin.Email;
        draft.Role = "Administrateur";
        draft.Actif = false;

        var result = store.UpdateUser(admin.Id, draft, passwordChanged: false);

        Assert.False(result.Success);
        Assert.Contains("désactiver", result.Error);
    }

    [Fact]
    public void UpdateUser_ChangeMotDePasse_JournalisePasswordReset()
    {
        var store = NewStore();
        var u = AddSimpleUser(store);
        var draft = new UserAccount
        {
            Prenom = u.Prenom,
            Nom = u.Nom,
            Identifiant = u.Identifiant,
            Email = u.Email,
            Role = u.Role,
            Actif = u.Actif,
            MotDePasse = "nouveaumdp1",
            Notes = u.Notes
        };

        var result = store.UpdateUser(u.Id, draft, passwordChanged: true);

        Assert.True(result.Success);
        Assert.True(PasswordCipher.Verify(store.FindUser(u.Id)!.MotDePasse, "nouveaumdp1"));
        Assert.Contains(store.Activity, a => a.Type == ActivityTypes.PasswordReset);
    }

    [Fact]
    public void DeleteUser_SupprimeEtJournalise()
    {
        var store = NewStore();
        var u = AddSimpleUser(store);
        var admin = store.Users.First(x => x.Identifiant == "admin");

        var result = store.DeleteUser(u.Id, admin.Id);

        Assert.True(result.Success);
        Assert.DoesNotContain(store.Users, x => x.Id == u.Id);
        Assert.Contains(store.Activity, a => a.Type == ActivityTypes.Delete);
    }

    [Fact]
    public void DeleteUser_PropreCompte_Refuse()
    {
        var store = NewStore();
        var admin = store.Users.First(u => u.Role == "Administrateur");
        var result = store.DeleteUser(admin.Id, actorUserId: admin.Id);

        Assert.False(result.Success);
        Assert.Contains("propre compte", result.Error);
    }

    [Fact]
    public void DeleteUser_DernierAdministrateur_Refuse()
    {
        var store = NewStore();
        var admin = store.Users.First(u => u.Role == "Administrateur");
        var result = store.DeleteUser(admin.Id, actorUserId: null);

        Assert.False(result.Success);
        Assert.Contains("dernier administrateur", result.Error);
    }

    [Fact]
    public void SetUserActive_DesactivationDernierAdmin_Refuse()
    {
        var store = NewStore();
        var admin = store.Users.First(u => u.Role == "Administrateur");
        var result = store.SetUserActive(admin.Id, active: false);

        Assert.False(result.Success);
        Assert.True(store.FindUser(admin.Id)!.Actif);
    }

    [Fact]
    public void SetUserActive_ReactiveLeCompte()
    {
        var store = NewStore();
        var cmoreau = AddSimpleUser(store, "cmoreau", actif: false);

        store.Login("admin", "admin123"); // il faut un admin actif restant
        var result = store.SetUserActive(cmoreau.Id, active: true);

        Assert.True(result.Success);
        Assert.True(store.FindUser(cmoreau.Id)!.Actif);
    }

    [Fact]
    public void ResetPassword_Reinitialise()
    {
        var store = NewStore();
        var u = AddSimpleUser(store);
        var result = store.ResetPassword(u.Id);

        Assert.True(result.Success);
        Assert.True(PasswordCipher.Verify(store.FindUser(u.Id)!.MotDePasse, DataStore.DEFAULT_PASSWORD));
    }

    [Fact]
    public void ValidateUser_DoublonIdentifiant_Refuse()
    {
        var store = NewStore();
        var admin = store.Users.First(u => u.Role == "Administrateur");
        var duplicate = ValidDraft(admin.Identifiant);
        duplicate.Role = "Gestionnaire"; // les comptes simples obtiennent un identifiant automatique

        var errors = store.ValidateUser(duplicate, existingId: null, isNew: true, checkPassword: true, confirmPassword: "secret123");

        Assert.True(errors.ContainsKey("identifiant"));
    }

    [Fact]
    public void ValidateUser_EmailInvalide_Refuse()
    {
        var store = NewStore();
        var draft = ValidDraft();
        draft.Email = "pas-un-email";
        var errors = store.ValidateUser(draft, null, true, true, "secret123");

        Assert.True(errors.ContainsKey("email"));
    }

    [Fact]
    public void ValidateUser_MotsDePasseDifferent_Refuse()
    {
        var store = NewStore();
        var draft = ValidDraft();
        draft.Role = "Gestionnaire";
        var errors = store.ValidateUser(draft, null, true, true, "autrepass");

        Assert.True(errors.ContainsKey("confirmPassword"));
    }

    [Fact]
    public void ValidateUser_MotDePasseCourt_Refuse()
    {
        var store = NewStore();
        var draft = ValidDraft();
        draft.Role = "Gestionnaire";
        draft.MotDePasse = "abc";
        var errors = store.ValidateUser(draft, null, true, true, "abc");

        Assert.True(errors.ContainsKey("password"));
    }

    [Fact]
    public void ValidateUser_ChampPersonnaliseObligatoire_Refuse()
    {
        var store = NewStore();
        var draft = ValidDraft();
        // Champ « Matricule » est obligatoire ? non par défaut ; on ajoute un champ requis
        var champ = new FieldDefinition { Label = "N° de badge", Type = FieldType.Texte, Requis = true };
        store.AddField(champ);

        var errors = store.ValidateUser(draft, null, true, true, "secret123");

        Assert.True(errors.ContainsKey("field_" + champ.Id));
    }

    [Fact]
    public void ValidateUser_TelephoneInvalide_Refuse()
    {
        var store = NewStore();
        var draft = ValidDraft();
        draft.Role = "Gestionnaire";
        draft.Telephone = "06 12 34 56 78";
        var errors = store.ValidateUser(draft, null, true, true, "secret123");

        Assert.True(errors.ContainsKey("telephone"));
    }

    [Fact]
    public void ValidateUser_TelephoneMadagascar_Accepte()
    {
        var store = NewStore();
        var draft = ValidDraft();
        draft.Role = "Gestionnaire";
        draft.Telephone = "+261 32 12 345 67";
        var errors = store.ValidateUser(draft, null, true, true, "secret123");

        Assert.False(errors.ContainsKey("telephone"));
    }

    [Fact]
    public void ValidateUser_TelephoneEspacesEtSansPlus_Refuse()
    {
        var store = NewStore();
        var draft = ValidDraft();
        draft.Role = "Gestionnaire";
        draft.Telephone = "261 32 12 345 67";
        var errors = store.ValidateUser(draft, null, true, true, "secret123");

        Assert.True(errors.ContainsKey("telephone"));
    }

    [Fact]
    public void ValidateUser_DonneeValide_AucuneErreur()
    {
        var store = NewStore();
        var errors = store.ValidateUser(ValidDraft(), null, true, true, "secret123");

        Assert.Empty(errors);
    }

    [Fact]
    public void ResetAll_RepropageLesDonneesDemo()
    {
        var store = NewStore();
        store.AddUser(ValidDraft("extrapersonne"));
        store.ResetAll();

        Assert.Equal(2, store.Users.Count);
        Assert.Contains(store.Users, u => u.Identifiant == "admin");
    }
}
