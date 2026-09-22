using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Xunit;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>ArtccParser / ArtccSaver — TEST_MATRIX §19 (ACC/*.artcc label files).</summary>
public sealed class ArtccParserTests
{
    private readonly CollectingWarnings _warnings = new();
    private ParseResult<LabelPoint> Parse(string text)
        => new ArtccParser(_warnings).Parse(ParserTestHelpers.Read(text), "FRA.artcc", new Shared.ColorPalette());

    // §19.1 — round-trip the real ACC files (lirr.artcc is absent; FRA*.artcc stand in).
    [Theory]
    [InlineData("ACC/FRA.artcc")]
    [InlineData("ACC/FRA-gates.artcc")]
    public void RoundTrip_Real(string rel)
    {
        string? path = RealSectorFiles.Path(rel);
        if (path is null) return;
        var (o, w) = ParserTestHelpers.RoundTrip(new ArtccParser(_warnings), new ArtccSaver(), path);
        Assert.Equal(o, w);
    }

    // §19.2 — a standard L; line → LabelPoint with Mode = FixName.
    [Fact]
    public void StandardLabel_FixNameMode()
    {
        var r = Parse("L;ABDAB;N037.53.21.000;E010.37.43.000;8;\r\n").Records[0];
        Assert.Equal(LabelMode.FixName, r.Mode);
        Assert.Equal("ABDAB", r.FixRef);
    }

    // §19.3 — FontSize preserved.
    [Fact]
    public void FontSize_Preserved()
        => Assert.Equal(8, Parse("L;ABDAB;N037.53.21.000;E010.37.43.000;8;\r\n").Records[0].FontSize);

    // §19.4 — a T; line is not a label record → warning + RawChunk, no record.
    [Fact]
    public void TLine_WarningAndRawChunk()
    {
        var pr = Parse("T;FRA BDRY;N043.10.00.000;E009.45.00.000;\r\n");
        Assert.Empty(pr.Records);
        Assert.Contains(pr.Chunks, c => c is RawChunk<LabelPoint>);
        Assert.NotEmpty(_warnings.Snapshot());
    }

    // §19.5 — LabelMode.Custom → Serialize emits CustomName as field 2.
    [Fact]
    public void Custom_SerializesCustomName()
    {
        var label = new LabelPoint
        {
            Mode = LabelMode.Custom,
            CustomName = "MYLABEL",
            Position = new Shared.Coordinate(41, 12),
            FontSize = 8,
        };
        Assert.StartsWith("L;MYLABEL;", new ArtccSaver().Serialize(label)[0]);
    }

    // §19.6 — LabelMode.None → Serialize emits an empty field 2.
    [Fact]
    public void None_SerializesEmptyField()
    {
        var label = new LabelPoint
        {
            Mode = LabelMode.None,
            Position = new Shared.Coordinate(41, 12),
            FontSize = 8,
        };
        Assert.StartsWith("L;;", new ArtccSaver().Serialize(label)[0]);
    }

    // §19.7 — re-parsing our own output with an empty field 2 → LabelMode.None.
    [Fact]
    public void ReparseEmptyField_IsNone()
    {
        var label = new LabelPoint
        {
            Mode = LabelMode.None,
            Position = new Shared.Coordinate(41, 12),
            FontSize = 8,
        };
        string line = new ArtccSaver().Serialize(label)[0];
        var reparsed = Parse(line + "\r\n").Records[0];
        Assert.Equal(LabelMode.None, reparsed.Mode);
        Assert.Null(reparsed.FixRef);
    }
}
