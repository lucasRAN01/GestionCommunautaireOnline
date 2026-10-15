using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GestionUtilisateurs.Shared.Models;

namespace GestionUtilisateurs.Shared.Services;

/// <summary>
/// Matricules automatiques : un format réglable dans les Paramètres
/// (ex. « M-{NNNN} » → M-0001) attribue un matricule aux nouveaux membres.
/// </summary>
public partial class DataStore
{
    private static readonly Regex JetonMatricule = new(@"\{(N+|AAAA|AA|MM|INI)\}", RegexOptions.Compiled);

    /// <summary>Message d'erreur si le format est invalide, sinon null. Un format vide (saisie manuelle) est valide.</summary>
    public static string? ValiderFormatMatricule(string? format)
    {
        var f = (format ?? "").Trim();
        if (f.Length == 0) return null;
        if (f.Length > 30) return L.T("Le format du matricule ne doit pas dépasser 30 caractères.");
        var numeros = JetonMatricule.Matches(f).Count(m => m.Groups[1].Value.StartsWith('N'));
        if (numeros != 1) return L.T("Le format doit contenir une seule fois le numéro, par exemple {NNNN}.");
        var reste = JetonMatricule.Replace(f, "");
        if (reste.Contains('{') || reste.Contains('}'))
            return L.T("Code inconnu dans le format. Codes possibles : {NNNN}, {AAAA}, {AA}, {MM}, {INI}.");
        return null;
    }

    /// <summary>Construit un matricule à partir du format, du numéro et de la date d'adhésion.</summary>
    public static string ConstruireMatricule(string format, int numero, DateTime date, UserAccount? membre = null)
    {
        return JetonMatricule.Replace(format.Trim(), m => m.Groups[1].Value switch
        {
            "AAAA" => date.ToString("yyyy", CultureInfo.InvariantCulture),
            "AA" => date.ToString("yy", CultureInfo.InvariantCulture),
            "MM" => date.ToString("MM", CultureInfo.InvariantCulture),
            "INI" => Initiales(membre),
            var n => numero.ToString(CultureInfo.InvariantCulture).PadLeft(n.Length, '0')
        });
    }

    private static string Initiales(UserAccount? u)
    {
        if (u == null) return "JR";
        var p = Replier(u.Prenom).Trim();
        var n = Replier(u.Nom).Trim();
        var s = (p.Length > 0 ? p[..1] : "") + (n.Length > 0 ? n[..1] : "");
        return s.ToUpperInvariant();
    }

    /// <summary>Exemple de matricule pour l'aperçu dans les Paramètres.</summary>
    public string ApercuMatricule(string? format)
    {
        var f = (format ?? "").Trim();
        if (f.Length == 0 || ValiderFormatMatricule(f) != null) return "";
        return ConstruireMatricule(f, ProchainNumero(f), DateTime.Today);
    }

    /// <summary>Plus grand numéro déjà utilisé avec ce format, plus un.</summary>
    private int ProchainNumero(string format)
    {
        var motif = new StringBuilder("^");
        var pos = 0;
        foreach (Match m in JetonMatricule.Matches(format))
        {
            motif.Append(Regex.Escape(format[pos..m.Index]));
            motif.Append(m.Groups[1].Value switch
            {
                "AAAA" => @"\d{4}",
                "AA" or "MM" => @"\d{2}",
                "INI" => "[A-Z]{0,2}",
                _ => @"(?<n>\d+)"
            });
            pos = m.Index + m.Length;
        }
        motif.Append(Regex.Escape(format[pos..])).Append('$');
        var re = new Regex(motif.ToString(), RegexOptions.IgnoreCase);
        var max = 0;
        foreach (var u in _users)
        {
            var mt = re.Match((u.Matricule ?? "").Trim());
            if (mt.Success && int.TryParse(mt.Groups["n"].Value, out var v) && v > max) max = v;
        }
        return max + 1;
    }

    /// <summary>Prochain matricule libre pour ce membre, ou "" si aucun format n'est réglé.</summary>
    public string ProchainMatricule(UserAccount? membre = null)
    {
        var format = (_settings.MatriculeFormat ?? "").Trim();
        if (format.Length == 0 || ValiderFormatMatricule(format) != null) return "";
        var date = DateTime.TryParse(membre?.DateCreation, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : DateTime.Today;
        var numero = ProchainNumero(format);
        string code;
        do code = ConstruireMatricule(format, numero++, date, membre);
        while (_users.Any(u => (u.Matricule ?? "").Trim().Equals(code, StringComparison.OrdinalIgnoreCase)));
        return code;
    }

    /// <summary>Donne un matricule à chaque fiche qui n'en a pas, de la plus ancienne à la plus récente.</summary>
    public (OperationResult Result, int Attribues) AttribuerMatriculesManquants()
    {
        if (!Can(Permissions.UsersManage)) return (OperationResult.Fail(L.T("Action réservée à la gestion des utilisateurs.")), 0);
        if ((_settings.MatriculeFormat ?? "").Trim().Length == 0)
            return (OperationResult.Fail(L.T("Réglez d'abord le format du matricule.")), 0);
        var sans = _users.Where(u => string.IsNullOrWhiteSpace(u.Matricule))
            .OrderBy(u => u.DateCreation ?? "", StringComparer.Ordinal).ToList();
        foreach (var u in sans) u.Matricule = ProchainMatricule(u);
        if (sans.Count > 0)
        {
            SaveUsers();
            LogActivity(ActivityTypes.Update, "Attribution de " + sans.Count + " matricule(s)");
            DataChanged?.Invoke();
        }
        return (OperationResult.Ok(), sans.Count);
    }
}
