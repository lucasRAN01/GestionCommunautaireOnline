using System.Globalization;
using GestionUtilisateurs.Shared.Models;

namespace GestionUtilisateurs.Shared.Services;

/// <summary>Critères d'affichage de la liste de documents (recherche, dossier courant, filtres, tri).</summary>
public sealed class DocumentQuery
{
    public string Search { get; set; } = "";
    /// <summary>Dossier courant ; null = racine.</summary>
    public string? FolderId { get; set; }
    public HashSet<string> Categories { get; } = new(StringComparer.Ordinal);
    public string Kind { get; set; } = DocumentBrowser.KindAll;
    public bool FavoritesOnly { get; set; }
    public string? UserId { get; set; }
    public string Sort { get; set; } = DocumentBrowser.SortRecent;

    public bool IsSearching => !string.IsNullOrWhiteSpace(Search);

    /// <summary>Recherche et favoris portent sur tous les dossiers, pas seulement le dossier courant.</summary>
    public bool IsGlobalScope => IsSearching || FavoritesOnly;

    public int ActiveFilterCount => Categories.Count
        + (Kind != DocumentBrowser.KindAll ? 1 : 0)
        + (FavoritesOnly ? 1 : 0)
        + (UserId != null ? 1 : 0);

    public bool HasActiveFilters => ActiveFilterCount > 0;

    /// <summary>Réinitialise les filtres (catégories, type, favoris, utilisateur) sans toucher à la recherche ni au tri.</summary>
    public void ResetFilters()
    {
        Categories.Clear();
        Kind = DocumentBrowser.KindAll;
        FavoritesOnly = false;
        UserId = null;
    }
}

/// <summary>Logique pure de la page Documents (filtrage, tri, fil d'Ariane, arborescence),
/// partagée par l'application Web et l'application MAUI et testable sans interface.</summary>
public static class DocumentBrowser
{
    public const string KindAll = "tous";

    public const string SortRecent = "recent";
    public const string SortOldest = "ancien";
    public const string SortName = "nom";
    public const string SortSize = "taille";

    public static readonly (string Key, string Label)[] SortChoices =
    {
        (SortRecent, "Récents"), (SortOldest, "Plus anciens"), (SortName, "Nom A-Z"), (SortSize, "Taille")
    };

    public static readonly (string Key, string Label)[] KindChoices =
    {
        (KindAll, "Tous"), ("pdf", "PDF"), ("image", "Images"), ("autre", "Autres")
    };

    public static string SortLabel(string? sort) =>
        SortChoices.FirstOrDefault(c => c.Key == sort).Label ?? "Récents";

    private const CompareOptions Loose = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    private static readonly CompareInfo Compare = ResolveCompareInfo();

    private static readonly StringComparer NameComparer = ResolveNameComparer();

    private static CompareInfo ResolveCompareInfo()
    {
        try { return CultureInfo.GetCultureInfo("fr-FR").CompareInfo; }
        catch (CultureNotFoundException) { return CultureInfo.InvariantCulture.CompareInfo; }
    }

    private static StringComparer ResolveNameComparer()
    {
        try { return StringComparer.Create(CultureInfo.GetCultureInfo("fr-FR"), CompareOptions.IgnoreCase); }
        catch (CultureNotFoundException) { return StringComparer.OrdinalIgnoreCase; }
    }

