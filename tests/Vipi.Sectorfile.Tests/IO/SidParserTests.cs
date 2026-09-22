using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;
using Xunit;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>SidParser / SidSaver — TEST_MATRIX §11.1 … §11.7.</summary>
public sealed class SidParserTests
{
    private readonly CollectingWarnings _warnings = new();
    private SidParser Parser => new(_warnings);
    private ParseResult<SidProcedure> Parse(string text) => Parser.Parse(ParserTestHelpers.Read(text), "lirf.sid", new ColorPalette());

    // §11.1 — round-trip the real lirf.sid (blank lines between runway groups).
    [Fact]
    public void RoundTrip_Real()
    {
        string? path = RealSectorFiles.Path("lirf.sid");
        if (path is null) return;
        var (original, written) = ParserTestHelpers.RoundTrip(Parser, new SidSaver(), path);
        Assert.Equal(original, written);
    }

    // §11.2 — record without optional fields (ICAO;Runway;Name;Field4;Field5;).
    [Fact]
    public void Parse_NoOptionalFields()
    {
        var r = Parse("LIRF;07;OST1E; ; ;\r\n").Records[0];
        Assert.Equal("LIRF", r.IcaoCode);
        Assert.Equal("07", r.Runway);
        Assert.Equal("OST1E", r.Name);
        Assert.Null(r.DefaultVisible);
        Assert.Null(r.RelatedFix);
    }

    // §11.3 — DefaultVisible = 0 and RelatedFix present.
    [Fact]
    public void Parse_DefaultVisibleAndRelatedFix()
    {
        var r = Parse("LIRF;07;OST1E; ; ;0;ELKAP;\r\n").Records[0];
        Assert.Equal(0, r.DefaultVisible);
        Assert.Equal("ELKAP", r.RelatedFix);
    }

    // §11.4 — Field4/Field5 are a literal space, preserved verbatim.
    [Fact]
    public void Parse_Field4Field5_VerbatimSpace()
    {
        var r = Parse("LIRF;07;OST1E; ; ;\r\n").Records[0];
        Assert.Equal(" ", r.Field4);
        Assert.Equal(" ", r.Field5);
    }

    // §11.5 — DefaultVisible = 1.
    [Fact]
    public void Parse_DefaultVisible1()
        => Assert.Equal(1, Parse("LIRF;07;OST1E; ; ;1;\r\n").Records[0].DefaultVisible);

    // §11.6 — RelatedFix absent → null.
    [Fact]
    public void Parse_NoRelatedFix_Null()
        => Assert.Null(Parse("LIRF;07;OST1E; ; ;1;\r\n").Records[0].RelatedFix);

    // §11.7 — saver omits optional fields when absent.
    [Fact]
    public void Saver_NoOptional_NotSerialized()
    {
        var r = new SidProcedure { IcaoCode = "LIRF", Runway = "07", Name = "OST1E", Field4 = " ", Field5 = " " };
        Assert.Equal("LIRF;07;OST1E; ; ;", new SidSaver().Serialize(r)[0]);
    }
}
