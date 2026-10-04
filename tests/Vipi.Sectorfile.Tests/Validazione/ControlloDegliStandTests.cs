using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;
using Vipi.Sectorfile.Validazione;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// Lotto «Subito», slice 12b (carta «file per file» R2, R2b, R6): tipo e slot di uno stand (5° e 6° campo di
/// <c>[GATES]</c>, manuale IVAO) letti, scritti e controllati; lo stand di codice più grande della taxiway che ha accanto.
/// </summary>
public sealed class ControlloDegliStandTests : IDisposable
{
    private readonly string _radice = Path.Combine(Path.GetTempPath(), "stand-" + Guid.NewGuid().ToString("N"));

    public ControlloDegliStandTests()
    {
        Directory.CreateDirectory(Path.Combine(_radice, "Include", "IT", "OTHER"));
        Scrivi("ITALY.isc", Righe("[INFO]", "N041.48.01.000", "E012.14.20.000", "60", "45", "+4.0", "IT", "", "[AIRPORT]", @"F;OTHER\itap.ap"));
        Scrivi(@"Include\IT\OTHER\itap.ap", Righe("LIRF;14;6000;N041.48.01.000;E012.14.20.000;FIUMICINO;"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_radice))
            Directory.Delete(_radice, recursive: true);
    }

    private static string Righe(params string[] righe) => string.Join("\r\n", righe) + "\r\n";

    private void Scrivi(string relativo, string testo)
        => File.WriteAllText(Path.Combine(_radice, relativo.Replace('\\', Path.DirectorySeparatorChar)), testo);

    private List<ProblemaDelSector> Di(Regola regola) => Validatore.ValidaLAlbero(_radice).Where(p => p.Regola == regola).ToList();

    private static ParseResult<Stand> Leggi(string testo)
        => new GtsParser(new CollectingWarnings()).Parse(ParserTestHelpers.Read(testo), "lirf.gts", new ColorPalette());

    [Fact]
    public void IlLettoreLeggeTipoESlot_ELoStandSenzaNonNeHa()
    {
        var stand = Leggi("A3O;LIRF;N041.48.16.944;E012.14.18.365;M;t_B73 t_A319 c_OAL;\r\n101;LIRF;N041.48.16.209;E012.14.15.712;\r\n" +
                          "F02;LIRF;N041.48.16.209;E012.14.15.712;;w_;\r\n").Records;

        Assert.Equal(("M", "t_B73 t_A319 c_OAL"), (stand[0].Type, stand[0].Slot));
        Assert.Equal(((string?)null, (string?)null), (stand[1].Type, stand[1].Slot));
        Assert.Equal(((string?)null, "w_"), (stand[2].Type, stand[2].Slot));
    }

    [Fact]
    public void LoScrittoreScriveTipoESlotSoloSeCiSono_ELoSlotSenzaTipoLasciaIlCampoVuoto()
    {
        var scrittore = new GtsSaver();
        var stand = new Stand { Number = "101", IcaoCode = "LIRF", Position = new Coordinate(41.5, 12.25) };
        string senza = Assert.Single(scrittore.Serialize(stand));
        Assert.Equal(4, senza.Split(';').Length - 1);

        stand.Type = "H";
        Assert.EndsWith(";H;", Assert.Single(scrittore.Serialize(stand)), StringComparison.Ordinal);
        stand.Type = null;
        stand.Slot = "w_ t_B744";
        Assert.EndsWith(";;w_ t_B744;", Assert.Single(scrittore.Serialize(stand)), StringComparison.Ordinal);
    }

    // Come una modifica dalla scheda: il tipo scritto su uno stand che non l'aveva tocca solo quella riga, e il resto del
    // file — lo stand commentato, i 48 «M» del fork — torna byte per byte.
    [Fact]
    public void ScrivereIlTipoToccaSoloLaSuaRiga()
    {
        const string testo = "//Piazzale 100\r\n101;LIRF;N041.48.16.944;E012.14.18.365;\r\n102;LIRF;N041.48.16.209;E012.14.15.712;M;\r\n" +
                             "//103;LIRF;N041.48.16.209;E012.14.15.712;\r\n";
        var letto = Leggi(testo).FissaLeBasi(new GtsSaver());
        letto.Records[0].Type = "H";
        letto.Records[0].Slot = "t_B77W";

        string tmp = Path.Combine(_radice, "lirf.gts");
        new FileSaverOrchestrator().Save(letto, new HashSet<Stand> { letto.Records[0] }, new GtsSaver(), tmp);

        Assert.Equal(testo.Replace("365;\r\n", "365;H;t_B77W;\r\n", StringComparison.Ordinal), File.ReadAllText(tmp));
    }

