using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;
using Xunit;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>VfiParser / VfiSaver — TEST_MATRIX §9.1 … §9.7.</summary>
public sealed class VfiParserTests
{
    private readonly CollectingWarnings _warnings = new();
    private VfiParser Parser => new(_warnings);
    private ParseResult<VfrPoint> Parse(string text) => Parser.Parse(ParserTestHelpers.Read(text), "lirf.vfi", new ColorPalette());

    // §9.1 — round-trip the real lirf.vfi (mixed compact/dotted DMS).
    [Fact]
    public void RoundTrip_Real()
    {
        string? path = RealSectorFiles.Path("lirf.vfi");
        if (path is null) return;
        var (original, written) = ParserTestHelpers.RoundTrip(Parser, new VfiSaver(), path);
        Assert.Equal(original, written);
    }

    // §9.2 — record with Type (5 fields) → Type populated; name with spaces preserved.
    [Fact]
    public void Parse_WithType()
    {
        var r = Parse("PONTE GALERIA;RFE1;N041.49.15.000;E012.21.23.000;1;\r\n").Records[0];
        Assert.Equal("PONTE GALERIA", r.Name);
        Assert.Equal("RFE1", r.Code);
        Assert.Equal(1, r.Type);
    }

    // §9.3 — record without Type (4 fields) → null.
    [Fact]
    public void Parse_WithoutType_Null()
        => Assert.Null(Parse("OSTIA;RFS2;N041.43.40.000;E012.16.30.000;\r\n").Records[0].Type);

    // §9.4 — compact-DMS coordinate parses.
    [Fact]
    public void Parse_CompactDms()
    {
        var r = Parse("PONTE GALERIA;RFE1;N0414915000;E0122123000;\r\n").Records[0];
        Assert.Equal(41.820833, r.Position.LatitudeDeg, 5);
    }

    // §9.5 — Type = 3 (VFR AREA) preserved.
    [Fact]
    public void Parse_Type3()
        => Assert.Equal(3, Parse("X;RFX;N041.00.00.000;E012.00.00.000;3;\r\n").Records[0].Type);

    // §9.6 — saver omits Type when null.
    [Fact]
    public void Saver_NullType_Omitted()
    {
        var r = new VfrPoint { Name = "X", Code = "C", Position = new Coordinate(41.8, 12.2), Type = null };
        Assert.Equal("X;C;N041.48.00.000;E012.12.00.000;", new VfiSaver().Serialize(r)[0]);
    }

    // §9.7 — saver emits dotted DMS and Type when present.
    [Fact]
    public void Saver_WithType_DottedDms()
    {
        var r = new VfrPoint { Name = "X", Code = "C", Position = new Coordinate(41.8, 12.2), Type = 2 };
        Assert.Equal("X;C;N041.48.00.000;E012.12.00.000;2;", new VfiSaver().Serialize(r)[0]);
    }
}
