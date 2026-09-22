using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;
using Xunit;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>GeoParser / GeoSaver — TEST_MATRIX §3.1 … §3.7.</summary>
public sealed class GeoParserTests
{
    private readonly CollectingWarnings _warnings = new();
    private GeoParser Parser => new(_warnings);
    private static readonly ColorPalette Palette = new();

    private ParseResult<Line> Parse(string text) => Parser.Parse(ParserTestHelpers.Read(text), "lirf.geo", Palette);

    // §3.1 and §3.3 (round-trip of GEO/lirf.geo and GEO/itgeo.geo) did not come into vIPI: 1.2 MB of
    // samples for two byte comparisons. Both files round-trip in tools/Vipi.SectorfileProva, which runs
    // the whole tree (F2 card §5).

    // §3.2 — round-trip a real RW_MARKINGS file (rf_mark.geo does not exist; ba_mark.geo stands in).
    [Fact]
    public void RoundTrip_Real_RwMarkings()
    {
        string? path = RealSectorFiles.Path("RW_MARKINGS/ba_mark.geo");
        if (path is null) return;
        var (original, written) = ParserTestHelpers.RoundTrip(Parser, new GeoSaver(), path);
        Assert.Equal(original, written);
    }

    // §3.4 — each non-comment line → one Line record.
    [Fact]
    public void Parse_EachSegmentLine_OneRecord()
    {
        var pr = Parse(
            "N041.49.04.288;E012.13.16.482;N041.49.04.619;E012.13.16.725;BUILDING;\r\n" +
            "N041.49.04.619;E012.13.16.725;N041.49.11.875;E012.13.42.087;BUILDING;\r\n" +
            "N041.49.11.875;E012.13.42.087;N041.49.12.024;E012.13.43.317;RUNWAY;\r\n");
        Assert.Equal(3, pr.Records.Count);
    }

    // §3.5 — colour name is captured verbatim on the Line.
    [Fact]
    public void Parse_Colour_Captured()
    {
        var r = Parse("N041.49.04.288;E012.13.16.482;N041.49.04.619;E012.13.16.725;RUNWAY;\r\n").Records[0];
        Assert.Equal("RUNWAY", r.Color);
        Assert.Equal(41.817858, r.Start.LatitudeDeg, 5);
    }

    // §3.6 — line with no colour (4 fields) → RawChunk + warning, no record.
    [Fact]
    public void Parse_MissingColour_RawChunkAndWarning()
    {
        var pr = Parse("N041.49.04.288;E012.13.16.482;N041.49.04.619;E012.13.16.725;\r\n");
        Assert.Empty(pr.Records);
        Assert.Equal(1, _warnings.Count);
        Assert.Contains(pr.Chunks, c => c is RawChunk<Line>);
    }

    // §3.7 — comments and blank lines interspersed → round-trip (chunk coverage preserved).
    [Fact]
    public void Parse_CommentsAndBlanks_RoundTrip()
    {
        const string text =
            "//header\r\n" +
            "N041.49.04.288;E012.13.16.482;N041.49.04.619;E012.13.16.725;BUILDING;\r\n" +
            "\r\n" +
            "//mid\r\n" +
            "N041.49.04.619;E012.13.16.725;N041.49.11.875;E012.13.42.087;BUILDING;\r\n";

        var read = ParserTestHelpers.Read(text);
        var pr = Parser.Parse(read, "lirf.geo", Palette);

        string tmp = Path.Combine(Path.GetTempPath(), "asd_geo_" + Guid.NewGuid().ToString("N") + ".geo");
        try
        {
            new FileSaverOrchestrator().Save(pr, new HashSet<Line>(), new GeoSaver(), tmp);
            Assert.Equal(System.Text.Encoding.UTF8.GetBytes(text), File.ReadAllBytes(tmp));
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }
}
