using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;
using Xunit;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>MvaAirportParser / MvaEnrouteParser / MvaSaver — TEST_MATRIX §12 (airport), §13 (enroute).</summary>
public sealed class MvaParserTests
{
    private readonly CollectingWarnings _warnings = new();
    private ParseResult<MvaSector> Airport(string text) => new MvaAirportParser(_warnings).Parse(ParserTestHelpers.Read(text), "liba.mva");
    private ParseResult<MvaSector> Enroute(string text) => new MvaEnrouteParser(_warnings).Parse(ParserTestHelpers.Read(text), "lirr.mva");

    // ── Airport (§12) ──────────────────────────────────────────────────────────

    // §12.1 — round-trip a real airport .mva (lirf.mva is absent; liba.mva stands in).
    [Fact]
    public void Airport_RoundTrip_Real()
    {
        string? path = RealSectorFiles.Path("liba.mva");
        if (path is null) return;
        var (o, w) = ParserTestHelpers.RoundTrip(new MvaAirportParser(_warnings), new MvaSaver(enroute: false), path);
        Assert.Equal(o, w);
    }

    // §12.2 / §12.3 — airport AltLabel = L; field 2; the repeated field 5 is not a separate value.
    [Fact]
    public void Airport_AltLabel_FromField2()
    {
        var r = Airport(
            "L;FL110;N041.00.00.000;E012.00.00.000;FL110;7;\r\n" +
            "T;FL110;N041.10.00.000;E012.10.00.000;\r\n" +
            "T;DUMMY;N000.00.00.000;E000.00.00.000;\r\n").Records[0];
        Assert.Equal("FL110", r.AltLabel);
        Assert.Equal(7, r.LabelSize);
        Assert.Single(r.LabelAnchors);
    }

    // Lotto «Subito» slice 5a: a named airport zone without an L; line (CERCHIO-BA in liba.mva) has no altitude, and
    // the saver wrote its vertices as `T;;N…;E…;` — without the zone's name, another zone for Aurora.
    [Fact]
    public void Airport_ZoneWithoutL_VerticesKeepTheZoneName()
    {
        var zona = Airport(
            "T;CERCHIO-BA;N041.30.50.674;E014.00.20.609;\r\n" +
            "T;CERCHIO-BA;N041.37.18.175;E014.00.29.254;\r\n").Records[0];
        zona.Vertices.Add(new MvaVertex { Position = Punto.Da(new Coordinate(41.5, 14.5)) });

        var righe = new MvaSaver(enroute: false).Serialize(zona);

        Assert.Equal("T;CERCHIO-BA;N041.30.00.000;E014.30.00.000;", righe[^1]);
        Assert.All(righe, r => Assert.StartsWith("T;CERCHIO-BA;", r, StringComparison.Ordinal));
    }

    // §12.4 — DUMMY row is kept in RawLines, never added to Vertices.
    [Fact]
    public void Airport_Dummy_InRawLinesNotVertices()
    {
        var pr = Airport(
            "L;FL110;N041.00.00.000;E012.00.00.000;FL110;7;\r\n" +
            "T;FL110;N041.10.00.000;E012.10.00.000;\r\n" +
            "T;FL110;N041.20.00.000;E012.20.00.000;\r\n" +
            "T;DUMMY;N000.00.00.000;E000.00.00.000;\r\n");
        var chunk = (RecordChunk<MvaSector>)pr.Chunks.First(c => c is RecordChunk<MvaSector>);
        Assert.Equal(2, chunk.Record.Vertices.Count);
        Assert.Contains(chunk.RawLines, l => l.StartsWith("T;DUMMY"));
    }

    // F3 slice 10 — lowercase "dummy" is a terminator too, as Aurora reads it.
    [Fact]
    public void Airport_LowercaseDummy_IsATerminatorToo()
    {
        var r = Airport(
            "L;FL110;N041.00.00.000;E012.00.00.000;FL110;7;\r\n" +
            "T;FL110;N041.10.00.000;E012.10.00.000;\r\n" +
            "T;dummy;N000.00.00.000;E000.00.00.000;\r\n").Records[0];
        Assert.Single(r.Vertices);
    }

    // §12.5 — DUMMY coordinates are never inspected (even absurd ones don't throw or become a vertex).
    [Fact]
    public void Airport_Dummy_AbsurdCoords_Ignored()
    {
        var r = Airport(
            "L;FL110;N041.00.00.000;E012.00.00.000;FL110;7;\r\n" +
            "T;FL110;N041.10.00.000;E012.10.00.000;\r\n" +
            "T;DUMMY;N099.99.99.999;E099.99.99.999;\r\n").Records[0];
        Assert.Single(r.Vertices);
    }

