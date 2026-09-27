using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;

namespace Vipi.SectorLab.Tests.Ispezione;

/// <summary>
/// Gli editor della scheda tipizzata (lotto «Subito», slice 3b) che hanno una parte nel core: la quota scritta come
/// sulla carta, e le voci degli elenchi (scali, piste, posizioni) prese dal sector.
/// </summary>
public sealed class EditorDeiCampiTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();
    private readonly ModificheInSospeso _modifiche = new();

    public void Dispose() => _albero.Dispose();

    private SessioneAperta Apri() => SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);

    private static int Indice(FileAperto file, string etichetta)
        => Ispettore.Etichette(file, null).Select((e, i) => (e, i)).First(v => v.e == etichetta).i;

    [Theory]
    [InlineData("FL80", false, "8000")]
    [InlineData("fl 080", false, "8000")]
    [InlineData("2500ft", false, "2500")]
    [InlineData("2500 FT", false, "2500")]
    [InlineData("2500'", false, "2500")]
    [InlineData("2500", false, "2500")]
    [InlineData("-10", false, "-10")]
    [InlineData("FL80", true, "80")]
    [InlineData("2500ft", true, "25")]
    [InlineData("25", true, "25")]
    public void UnaQuotaSiScriveComeSullaCartaEFinisceNellUnitaDelCampo(string scritto, bool inCentinaia, string atteso)
    {
        Assert.True(Quote.Leggi(scritto, inCentinaia, out string valore, out string? perche), perche);
        Assert.Equal(atteso, valore);
    }

    [Theory]
    [InlineData("2550ft", true)]
    [InlineData("alta", false)]
    [InlineData("FL", false)]
    [InlineData("", true)]
    public void QuelloCheNonEUnaQuotaSiRifiutaColPerche(string scritto, bool inCentinaia)
    {
        Assert.False(Quote.Leggi(scritto, inCentinaia, out _, out string? perche));
        Assert.False(string.IsNullOrWhiteSpace(perche));
    }

    [Fact]
    public void UnaQuotaInCentinaiaDiceCosaVuolDire()
    {
        Assert.Equal("= 2 500 ft", Quote.Significato("25", inCentinaia: true));
        Assert.Equal("= 11 000 ft", Quote.Significato("110", inCentinaia: true));
        Assert.Null(Quote.Significato("25", inCentinaia: false));
        Assert.Null(Quote.Significato("TRL", inCentinaia: true));
    }

    [Fact]
    public void LaQuotaDiUnaMvaDiAccSiScriveInCentinaiaNellEtichetta()
    {
        var sessione = Apri();
        var file = sessione.File["SectorFiles/Include/IT/ENRMVA/lirr.mva"];
        var righe = ((IFileConRecord)file).RigheDelFile([]);

        var fatta = _modifiche.Cambia(file, 0, "AltLabel", "FL90");

        Assert.IsType<ModificaDiCampo>(fatta);
        var dopo = ((IFileConRecord)file).RigheDelFile(_modifiche.SporchiDi(file.Relativo));
        // La riga L della zona cambia nel 5° campo, e solo lì.
        Assert.Equal("L;LIRR;N041.08.58.289;E013.24.48.073;100;8;", righe[0]);
        Assert.Equal("L;LIRR;N041.08.58.289;E013.24.48.073;90;8;", dopo[0]);
    }

    [Fact]
    public void LAltitudineDiTransizioneSiScriveAncheComeLivello()
    {
        var sessione = Apri();
        var file = sessione.File["SectorFiles/Include/IT/OTHER/itap.ap"];
        int lirf = Indice(file, "LIRF");

        Assert.IsType<ModificaDiCampo>(_modifiche.Cambia(file, lirf, "TransitionAltFt", "FL70"));

        var dopo = ((IFileConRecord)file).RigheDelFile(_modifiche.SporchiDi(file.Relativo));
        Assert.Contains("LIRF;14;7000;N041.48.01.000;E012.14.20.000;FIUMICINO;", dopo);
    }

    [Fact]
    public void UnaQuotaSbagliataNonToccaIlRecord()
    {
        var sessione = Apri();
        var file = sessione.File["SectorFiles/Include/IT/ENRMVA/lirr.mva"];

        var rifiutata = Assert.IsType<ModificaRifiutata>(_modifiche.Cambia(file, 0, "AltLabel", "2550ft"));

        Assert.Contains("centinaia", rifiutata.Motivo, StringComparison.Ordinal);
        Assert.False(_modifiche.CEQualcosa);
    }

    [Fact]
    public void LeVociDegliElenchiVengonoDalSector()
    {
        var voci = VociDegliElenchi.Di(Apri());

        Assert.Contains("LIRF", voci.Scali);
        Assert.Contains("LIMM_WS2_CTR", voci.Posizioni);
        // Le piste di LIRF nell'ordine del .rw, i due versi di ognuna; la voce del menu generale in coda, sempre.
        Assert.Equal(["16L", "34R", "16R", "34L", "07", "25", "MAPS"], voci.PisteDi("LIRF"));
        Assert.Equal(["MAPS"], voci.PisteDi("XXXX"));
        Assert.Empty(voci.PisteDi(""));
        Assert.Same(voci.Scali, voci.Voci(FonteDellElenco.Scali));
        Assert.Equal(voci.PisteDi("LIRF"), voci.Voci(FonteDellElenco.Piste, "LIRF"));
    }

    [Theory]
    [InlineData("07", true)]
    [InlineData("16L", true)]
    [InlineData("36", true)]
    [InlineData("37", false)]
    [InlineData("MAPS", false)]
    [InlineData("NE", false)]
    public void UnaPistaVeraSiRiconosceDalNumero(string voce, bool vera)
        => Assert.Equal(vera, VociDegliElenchi.EUnaPistaVera(voce));
}
