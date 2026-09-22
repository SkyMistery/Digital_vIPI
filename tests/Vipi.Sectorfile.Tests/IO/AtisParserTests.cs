using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;
using Xunit;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>AtisParser / AtisSaver — TEST_MATRIX §14.1 … §14.4.</summary>
public sealed class AtisParserTests
{
    private readonly CollectingWarnings _warnings = new();
    private AtisParser Parser => new(_warnings);
    private ParseResult<AtisData> Parse(string text) => Parser.Parse(ParserTestHelpers.Read(text), "lica.atis", new ColorPalette());

    // §14.1 — round-trip a real .atis (lirf.atis is absent; lica.atis stands in).
    [Fact]
    public void RoundTrip_Real()
    {
        string? path = RealSectorFiles.Path("lica.atis");
        if (path is null) return;
        var (original, written) = ParserTestHelpers.RoundTrip(Parser, new AtisSaver(), path);
        Assert.Equal(original, written);
    }

    // §14.2 — template with placeholders preserved verbatim.
    [Fact]
    public void Parse_Template_Verbatim()
    {
        const string template = "This is [STATION_NAME] information [ATIS_LETTER] time [ATIS_TIME].";
        var r = Assert.Single(Parse(template + "\r\n").Records);
        Assert.Equal(template, r.Template);
    }

    // §14.3 — empty file → no records (AirportLoader maps this to a null Atis).
    [Fact]
    public void Parse_Empty_NoRecords() => Assert.Empty(Parse(string.Empty).Records);

    // §14.4 — a comment before the template becomes the record's LeadingComments.
    [Fact]
    public void Parse_LeadingComment()
    {
        var pr = Parse("//arrival+departure\r\nThis is [STATION_NAME] information [ATIS_LETTER].\r\n");
        var chunk = (RecordChunk<AtisData>)pr.Chunks.First(c => c is RecordChunk<AtisData>);
        Assert.Equal(new[] { "//arrival+departure" }, chunk.LeadingComments);
    }
}
