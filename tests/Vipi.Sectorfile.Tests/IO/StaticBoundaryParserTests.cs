using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Xunit;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>HartccParser / LartccParser + savers — TEST_MATRIX §17 (hartcc), §18 (lartcc).</summary>
public sealed class StaticBoundaryParserTests
{
    private readonly CollectingWarnings _warnings = new();
    private ParseResult<StaticBoundaryGroup> Hartcc(string text)
        => new HartccParser(_warnings).Parse(ParserTestHelpers.Read(text), "lirr.hartcc");
    private ParseResult<StaticBoundaryGroup> Lartcc(string text)
        => new LartccParser(_warnings).Parse(ParserTestHelpers.Read(text), "lirr_tma.lartcc");

    private static RecordChunk<StaticBoundaryGroup> FirstRecord(ParseResult<StaticBoundaryGroup> pr)
        => (RecordChunk<StaticBoundaryGroup>)pr.Chunks.First(c => c is RecordChunk<StaticBoundaryGroup>);

    // §17.1 — round-trip the real .hartcc file.
    [Fact]
    public void Hartcc_RoundTrip_Real()
    {
        string? path = RealSectorFiles.Path("HI_AIRSPACE/lirr.hartcc");
        if (path is null) return;
        var (o, w) = ParserTestHelpers.RoundTrip(new HartccParser(_warnings), new HartccSaver(), path);
        Assert.Equal(o, w);
    }

    // §18.1 — round-trip the real .lartcc file.
    [Fact]
    public void Lartcc_RoundTrip_Real()
    {
        string? path = RealSectorFiles.Path("LOW_AIRSPACE/lirr_tma.lartcc");
        if (path is null) return;
        var (o, w) = ParserTestHelpers.RoundTrip(new LartccParser(_warnings), new LartccSaver(), path);
        Assert.Equal(o, w);
    }

    // §17.2 — a single group with a single polygon.
    [Fact]
    public void SingleGroup_SinglePolygon()
    {
        var pr = Hartcc(
            "T;RR NE;N041.00.00.000;E012.00.00.000;\r\n" +
            "T;RR NE;N041.10.00.000;E012.10.00.000;\r\n");
        Assert.Single(pr.Records);
        Assert.Equal("RR NE", pr.Records[0].Name);
        Assert.Single(pr.Records[0].Polygons);
        Assert.Equal(2, pr.Records[0].Polygons[0].Vertices.Count);
    }

    // §17.3 — a DUMMY line splits one group into two polygons.
    [Fact]
    public void Dummy_SplitsIntoTwoPolygons()
    {
        var g = Hartcc(
            "T;RR NE;N041.00.00.000;E012.00.00.000;\r\n" +
            "T;DUMMY;N000.00.00.000;E000.00.00.000;\r\n" +
            "T;RR NE;N041.10.00.000;E012.10.00.000;\r\n").Records[0];
        Assert.Equal(2, g.Polygons.Count);
        Assert.Single(g.Polygons[0].Vertices);
        Assert.Single(g.Polygons[1].Vertices);
    }

    // F3 slice 10 — a "dummy" in the NAME field (2) is a separator in any case, as Aurora reads it: FRA-gates.artcc
    // and three more files write T;dummy;N000.00.00.000;E000.00.00.000; (100 rows). Read as a vertex, every COP
    // circle of FRA-gates drew a line to N0 E0 on the Lab's map.
    [Fact]
    public void LowercaseDummyInTheNameField_IsASeparator_AndIsWrittenBackAsItWas()
    {
        var pr = Hartcc(
            "T;RR NE;N041.00.00.000;E012.00.00.000;\r\n" +
            "T;dummy;N000.00.00.000;E000.00.00.000;\r\n" +
            "T;RR NE;N041.10.00.000;E012.10.00.000;\r\n");
        var g = pr.Records[0];

        Assert.Equal(2, g.Polygons.Count);
        Assert.DoesNotContain(g.Polygons.SelectMany(p => p.Vertices), v => v.Position is { LatitudeDeg: 0, LongitudeDeg: 0 });
        Assert.Contains(FirstRecord(pr).RawLines, l => l.StartsWith("T;dummy;", StringComparison.Ordinal));
    }

    // §17.4 — "dummy" as a FIX name (fields 3 and 4) is a fix-pair vertex, not a separator: only field 2 separates.
    [Fact]
    public void LowercaseDummy_IsNormalVertex()
    {
        var g = Hartcc(
            "T;RR NE;N041.00.00.000;E012.00.00.000;\r\n" +
            "T;RR NE;dummy;dummy;\r\n").Records[0];
        Assert.Single(g.Polygons);
        Assert.Equal(2, g.Polygons[0].Vertices.Count);
        Assert.Equal("dummy", g.Polygons[0].Vertices[1].FixA);   // treated as a fix-pair
    }

    // §17.5 — fix-pair vertex: field 3 is not a coordinate → FixA/FixB set, Position null.
    [Fact]
    public void FixPairVertex()
    {
        var v = Hartcc("T;RR CONF1;TIPNI;TIPNI;\r\n").Records[0].Polygons[0].Vertices[0];
        Assert.Null(v.Position);
        Assert.Equal("TIPNI", v.FixA);
        Assert.Equal("TIPNI", v.FixB);
    }

