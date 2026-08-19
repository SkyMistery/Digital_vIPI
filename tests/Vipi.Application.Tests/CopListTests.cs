using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>L'elenco dei punti di una clausola: formato salvato, riletture e i due casi limite che contano.</summary>
public class CopListTests
{
    [Fact]
    public void Un_elenco_si_rilegge_come_e_stato_scritto()
    {
        Assert.Equal(new[] { "TIGRA", "NOSTO", "LATAN" }, CopList.Parse("TIGRA, NOSTO, LATAN"));
        Assert.Equal("TIGRA, NOSTO", CopList.Format(new[] { " TIGRA ", "NOSTO", "  ", null }));
    }

    [Fact]
    public void I_token_veri_dell_archivio_restano_interi()
    {
        // Nessuno di questi contiene una virgola, ed e' la ragione per cui il separatore e' la virgola.
        foreach (var token in new[] { "ALL", "ALL to GR", "Y01-Y12", "TOPNO 3A" })
            Assert.Equal(new[] { token }, CopList.Parse(token));
    }

    [Fact]
    public void Un_elenco_vuoto_vale_un_punto_non_indicato()
    {
        // «Nessun punto» e' un caso che la frase sa dire (il «—» di FallbackMissingPoint). Restituire zero
        // elementi lo farebbe sparire, cioe' trasformerebbe una clausola incompleta in una clausola assente:
        // l'editore non la troverebbe piu' per correggerla.
        Assert.Equal(new[] { "" }, CopList.Parse(""));
        Assert.Equal(new[] { "" }, CopList.Parse(null));
        Assert.Equal(new[] { "" }, CopList.Parse("  ,  "));
        Assert.Equal(1, CopList.Count(""));
    }

    [Fact]
    public void Due_elenchi_si_confrontano_per_contenuto_e_ordine()
    {
        Assert.True(CopList.SameAs("TIGRA, NOSTO", "tigra,  nosto"));
        Assert.False(CopList.SameAs("TIGRA, NOSTO", "NOSTO, TIGRA"));
    }
}
