using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Xunit;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>FicParser / FicSaver — TEST_MATRIX §6.1 … §6.6.</summary>
public sealed class FicParserTests
{
    private readonly CollectingWarnings _warnings = new();
    private FicParser Parser => new(_warnings);
    private ParseResult<FicSector> Parse(string text) => Parser.Parse(ParserTestHelpers.Read(text), "limmfic.tfl");

    private const string V = "N045.30.40.658;E010.31.40.447;\r\nN045.30.57.553;E010.32.01.568;\r\n";

    // §6.1 — round-trip the real DYNAMIC_SEC/limmfic.tfl.
    [Fact]
    public void RoundTrip_Real()
    {
        string? path = RealSectorFiles.Path("DYNAMIC_SEC/limmfic.tfl");
        if (path is null) return;
        var (o, w) = ParserTestHelpers.RoundTrip(Parser, new FicSaver(), path);
        Assert.Equal(o, w);
    }

    // §6.2 — FillColor "CTR" + SectorCode ending "FSS" → IsFssPerimeter = true.
    [Fact]
    public void FssPerimeter_True()
        => Assert.True(Parse("LIMM_FSS;CTR;1;CTR;1;\r\n" + V).Records[0].IsFssPerimeter);

    // §6.3 — FillColor "LIMMFIC" → IsFssPerimeter = false (geographic reference shape).
    [Fact]
    public void GeographicShape_NotFssPerimeter()
        => Assert.False(Parse("LIMM_FSS;LIMMFIC;1;LIMMFIC;0;\r\n" + V).Records[0].IsFssPerimeter);

    // §6.4 — comment preceding the block → ShapeLabel.
    [Fact]
    public void ShapeLabel_FromLeadingComment()
        => Assert.Equal("GARDA", Parse("//GARDA\r\nLIMM_FSS;LIMMFIC;1;LIMMFIC;0;\r\n" + V).Records[0].ShapeLabel);

    // Real-file convention — comment immediately AFTER the header → ShapeLabel.
    [Fact]
    public void ShapeLabel_FromInBlockComment()
        => Assert.Equal("LAGO DI COMO", Parse("LIMM_FSS;LIMMFIC;1;LIMMFIC;0;\r\n//LAGO DI COMO\r\n" + V).Records[0].ShapeLabel);

    // §6.5 — no comment → ShapeLabel null.
    [Fact]
    public void NoComment_NullShapeLabel()
        => Assert.Null(Parse("LIMM_FSS;LIMMFIC;1;LIMMFIC;0;\r\n" + V).Records[0].ShapeLabel);

    // §6.6 — comment separated from the block by a blank line → not a ShapeLabel.
    [Fact]
    public void NonAdjacentComment_NullShapeLabel()
        => Assert.Null(Parse("//GARDA\r\n\r\nLIMM_FSS;LIMMFIC;1;LIMMFIC;0;\r\n" + V).Records[0].ShapeLabel);
}
