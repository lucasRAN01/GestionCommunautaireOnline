using GestionUtilisateurs.Shared.Models;
using GestionUtilisateurs.Shared.Services;

namespace GestionUtilisateurs.Blazor.Tests;

public class DataStoreFieldTests
{
    private static DataStore NewStore()
    {
        var store = new DataStore(new MemoryStorageProvider());
        store.EnsureInitialized();
        return store;
    }

    [Fact]
    public void AddField_AjouteEtAttribueOrdre()
    {
        var store = NewStore();
        var champ = new FieldDefinition { Label = "Poste", Type = FieldType.Texte };
        var result = store.AddField(champ);

        Assert.True(result.Success);
        Assert.NotNull(champ.Id);
        Assert.Equal(2, store.Fields.Count);
        Assert.Equal(1, champ.Ordre);
        Assert.Contains(store.Activity, a => a.Type == ActivityTypes.FieldsChange);
    }

    [Fact]
    public void AddField_LibelleVide_Refuse()
    {
        var store = NewStore();
        var result = store.AddField(new FieldDefinition { Label = "  ", Type = FieldType.Texte });

        Assert.False(result.Success);
        Assert.Contains("libellé", result.Error);
    }

    [Fact]
    public void AddField_ListeSansOption_Refuse()
    {
        var store = NewStore();
        var result = store.AddField(new FieldDefinition { Label = "Secteur", Type = FieldType.Liste, Options = new() });

        Assert.False(result.Success);
        Assert.Contains("liste", result.Error);
    }

    [Fact]
    public void UpdateField_ModifieLeChamp()
    {
        var store = NewStore();
        var champ = store.Fields.First();
        champ.Label = "Nouveau libellé";
        var result = store.UpdateField(champ);

        Assert.True(result.Success);
        Assert.Equal("Nouveau libellé", store.Fields.First(f => f.Id == champ.Id).Label);
    }

    [Fact]
    public void UpdateField_Inexistant_Refuse()
    {
        var store = NewStore();
        var result = store.UpdateField(new FieldDefinition { Id = Guid.NewGuid().ToString("N"), Label = "X" });

        Assert.False(result.Success);
    }

    [Fact]
    public void DeleteField_SupprimeEtRenvoieLesClesFichiers()
    {
        var store = NewStore();
        // Ajoute un champ image et un utilisateur avec fichier
        var champ = new FieldDefinition { Label = "Pièce d'identité", Type = FieldType.Image };
        store.AddField(champ);

        var draft = new UserAccount
        {
            Prenom = "Nora",
            Nom = "Personne",
            Identifiant = "npersonne",
            Email = "npersonne@gestionlocal.fr",
            Role = "Utilisateur",
            Actif = true,
            MotDePasse = "secret123",
            Champs = { [champ.Id] = new FieldValue { Fichier = new FileRef { Key = "cle-blob-1", Nom = "scan.pdf", Mime = "application/pdf", Taille = 123 } } }
        };
        store.AddUser(draft);

        var (result, fileKeys) = store.DeleteField(champ.Id);

        Assert.True(result.Success);
        Assert.Single(fileKeys);
        Assert.Equal("cle-blob-1", fileKeys[0]);
        Assert.DoesNotContain(store.Fields, f => f.Id == champ.Id);
        Assert.False(store.FindUser(draft.Id)!.Champs.ContainsKey(champ.Id));
    }

    [Fact]
    public void MoveField_Reordonne()
    {
        var store = NewStore();
        var first = store.Fields.First();
        var second = new FieldDefinition { Label = "Deuxième", Type = FieldType.Texte };
        Assert.True(store.AddField(second).Success);

        store.MoveField(first.Id, +1);

        var ordered = store.Fields.ToList();
        Assert.Equal(second.Id, ordered.First().Id);
        Assert.Equal(first.Id, ordered.Skip(1).First().Id);
    }

    [Fact]
    public void Activity_CapA500Entrees()
    {
        var store = NewStore();
        for (var i = 0; i < 520; i++)
            store.LogActivity(ActivityTypes.Update, "Entrée " + i);

        Assert.Equal(500, store.Activity.Count);
    }

    [Fact]
    public void Persistance_ReloadDepuisLeStockage()
    {
        var provider = new MemoryStorageProvider();
        var store = new DataStore(provider);
        store.EnsureInitialized();
        var id = store.Users.First().Id;
        store.ResetPassword(id);

        var reloaded = new DataStore(provider);
        reloaded.EnsureInitialized();

        Assert.Equal(store.Users.Count, reloaded.Users.Count);
        Assert.True(PasswordCipher.Verify(reloaded.Users.First(u => u.Id == id).MotDePasse, DataStore.DEFAULT_PASSWORD));
    }
}
