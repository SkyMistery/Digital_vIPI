namespace Aip.Pdf.Tests;

public sealed class LimitiEPagineTests
{
    [Theory]
    [InlineData("FL310", VerticalReference.FlightLevel, 310.0, VerticalUnit.Fl)]
    [InlineData("FL 95", VerticalReference.FlightLevel, 95.0, VerticalUnit.Fl)]
    [InlineData("UNL", VerticalReference.Unlimited, null, null)]
    [InlineData("GND", VerticalReference.Ground, null, null)]
    [InlineData("SFC", VerticalReference.Ground, null, null)]
    [InlineData("2000ft AMSL", VerticalReference.Msl, 2000.0, VerticalUnit.Ft)]
    [InlineData("1000 FT AGL", VerticalReference.Agl, 1000.0, VerticalUnit.Ft)]
    [InlineData("300 m AGL", VerticalReference.Agl, 300.0, VerticalUnit.M)]
    [InlineData("2000ft AMSL or 1000ft AGL whichever is higher", VerticalReference.Unknown, null, null)]
    [InlineData("3000ft", VerticalReference.Unknown, null, null)]
    public void UnLimite(string testo, VerticalReference riferimento, double? valore, VerticalUnit? unita)
    {
        var limite = LimitText.Parse(testo);

        Assert.Equal(testo, limite.Text);
        Assert.Equal(riferimento, limite.Reference);
        Assert.Equal(valore, limite.Value);
        Assert.Equal(unita, limite.Unit);
    }

    [Fact]
    public void GliSpaziDelPdfSiStringono() => Assert.Equal("2000ft AMSL", LimitText.Parse("  2000ft \n AMSL ").Text);

    [Fact]
    public void LaDataEIlRiferimentoDellaPagina()
    {
        string[] dispari = ["ENR 5.1.2 - 1", "AIP - Italia AIRAC effective date 09 JUL 2026", "ENR 5.1.2 ZONE REGOLAMENTATE"];
        string[] pari = ["ENR 5.1.2 - 2", "AIRAC effective date 6 AUG 2026 AIP - Italia"];

        Assert.Equal(new DateOnly(2026, 7, 9), PageInfo.Date(dispari));
        Assert.Equal(new DateOnly(2026, 8, 6), PageInfo.Date(pari));
        Assert.Equal("ENR 5.1.2-1", PageInfo.Reference(dispari));
        Assert.Equal("ENR 5.1.2-2", PageInfo.Reference(pari));
        Assert.Null(PageInfo.Date(["nessuna data qui"]));
        Assert.Null(PageInfo.Reference(["AIP - Italia"]));
    }

    [Theory]
    [InlineData("LI R4 - Fossano", "LI R4", "Fossano", AirspaceType.Restricted)]
    [InlineData("LI R10/A - Foci del Reno", "LI R10/A", "Foci del Reno", AirspaceType.Restricted)]
    [InlineData("LI R48 A Pippo", "LI R48 A", "Pippo", AirspaceType.Restricted)]
    [InlineData("LI/LD D35/A - Prova", "LI/LD D35/A", "Prova", AirspaceType.Danger)]
    [InlineData("LI TSA73 bis - Prova", "LI TSA73 bis", "Prova", AirspaceType.Tsa)]
    [InlineData("LI P1 - Roma", "LI P1", "Roma", AirspaceType.Prohibited)]
    [InlineData("EUC 60 Prova", "EUC 60", "Prova", AirspaceType.Cba)]
    public void GliIdentificativiDellaF0(string riga, string id, string nome, AirspaceType tipo)
    {
        var letto = Italia.ItalianAip.Identificativo(riga);

        Assert.Equal((id, nome), letto);
        Assert.Equal(tipo, Italia.ItalianAip.Tipo(id));
    }

    [Fact]
    public void ILimitiAndatiACapoSiRiuniscono() =>
        Assert.Equal(
            ["2000ft AMSL or 1000ft AGL whichever is higher", "SFC"],
            Italia.ItalianAip.UnisciLimiti(["2000ft AMSL", "or 1000ft AGL", "whichever", "is higher", "SFC"]));
}
