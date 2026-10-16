using System.Text.RegularExpressions;
using GestionUtilisateurs.Shared.Models;
using GestionUtilisateurs.Shared.Services;

namespace GestionUtilisateurs.Blazor.Tests;

[CollectionDefinition("Langue", DisableParallelization = true)]
public class LangueCollection { }

[Collection("Langue")]
public class LanguesTests
{
    [Fact]
    public void Traductions_ChargeesEtRepliEnFrancais()
    {
        try
        {
            L.Definir(L.En);
            Assert.Equal("Members", L.T("Utilisateurs"));
            Assert.Equal("Texte jamais traduit", L.T("Texte jamais traduit"));
            L.Definir(L.Fr);
            Assert.Equal("Utilisateurs", L.T("Utilisateurs"));
        }
        finally { L.Definir(L.Fr); }
    }

    [Fact]
    public void Traductions_MemesTextesEnAnglaisEtEnMalgache()
    {
        var en = L.Dictionnaire(L.En);
        var mg = L.Dictionnaire(L.Mg);
        Assert.True(en.Count > 1000);
        Assert.Empty(en.Keys.Except(mg.Keys));
        Assert.Empty(mg.Keys.Except(en.Keys));
        static string Codes(string s) => string.Join(",", Regex.Matches(s, @"\{\d+[^}]*\}").Select(m => m.Value).OrderBy(v => v));
        Assert.Empty(en.Where(kv => Codes(kv.Key) != Codes(kv.Value)).Select(kv => kv.Key));
        Assert.Empty(mg.Where(kv => Codes(kv.Key) != Codes(kv.Value)).Select(kv => kv.Key));
    }

    [Fact]
    public void NomApplication_RemplaceLaCommunaute()
    {
        try
        {
            L.Definir(L.Fr);
            const string texte = "Tableau de bord : un aperçu de votre communauté.";
            Assert.Equal(texte, L.T(texte));
            L.NomApplication = () => "Avotra";
            Assert.Equal("Tableau de bord d'Avotra.", L.T(texte));
            L.NomApplication = () => "Fikambanana";
            Assert.Equal("Tableau de bord de Fikambanana.", L.T(texte));
            Assert.Equal("Fikambanana n'a pas encore raconté son histoire.", L.T("La communauté n'a pas encore raconté son histoire."));
            L.Definir(L.En);
            Assert.Equal("Fikambanana dashboard.", L.T(texte));
            Assert.Equal("Your Fikambanana space online, shared with all members. Let us guide you in 6 steps.",
                L.F("Votre gestion communautaire en ligne, partagée avec tous les membres. Laissez-vous guider en {0} étapes.", 6));
            // L'historique enregistré reste en français, sans le nom.
            Assert.Equal(texte, L.EnFrancais(() => L.T(texte)));
            L.NomApplication = () => null;
            Assert.Equal("Dashboard: an overview of your community.", L.T(texte));
        }
        finally
        {
            L.NomApplication = null;
            L.Definir(L.Fr);
        }
    }

    [Fact]
    public void NomApplication_VariantesCompletes()
    {
        var fr = L.VariantesAvecNom(L.Fr);
        var en = L.VariantesAvecNom(L.En);
        var mg = L.VariantesAvecNom(L.Mg);
        Assert.True(fr.Count > 30);
        Assert.Empty(fr.Keys.Except(en.Keys));
        Assert.Empty(fr.Keys.Except(mg.Keys));
        Assert.Empty(en.Keys.Except(fr.Keys));
        Assert.Empty(mg.Keys.Except(fr.Keys));
        // Chaque texte existe dans l'application (il est traduit) et cite le nom.
        Assert.DoesNotContain(fr.Keys, k => !L.EstTraduit(L.En, k));
        Assert.DoesNotContain(fr.Values, v => !v.Contains("{app}") && !v.Contains("{de_app}"));
        Assert.DoesNotContain(en.Values.Concat(mg.Values), v => !v.Contains("{app}") || v.Contains("{de_app}"));
        static string Codes(string s) => string.Join(",", Regex.Matches(s, @"\{\d+[^}]*\}").Select(m => m.Value).OrderBy(v => v));
        foreach (var d in new[] { fr, en, mg })
            Assert.Empty(d.Where(kv => Codes(kv.Key) != Codes(kv.Value)).Select(kv => kv.Key));
    }

    [Fact]
    public void Historique_ResteEnFrancais()
    {
        try
        {
            L.Definir(L.Mg);
            Assert.NotEqual("Parent", LiensFamille.Libelle(LiensFamille.Parent));
            Assert.Equal("Parent", L.EnFrancais(() => LiensFamille.Libelle(LiensFamille.Parent)));
        }
        finally { L.Definir(L.Fr); }
    }
}
