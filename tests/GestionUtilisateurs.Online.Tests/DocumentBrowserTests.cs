using GestionUtilisateurs.Shared.Models;
using GestionUtilisateurs.Shared.Services;

namespace GestionUtilisateurs.Blazor.Tests;

/// <summary>Logique de la page Documents (filtres, tri, fil d'Ariane, arborescence) partagée Web / MAUI.</summary>
public class DocumentBrowserTests
{
    private static DocumentItem Doc(string id, string titre, string? folder = null, string mime = "application/pdf",
        string cat = DocumentCategories.Autre, bool fav = false, long taille = 100, string date = "2026-01-01T10:00:00+00:00",
        bool supprime = false, string? user = null, params string[] tags) => new()
    {
        Id = id,
        Titre = titre,
        FolderId = folder,
        Mime = mime,
        Categorie = cat,
        Favori = fav,
        Taille = taille,
        DateCreation = date,
        DateModification = date,
        Supprime = supprime,
        UtilisateurId = user,
        Tags = tags.ToList()
    };

    private static List<DocumentItem> Sample() => new()
    {
        Doc("a", "Facture électricité", folder: "f1", cat: DocumentCategories.Factures, date: "2026-03-01T10:00:00+00:00", taille: 500),
        Doc("b", "Contrat de bail", fav: true, cat: DocumentCategories.Contrats, date: "2026-02-01T10:00:00+00:00", taille: 2000),
        Doc("c", "Photo réunion", mime: "image/png", cat: DocumentCategories.Photos, date: "2026-04-01T10:00:00+00:00", taille: 50, user: "u1"),
        Doc("d", "zèbre notes", mime: "text/plain", date: "2026-01-15T10:00:00+00:00", taille: 10, tags: "maison"),
        Doc("e", "Ancien", supprime: true, date: "2026-05-01T10:00:00+00:00")
    };

    [Fact]
    public void Filter_RootFolder_ExcludesTrashAndSubfolderDocs_SortedRecentFirst()
    {
        var result = DocumentBrowser.Filter(Sample(), new DocumentQuery());

        Assert.Equal(new[] { "c", "b", "d" }, result.Select(d => d.Id));
    }

    [Fact]
    public void Filter_Search_IsAccentAndCaseInsensitive_AndSpansAllFolders()
    {
        var query = new DocumentQuery { Search = "ELECTRICITE", FolderId = null };

        var result = DocumentBrowser.Filter(Sample(), query);

        Assert.True(query.IsGlobalScope);
        Assert.Equal("a", Assert.Single(result).Id);
    }

    [Theory]
    [InlineData("maison", "d")]
    [InlineData("contrats", "b")]
    [InlineData("reunion", "c")]
    public void Filter_Search_MatchesTagsCategoryLabelAndTitle(string term, string expectedId)
    {
        var result = DocumentBrowser.Filter(Sample(), new DocumentQuery { Search = term });

        Assert.Equal(expectedId, Assert.Single(result).Id);
    }

    [Fact]
    public void Filter_FavoritesOnly_IsGlobalAcrossFolders()
    {
        var docs = Sample();
        docs.Add(Doc("f", "Favori rangé", folder: "f1", fav: true));

        var result = DocumentBrowser.Filter(docs, new DocumentQuery { FavoritesOnly = true, FolderId = "autre" });

        Assert.Equal(new[] { "b", "f" }, result.Select(d => d.Id).OrderBy(x => x));
    }

    [Fact]
    public void Filter_KindCategoryAndUser_Combine()
    {
        var docs = Sample();

        Assert.Equal("c", Assert.Single(DocumentBrowser.Filter(docs, new DocumentQuery { Kind = "image" })).Id);
        Assert.Equal("d", Assert.Single(DocumentBrowser.Filter(docs, new DocumentQuery { Kind = "autre" })).Id);
        Assert.Equal("c", Assert.Single(DocumentBrowser.Filter(docs, new DocumentQuery { UserId = "u1" })).Id);

        var byCat = new DocumentQuery();
        byCat.Categories.Add(DocumentCategories.Contrats);
        Assert.Equal("b", Assert.Single(DocumentBrowser.Filter(docs, byCat)).Id);
    }

    [Fact]
    public void Filter_InsideFolder_ShowsOnlyThatFolder()
    {
        var result = DocumentBrowser.Filter(Sample(), new DocumentQuery { FolderId = "f1" });

        Assert.Equal("a", Assert.Single(result).Id);
    }

    [Theory]
    [InlineData(DocumentBrowser.SortRecent, "c,b,d")]
    [InlineData(DocumentBrowser.SortOldest, "d,b,c")]
    [InlineData(DocumentBrowser.SortSize, "b,c,d")]
    [InlineData(DocumentBrowser.SortName, "b,c,d")]
    public void Filter_Sorts(string sort, string expected)
    {
        var result = DocumentBrowser.Filter(Sample(), new DocumentQuery { Sort = sort });

        Assert.Equal(expected, string.Join(",", result.Select(d => d.Id)));
    }

