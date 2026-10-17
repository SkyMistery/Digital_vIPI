using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;
using Xunit;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// I simboli del <c>.sym</c> (lotto «Subito» slice 4d; lettore e scrittore dalla 17a): righe vere di <c>symbols.sym</c>
/// del fork, e il file intero in <c>Campioni</c>.
/// </summary>
public sealed class SymParserTests
{
    private const string Apt = "0000000000000;0000011100000;0000100010000;0001000001000;0010000000100;0010000000100;1111111111111;0010000000100;0010000000100;0001000001000;0000100010000;0000011100000;0000000000000;";
    private const string Fix = "0000000100000;0000001100000;0000010100000;0000100100000;0001000100000;0010000100000;0100000100000;0010000100000;0001000100000;0000100100000;0000010100000;0000001100000;0000000100000;";

    private readonly CollectingWarnings _warnings = new();
    private SymParser Parser => new(_warnings);

    private ParseResult<Simbolo> Leggi(string testo) => Parser.Parse(ParserTestHelpers.Read(testo), "s.sym").FissaLeBasi(new SymSaver());

    private static IReadOnlyList<string> Scritte(ParseResult<Simbolo> letto, params Simbolo[] toccati)
        => new FileSaverOrchestrator().Righe(letto, new HashSet<Simbolo>(toccati), new SymSaver());

    [Fact]
    public void OgniGruppoEUnaColonna_IlFixPuntaInAlto()
    {
        var fix = Assert.Single(Leggi("//FIX vuoto\r\n" + Fix + "\r\n").Records);

        Assert.Equal("FIX vuoto", fix.Nome);
        // La base del triangolo è la riga 7, da un bordo all'altro; la punta è in alto al centro (x 6, y 1).
        Assert.All(Enumerable.Range(0, 13), x => Assert.True(fix.Acceso(x, 7)));
        Assert.True(fix.Acceso(6, 1));
        Assert.False(fix.Acceso(6, 0));
        Assert.False(fix.Acceso(0, 0));
    }

    [Fact]
    public void NomiERigheComeNelFile()
    {
        var simboli = Leggi(
            "//APT\r\n" + Apt + "\r\n\r\nAC_comb SEL\r\n" + Fix + "\r\n////ALTRI\r\n\r\n//CROCE X\r\n" + Fix + "\r\n\r\n" + Apt + "\r\n").Records;

        Assert.Equal(["APT", "AC_comb SEL", "CROCE X", null], simboli.Select(s => s.Nome));
        Assert.Equal([false, true, false, false], simboli.Select(s => s.NomeSenzaBarre));
        Assert.Equal([2, 5, 9, 11], simboli.Select(s => s.Source.LineNumber));
        Assert.Equal(0, _warnings.Count);
    }

    [Fact]
    public void UnTitoloSopraIlNomeRestaUnCommentoDavanti()
    {
        var letto = Leggi("////ALTRI\r\n//CROCE X\r\n" + Fix + "\r\n");

        Assert.Equal("CROCE X", Assert.Single(letto.Records).Nome);
        var record = Assert.IsType<RecordChunk<Simbolo>>(Assert.Single(letto.Chunks));
        Assert.Equal(["////ALTRI"], record.LeadingComments);
        Assert.Equal(["//CROCE X", Fix], record.RawLines);
    }

    [Fact]
    public void UnSimboloCheNonE13Per13AvvisaERestaComE()
    {
        var letto = Leggi("//CORTO\r\n0000;1111;\r\n//APT\r\n" + Apt + "\r\n");

        Assert.Equal(1, _warnings.Count);
        Assert.Equal("APT", Assert.Single(letto.Records).Nome);
        Assert.Equal(["//CORTO", "0000;1111;", "//APT", Apt], Scritte(letto));
    }

    [Fact]
    public void UnTestoCheNonStaSopraUnSimboloAvvisaERestaComE()
    {
        var letto = Leggi("PIPPO\r\n\r\n//APT\r\n" + Apt + "\r\n");

        Assert.Equal(1, _warnings.Count);
        Assert.Equal(["PIPPO", "", "//APT", Apt], Scritte(letto));
    }

