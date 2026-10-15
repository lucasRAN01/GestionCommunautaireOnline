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
