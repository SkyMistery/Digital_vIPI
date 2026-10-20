using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>
/// Carta F7, slice 7f (P10): la mappa di gruppo in testa al <c>.sid</c>, composta dalle SID col tracciato come quelle
/// delle STAR. Nel <c>.sid</c> il tratto nuovo comincia dopo una riga vuota, e il primo punto porta la coda del nome
/// della SID come etichetta. La forma è quella della prova T4 (lirn.sid, ALAX7G e ALAX7H).
/// </summary>
public sealed class MappeComposteDelleSidTests
{
    private readonly CollectingWarnings _warnings = new();

    private ParseResult<SidProcedure> Sid(params string[] righe)
        => new SidParser(_warnings).Parse(ParserTestHelpers.Read(string.Join("\r\n", righe) + "\r\n"), "lirn.sid", new ColorPalette());

    private static readonly string[] Procedure =
    [
        "LIRN;06;ALAX7G;;;;ALAXI;1;", "GEMMA;GEMMA;", "RN605;RN605;", "VUNHO;VUNHO;", "MORCI;MORCI;", "MEZHO;MEZHO;", "ALAXI;ALAXI;",
        "LIRN;06;ALAX7H; ; ;0;ALAXI;1;", "VUNHO;VUNHO;", "RN703;RN703;", "MORCI;MORCI;", "MEZHO;MEZHO;", "ALAXI;ALAXI;",
        "LIRN;06:24;ISKI8J; ; ;",
    ];

    private static readonly string[] Mappa =
    [
        "LIRN;MAPS;SID RNAV(ALL);;;;;1;",
        "GEMMA;GEMMA;7G;", "RN605;RN605;", "VUNHO;VUNHO;", "MORCI;MORCI;", "MEZHO;MEZHO;", "ALAXI;ALAXI;",
        "",
        "VUNHO;VUNHO;7H;", "RN703;RN703;", "MORCI;MORCI;",
    ];

    private static List<string> Chiavi(IEnumerable<PuntoDellaMappa> punti)
        => [.. punti.Select(p => (p.IniziaUnTratto ? "|" : "") + p.Chiave + (p.Suffisso is null ? "" : "/" + p.Suffisso))];

    [Fact]
    public void LaMappaDelleSid_ComeQuellaDelleStar_ConLaRigaVuota_ELaCodaDelNome()
    {
        var letto = Sid(["//@\"SID RNAV(ALL)\" compose=ALAX7G,ALAX7H", "//@START", .. Mappa, "//@END \"SID RNAV(ALL)\"", "", .. Procedure]);
        var composta = Assert.Single(MappeComposte.Di(letto));
        Assert.True(MappeComposte.EUnaMappa(composta.Mappa));

        var rigenerata = composta.Componi(letto.Records);

        // ALAX7H si ferma su MORCI, il primo punto già disegnato da ALAX7G (D8).
        Assert.Equal(["GEMMA/7G", "RN605", "VUNHO", "MORCI", "MEZHO", "ALAXI", "|VUNHO/7H", "RN703", "MORCI"], Chiavi(rigenerata.Punti));
        Assert.True(MappeComposte.Uguale(composta.Mappa, rigenerata.Punti));
        Assert.Empty(rigenerata.Mancanti);
        // La SID senza tracciato non è nell'elenco e non c'entra; la mappa non è una SID da elencare.
        Assert.Equal([new ProceduraDellaComposta(null, "ALAX7G"), new ProceduraDellaComposta(null, "ALAX7H")],
            MappeComposte.ProcedureCheDisegna(composta.Mappa, letto.Records));
    }

    [Fact]
    public void ApplicataAllaSid_LaMappaHaIlTracciatoNuovo()
    {
        var letto = Sid(["//@\"SID RNAV(ALL)\" compose=ALAX7H", "//@START", .. Mappa, "//@END \"SID RNAV(ALL)\"", "", .. Procedure]);
        var composta = Assert.Single(MappeComposte.Di(letto));
        var rigenerata = composta.Componi(letto.Records);
        Assert.False(MappeComposte.Uguale(composta.Mappa, rigenerata.Punti));

        Assert.True(MappeComposte.Applica(composta.Mappa, rigenerata.Punti));

        var mappa = (SidProcedure)composta.Mappa;
        Assert.Equal(["VUNHO", "RN703", "MORCI", "MEZHO", "ALAXI"], mappa.Track.Select(t => t.Punto.Nome));
        Assert.Equal("7H", mappa.Track[0].Etichetta);
        Assert.All(mappa.Track, t => Assert.False(t.NuovoTratto));
    }

    [Fact]
    public void UnaMappaDiSidSenzaElencoNonECompostaEDiQualunqueLeTrova()
    {
        var letto = Sid([.. Mappa, "", .. Procedure]);

        Assert.Empty(MappeComposte.Di(letto));
        Assert.Empty(MappeComposte.DiQualunque(letto));
        Assert.Equal("SID RNAV(ALL)", MappeComposte.Nome(letto.Records[0]));
        Assert.False(MappeComposte.EUnaMappa(letto.Records[1]));
    }
}
