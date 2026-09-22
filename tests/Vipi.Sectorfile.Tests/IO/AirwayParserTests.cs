using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Xunit;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>AirwayParser / AirwaySaver — TEST_MATRIX §23.1 … §23.5.</summary>
public sealed class AirwayParserTests
{
    private readonly CollectingWarnings _warnings = new();
    private AirwayParser Parser => new(_warnings);
    private ParseResult<Airway> Parse(string text) => Parser.Parse(ParserTestHelpers.Read(text), "itawlow.lairway");

    // §23.1 — round-trip the real itawlow.lairway.
    [Fact]
    public void RoundTrip_Real_Lairway()
    {
        string? path = RealSectorFiles.Path("AIRWAY/itawlow.lairway");
        if (path is null) return;
        var (o, w) = ParserTestHelpers.RoundTrip(Parser, new AirwaySaver(), path);
        Assert.Equal(o, w);
    }

    // §23.2 — fully commented itawhigh.hairway → no records (and still round-trips).
    [Fact]
    public void FullyCommented_Hairway_NoRecords()
    {
        string? path = RealSectorFiles.Path("AIRWAY/itawhigh.hairway");
        if (path is null) return;
        var pr = Parser.Parse(path, new Vipi.Sectorfile.Shared.ColorPalette());
        Assert.Empty(pr.Records);

        var (o, w) = ParserTestHelpers.RoundTrip(Parser, new AirwaySaver(), path);
        Assert.Equal(o, w);
    }

    // §23.3 — T; lines → FixLabels populated.
    [Fact]
    public void TLines_PopulateFixLabels()
    {
        var r = Assert.Single(Parse("T;A1;FIX1;FIX1;\r\nT;A1;FIX2;FIX2;\r\n").Records);
        Assert.Equal("A1", r.Name);
        Assert.Equal(new[] { "FIX1", "FIX2" }, r.FixLabels);
        Assert.Empty(r.Coordinates);
    }

    // §23.4 — L; lines → Coordinates populated.
    [Fact]
    public void LLines_PopulateCoordinates()
    {
        var r = Assert.Single(Parse("L;A1;N041.00.00.000;E012.00.00.000;\r\nL;A1;N042.00.00.000;E013.00.00.000;\r\n").Records);
        Assert.Equal(2, r.Coordinates.Count);
        Assert.Empty(r.FixLabels);
        Assert.Equal(41.0, r.Coordinates[0].LatitudeDeg, 6);
    }

    // §23.5 — mix of T; and L; for the same (contiguous) airway → one Airway with both.
    [Fact]
    public void MixedTL_SameAirway_OneRecord()
    {
        var r = Assert.Single(Parse("T;A1;FIX1;FIX1;\r\nL;A1;N041.00.00.000;E012.00.00.000;\r\n").Records);
        Assert.Equal(new[] { "FIX1" }, r.FixLabels);
        Assert.Single(r.Coordinates);
    }

    // Two different names in one run → two separate Airway records.
    [Fact]
    public void DifferentNames_SplitRecords()
    {
        var pr = Parse("T;A1;FIX1;FIX1;\r\nT;A2;FIX2;FIX2;\r\n");
        Assert.Equal(2, pr.Records.Count);
        Assert.Equal("A1", pr.Records[0].Name);
        Assert.Equal("A2", pr.Records[1].Name);
    }
}
