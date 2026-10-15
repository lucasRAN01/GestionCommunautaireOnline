using GestionUtilisateurs.Shared.Services;

namespace GestionUtilisateurs.Shared.Models;

/// <summary>Jours fériés fixes de Madagascar (dates calendaires, sans fêtes mobiles).</summary>
public static class Holidays
{
    private static readonly (int Month, int Day, string Label)[] Fixed = new[]
    {
        (1, 1, "Jour de l'An"),
        (3, 29, "Commémoration des martyrs de 1947"),
        (5, 1, "Fête du Travail"),
        (5, 25, "Jour de l'Afrique"),
        (6, 26, "Fête de l'Indépendance"),
        (8, 15, "Assomption"),
        (11, 1, "Toussaint"),
        (12, 25, "Noël")
    };

    /// <summary>Libellé du jour férié pour cette date, sinon null.</summary>
    public static string? GetFixedHoliday(DateTime date)
    {
        foreach (var (m, d, label) in Fixed)
            if (date.Month == m && date.Day == d) return L.T(label);
        return null;
    }
}