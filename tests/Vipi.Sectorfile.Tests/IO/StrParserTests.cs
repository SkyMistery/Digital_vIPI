using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Xunit;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>StrParser / StrSaver — TEST_MATRIX §7.1 … §7.13.</summary>
public sealed class StrParserTests
{
    private readonly CollectingWarnings _warnings = new();
    private StrParser Parser => new(_warnings);
    private ParseResult<StrRecord> Parse(string text) => Parser.Parse(ParserTestHelpers.Read(text), "lirf.str");

    // §7.1 / §7.13 — round-trip the real .str file (multiple records, <br>, inline comments, blanks).
    [Fact]
    public void RoundTrip_Real()
    {
        string? path = RealSectorFiles.Path("lirf.str");
        if (path is null) return;
        var (o, w) = ParserTestHelpers.RoundTrip(Parser, new StrSaver(), path);
        Assert.Equal(o, w);
    }

    // §7.2 — body of all coordinate lines → GeometricStrRecord.
    [Fact]
    public void AllCoordinates_IsGeometric()
    {
        var r = Parse(
            "LIRF;MAPS;ZONE; ; ;0;\r\n" +
            "N041.00.00.000;E012.00.00.000;\r\n" +
            "N041.10.00.000;E012.10.00.000;\r\n").Records[0];
        var g = Assert.IsType<GeometricStrRecord>(r);
        Assert.Single(g.Segments);
        Assert.Equal(2, g.Segments[0].Points.Count);
    }

    // §7.3 — body of all fix-reference lines → ProcedureStrRecord.
    [Fact]
    public void AllFixRefs_IsProcedure()
    {
        var r = Parse(
            "LIRF;16L:16R;ELKA3A; ; ;\r\n" +
            "ELKAP;ELKAP;\r\n" +
            "BIBEK;BIBEK;\r\n").Records[0];
        var p = Assert.IsType<ProcedureStrRecord>(r);
        Assert.Equal(2, p.Waypoints.Count);
        Assert.Equal("ELKAP", p.Waypoints[0].FixName);
    }

    // §7.4 — body mixing fix references and coordinates → HoldingStrRecord.
    [Fact]
    public void Mixed_IsHolding()
    {
        var r = Parse(
            "LIBA;11L;HLD-ELVAD; ; ;2;\r\n" +
            "ELVAD;ELVAD;\r\n" +
            "N041.38.57.354;E015.22.38.475;\r\n").Records[0];
        var h = Assert.IsType<HoldingStrRecord>(r);
        Assert.Equal(2, h.Points.Count);
        Assert.IsType<HoldingFixPoint>(h.Points[0]);
        Assert.IsType<HoldingCoordPoint>(h.Points[1]);
    }

    // §7.5 / §7.6 — <br> in field 3: that point is the FIRST of a NEW segment; the previous
    // segment does NOT contain it.
    [Fact]
    public void Br_StartsNewSegmentAtThatPoint()
    {
        var g = (GeometricStrRecord)Parse(
            "LIRF;MAPS;ZONE; ; ;0;\r\n" +
            "N041.00.00.000;E012.00.00.000;\r\n" +
            "N041.10.00.000;E012.10.00.000;\r\n" +
            "N041.20.00.000;E012.20.00.000;<br>\r\n" +
            "N041.30.00.000;E012.30.00.000;\r\n").Records[0];

        Assert.Equal(2, g.Segments.Count);
        Assert.Equal(2, g.Segments[0].Points.Count);                    // §7.6: only p1, p2
        Assert.Equal(2, g.Segments[1].Points.Count);                    // p3 (br) + p4
        Assert.Equal(41.0 + 20.0 / 60, g.Segments[1].Points[0].LatitudeDeg, 6);  // §7.5: br point is first
        Assert.DoesNotContain(g.Segments[0].Points, p => p.LatitudeDeg > 41.25);  // §7.6: previous excludes the br point (41.333)
    }

    // §7.7 — empty LabelLat/LabelLon fields → null.
    [Fact]
    public void EmptyLabel_IsNull()
    {
        var r = Parse("LIRF;MAPS;ZONE; ; ;0;\r\nN041.00.00.000;E012.00.00.000;\r\n").Records[0];
        Assert.Null(r.LabelLat);
        Assert.Null(r.LabelLon);
    }

    // §7.8 — fix-reference body without a 3rd field → SuffixCode null.
    [Fact]
    public void NoSuffix_IsNull()
    {
        var p = (ProcedureStrRecord)Parse(
            "LIRF;16L:16R;ELKA3A; ; ;\r\nBIBEK;BIBEK;\r\n").Records[0];
        Assert.Null(p.Waypoints[0].SuffixCode);
    }

    // §7.9 — SuffixCode "3A" preserved.
    [Fact]
    public void Suffix_Preserved()
    {
        var p = (ProcedureStrRecord)Parse(
            "LIRF;16L:16R;ELKA3A; ; ;\r\nELKAP;ELKAP;3A;\r\n").Records[0];
        Assert.Equal("3A", p.Waypoints[0].SuffixCode);
    }

    // §7.10 — RecordType = 5 → GoAround.
    [Fact]
    public void RecordType_GoAround()
    {
        var r = Parse("LIRF;MAPS;LIRF ATZ; ; ;5;\r\nN041.00.00.000;E012.00.00.000;\r\n").Records[0];
        Assert.Equal(StrRecordType.GoAround, r.RecordType);
    }

    // §7.11 — Transition and RNAV absent → null.
    [Fact]
    public void TransitionAndRnav_Absent_IsNull()
    {
        var r = Parse("LIRF;MAPS;ZONE; ; ;1;\r\nN041.00.00.000;E012.00.00.000;\r\n").Records[0];
        Assert.Null(r.Transition);
        Assert.Null(r.IsRnav);
    }

    // §7.12 — RunwaySpec "07:16L:16R" kept as an opaque string.
    [Fact]
    public void RunwaySpec_Opaque()
    {
        var r = Parse("LIRF;07:16L:16R;FOO; ; ;0;\r\nN041.00.00.000;E012.00.00.000;\r\n").Records[0];
        Assert.Equal("07:16L:16R", r.RunwaySpec);
    }

    // Multiple records in one file, blank lines inside a record do not split it.
    [Fact]
    public void BlankLinesInsideRecord_DoNotSplit()
    {
        var pr = Parse(
            "LIRF;MAPS;ZONE; ; ;0;\r\n" +
            "N041.00.00.000;E012.00.00.000;\r\n" +
            "\r\n" +
            "N041.10.00.000;E012.10.00.000;<br>\r\n" +
            "N041.20.00.000;E012.20.00.000;\r\n" +
            "\r\n" +
            "LIRF;MAPS;ZONE2; ; ;0;\r\n" +
            "N042.00.00.000;E013.00.00.000;\r\n");
        Assert.Equal(2, pr.Records.Count);
        var g = (GeometricStrRecord)pr.Records[0];
        Assert.Equal(2, g.Segments.Count);   // blank lines are cosmetic; only <br> splits segments
    }
}
