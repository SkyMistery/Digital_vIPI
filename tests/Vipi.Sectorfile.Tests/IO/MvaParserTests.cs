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
}
