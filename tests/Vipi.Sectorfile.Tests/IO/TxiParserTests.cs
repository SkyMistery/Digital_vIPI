using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;
using Xunit;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>TxiParser / TxiSaver — TEST_MATRIX §8.1 … §8.4.</summary>
public sealed class TxiParserTests
{
    private readonly CollectingWarnings _warnings = new();
    private TxiParser Parser => new(_warnings);
    private ParseResult<TaxiwayLabel> Parse(string text) => Parser.Parse(ParserTestHelpers.Read(text), "lirf.txi", new ColorPalette());

    // §8.1 — round-trip the real lirf.txi (repeated label names → _N marker suffix only if dirty).
    [Fact]
    public void RoundTrip_Real()
    {
        string? path = RealSectorFiles.Path("lirf.txi");
        if (path is null) return;
        var (original, written) = ParserTestHelpers.RoundTrip(Parser, new TxiSaver(), path);
        Assert.Equal(original, written);
    }

    // §8.2 — standard 4-field record.
    [Fact]
    public void Parse_StandardRecord()
    {
        var r = Parse("AA;LIRF;N041.48.55.339;E012.13.43.605;\r\n").Records[0];
        Assert.Equal("AA", r.Name);
        Assert.Equal("LIRF", r.IcaoCode);
        Assert.Equal(41.815372, r.Position.LatitudeDeg, 5);
    }

    // §8.3 — empty file → no records.
    [Fact]
    public void Parse_Empty_NoRecords() => Assert.Empty(Parse(string.Empty).Records);

    // §8.4 — malformed line (3 fields) → RawChunk + warning.
    [Fact]
    public void Parse_TooFewFields_RawChunkAndWarning()
    {
        var pr = Parse("AA;LIRF;N041.48.55.339;\r\n");
        Assert.Empty(pr.Records);
        Assert.Equal(1, _warnings.Count);
        Assert.Contains(pr.Chunks, c => c is RawChunk<TaxiwayLabel>);
    }
}
