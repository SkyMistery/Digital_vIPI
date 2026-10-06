using Vipi.SectorLab.Core.Ispezione;

namespace Vipi.SectorLab.Tests.Ispezione;

/// <summary>
/// La quota di una zona MVA (lotto «Subito» slice 15c, «file per file» E2, S2): in centinaia di piedi, più i valori
/// speciali che il fork usa — <c>TRL</c>, <c>NO MINIMA</c>, <c>70/TRL</c>, <c>*30/40</c>. Prima l'editor li rifiutava
/// («non è una quota»), e una quota in piedi senza unità (<c>2500</c>) restava 2500, cioè 250 000 ft.
/// </summary>
public sealed class QuoteDelleMvaTests
{
    [Theory]
    [InlineData("25", "25")]
    [InlineData("FL85", "85")]
    [InlineData("2500ft", "25")]
    // Una quota piena scritta senza unità è in piedi: nelle centinaia nessuna MVA arriva a 1000.
    [InlineData("2500", "25")]
    [InlineData("10000", "100")]
    [InlineData("trl", "TRL")]
    [InlineData(" no  minima ", "NO MINIMA")]
    [InlineData("70/trl", "70/TRL")]
    [InlineData("7000/TRL", "70/TRL")]
    [InlineData("FL80/TRL", "80/TRL")]
    [InlineData("*30/40", "*30/40")]
    public void SiScriveInCentinaiaOConUnValoreSpeciale(string scritto, string atteso)
    {
        Assert.True(Quote.LeggiDiMva(scritto, out string inUnita, out string? perche), perche);
        Assert.Equal(atteso, inUnita);
    }

    [Theory]
    [InlineData("2550")]
    [InlineData("alta")]
    [InlineData("TRL/70")]
    [InlineData("")]
    public void QuelloCheNonEUnaQuotaSiRifiutaColPerche(string scritto)
    {
        Assert.False(Quote.LeggiDiMva(scritto, out _, out string? perche));
        Assert.Contains("TRL", perche, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("25", "= 2 500 ft")]
    [InlineData("110", "= 11 000 ft")]
    [InlineData("TRL", "= il livello di transizione")]
    [InlineData("NO MINIMA", "= nessuna minima di vettoramento")]
    [InlineData("70/TRL", "= 7 000 ft, o il livello di transizione se è più alto")]
    public void OgniValoreDiceCosaVuolDire(string valore, string atteso) => Assert.Equal(atteso, Quote.SignificatoDiMva(valore));

    [Theory]
    [InlineData("2500", "in piedi")]
    [InlineData("FL85", "livello di volo")]
    public void UnaQuotaPienaNelFileSiVedeCheNonEInCentinaia(string valore, string dice)
        => Assert.Contains(dice, Quote.SignificatoDiMva(valore), StringComparison.Ordinal);
}
