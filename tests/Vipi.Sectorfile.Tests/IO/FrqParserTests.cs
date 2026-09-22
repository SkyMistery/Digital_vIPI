using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;
using Xunit;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>FrqParser / FrqSaver — TEST_MATRIX §16.1 … §16.10.</summary>
public sealed class FrqParserTests
{
    private readonly CollectingWarnings _warnings = new();
    private FrqParser Parser => new(_warnings);
    private ParseResult<AtcPosition> Parse(string text) => Parser.Parse(ParserTestHelpers.Read(text), "itfreq.frq", new ColorPalette());

    // §16.1 / §16.2 — round-trip the real .frq files.
    [Fact]
    public void RoundTrip_Real_Itfreq()
    {
        string? path = RealSectorFiles.Path("OTHER/itfreq.frq");
        if (path is null) return;
        var (o, w) = ParserTestHelpers.RoundTrip(Parser, new FrqSaver(), path);
        Assert.Equal(o, w);
    }

    [Fact]
    public void RoundTrip_Real_Lirr()
    {
        string? path = RealSectorFiles.Path("OTHER/lirr.frq");
        if (path is null) return;
        var (o, w) = ParserTestHelpers.RoundTrip(Parser, new FrqSaver(), path);
        Assert.Equal(o, w);
    }

    // §16.3 — negative transfer entry → IsNegative = true, code without '-'.
    [Fact]
    public void Parse_NegativeTransfer()
    {
        var r = Parse("LIMM_ES2_CTR;130.730;LIMM -LIMC_ASW_APP;PREFS\\CTR.cpr;;0;\r\n").Records[0];
        Assert.Equal(2, r.TransferList.Count);
        Assert.False(r.TransferList[0].IsNegative);
        Assert.Equal("LIMM", r.TransferList[0].PositionCode);
        Assert.True(r.TransferList[1].IsNegative);
        Assert.Equal("LIMC_ASW_APP", r.TransferList[1].PositionCode);
    }

    // §16.4 — empty TransferList → no entries.
    [Fact]
    public void Parse_EmptyTransferList()
        => Assert.Empty(Parse("LIMC_DEL;120.900;;PREFS\\TWR.cpr;;0;\r\n").Records[0].TransferList);

    // §16.5 — BlockCpdlc "1" → true.
    [Fact]
    public void Parse_BlockCpdlc()
        => Assert.True(Parse("X_CTR;129.825;;PREFS\\CTR.cpr;;1;\r\n").Records[0].BlockCpdlc);

    // §16.6 — empty AtisFile → null.
    [Fact]
    public void Parse_EmptyAtisFile_Null()
        => Assert.Null(Parse("X_CTR;120.000;;PREFS\\CTR.cpr;;0;\r\n").Records[0].AtisFile);

    // §16.7 — DatisFile absent → null; present → captured.
    [Fact]
    public void Parse_DatisFile()
    {
        Assert.Null(Parse("X_CTR;120.000;;PREFS\\CTR.cpr;;0;\r\n").Records[0].DatisFile);
        Assert.Equal("datis-acc.datis",
            Parse("X_CTR;120.000;;PREFS\\CTR.cpr;;0;;datis-acc.datis\r\n").Records[0].DatisFile);
    }

    // §16.8 — a leading comment becomes the record's LeadingComments.
    [Fact]
    public void Parse_LeadingComment()
    {
        var pr = Parse("//MILANO ACC\r\nLIMM_WS2_CTR;135.455;LILA LILE;PREFS\\CTR.cpr;;0;;datis-acc.datis\r\n");
        var chunk = (RecordChunk<AtcPosition>)pr.Chunks.First(c => c is RecordChunk<AtcPosition>);
        Assert.Equal(new[] { "//MILANO ACC" }, chunk.LeadingComments);
    }

    // §16.9 / §16.10 — position code (incl. _TWR / _CTR suffix) captured verbatim;
    // suffix→ownership classification is the loader's responsibility, not the parser's.
    [Theory]
    [InlineData("LIMC_TWR")]
    [InlineData("LIMM_WS2_CTR")]
    public void Parse_CodeVerbatim(string code)
        => Assert.Equal(code, Parse($"{code};128.350;LIMM;PREFS\\TWR.cpr;;0;\r\n").Records[0].Code);
}
