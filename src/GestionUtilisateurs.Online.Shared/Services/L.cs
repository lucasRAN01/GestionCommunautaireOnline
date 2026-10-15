using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace GestionUtilisateurs.Shared.Services;

/// <summary>
/// Traduction de l'interface. Le texte français sert de clé : <c>L.T("Utilisateurs")</c>
/// renvoie « Users » en anglais et « Mpikambana » en malgache. Un texte absent des
/// fichiers I18n/&lt;langue&gt;/*.json reste en français, rien ne casse.
/// La langue est un choix personnel de chaque membre (comme le thème).
/// </summary>
public static class L
{
    public const string Fr = "fr";
    public const string En = "en";
    public const string Mg = "mg";

    /// <summary>Clé du navigateur qui retient la dernière langue choisie (avant connexion).</summary>
    public const string CleNavigateur = "gu2:langue";

    public static readonly IReadOnlyList<(string Code, string Nom)> Langues = new[]
    {
        (Fr, "Français"),
        (En, "English"),
        (Mg, "Malagasy")
    };

    private static readonly Dictionary<string, Dictionary<string, string>> Dicos = Charger();

    public static string Langue { get; private set; } = Fr;

    /// <summary>Langue changée : l'application se recharge pour tout réafficher.</summary>
    public static event Action? Changed;

    public static string Normaliser(string? code) => code switch
    {
        En => En,
        Mg => Mg,
        _ => Fr
    };

    public static void Definir(string? code)
    {
        var c = Normaliser(code);
        if (c == Langue) return;
        Langue = c;
        Changed?.Invoke();
    }

    /// <summary>Traduit un texte français (le texte lui-même si aucune traduction n'existe).</summary>
    public static string T(string? fr)
    {
        if (string.IsNullOrEmpty(fr) || Langue == Fr) return fr ?? "";
        return Dicos.TryGetValue(Langue, out var d) && d.TryGetValue(fr, out var t) && !string.IsNullOrEmpty(t) ? t : fr;
    }

    /// <summary>Traduit un modèle avec des valeurs : <c>L.F("{0} membre(s)", n)</c>.</summary>
    public static string F(string fr, params object?[] valeurs)
    {
        try { return string.Format(CultureInfo.CurrentCulture, T(fr), valeurs); }
        catch (FormatException) { return string.Format(CultureInfo.CurrentCulture, fr, valeurs); }
    }

    /// <summary>Vrai si une traduction existe (utile aux tests et au suivi).</summary>
    public static bool EstTraduit(string langue, string fr)
        => Dicos.TryGetValue(langue, out var d) && d.ContainsKey(fr);

    public static IReadOnlyDictionary<string, string> Dictionnaire(string langue)
        => Dicos.TryGetValue(langue, out var d) ? d : new Dictionary<string, string>();

    private static readonly string[] MoisFr =
        { "janvier", "février", "mars", "avril", "mai", "juin", "juillet", "août", "septembre", "octobre", "novembre", "décembre" };
    private static readonly string[] JoursFr =
        { "dimanche", "lundi", "mardi", "mercredi", "jeudi", "vendredi", "samedi" };

    /// <summary>Nom du mois (1 à 12) en minuscules dans la langue choisie.</summary>
    public static string Mois(int mois) => T(MoisFr[Math.Clamp(mois, 1, 12) - 1]);

    /// <summary>Nom du jour en minuscules dans la langue choisie.</summary>
    public static string Jour(DayOfWeek jour) => T(JoursFr[(int)jour]);

    /// <summary>« mars 2026 » / « March 2026 » / « Martsa 2026 ».</summary>
    public static string MoisAnnee(DateTime d) => Mois(d.Month) + " " + d.Year.ToString(CultureInfo.InvariantCulture);

    /// <summary>« lundi 5 mars 2026 » dans la langue choisie.</summary>
    public static string DateLongue(DateTime d) => Langue == En
        ? Majuscule(Jour(d.DayOfWeek)) + " " + d.Day + " " + Majuscule(Mois(d.Month)) + " " + d.Year
        : Jour(d.DayOfWeek) + " " + d.Day + " " + Mois(d.Month) + " " + d.Year;

    public static string Majuscule(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];

    private static Dictionary<string, Dictionary<string, string>> Charger()
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var asm = typeof(L).Assembly;
        foreach (var nom in asm.GetManifestResourceNames().OrderBy(n => n, StringComparer.Ordinal))
        {
            var chemin = nom.Replace('\\', '/');
            if (!chemin.StartsWith("i18n/", StringComparison.Ordinal) || !chemin.EndsWith(".json", StringComparison.Ordinal)) continue;
            var parts = chemin.Split('/');
            if (parts.Length < 3) continue;
            var langue = parts[1];
            try
            {
                using var flux = asm.GetManifestResourceStream(nom);
                if (flux == null) continue;
                var dico = JsonSerializer.Deserialize<Dictionary<string, string>>(flux);
                if (dico == null) continue;
                if (!result.TryGetValue(langue, out var cible))
                    result[langue] = cible = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var (k, v) in dico)
                    if (!string.IsNullOrEmpty(v)) cible[k] = v;
            }
            catch (JsonException)
            {
                // Fichier de traduction mal formé : ignoré, les textes restent en français.
            }
        }
        return result;
    }
}