    [Fact]
    public void IlFileDelForkSiRileggeERiscriveUguale()
    {
        string percorso = RealSectorFiles.Path("symbols.sym")!;
        var letto = Parser.Parse(percorso, new ColorPalette());

        Assert.Equal(0, _warnings.Count);
        Assert.Equal(23, letto.Records.Count);
        Assert.Equal(["APT", "FIX vuoto", "FIX vuoto piccolo", "FIX pieno", "FIX pieno piccolo", "TERM", "AC_COMB", "AC_comb SEL",
                      "AC_DUPE", "AC_COMB_MODE_S", "simbolo acft coast", "TAC2", "VOR", "VOR2", "VOR3", "NDB", "VFR", "CROCE X", null,
                      "CROCE +", "simbolo croce dentro rombo", "PAR 2090", "PAR2080"],
                     letto.Records.Select(s => s.Nome));
        Assert.Equal(["AC_comb SEL", "AC_DUPE"], letto.Records.Where(s => s.NomeSenzaBarre).Select(s => s.Nome));

        var (originale, scritto) = ParserTestHelpers.RoundTrip(new SymParser(_warnings), new SymSaver(), percorso);
        Assert.Equal(originale, scritto);
    }

    [Fact]
    public void TuttiTocchiSenzaCambiare_IlFileResta_Uguale()
    {
        string percorso = RealSectorFiles.Path("symbols.sym")!;
        var letto = Parser.Parse(percorso, new ColorPalette()).FissaLeBasi(new SymSaver());

        Assert.Equal(File.ReadAllLines(percorso), Scritte(letto, [.. letto.Records]));
    }

    [Fact]
    public void UnPixelCambiatoCambiaSoloLaSuaColonna()
    {
        var letto = Leggi("//APT\r\n" + Apt + "\r\n");
        var apt = letto.Records[0];

        apt.Pixel = Simbolo.ConIlPixel(apt.Pixel, 0, 0, acceso: true);

        Assert.Equal(["//APT", "1" + Apt[1..]], Scritte(letto, apt));
    }

    [Fact]
    public void UnNomeCambiatoSiRiscriveAlSuoPosto()
    {
        var letto = Leggi("////ALTRI\r\n//CROCE X\r\n" + Fix + "\r\n");
        var croce = letto.Records[0];

        croce.Nome = "CROCE";

        Assert.Equal(["////ALTRI", "//CROCE", Fix], Scritte(letto, croce));
    }

    [Fact]
    public void IlNomeSenzaBarreSiCorreggeColCommento()
    {
        var letto = Leggi("AC_comb SEL\r\n" + Fix + "\r\n");
        var sel = letto.Records[0];

        sel.NomeSenzaBarre = false;

        Assert.Equal(["//AC_comb SEL", Fix], Scritte(letto, sel));
    }

    [Fact]
    public void UnSimboloSenzaNomePrendeIlSuoCommento()
    {
        var letto = Leggi("//CROCE X\r\n" + Fix + "\r\n\r\n" + Apt + "\r\n");
        var senzaNome = letto.Records[1];

        senzaNome.Nome = "CROCE NEL CERCHIO";

        Assert.Equal(["//CROCE X", Fix, "", "//CROCE NEL CERCHIO", Apt], Scritte(letto, senzaNome));
    }

    [Fact]
    public void IlPixelSiAccendeESiSpegne()
    {
        string acceso = Simbolo.ConIlPixel(Simbolo.Vuoto, 12, 3, acceso: true);
        var simbolo = new Simbolo { Pixel = acceso };

        Assert.True(simbolo.Acceso(12, 3));
        Assert.Equal(1, acceso.Count(c => c == '1'));
        Assert.Equal(Simbolo.Vuoto, Simbolo.ConIlPixel(acceso, 12, 3, acceso: false));
        Assert.Throws<ArgumentOutOfRangeException>(() => Simbolo.ConIlPixel(acceso, 13, 0, acceso: true));
    }

    [Fact]
    public void IFormatiDannoIlLettoreDelSym()
    {
        var avvisi = new CollectingWarnings();
        Assert.True(Formati.Usa("x/symbols.sym", avvisi, new NomeDelLettore(), out string? nome));
        Assert.Equal(nameof(SymParser), nome);
    }

    private sealed class NomeDelLettore : IUsoDelFormato<string>
    {
        public string Usa<T>(IFileParser<T> lettore, IFileSaver<T> scrittore)
            where T : class
            => lettore.GetType().Name;
    }
}