    // §12.6 — commented L; (active T) → LabelAnchors empty, record still valid.
    [Fact]
    public void Airport_CommentedL_NoAnchors()
    {
        var r = Assert.Single(Airport(
            "//L;FL110;N041.00.00.000;E012.00.00.000;FL110;7;\r\n" +
            "T;FL110;N041.10.00.000;E012.10.00.000;\r\n" +
            "T;DUMMY;N000.00.00.000;E000.00.00.000;\r\n").Records);
        Assert.Empty(r.LabelAnchors);
        Assert.Single(r.Vertices);
    }

    // §12.7 — active L; with all T; commented → Vertices empty, record still valid.
    [Fact]
    public void Airport_AllTCommented_NoVertices()
    {
        var r = Assert.Single(Airport(
            "L;FL110;N041.00.00.000;E012.00.00.000;FL110;7;\r\n" +
            "//T;FL110;N041.10.00.000;E012.10.00.000;\r\n" +
            "T;DUMMY;N000.00.00.000;E000.00.00.000;\r\n").Records);
        Assert.Empty(r.Vertices);
        Assert.Single(r.LabelAnchors);
    }

    // §12.8 — AltLabel "3000N" preserved verbatim.
    [Fact]
    public void Airport_AltLabel_Verbatim()
        => Assert.Equal("3000N", Airport("L;3000N;N041.00.00.000;E012.00.00.000;3000N;7;\r\nT;DUMMY;N000.00.00.000;E000.00.00.000;\r\n").Records[0].AltLabel);

    // §12.9 — multiple blocks (blank-separated) → correct record count.
    [Fact]
    public void Airport_MultipleBlocks_Count()
    {
        var pr = Airport(
            "L;FL110;N041.00.00.000;E012.00.00.000;FL110;7;\r\nT;DUMMY;N000.00.00.000;E000.00.00.000;\r\n" +
            "\r\n" +
            "L;FL090;N042.00.00.000;E013.00.00.000;FL090;7;\r\nT;DUMMY;N000.00.00.000;E000.00.00.000;\r\n");
        Assert.Equal(2, pr.Records.Count);
    }

    // ── Enroute (§13) ──────────────────────────────────────────────────────────

    // §13.1 — round-trip the real ENRMVA/lirr.mva.
    [Fact]
    public void Enroute_RoundTrip_Real()
    {
        string? path = RealSectorFiles.Path("ENRMVA/lirr.mva");
        if (path is null) return;
        var (o, w) = ParserTestHelpers.RoundTrip(new MvaEnrouteParser(_warnings), new MvaSaver(enroute: true), path);
        Assert.Equal(o, w);
    }

    // §13.2 / §13.3 — enroute AltLabel = L; field 5 (field 2 = FIR code, not the label).
    [Fact]
    public void Enroute_AltLabel_FromField5()
    {
        var r = Enroute(
            "L;LIRR;N041.00.00.000;E012.00.00.000;100;8;\r\n" +
            "T;LIRR;N041.10.00.000;E012.10.00.000;LIRR;\r\n" +
            "T;DUMMY;N000.00.00.000;E000.00.00.000;\r\n").Records[0];
        Assert.Equal("100", r.AltLabel);
        Assert.Equal(8, r.LabelSize);
        Assert.Equal("LIRR", r.Vertices[0].ExtraField);
    }

    // §13.4 — multiple L; lines per block → more than one LabelAnchor.
    [Fact]
    public void Enroute_MultipleL_MultipleAnchors()
    {
        var r = Enroute(
            "L;LIRR;N041.00.00.000;E012.00.00.000;100;8;\r\n" +
            "L;LIRR;N042.00.00.000;E013.00.00.000;100;8;\r\n" +
            "T;LIRR;N041.10.00.000;E012.10.00.000;LIRR;\r\n" +
            "T;DUMMY;N000.00.00.000;E000.00.00.000;\r\n").Records[0];
        Assert.Equal(2, r.LabelAnchors.Count);
    }

    // §13.5 / §13.6 — composite AltLabels preserved verbatim.
    [Theory]
    [InlineData("70/TRL")]
    [InlineData("*30/40")]
    public void Enroute_AltLabel_Composite(string label)
        => Assert.Equal(label, Enroute($"L;LIRR;N041.00.00.000;E012.00.00.000;{label};8;\r\nT;DUMMY;N000.00.00.000;E000.00.00.000;\r\n").Records[0].AltLabel);

