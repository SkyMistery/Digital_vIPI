using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Xunit;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>TflParser / TflSaver — TEST_MATRIX §5.1 … §5.12.</summary>
public sealed class TflParserTests
{
    private readonly CollectingWarnings _warnings = new();
    private TflParser Parser => new(_warnings);
    private ParseResult<TflSector> Parse(string text) => Parser.Parse(ParserTestHelpers.Read(text), "lirrctr.tfl");

    private const string V = "N041.00.00.000;E012.00.00.000;\r\nN041.10.00.000;E012.10.00.000;\r\nN041.20.00.000;E012.20.00.000;\r\n";

    // §5.1 / §5.2 / §5.3 — round-trip the real .tfl files.
    [Theory]
    [InlineData("DYNAMIC_SEC/lirrctr.tfl")]
    [InlineData("OTHER/GCI.tfl")]
    [InlineData("DYNAMIC_SEC/twrs.tfl")]
    public void RoundTrip_Real(string rel)
    {
        string? path = RealSectorFiles.Path(rel);
        if (path is null) return;
        var (o, w) = ParserTestHelpers.RoundTrip(Parser, new TflSaver(), path);
        Assert.Equal(o, w);
    }

    // §5.4 — SectorCode ending "FSS" → Fss regardless of FillColor.
    [Fact]
    public void SectorCode_EndsFss_IsFss()
        => Assert.Equal(SectorType.Fss, Parse("LIRR_FSS;CTR;1;CTR;1;\r\n" + V).Records[0].Type);

    // §5.5 — LIRR_NE_CTR + FillColor CTR → Ctr (not FSS).
    [Fact]
    public void Ctr_NotFss()
        => Assert.Equal(SectorType.Ctr, Parse("LIRR_NE_CTR;CTR;1;CTR;1;\r\n" + V).Records[0].Type);

    // §5.6 — hex FillColor accepted and preserved verbatim.
    [Fact]
    public void HexFillColor_Verbatim()
    {
        var r = Parse("LIRE_APP;#0C0C0C;1;#0C0C0C;1;\r\n" + V).Records[0];
        Assert.Equal("#0C0C0C", r.FillColor);
        Assert.Equal("#0C0C0C", r.StrokeColor);
    }

    // §5.7 — palette-name FillColor accepted.
    [Fact]
    public void PaletteFillColor()
    {
        var r = Parse("LIRE_APP;APP;1;APP;1;\r\n" + V).Records[0];
        Assert.Equal("APP", r.FillColor);
        Assert.Equal(SectorType.App, r.Type);
    }

    // §5.8 — colon-separated multi-position SectorCode kept as an opaque string.
    [Fact]
    public void ColonSeparatedCode_Opaque()
        => Assert.Equal("LIZZ_AEW_CTR:LIRO_CRC_CTR", Parse("LIZZ_AEW_CTR:LIRO_CRC_CTR;GCI;1;GCI;0;\r\n" + V).Records[0].SectorCode);

    // §5.9 — multiple sectors separated by a blank line.
    [Fact]
    public void MultipleSectors_Count()
        => Assert.Equal(2, Parse("A_APP;APP;1;APP;1;\r\n" + V + "\r\nB_APP;APP;1;APP;1;\r\n" + V).Records.Count);

    // §5.10 — a header with no vertices → empty Vertices.
    [Fact]
    public void ZeroVertices()
        => Assert.Empty(Parse("A_APP;APP;1;APP;1;\r\n\r\nB_APP;APP;1;APP;1;\r\n" + V).Records[0].Vertices);

    // §5.11 — SectorType inference for every enum value.
    [Theory]
    [InlineData("X_CTR", "CTR", SectorType.Ctr)]
    [InlineData("X", "APP", SectorType.App)]
    [InlineData("X_FSS", "CTR", SectorType.Fss)]
    [InlineData("X", "TMA", SectorType.Tma)]
    [InlineData("X", "UIR", SectorType.Uir)]
    [InlineData("X", "ATZ", SectorType.Atz)]
    [InlineData("X", "GCA", SectorType.Gca)]
    public void SectorTypeInference(string code, string fill, SectorType expected)
        => Assert.Equal(expected, Parse($"{code};{fill};1;{fill};1;\r\n" + V).Records[0].Type);

    // §5.12 — comments between sectors round-trip verbatim.
    [Fact]
    public void CommentsBetweenSectors_RoundTrip()
    {
        string text = "//ROMA\r\nA_APP;APP;1;APP;1;\r\n" + V + "\r\n//section\r\nB_APP;APP;1;APP;1;\r\n" + V;
        var pr = Parser.Parse(ParserTestHelpers.Read(text), "lirrctr.tfl");
        string tmp = Path.Combine(Path.GetTempPath(), "asd_tfl_" + Guid.NewGuid().ToString("N") + ".tfl");
        try
        {
            new FileSaverOrchestrator().Save(pr, new HashSet<TflSector>(), new TflSaver(), tmp);
            Assert.Equal(System.Text.Encoding.UTF8.GetBytes(text), File.ReadAllBytes(tmp));
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }
}
