using GestionUtilisateurs.Shared.Models;

namespace GestionUtilisateurs.Shared.Services;

/// <summary>
/// Liens de famille entre membres : chaque lien est enregistré des deux côtés
/// (X parent de Y ⇔ Y enfant de X). Grands-parents, petits-enfants et frères
/// et sœurs par un parent commun en sont déduits.
/// </summary>
public partial class DataStore
{
    /// <summary>La famille d'un membre, déduite de ses liens.</summary>
    public sealed record FamilleVue(
        IReadOnlyList<UserAccount> Parents,
        IReadOnlyList<UserAccount> Enfants,
        IReadOnlyList<UserAccount> Conjoints,
        IReadOnlyList<UserAccount> FreresSoeurs,
        IReadOnlyList<UserAccount> GrandsParents,
        IReadOnlyList<UserAccount> PetitsEnfants)
    {
        public bool EstVide => Parents.Count + Enfants.Count + Conjoints.Count + FreresSoeurs.Count
            + GrandsParents.Count + PetitsEnfants.Count == 0;
    }

    private List<UserAccount> Lies(UserAccount u, string type)
        => (u.Famille ?? new List<LienFamille>())
            .Where(l => l.Type == type)
            .Select(l => FindUserByIdentifier(l.Identifiant))
            .OfType<UserAccount>()
            .Where(x => x.Id != u.Id)
            .DistinctBy(x => x.Id)
            .OrderBy(x => x.NomComplet, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    public FamilleVue Famille(UserAccount u)
    {
        var parents = Lies(u, LiensFamille.Parent);
        var enfants = Lies(u, LiensFamille.Enfant);
        var conjoints = Lies(u, LiensFamille.Conjoint);
        // Frères et sœurs : déclarés, ou enfants d'un même parent.
        var fratrie = Lies(u, LiensFamille.Fratrie)
            .Concat(parents.SelectMany(p => Lies(p, LiensFamille.Enfant)))
            .Where(x => x.Id != u.Id)
            .DistinctBy(x => x.Id)
            .OrderBy(x => x.NomComplet, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var grandsParents = parents.SelectMany(p => Lies(p, LiensFamille.Parent))
            .Where(x => x.Id != u.Id).DistinctBy(x => x.Id).ToList();
        var petitsEnfants = enfants.SelectMany(e => Lies(e, LiensFamille.Enfant))
            .Where(x => x.Id != u.Id).DistinctBy(x => x.Id).ToList();
        return new FamilleVue(parents, enfants, conjoints, fratrie, grandsParents, petitsEnfants);
    }

    /// <summary>Lien explicite entre deux fiches (null si aucun).</summary>
    public string? LienEntre(UserAccount u, string autreIdentifiant)
        => (u.Famille ?? new List<LienFamille>())
            .FirstOrDefault(l => l.Identifiant.Equals(autreIdentifiant, StringComparison.OrdinalIgnoreCase))?.Type;

    /// <summary>Déclare que <paramref name="autreIdentifiant"/> est le <paramref name="type"/> du membre <paramref name="userId"/>.</summary>
    public OperationResult AjouterLienFamille(string userId, string autreIdentifiant, string type)
    {
        if (!Can(Permissions.UsersManage)) return OperationResult.Fail(L.T("Action réservée à la gestion des utilisateurs."));
        if (!LiensFamille.Tous.Contains(type)) return OperationResult.Fail(L.T("Type de lien inconnu."));
        var moi = _users.FirstOrDefault(u => u.Id == userId);
        var autre = FindUserByIdentifier(autreIdentifiant ?? "");
        if (moi == null || autre == null) return OperationResult.Fail(L.T("Membre introuvable."));
        if (moi.Id == autre.Id) return OperationResult.Fail(L.T("Un membre ne peut pas être lié à lui-même."));
        if (LienEntre(moi, autre.Identifiant) is { } existant)
            return OperationResult.Fail(L.F("{0} est déjà lié(e) à cette fiche ({1}).", autre.NomComplet, L.T(LiensFamille.Libelle(existant)).ToLowerInvariant()));
        if (type is LiensFamille.Parent or LiensFamille.Enfant)
        {
            // Pas de boucle : on ne peut pas être son propre ancêtre.
            var parent = type == LiensFamille.Parent ? autre : moi;
            var enfant = type == LiensFamille.Parent ? moi : autre;
            if (EstAncetre(enfant, parent))
                return OperationResult.Fail(L.F("{0} est déjà un ascendant de {1}.", enfant.NomComplet, parent.NomComplet));
        }

        moi.Famille ??= new List<LienFamille>();
        autre.Famille ??= new List<LienFamille>();
        moi.Famille.Add(new LienFamille { Identifiant = autre.Identifiant, Type = type });
        autre.Famille.RemoveAll(l => l.Identifiant.Equals(moi.Identifiant, StringComparison.OrdinalIgnoreCase));
        autre.Famille.Add(new LienFamille { Identifiant = moi.Identifiant, Type = LiensFamille.Inverse(type) });
        SaveUsers();
        LogActivity(ActivityTypes.Update, "Lien de famille : " + autre.Identifiant + " (" + L.EnFrancais(() => LiensFamille.Libelle(type)).ToLowerInvariant() + ") de " + moi.Identifiant);
        DataChanged?.Invoke();
        return OperationResult.Ok();
    }

    /// <summary>Retire le lien entre deux membres, des deux côtés.</summary>
    public OperationResult RetirerLienFamille(string userId, string autreIdentifiant)
    {
        if (!Can(Permissions.UsersManage)) return OperationResult.Fail(L.T("Action réservée à la gestion des utilisateurs."));
        var moi = _users.FirstOrDefault(u => u.Id == userId);
        if (moi == null) return OperationResult.Fail(L.T("Membre introuvable."));
        var n = (moi.Famille ?? new List<LienFamille>()).RemoveAll(l => l.Identifiant.Equals(autreIdentifiant, StringComparison.OrdinalIgnoreCase));
        var autre = FindUserByIdentifier(autreIdentifiant);
        if (autre != null)
            n += (autre.Famille ?? new List<LienFamille>()).RemoveAll(l => l.Identifiant.Equals(moi.Identifiant, StringComparison.OrdinalIgnoreCase));
        if (n == 0) return OperationResult.Fail(L.T("Aucun lien à retirer."));
        SaveUsers();
        LogActivity(ActivityTypes.Update, "Lien de famille retiré entre " + moi.Identifiant + " et " + autreIdentifiant);
        DataChanged?.Invoke();
        return OperationResult.Ok();
    }

    /// <summary>Vrai si <paramref name="a"/> est parent, grand-parent… de <paramref name="b"/>.</summary>
    private bool EstAncetre(UserAccount a, UserAccount b)
    {
        var vus = new HashSet<string>(StringComparer.Ordinal);
        var file = new Queue<UserAccount>(Lies(b, LiensFamille.Parent));
        while (file.Count > 0)
        {
            var p = file.Dequeue();
            if (p.Id == a.Id) return true;
            if (!vus.Add(p.Id)) continue;
            foreach (var gp in Lies(p, LiensFamille.Parent)) file.Enqueue(gp);
        }
        return false;
    }

    /// <summary>Responsable légal affiché : celui saisi, sinon un parent lié à la fiche.</summary>
    public (string Nom, string Telephone) ResponsableAffiche(UserAccount u)
    {
        if (!string.IsNullOrWhiteSpace(u.ResponsableLegal)) return (u.ResponsableLegal.Trim(), (u.ResponsableTelephone ?? "").Trim());
        var parent = Lies(u, LiensFamille.Parent).FirstOrDefault(p => !p.EstMineur);
        return parent == null ? ("", "") : (L.F("{0} (parent)", parent.NomComplet), parent.Telephone ?? "");
    }
}