    /// <summary>Recherche insensible à la casse et aux accents (« electricite » trouve « Électricité »).</summary>
    public static bool Matches(string? text, string term)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(term)) return false;
        try { return Compare.IndexOf(text, term, Loose) >= 0; }
        catch (ArgumentException) { return text.Contains(term, StringComparison.OrdinalIgnoreCase); }
    }

    /// <summary>Documents visibles (hors corbeille) selon les critères, triés.</summary>
    public static List<DocumentItem> Filter(IEnumerable<DocumentItem> documents, DocumentQuery query)
    {
        var term = query.Search.Trim();
        var result = documents.Where(d => !d.Supprime);

        if (term.Length > 0)
            result = result.Where(d =>
                Matches(d.Titre, term) ||
                Matches(d.Description, term) ||
                Matches(d.FileName, term) ||
                Matches(DocumentCategories.Label(d.Categorie), term) ||
                d.Tags.Any(t => Matches(t, term)));

        if (!query.IsGlobalScope)
            result = result.Where(d => string.Equals(d.FolderId ?? "", query.FolderId ?? "", StringComparison.Ordinal));

        if (query.Categories.Count > 0)
            result = result.Where(d => query.Categories.Contains(d.Categorie));
        if (query.Kind != KindAll)
            result = result.Where(d => DocumentKinds.KindOf(d.Mime) == query.Kind);
        if (query.FavoritesOnly)
            result = result.Where(d => d.Favori);
        if (query.UserId != null)
            result = result.Where(d => d.UtilisateurId == query.UserId);

        return Sort(result, query.Sort).ToList();
    }

    public static IEnumerable<DocumentItem> Sort(IEnumerable<DocumentItem> documents, string? sort) => sort switch
    {
        SortName => documents.OrderBy(d => d.Titre, NameComparer).ThenByDescending(d => d.DateModification, StringComparer.Ordinal),
        SortSize => documents.OrderByDescending(d => d.Taille).ThenBy(d => d.Titre, NameComparer),
        SortOldest => documents.OrderBy(d => d.DateModification, StringComparer.Ordinal).ThenBy(d => d.DateCreation, StringComparer.Ordinal),
        _ => documents.OrderByDescending(d => d.DateModification, StringComparer.Ordinal).ThenByDescending(d => d.DateCreation, StringComparer.Ordinal)
    };

    /// <summary>Chemin Racine → dossier courant (exclut la racine), protégé contre les cycles et les parents absents.</summary>
    public static List<DocumentFolder> Breadcrumb(IReadOnlyDictionary<string, DocumentFolder> folders, string? currentId)
    {
        var path = new List<DocumentFolder>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var cursor = currentId;
        while (!string.IsNullOrWhiteSpace(cursor) && seen.Add(cursor) && folders.TryGetValue(cursor, out var folder))
        {
            path.Insert(0, folder);
            cursor = folder.ParentId;
        }
        return path;
    }

    /// <summary>Chemin lisible « Parent / Enfant » ; « Racine » pour la racine ou un dossier introuvable.</summary>
    public static string FolderPath(IReadOnlyDictionary<string, DocumentFolder> folders, string? folderId)
    {
        var path = Breadcrumb(folders, folderId);
        return path.Count == 0 ? "Racine" : string.Join(" / ", path.Select(f => f.Nom));
    }

    /// <summary>Nombre d'éléments directs (documents hors corbeille + sous-dossiers) par dossier, en un seul passage.</summary>
    public static Dictionary<string, int> ItemCounts(IEnumerable<DocumentItem> documents, IEnumerable<DocumentFolder> folders)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var d in documents)
            if (!d.Supprime && !string.IsNullOrWhiteSpace(d.FolderId))
                counts[d.FolderId] = counts.GetValueOrDefault(d.FolderId) + 1;
        foreach (var f in folders)
            if (!string.IsNullOrWhiteSpace(f.ParentId))
                counts[f.ParentId] = counts.GetValueOrDefault(f.ParentId) + 1;
        return counts;
    }

    /// <summary>Arborescence aplatie en profondeur d'abord (ordre alphabétique) pour la barre latérale.
    /// Les dossiers dont le parent est absent sont rattachés à la racine ; les cycles sont ignorés.</summary>
    public static List<(DocumentFolder Folder, int Depth)> FolderTree(IEnumerable<DocumentFolder> folders)
    {
        var all = folders.ToList();
        var ids = new HashSet<string>(all.Select(f => f.Id), StringComparer.Ordinal);
        var children = all
            .GroupBy(f => !string.IsNullOrWhiteSpace(f.ParentId) && ids.Contains(f.ParentId) ? f.ParentId! : "")
            .ToDictionary(g => g.Key, g => g.OrderBy(f => f.Nom, NameComparer).ToList(), StringComparer.Ordinal);

        var result = new List<(DocumentFolder, int)>();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        void Walk(string parentKey, int depth)
        {
            if (!children.TryGetValue(parentKey, out var list)) return;
            foreach (var f in list)
            {
                if (!visited.Add(f.Id)) continue;
                result.Add((f, depth));
                Walk(f.Id, depth + 1);
            }
        }

        Walk("", 0);
        // Dossiers pris dans un cycle de parents : jamais atteints depuis la racine, rattachés à la racine.
        foreach (var f in all.OrderBy(f => f.Nom, NameComparer))
        {
            if (!visited.Add(f.Id)) continue;
            result.Add((f, 0));
            Walk(f.Id, 1);
        }
        return result;
    }

    /// <summary>« 0 document », « 1 document », « 3 documents ».</summary>
    public static string CountLabel(int count, string singular, string? plural = null) =>
        count + " " + (count > 1 ? plural ?? singular + "s" : singular);
}
