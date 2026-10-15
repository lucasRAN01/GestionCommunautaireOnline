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
            Assert.Equal("Users", L.T("Utilisateurs"));
            Assert.Equal("Texte jamais traduit", L.T("Texte jamais traduit"));
            L.Definir(L.Fr);
            Assert.Equal("Utilisateurs", L.T("Utilisateurs"));
        }
        finally { L.Definir(L.Fr); }
    }
}