    // §13.7 / §13.10 — plain-text line between blocks is silently skipped (no record, no warning).
    [Fact]
    public void Enroute_PlainText_SilentlySkipped()
    {
        var pr = Enroute(
            "EX ETNA\r\n" +
            "L;LIRR;N041.00.00.000;E012.00.00.000;100;8;\r\n" +
            "T;DUMMY;N000.00.00.000;E000.00.00.000;\r\n");
        Assert.Single(pr.Records);
        Assert.Equal(0, _warnings.Count);
        Assert.Contains(pr.Chunks, c => c is RawChunk<MvaSector> raw && raw.Lines.Contains("EX ETNA"));
    }

    // §13.8 — DUMMY in enroute → RawLines, coordinates not read.
    [Fact]
    public void Enroute_Dummy_NotVertex()
    {
        var r = Enroute(
            "L;LIRR;N041.00.00.000;E012.00.00.000;100;8;\r\n" +
            "T;LIRR;N041.10.00.000;E012.10.00.000;LIRR;\r\n" +
            "T;DUMMY;N000.00.00.000;E000.00.00.000;\r\n").Records[0];
        Assert.Single(r.Vertices);
    }

    // §13.9 — commented L; in enroute → LabelAnchors empty, record valid.
    [Fact]
    public void Enroute_CommentedL_NoAnchors()
    {
        var r = Assert.Single(Enroute(
            "//L;LIRR;N041.00.00.000;E012.00.00.000;100;8;\r\n" +
            "T;LIRR;N041.10.00.000;E012.10.00.000;LIRR;\r\n" +
            "T;DUMMY;N000.00.00.000;E000.00.00.000;\r\n").Records);
        Assert.Empty(r.LabelAnchors);
        Assert.Equal(string.Empty, r.AltLabel);
        Assert.Single(r.Vertices);
    }

    // Lotto «Subito», slice 3b: la quota cambiata dalla scheda. Una zona di ACC con le T tutte commentate non ha
    // vertici che portino il gruppo nel 5° campo, e lo scrittore ci metteva la QUOTA (L;LIRR;…;100;8; → L;90;…;90;8;):
    // il gruppo LIRR (la voce della MVA Selection di Aurora) spariva. Il gruppo è il 2° campo della prima riga.
    [Fact]
    public void Enroute_ZonaSenzaVerticiAttivi_TieneIlGruppoNellaL()
    {
        var letto = Enroute(
            "L;LIRR;N041.08.58.289;E013.24.48.073;100;8;\r\n" +
            "//T;LIRR;N041.25.40.000;E013.11.48.000;LIRR;\r\n" +
            "//T;LIRR;N041.02.37.000;E013.07.22.000;LIRR;\r\n" +
            "T;DUMMY;N000.00.00.000;E000.00.00.000;\r\n").FissaLeBasi(new MvaSaver(enroute: true));
        var zona = Assert.Single(letto.Records);
        zona.AltLabel = "90";

        var righe = new FileSaverOrchestrator().Righe(letto, new HashSet<MvaSector> { zona }, new MvaSaver(enroute: true));

        Assert.Equal("L;LIRR;N041.08.58.289;E013.24.48.073;90;8;", righe[0]);
        Assert.Equal("//T;LIRR;N041.25.40.000;E013.11.48.000;LIRR;", righe[1]);
        Assert.Equal("T;DUMMY;N000.00.00.000;E000.00.00.000;", righe[3]);
    }

    // Stessa cosa per un vertice di ACC senza il 5° campo: il 2° è il gruppo, non la quota.
    [Fact]
    public void Enroute_VerticeSenzaQuintoCampo_TieneIlGruppo()
    {
        var zona = Assert.Single(Enroute(
            "L;LIRR;N041.00.00.000;E012.00.00.000;100;8;\r\n" +
            "T;LIRR;N041.10.00.000;E012.10.00.000;\r\n" +
            "T;DUMMY;N000.00.00.000;E000.00.00.000;\r\n").Records);
        zona.AltLabel = "90";

        var righe = new MvaSaver(enroute: true).Serialize(zona);

        Assert.Equal(["L;LIRR;N041.00.00.000;E012.00.00.000;90;8;", "T;LIRR;N041.10.00.000;E012.10.00.000;"], righe);
    }