    [Fact]
    public void Sort_ByName_IgnoresCaseAndAccents()
    {
        var docs = new[] { Doc("1", "zoo"), Doc("2", "Éclair"), Doc("3", "abricot"), Doc("4", "Etoile") };

        var result = DocumentBrowser.Sort(docs, DocumentBrowser.SortName).Select(d => d.Id);

        Assert.Equal(new[] { "3", "2", "4", "1" }, result);
    }

    [Fact]
    public void Query_ActiveFilterCount_AndReset()
    {
        var q = new DocumentQuery { Kind = "pdf", FavoritesOnly = true, UserId = "u1", Search = "x", Sort = DocumentBrowser.SortSize };
        q.Categories.Add(DocumentCategories.Travail);
        q.Categories.Add(DocumentCategories.Scans);

        Assert.Equal(5, q.ActiveFilterCount);
        Assert.True(q.HasActiveFilters);

        q.ResetFilters();

        Assert.Equal(0, q.ActiveFilterCount);
        Assert.Equal("x", q.Search);
        Assert.Equal(DocumentBrowser.SortSize, q.Sort);
    }

    private static Dictionary<string, DocumentFolder> Folders(params DocumentFolder[] folders) =>
        folders.ToDictionary(f => f.Id);

    [Fact]
    public void Breadcrumb_ReturnsPathFromRoot()
    {
        var folders = Folders(
            new DocumentFolder { Id = "a", Nom = "Administratif" },
            new DocumentFolder { Id = "b", Nom = "Factures", ParentId = "a" },
            new DocumentFolder { Id = "c", Nom = "Électricité", ParentId = "b" });

        Assert.Equal(new[] { "a", "b", "c" }, DocumentBrowser.Breadcrumb(folders, "c").Select(f => f.Id));
        Assert.Empty(DocumentBrowser.Breadcrumb(folders, null));
        Assert.Equal("Administratif / Factures / Électricité", DocumentBrowser.FolderPath(folders, "c"));
        Assert.Equal("Racine", DocumentBrowser.FolderPath(folders, "inconnu"));
    }

    [Fact]
    public void Breadcrumb_ToleratesCyclesAndMissingParents()
    {
        var folders = Folders(
            new DocumentFolder { Id = "a", Nom = "A", ParentId = "b" },
            new DocumentFolder { Id = "b", Nom = "B", ParentId = "a" },
            new DocumentFolder { Id = "o", Nom = "Orphelin", ParentId = "absent" });

        Assert.Equal(2, DocumentBrowser.Breadcrumb(folders, "a").Count);
        Assert.Equal("o", Assert.Single(DocumentBrowser.Breadcrumb(folders, "o")).Id);
    }

    [Fact]
    public void FolderTree_IsDepthFirstAlphabetical_WithOrphansAndCyclesAtRoot()
    {
        var tree = DocumentBrowser.FolderTree(new[]
        {
            new DocumentFolder { Id = "z", Nom = "Zeta" },
            new DocumentFolder { Id = "a", Nom = "Alpha" },
            new DocumentFolder { Id = "a2", Nom = "Sous-alpha", ParentId = "a" },
            new DocumentFolder { Id = "o", Nom = "Orphelin", ParentId = "absent" },
            new DocumentFolder { Id = "c1", Nom = "Cycle 1", ParentId = "c2" },
            new DocumentFolder { Id = "c2", Nom = "Cycle 2", ParentId = "c1" }
        });

        Assert.Equal(new[] { "a", "a2", "o", "z", "c1", "c2" }, tree.Select(t => t.Folder.Id));
        Assert.Equal(new[] { 0, 1, 0, 0, 0, 1 }, tree.Select(t => t.Depth));
    }

    [Fact]
    public void ItemCounts_CountsDirectDocumentsAndSubfolders_IgnoringTrash()
    {
        var docs = new[]
        {
            Doc("1", "x", folder: "a"), Doc("2", "y", folder: "a"), Doc("3", "z", folder: "a", supprime: true), Doc("4", "r")
        };
        var folders = new[] { new DocumentFolder { Id = "a" }, new DocumentFolder { Id = "b", ParentId = "a" } };

        var counts = DocumentBrowser.ItemCounts(docs, folders);

        Assert.Equal(3, counts["a"]);
        Assert.False(counts.ContainsKey("b"));
    }

    [Theory]
    [InlineData(0, "0 document")]
    [InlineData(1, "1 document")]
    [InlineData(3, "3 documents")]
    public void CountLabel_PluralizesInFrench(int count, string expected)
    {
        Assert.Equal(expected, DocumentBrowser.CountLabel(count, "document"));
    }

    [Fact]
    public void SortLabel_FallsBackToRecent()
    {
        Assert.Equal("Taille", DocumentBrowser.SortLabel(DocumentBrowser.SortSize));
        Assert.Equal("Récents", DocumentBrowser.SortLabel("inconnu"));
    }
}
