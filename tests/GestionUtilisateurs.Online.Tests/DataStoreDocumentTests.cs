using GestionUtilisateurs.Shared.Models;
using GestionUtilisateurs.Shared.Services;

namespace GestionUtilisateurs.Blazor.Tests;

public class DataStoreDocumentTests
{
    private static DataStore NewStore()
    {
        var store = new DataStore(new MemoryStorageProvider());
        store.EnsureInitialized();
        return store;
    }

    private static DocumentItem SampleDoc() => new()
    {
        Titre = "Facture janvier",
        Description = "Facture d'électricité",
        Categorie = DocumentCategories.Factures,
        Tags = new List<string> { "maison", "2026" },
        FileName = "facture.pdf",
        Mime = "application/pdf",
        Taille = 1234,
        Data = "data:application/pdf;base64,AAA"
    };

    [Fact]
    public void AddFolder_Simple_Success()
    {
        var store = NewStore();

        var result = store.AddFolder(new DocumentFolder { Nom = "Administratif" });

        Assert.True(result.Success);
        var folder = Assert.Single(store.Folders);
        Assert.Equal("Administratif", folder.Nom);
        Assert.False(string.IsNullOrWhiteSpace(folder.Id));
    }

    [Fact]
    public void AddFolder_SansNom_Echec()
    {
        var store = NewStore();

        var result = store.AddFolder(new DocumentFolder { Nom = "  " });

        Assert.False(result.Success);
        Assert.Empty(store.Folders);
    }

    [Fact]
    public void AddFolder_Doublon_MemeEmplacement_Echec()
    {
        var store = NewStore();
        store.AddFolder(new DocumentFolder { Nom = "Travail" });

        var result = store.AddFolder(new DocumentFolder { Nom = "travail" });

        Assert.False(result.Success);
        Assert.Single(store.Folders);
    }

    [Fact]
    public void DeleteFolder_ContenuRemonteAuParent()
    {
        var store = NewStore();
        store.AddFolder(new DocumentFolder { Nom = "Parent" });
        var parent = store.Folders.Single();
        store.AddFolder(new DocumentFolder { Nom = "Enfant", ParentId = parent.Id });
        var enfant = store.Folders.Single(f => f.Nom == "Enfant");
        var doc = SampleDoc();
        doc.FolderId = enfant.Id;
        store.AddDocument(doc);

        var result = store.DeleteFolder(enfant.Id);

        Assert.True(result.Success);
        Assert.Single(store.Folders);
        Assert.Equal(parent.Id, store.Documents.Single().FolderId);
    }

    [Fact]
    public void AddDocument_Simple_Success()
    {
        var store = NewStore();

        var result = store.AddDocument(SampleDoc());

        Assert.True(result.Success);
        var doc = Assert.Single(store.Documents);
        Assert.Equal("Facture janvier", doc.Titre);
        Assert.Equal(2, doc.Tags.Count);
        Assert.False(string.IsNullOrWhiteSpace(doc.DateCreation));
    }

    [Fact]
    public void AddDocument_SansTitre_Echec()
    {
        var store = NewStore();
        var draft = SampleDoc();
        draft.Titre = "";

        var result = store.AddDocument(draft);

        Assert.False(result.Success);
        Assert.Empty(store.Documents);
    }

    [Fact]
    public void AddDocument_SansFichier_Echec()
    {
        var store = NewStore();
        var draft = SampleDoc();
        draft.Data = "";

        var result = store.AddDocument(draft);

        Assert.False(result.Success);
        Assert.Empty(store.Documents);
    }

    [Fact]
    public void TrashRestore_Cycle_Success()
    {
        var store = NewStore();
        store.AddDocument(SampleDoc());
        var id = store.Documents.Single().Id;

        Assert.True(store.TrashDocuments(new[] { id }).Success);
        Assert.True(store.Documents.Single().Supprime);

        Assert.True(store.RestoreDocuments(new[] { id }).Success);
        var restored = store.Documents.Single();
        Assert.False(restored.Supprime);
        Assert.Null(restored.DateSuppression);
    }

    [Fact]
    public void Purge_SupprimeDefinitivement()
    {
        var store = NewStore();
        store.AddDocument(SampleDoc());
        var id = store.Documents.Single().Id;
        store.TrashDocuments(new[] { id });

        var result = store.PurgeDocument(id);

        Assert.True(result.Success);
        Assert.Empty(store.Documents);
    }

    [Fact]
    public void EmptyTrash_VideLaCorbeille()
    {
        var store = NewStore();
        store.AddDocument(SampleDoc());
        store.AddDocument(new DocumentItem
        {
            Titre = "Note",
            FileName = "note.txt",
            Mime = "text/plain",
            Taille = 10,
            Data = "data:text/plain;base64,AAA"
        });
        var ids = store.Documents.Select(d => d.Id).ToList();
        store.TrashDocuments(new[] { ids[0] });

        var result = store.EmptyTrash();

        Assert.True(result.Success);
        Assert.Single(store.Documents);
        Assert.False(store.Documents.Single().Supprime);
    }

    [Fact]
    public void MoveDocuments_ChangeDossier()
    {
        var store = NewStore();
        store.AddFolder(new DocumentFolder { Nom = "A" });
        store.AddFolder(new DocumentFolder { Nom = "B" });
        var b = store.Folders.Single(f => f.Nom == "B");
        store.AddDocument(SampleDoc());
        var id = store.Documents.Single().Id;

        var result = store.MoveDocuments(new[] { id }, b.Id);

        Assert.True(result.Success);
        Assert.Equal(b.Id, store.Documents.Single().FolderId);
    }

    [Fact]
    public void SetDocumentFavorite_Bascule()
    {
        var store = NewStore();
        store.AddDocument(SampleDoc());
        var id = store.Documents.Single().Id;

        Assert.True(store.SetDocumentFavorite(id, true).Success);
        Assert.True(store.Documents.Single().Favori);
        Assert.True(store.SetDocumentFavorite(id, false).Success);
        Assert.False(store.Documents.Single().Favori);
    }

    [Fact]
    public void UpdateEtMove_RenseignentModifiePar()
    {
        var store = NewStore();
        Assert.True(store.Login("admin", "admin123").Success);
        store.AddDocument(SampleDoc());
        var id = store.Documents.Single().Id;
        Assert.Equal("admin", store.Documents.Single().CreatedBy);

        Assert.True(store.UpdateDocument(id, new DocumentItem { Titre = "Facture février" }).Success);
        Assert.Equal("admin", store.Documents.Single().ModifiePar);

        store.AddFolder(new DocumentFolder { Nom = "C" });
        var c = store.Folders.Single(f => f.Nom == "C");
        Assert.True(store.MoveDocuments(new[] { id }, c.Id).Success);
        Assert.Equal("admin", store.Documents.Single().ModifiePar);
    }
}
