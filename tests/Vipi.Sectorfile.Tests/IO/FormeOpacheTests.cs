using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// Carta F2, slice 5: le forme legittime che la libreria A non modellava e riscriveva senza capirle — 7 162 righe
/// opache sul master del 22 settembre 2026, 7 dopo. Ogni file vero qui dentro si legge SENZA avvisi: quelli che
/// restano nell'albero sono errori del sector (carta §5, slice 5), e hanno i loro test.
/// </summary>
public sealed class FormeOpacheTests
{
    private readonly CollectingWarnings _warnings = new();

    // .fix a 4 campi (nascosti, `BC404;…;3;`) e a 3 (`POE1;lat;lon;`): 2 041 righe.
    [Theory]
    [InlineData("NAVAIDS/VFR_NASCOSTI.fix")]
    [InlineData("NAVAIDS/APT.fix")]
    public void IFixSenzaICampiInCodaSonoFix(string campione)
    {
        string percorso = RealSectorFiles.Path(campione)!;
        var letto = new FixParser(_warnings).Parse(percorso, new ColorPalette());

        // APT.fix:294 `E008-11.31.443` è un errore vero del sector: l'unico avviso ammesso.
        Assert.All(_warnings.Snapshot(), a => Assert.Equal("MG763;N044.03.11.145;E008-11.31.443;3;", a.RawSnippet));
        Assert.Equal(RigheAttive(percorso) - _warnings.Count, letto.Records.Count);
        Assert.Contains(letto.Records, f => f.DisplayType == 3 && f.ExtraField is null);
    }

    // .artcc: le righe T; sono il bordo (5 041 righe, tutti i bordi di FRA e FRA-gates).
    [Theory]
    [InlineData("ACC/FRA.artcc")]
    [InlineData("ACC/FRA-gates.artcc")]
    public void LeRigheTDegliArtccSonoBordi(string campione)
    {
        string percorso = RealSectorFiles.Path(campione)!;
        var letto = new ArtccParser(_warnings).Parse(percorso, new ColorPalette());

        Assert.Empty(_warnings.Snapshot());
        int righeT = File.ReadLines(percorso).Count(r => r.StartsWith("T;", StringComparison.Ordinal));
        // Maiuscolo o minuscolo (F3 slice 10): FRA-gates scrive anche «T;dummy;», e per Aurora è un separatore.
        int dummy = File.ReadLines(percorso).Count(r => r.StartsWith("T;DUMMY;", StringComparison.OrdinalIgnoreCase));
        var gruppi = letto.Records.OfType<StaticBoundaryGroup>().ToList();
        Assert.Equal(righeT - dummy, gruppi.Sum(g => g.Polygons.Sum(p => p.Vertices.Count)));
        // Un gruppo coi vertici non si chiama DUMMY (lo scrittore li riscriverebbe come separatori, F2 slice 5). Una
        // riga «T;dummy;» da sola fra due righe vuote (FRA-gates:985) è un gruppo SENZA vertici: non disegna niente.
        Assert.DoesNotContain(gruppi, g => g.Name.Equals("DUMMY", StringComparison.OrdinalIgnoreCase) && g.Polygons.Count > 0);
    }

    // .frq: una posizione che si ferma all'elenco dei trasferimenti (35 righe) è una posizione.
    [Fact]
    public void UnaPosizioneSenzaProfiloEUnaPosizione()
    {
        var letto = new FrqParser(_warnings).Parse(RealSectorFiles.Path("OTHER/itfreq.frq")!, new ColorPalette());

        Assert.Empty(_warnings.Snapshot());
        var aew = letto.Records.Single(p => p.Code == "LIZZ_AEW_CTR");
        Assert.Null(aew.Profile);
        Assert.Contains(aew.TransferList, t => t.PositionCode == "LIRR");
    }

    [Fact]
    public void UnaPosizioneSenzaProfiloSiRiscriveSenza()
    {
        var p = Assert.Single(new FrqParser(_warnings).Parse(ParserTestHelpers.Read("LIZZ_AEW_CTR;136.400;LIMM LIRR;\r\n"), "x.frq", new ColorPalette()).Records);
        Assert.Equal("LIZZ_AEW_CTR;136.400;LIMM LIRR;", new FrqSaver().Serialize(p)[0]);
    }

    // .geo: un segmento col colore vuoto (`…;E013.18.35.124;;`, 10 righe) è un segmento.
    [Fact]
    public void UnSegmentoColColoreVuotoEUnSegmento()
    {
        var letto = new GeoParser(_warnings).Parse(RealSectorFiles.Path("GEO/liap.geo")!, new ColorPalette());

        Assert.Empty(_warnings.Snapshot());
        var vuoto = letto.Records.First(l => l.Color.Length == 0);
        Assert.Equal("N042.23.09.144;E013.18.34.590;N042.22.57.221;E013.18.35.124;;", new GeoSaver().Serialize(vuoto)[0]);
    }

    // .pol: il commento subito dopo l'intestazione (`//BR_twy_B`) è il nome del poligono, non la sua fine. In A
    // chiudeva il poligono a zero vertici, e i vertici finivano in righe grezze (32 poligoni).
    [Fact]
    public void IlCommentoDopoLIntestazioneNonChiudeIlPoligono()
    {
        string percorso = RealSectorFiles.Path("GND_LAYOUT/br_ad_gnd.pol")!;
        var letto = new PolParser(_warnings).Parse(percorso, new ColorPalette());

        Assert.Empty(_warnings.Snapshot());
        int vertici = File.ReadLines(percorso).Count(r => r.Length > 1 && r[0] is 'N' or 'S' && char.IsAsciiDigit(r[1]));
        Assert.Equal(vertici, letto.Records.Sum(p => p.Vertices.Count));
    }

    [Fact]
    public void UnPoligonoDavveroVuotoSiDiceAncora()
    {
        new PolParser(_warnings).Parse(ParserTestHelpers.Read(
            "STATIC;TAXIWAY;1;TAXIWAY;\r\n//ML_twy_N\r\nSTATIC;APRON;1;APRON;\r\nN045.27.36.003;E009.16.36.198;\r\n" +
            "N045.27.37.003;E009.16.36.198;\r\nN045.27.37.003;E009.16.37.198;\r\n"), "ml_ad_gnd.pol");

        Assert.Equal("Polygon with 0 vertices", Assert.Single(_warnings.Snapshot()).Message);
    }

    private static int RigheAttive(string percorso)
        => File.ReadLines(percorso).Count(r => r.Trim().Length > 0 && !r.TrimStart().StartsWith("//", StringComparison.Ordinal));
}
