using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Settori militari e FSS della vIPI di ACC nelle loro sezioni (21 settembre 2026): chi è di quale famiglia, e
/// dove stanno le due sezioni nel blocco Aerovia.
/// </summary>
public class AccFamigliaAorTests
{
    [Theory]
    [InlineData("LIRR_MIL_CTR", FamigliaAor.Mil)]
    [InlineData("LIMM_N_MIL_CTR", FamigliaAor.Mil)]
    [InlineData("LIRR_AMIL_CTR", FamigliaAor.Mil)]     // «contiene MIL» nella parte di mezzo
    [InlineData("lirr_mil_ctr", FamigliaAor.Mil)]
    [InlineData("LIRR_FSS", FamigliaAor.Fss)]
    [InlineData("LIRR_PLN_FSS", FamigliaAor.Fss)]
    [InlineData("LIRR_E_CTR", FamigliaAor.Ordinaria)]
    [InlineData("MIL_CTR", FamigliaAor.Ordinaria)]      // «MIL» in testa è il codice, non la parte di mezzo
    [InlineData("LIRR_CTR", FamigliaAor.Ordinaria)]
    [InlineData("", FamigliaAor.Ordinaria)]
    [InlineData(null, FamigliaAor.Ordinaria)]
    public void La_famiglia_si_legge_dal_callsign(string? callsign, FamigliaAor attesa) =>
        Assert.Equal(attesa, AccFamigliaAorRegola.Di(callsign));

    [Theory]
    [InlineData("it")]
    [InlineData("en")]
    public void Le_due_sezioni_portano_le_sigle_degli_enti_in_tutte_e_due_le_lingue(string lingua)
    {
        var catalogo = SectionCatalog.For(SectionProfile.AccAerovia);
        Assert.Equal("SCCAM", catalogo.Single(d => d.Key == SectionKeys.AorMil).TitleIn(lingua));
        Assert.Equal("FIC", catalogo.Single(d => d.Key == SectionKeys.AorFss).TitleIn(lingua));
    }

    [Fact]
    public void Nel_blocco_Aerovia_il_militare_sta_prima_delle_regolamentate_e_il_FSS_dopo()
    {
        var chiavi = SectionCatalog.For(SectionProfile.AccAerovia).OrderBy(d => d.Order).Select(d => d.Key).ToList();
        var regolamentate = chiavi.IndexOf(SectionKeys.Regulated);
        Assert.Equal(regolamentate - 1, chiavi.IndexOf(SectionKeys.AorMil));
        Assert.Equal(regolamentate + 1, chiavi.IndexOf(SectionKeys.AorFss));
        Assert.DoesNotContain(SectionKeys.AorMil, SectionCatalog.For(SectionProfile.AccAppBlock).Select(d => d.Key));
        Assert.DoesNotContain(SectionKeys.AorFss, SectionCatalog.For(SectionProfile.AccAppBlock).Select(d => d.Key));
    }
}
