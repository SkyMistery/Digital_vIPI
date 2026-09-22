using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Xunit;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>PolParser / PolSaver — TEST_MATRIX §4.1 … §4.9.</summary>
public sealed class PolParserTests
{
    private readonly CollectingWarnings _warnings = new();
    private PolParser Parser => new(_warnings);

    private const string Block =
        "//AD_BOUNDARY_Polygon\r\n" +
        "STATIC;GRASS;1;GRASS;\r\n" +
        "N041.49.04.288;E012.13.16.482;\r\n" +
        "N041.49.04.619;E012.13.16.725;\r\n" +
        "N041.49.11.875;E012.13.42.087;\r\n";

    private ParseResult<Polygon> Parse(string text) => Parser.Parse(ParserTestHelpers.Read(text), "rf_ad_gnd.pol");

    // §4.1 — round-trip the real GND_LAYOUT/rf_ad_gnd.pol.
    [Fact]
    public void RoundTrip_Real()
    {
        string? path = RealSectorFiles.Path("GND_LAYOUT/rf_ad_gnd.pol");
        if (path is null) return;
        var (original, written) = ParserTestHelpers.RoundTrip(Parser, new PolSaver(), path);
        Assert.Equal(original, written);
    }

    // §4.2 — header + vertices → one Polygon with the header's FillColor; leading comment attached.
    [Fact]
    public void Parse_HeaderAndVertices_Polygon()
    {
        var pr = Parse(Block);
        var p = Assert.Single(pr.Records);
        Assert.Equal("GRASS", p.FillColor);
        Assert.Equal("GRASS", p.LineColor);
        Assert.Equal(1f, p.LineWeight);
        Assert.Equal(3, p.Vertices.Count);

        var chunk = (RecordChunk<Polygon>)pr.Chunks.First(c => c is RecordChunk<Polygon>);
        Assert.Equal(new[] { "//AD_BOUNDARY_Polygon" }, chunk.LeadingComments);
    }

    // §4.3 — multiple blocks → correct record count.
    [Fact]
    public void Parse_MultipleBlocks_Count()
    {
        var pr = Parse(Block + "\r\n" + Block.Replace("GRASS", "TAXIWAY"));
        Assert.Equal(2, pr.Records.Count);
        Assert.Equal("TAXIWAY", pr.Records[1].FillColor);
    }

    // §4.4 / §4.5 — FillColor classification values parsed verbatim.
    [Theory]
    [InlineData("GRASS")]
    [InlineData("HOLE")]
    public void Parse_FillColor(string fill)
    {
        var pr = Parse($"STATIC;{fill};1;{fill};\r\nN041.00.00.000;E012.00.00.000;\r\nN041.00.01.000;E012.00.01.000;\r\nN041.00.02.000;E012.00.02.000;\r\n");
        Assert.Equal(fill, pr.Records[0].FillColor);
    }

    // §4.6 — unknown FillColor is accepted and stored verbatim (classification is the loader's job).
    [Fact]
    public void Parse_UnknownFillColor_StoredVerbatim()
    {
        var pr = Parse("STATIC;WEIRDCOLOR;1;WEIRDCOLOR;\r\nN041.00.00.000;E012.00.00.000;\r\nN041.00.01.000;E012.00.01.000;\r\nN041.00.02.000;E012.00.02.000;\r\n");
        Assert.Equal("WEIRDCOLOR", pr.Records[0].FillColor);
        Assert.Equal(0, _warnings.Count);
    }

    // §4.7 — a 2-vertex polygon is kept, but a warning is raised.
    [Fact]
    public void Parse_TwoVertexPolygon_ValidWithWarning()
    {
        var pr = Parse("STATIC;GRASS;1;GRASS;\r\nN041.00.00.000;E012.00.00.000;\r\nN041.00.01.000;E012.00.01.000;\r\n");
        Assert.Single(pr.Records);
        Assert.Equal(2, pr.Records[0].Vertices.Count);
        Assert.Equal(1, _warnings.Count);
    }

    // §4.8 — comments and blanks between blocks round-trip verbatim.
    [Fact]
    public void Parse_CommentsBetweenBlocks_RoundTrip()
    {
        string text = Block + "\r\n//standalone\r\n\r\n" + Block.Replace("GRASS", "HOLE");
        var pr = Parser.Parse(ParserTestHelpers.Read(text), "rf_ad_gnd.pol");

        string tmp = Path.Combine(Path.GetTempPath(), "asd_pol_" + Guid.NewGuid().ToString("N") + ".pol");
        try
        {
            new FileSaverOrchestrator().Save(pr, new HashSet<Polygon>(), new PolSaver(), tmp);
            Assert.Equal(System.Text.Encoding.UTF8.GetBytes(text), File.ReadAllBytes(tmp));
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    // §4.9 — vertices before any header are kept verbatim in a RawChunk (not a Polygon).
    [Fact]
    public void Parse_VerticesBeforeHeader_RawChunk()
    {
        var pr = Parse("N041.00.00.000;E012.00.00.000;\r\nSTATIC;GRASS;1;GRASS;\r\nN041.00.01.000;E012.00.01.000;\r\nN041.00.02.000;E012.00.02.000;\r\nN041.00.03.000;E012.00.03.000;\r\n");
        Assert.Single(pr.Records);   // only the headed block
        Assert.Contains(pr.Chunks, c => c is RawChunk<Polygon> raw && raw.Lines.Any(l => l.StartsWith("N041.00.00.000")));
    }
}