    // ── Lotto «Subito» slice 15a (S1) ─────────────────────────────────────────────────────────────────────────────
    // Le MVA di scalo si leggono come quelle di ACC: il 2° campo della L è il nome (il gruppo, o la zona), il 5° la
    // quota scritta a schermo — il formato del manuale, e così sono tutte le 226 etichette del fork. Prima la quota era
    // il 2° campo: «RR US0» al posto di «110», e cambiarla avrebbe cancellato quella vera (per questo erano in sola
    // lettura dalla slice 3b).
    [Fact]
    public void Airport_LaQuotaEIlQuintoCampo_IlSecondoEIlNome()
    {
        var letto = Airport(
            "L;RR US0;N041.09.33.780;E015.00.54.430;110;8;\r\n" +
            "T;RR US0;N041.16.00.000;E014.53.00.000;\r\n" +
            "T;RR US0;N041.12.00.000;E015.07.00.000;\r\n" +
            "T;DUMMY;N000.00.00.000;E000.00.00.000;\r\n").FissaLeBasi(new MvaSaver(enroute: false));
        var zona = Assert.Single(letto.Records);
        Assert.Equal("110", zona.AltLabel);
        Assert.Equal("RR US0", zona.Nome);
        Assert.Equal(8, zona.LabelSize);

        zona.AltLabel = "90";
        var righe = new FileSaverOrchestrator().Righe(letto, new HashSet<MvaSector> { zona }, new MvaSaver(enroute: false));

        Assert.Equal(
            ["L;RR US0;N041.09.33.780;E015.00.54.430;90;8;", "T;RR US0;N041.16.00.000;E014.53.00.000;",
             "T;RR US0;N041.12.00.000;E015.07.00.000;", "T;DUMMY;N000.00.00.000;E000.00.00.000;"], righe);
    }

    // Nei file «un nome per zona» il nome somiglia a una quota (3500) e la quota vera è nel 5° campo (35): cambiata la
    // quota, il nome della zona resta.
    [Fact]
    public void Airport_UnNomeCheSembraUnaQuotaRestaIlNome()
    {
        var letto = Airport(
            "L;3500;N041.00.00.000;E012.00.00.000;35;7;\r\n" +
            "T;3500;N041.10.00.000;E012.10.00.000;\r\n").FissaLeBasi(new MvaSaver(enroute: false));
        var zona = Assert.Single(letto.Records);
        Assert.Equal("35", zona.AltLabel);

        zona.AltLabel = "40";
        var righe = new FileSaverOrchestrator().Righe(letto, new HashSet<MvaSector> { zona }, new MvaSaver(enroute: false));

        Assert.Equal(["L;3500;N041.00.00.000;E012.00.00.000;40;7;", "T;3500;N041.10.00.000;E012.10.00.000;"], righe);
    }

    // Un blocco che raccoglie le etichette di più zone (liba.mva: sei L con sei quote, in fondo al file) ha una quota
    // per riga, e il modello ne tiene una: il lettore lo dice, e la scheda non la fa scrivere (8 blocchi sul fork).
    [Fact]
    public void UnBloccoConEtichetteDiQuoteDiverseLoDice()
    {
        var raccolta = Assert.Single(Airport(
            "L;90;N041.43.34.000;E014.14.55.000;90;7;\r\n" +
            "L;60;N041.35.56.000;E014.47.57.000;60;7;\r\n").Records);
        Assert.True(raccolta.EtichetteDiverse);

        // Due etichette della stessa zona, con la stessa quota (libb.mva): è una zona sola.
        var zona = Assert.Single(Enroute(
            "L;LIBB;N041.27.11.041;E017.53.31.105;100;8;\r\n" +
            "L;LIBB;N039.21.59.653;E017.29.40.855;100;8;\r\n" +
            "T;LIBB;N042.03.45.000;E016.57.44.000;LIBB;\r\n").Records);
        Assert.False(zona.EtichetteDiverse);
    }

    // E3: un vertice che il Lab aggiunge a una zona di ACC porta il gruppo nel 5° campo, come gli altri; uno letto
    // senza resta senza (la riga non cambia sotto le mani di chi non l'ha toccata).
    [Fact]
    public void Enroute_UnVerticeNuovoPortaIlGruppo()
    {
        var zona = Assert.Single(Enroute(
            "L;LIRR;N041.00.00.000;E012.00.00.000;100;8;\r\n" +
            "T;LIRR;N041.10.00.000;E012.10.00.000;LIRR;\r\n" +
            "T;LIRR;N041.20.00.000;E012.20.00.000;\r\n" +
            "T;DUMMY;N000.00.00.000;E000.00.00.000;\r\n").Records);
        zona.Vertices.Add(new MvaVertex { Position = Punto.Da(new Coordinate(41.5, 12.5)) });

        var righe = new MvaSaver(enroute: true).Serialize(zona);

        Assert.Equal(
            ["L;LIRR;N041.00.00.000;E012.00.00.000;100;8;", "T;LIRR;N041.10.00.000;E012.10.00.000;LIRR;",
             "T;LIRR;N041.20.00.000;E012.20.00.000;", "T;LIRR;N041.30.00.000;E012.30.00.000;LIRR;"], righe);
    }
}