    // La lezione della 9b: un campo nuovo della scheda si misura su OGNI record prima di offrirlo. Sul campione vero
    // `lirf.gts`, e su tutti i .gts di un albero vero se SECTORLAB_ALBERO_VERO dice dov'è (il clone del sector: 52 file,
    // 1 672 stand il 4 ottobre 2026): tipo e slot scritti su ogni stand cambiano la sua riga e nient'altro.
    [Fact]
    public void TipoESlotScrittiSuOgniStandCambianoSoloLaSuaRiga()
    {
        var file = new List<string> { RealSectorFiles.Path("lirf.gts")! };
        if (Environment.GetEnvironmentVariable("SECTORLAB_ALBERO_VERO") is { Length: > 0 } albero)
            file.AddRange(Directory.GetFiles(albero, "*.gts", SearchOption.AllDirectories));

        int stand = 0;
        foreach (string percorso in file)
        {
            string[] prima = File.ReadAllLines(percorso, System.Text.Encoding.Latin1);
            var letto = new GtsParser(new CollectingWarnings()).Parse(percorso, new ColorPalette()).FissaLeBasi(new GtsSaver());
            foreach (var s in letto.Records)
            {
                s.Type = "H";
                s.Slot = "w_";
            }

            string tmp = Path.Combine(_radice, "scritto.gts");
            new FileSaverOrchestrator().Save(letto, new HashSet<Stand>(letto.Records), new GtsSaver(), tmp);
            string[] dopo = File.ReadAllLines(tmp, System.Text.Encoding.Latin1);

            Assert.Equal(prima.Length, dopo.Length);
            var righeDegliStand = letto.Records.Select(s => s.Source.LineNumber).ToHashSet();
            for (int i = 0; i < prima.Length; i++)
            {
                if (!righeDegliStand.Contains(i + 1))
                {
                    Assert.Equal(prima[i], dopo[i]);
                    continue;
                }

                // I primi quattro campi restano byte per byte; poi il tipo e lo slot nuovi.
                string[] p = prima[i].Split(';'), d = dopo[i].Split(';');
                Assert.Equal(p.Take(4), d.Take(4));
                Assert.Equal(["H", "w_"], d.Skip(4).Take(2));
                stand++;
            }
        }

        Assert.True(stand > 100, $"solo {stand} stand");
    }

    [Theory]
    [InlineData("t_A320 t_B738 c_OAL c_AEE", 0)]
    [InlineData("w_", 0)]
    [InlineData("d_LIRF w_", 0)]
    [InlineData("A320", 1)]
    [InlineData("x_A320", 1)]
    [InlineData("t_", 1)]
    [InlineData("w_CARGO", 1)]
    [InlineData("d_ROMA1", 1)]
    [InlineData("t_A-320 c_", 2)]
    public void GliSlotSiControllanoFiltroPerFiltro(string slot, int problemi)
        => Assert.Equal(problemi, SlotDelloStand.Problemi(slot).Count);

    [Fact]
    public void LoSlotSiScriveInMaiuscoloESenzaDoppioni()
    {
        Assert.Equal("t_A320 c_OAL w_", SlotDelloStand.Normale(" T_a320  c_oal w_ t_A320 "));
        Assert.Null(SlotDelloStand.Normale("  "));
        Assert.Equal([new('t', "A320"), new('w', "")], SlotDelloStand.Leggi("t_A320 boh w_"));
    }

    [Fact]
    public void TipoSlotENomeFuoriDalManualeSonoAvvisi()
    {
        Scrivi(@"Include\IT\lirf.gts", Righe(
            "101;LIRF;N041.48.16.944;E012.14.18.365;M;t_A320 w_;",
            "102;LIRF;N041.48.16.209;E012.14.15.712;X;",
            "103;LIRF;N041.48.16.209;E012.14.15.712;;A320;",
            "STAND COL NOME TROPPO LUNGO;LIRF;N041.48.16.209;E012.14.15.712;"));

        var problemi = Di(Regola.ValoreFuoriElenco).Where(p => p.File.EndsWith("lirf.gts", StringComparison.Ordinal)).ToList();

        Assert.Equal([2, 3, 4], problemi.Select(p => p.Riga));
        Assert.All(problemi, p => Assert.Equal(Gravita.Avviso, p.Gravita));
        Assert.Contains("L, M, H, S o G", problemi[0].Dettaglio, StringComparison.Ordinal);
        Assert.Contains("A320", problemi[1].Dettaglio, StringComparison.Ordinal);
        Assert.Contains("al massimo 20", problemi[2].Dettaglio, StringComparison.Ordinal);
    }

    [Fact]
    public void UnoStandDiCodicePiuGrandeDellaTaxiwayCheHaAccantoEUnAvviso()
    {
        // La taxiway A arriva al codice C; la B, più lontana, al codice F e non conta. Lo stand 102 è di codice C: va bene.
        Scrivi(@"Include\IT\lirf.txi", Righe(
            "//@\"A\" code=C", "A;LIRF;N041.48.16.000;E012.14.18.000;",
            "//@\"B\" code=F", "B;LIRF;N041.48.30.000;E012.14.18.000;",
            "C;LIRF;N041.48.16.500;E012.14.18.000;"));
        Scrivi(@"Include\IT\lirf.gts", Righe(
            "//@\"101\" code=E use=cargo", "101;LIRF;N041.48.16.944;E012.14.18.365;",
            "//@\"102\" code=C", "102;LIRF;N041.48.16.209;E012.14.15.712;",
            "103;LIRF;N041.48.16.209;E012.14.15.712;"));

        var p = Assert.Single(Di(Regola.StandPiuGrandeDellaTaxiway));

        Assert.Equal((2, Gravita.Avviso), (p.Riga, p.Gravita));
        Assert.Contains("«A»", p.Dettaglio, StringComparison.Ordinal);
        Assert.Empty(Di(Regola.TagNonValido));
        Assert.Empty(Di(Regola.TagFuoriCatalogo));
    }
}