    // §17.6 — coordinate vertex: field 3 is a coordinate → Position set.
    [Fact]
    public void CoordinateVertex()
    {
        var v = Hartcc("T;RR CONF1;N044.23.17.000;E011.07.44.000;\r\n").Records[0].Polygons[0].Vertices[0];
        Assert.NotNull(v.Position);
        Assert.Null(v.FixA);
        Assert.Equal(44.0 + 23.0 / 60 + 17.0 / 3600, v.Position!.Value.LatitudeDeg, 6);
    }

    // §17.6b — a fix starting with 'N' (e.g. NILTO) is still a fix-pair, not a coordinate.
    [Fact]
    public void FixStartingWithN_IsFixPair()
    {
        var v = Hartcc("T;RR CONF1;NILTO;NILTO;\r\n").Records[0].Polygons[0].Vertices[0];
        Assert.Null(v.Position);
        Assert.Equal("NILTO", v.FixA);
    }

    // §17.7 — "RR CONF1" is modelled as a normal group (no special CONF handling).
    [Fact]
    public void ConfGroup_IsNormalGroup()
        => Assert.Equal("RR CONF1", Hartcc("T;RR CONF1;N044.23.17.000;E011.07.44.000;\r\n").Records[0].Name);

    // §17.8 — three DUMMY-separated polygons → Polygons.Count == 3.
    [Fact]
    public void ThreePolygons()
    {
        var g = Hartcc(
            "T;RR NE;N041.00.00.000;E012.00.00.000;\r\n" +
            "T;DUMMY;N000.00.00.000;E000.00.00.000;\r\n" +
            "T;RR NE;N041.10.00.000;E012.10.00.000;\r\n" +
            "T;DUMMY;N000.00.00.000;E000.00.00.000;\r\n" +
            "T;RR NE;N041.20.00.000;E012.20.00.000;\r\n").Records[0];
        Assert.Equal(3, g.Polygons.Count);
    }

    // §17.9 — DUMMY lines go to RawLines, never to Vertices.
    [Fact]
    public void Dummy_InRawLinesNotVertices()
    {
        var pr = Hartcc(
            "T;RR NE;N041.00.00.000;E012.00.00.000;\r\n" +
            "T;DUMMY;N000.00.00.000;E000.00.00.000;\r\n" +
            "T;RR NE;N041.10.00.000;E012.10.00.000;\r\n");
        var chunk = FirstRecord(pr);
        Assert.Equal(2, chunk.Record.Polygons.Sum(p => p.Vertices.Count));
        Assert.Contains(chunk.RawLines, l => l.StartsWith("T;DUMMY"));
    }

    // §17.10 — multiple blank-separated groups → Groups.Count correct.
    [Fact]
    public void MultipleGroups_Count()
    {
        var pr = Hartcc(
            "T;RR NE;N041.00.00.000;E012.00.00.000;\r\n\r\n" +
            "T;RR NW;N041.10.00.000;E012.10.00.000;\r\n\r\n" +
            "T;RR ES;N041.20.00.000;E012.20.00.000;\r\n\r\n" +
            "T;RR EW;N041.30.00.000;E012.30.00.000;\r\n");
        Assert.Equal(4, pr.Records.Count);
        Assert.Equal(new[] { "RR NE", "RR NW", "RR ES", "RR EW" }, pr.Records.Select(r => r.Name));
    }

    // §18.2 — LartccParser shares the structure: same parsing applies.
    [Fact]
    public void Lartcc_SameStructure()
    {
        var g = Lartcc(
            "T;RR CNF1;N041.30.18.000;E011.20.15.000;\r\n" +
            "T;DUMMY;N000.00.00.000;E000.00.00.000;\r\n" +
            "T;RR CNF1;N042.10.30.000;E011.26.45.000;\r\n").Records[0];
        Assert.Equal("RR CNF1", g.Name);
        Assert.Equal(2, g.Polygons.Count);
    }

    // §18.3 — absence of any CONF* group is not an error.
    [Fact]
    public void Lartcc_NoConf_NoError()
    {
        var pr = Lartcc("T;RR ES0;N041.00.00.000;E012.00.00.000;\r\n");
        Assert.Single(pr.Records);
        Assert.Empty(_warnings.Snapshot());
    }

    // Leading comment before a group → LeadingComments; in-block comment after DUMMY → RawLines.
    [Fact]
    public void Comments_LeadingVsInBlock()
    {
        var pr = Hartcc(
            "//CONF2 - CONF1\r\n" +
            "T;RR CONF2;N044.00.00.000;E011.00.00.000;\r\n" +
            "T;DUMMY;N000.00.00.000;E000.00.00.000;\r\n" +
            "//RR CONF2 - TS+US+OV\r\n" +
            "T;RR CONF2;N041.44.00.000;E010.34.10.000;\r\n");
        var chunk = FirstRecord(pr);
        Assert.Contains(chunk.LeadingComments, l => l.Contains("CONF2 - CONF1"));
        Assert.Contains(chunk.RawLines, l => l.Contains("TS+US+OV"));
    }
}
