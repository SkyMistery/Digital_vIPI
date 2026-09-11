using Vipi.Application.Content;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Le procedure d'avvicinamento si leggono «ILS, VOR» — una virgola e uno spazio — qualunque sia la forma in
/// archivio (committente, 11 settembre 2026). Le forme qui sotto sono quelle che il testo libero ha lasciato:
/// misurate sul <c>vipi.db</c> di sviluppo («\tILS, VOR, RNAV») o possibili scrivendo a mano.
/// </summary>
public class AirportRunwayListsTests
{
    [Theory]
    [InlineData("ILS,VOR", "ILS, VOR")]
    [InlineData("ILS, VOR", "ILS, VOR")]
    [InlineData("ILS ,VOR", "ILS, VOR")]
    [InlineData("\tILS, VOR, RNAV", "ILS, VOR, RNAV")]
    [InlineData("ILS;VOR", "ILS, VOR")]
    [InlineData("ILS,,VOR,", "ILS, VOR")]
    [InlineData("ILS", "ILS")]
    public void Le_voci_si_leggono_separate_da_virgola_e_spazio(string archivio, string letto)
    {
        Assert.Equal(letto, AirportRunwayLists.Format(archivio));
    }

    /// <summary>L'ordine è quello scritto: la lettura non riordina, formatta.</summary>
    [Fact]
    public void L_ordine_resta_quello_scritto()
    {
        Assert.Equal("VOR, ILS", AirportRunwayLists.Format("VOR,ILS"));
    }

    /// <summary>⚠️ «L JET» è UNA voce: gli spazi dentro una voce restano (uno solo).</summary>
    [Fact]
    public void Una_voce_con_uno_spazio_resta_una_voce()
    {
        Assert.Equal(new[] { "L JET", "R" }, AirportRunwayLists.Tokens("L  JET,R"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" , ")]
    [InlineData("—")]
    public void Vuoto_si_legge_trattino(string? archivio)
    {
        Assert.Equal("—", AirportRunwayLists.Format(archivio));
    }
}
