using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Tests.Ispezione;

/// <summary>Il selettore dei colori della scheda (lotto «Subito» slice 4, D2 e I1): cosa legge e cosa scrive.</summary>
public sealed class ColoriDellaSchedaTests
{
    private static ColorPalette Definiti()
    {
        var avvisi = new RaccoltaDiAvvisi();
        var palette = new DefParser(avvisi).Parse(SectorFileReader.Decode(
            "GRASS;#406230;\r\nTAXIWAY;#767587;\r\nVETRO;#80D3D3D3;\r\n"u8.ToArray()), "colors.def");
        Assert.Equal(0, avvisi.Count);
        return palette;
    }

    [Fact]
    public void UnNomeDiColorsDefHaIlSuoColore()
    {
        var letto = ColoriDellaScheda.Leggi("grass", Definiti());

        Assert.Equal("#406230", letto.Esadecimale);
        Assert.Equal("GRASS", letto.Nome);
        Assert.Equal(100, letto.Opacita);
        Assert.Null(letto.Avviso);
    }

    [Theory]
    [InlineData("#2f2f2f", "#2f2f2f")]
    [InlineData("24,183,108", "#18b76c")]
    [InlineData("%48:98:0", "#306200")]
    public void UnValoreNelleFormeDelManuale(string scritto, string esadecimale)
    {
        var letto = ColoriDellaScheda.Leggi(scritto, Definiti());

        Assert.Equal(esadecimale, letto.Esadecimale);
        Assert.Null(letto.Nome);
        Assert.Null(letto.Avviso);
    }

    [Fact]
    public void LOpacitaPortaLAvvisoSuSmoothDrawing()
    {
        var valore = ColoriDellaScheda.Leggi("#80406230", Definiti());
        Assert.Equal(50, valore.Opacita);
        Assert.Contains("Smooth Drawing", valore.Avviso, StringComparison.Ordinal);

        // Anche un nome il cui colore in colors.def ha l'opacità (manuale: ogni forma vale anche in [DEFINE]).
        var nome = ColoriDellaScheda.Leggi("VETRO", Definiti());
        Assert.Contains("VETRO di colors.def", nome.Avviso, StringComparison.Ordinal);
        Assert.Contains("Smooth Drawing", nome.Avviso, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("COAST")]   // lo schema lo colora nei .geo, ma in [FILLCOLOR] non è di colors.def: l'orfano limw.pol
    [InlineData("#12345")]
    [InlineData("")]
    public void UnNomeOUnValoreCheNessunoConosceSiDice(string scritto)
    {
        var letto = ColoriDellaScheda.Leggi(scritto, Definiti());

        Assert.True(letto.Sconosciuto);
        Assert.NotNull(letto.Avviso);
    }

    [Fact]
    public void INomiInOrdineColSignificatoDelCampo()
    {
        var nomi = ColoriDellaScheda.Nomi(Definiti(), [new ValoreFisso("GRASS", "erba")]);

        Assert.Equal(["GRASS", "TAXIWAY", "VETRO"], nomi.Select(n => n.Nome));
        Assert.Equal("erba", nomi[0].Significato);
        Assert.Null(nomi[1].Significato);
        Assert.Equal("#767587", nomi[1].Esadecimale);
    }

    [Theory]
    [InlineData("#406230", 100, "#406230")]
    [InlineData("#406230", 50, "#80406230")]
    [InlineData("#a0b0c0", 0, "#00A0B0C0")]
    [InlineData("#a0b0c0", 140, "#A0B0C0")]   // oltre 100 è pieno
    public void IlSelettoreScriveRRGGBBOConLOpacitaAARRGGBB(string scelto, int opacita, string scritto)
        => Assert.Equal(scritto, ColoriDellaScheda.DaScrivere(scelto, opacita));

    [Fact]
    public void UnColoreCheNonSiLeggeNonSiScrive()
        => Assert.Null(ColoriDellaScheda.DaScrivere("rosso", 100));
}
