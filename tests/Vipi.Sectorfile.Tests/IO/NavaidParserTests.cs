using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;
using Xunit;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>VorParser/NdbParser/FixParser (+savers) — TEST_MATRIX §20, §21, §22.</summary>
public sealed class NavaidParserTests
{
    private readonly CollectingWarnings _warnings = new();
    private static readonly ColorPalette Palette = new();

    private ParseResult<Vor> ParseVor(string text) => new VorParser(_warnings).Parse(ParserTestHelpers.Read(text), "itvor.vor", Palette);
    private ParseResult<Ndb> ParseNdb(string text) => new NdbParser(_warnings).Parse(ParserTestHelpers.Read(text), "itndb.ndb", Palette);
    private ParseResult<Fix> ParseFix(string text) => new FixParser(_warnings).Parse(ParserTestHelpers.Read(text), "ENR.fix", Palette);

    // §20.1 — round-trip the real .vor (TEST_MATRIX names it itavors.vor; the repo file is itvor.vor).
    [Fact]
    public void Vor_RoundTrip_Real()
    {
        string? path = RealSectorFiles.Path("NAVAIDS/itvor.vor");
        if (path is null) return;
        var (o, w) = ParserTestHelpers.RoundTrip(new VorParser(_warnings), new VorSaver(), path);
        Assert.Equal(o, w);
    }

    // §20.2 — Field5/Field6 present → preserved verbatim.
    [Fact]
    public void Vor_WithExtraFields()
    {
        var r = ParseVor("AEA;111.65;N040.38.17.400;E008.17.30.400;0;2;\r\n").Records[0];
        Assert.Equal("0", r.ExtraField5);
        Assert.Equal("2", r.ExtraField6);
    }

    // §20.3 — no Field5/Field6 → null.
    [Fact]
    public void Vor_NoExtraFields_Null()
    {
        var r = ParseVor("ALB;116.95;N044.02.53.400;E008.07.39.400;\r\n").Records[0];
        Assert.Null(r.ExtraField5);
        Assert.Null(r.ExtraField6);
    }

    // §20.4 — frequency "111.65" → decimal.
    [Fact]
    public void Vor_Frequency_Decimal()
        => Assert.Equal(111.65m, ParseVor("AEA;111.65;N040.38.17.400;E008.17.30.400;0;2;\r\n").Records[0].Frequency);

    // §21.1 — round-trip the real .ndb (TEST_MATRIX names it itandb.ndb; the repo file is itndb.ndb).
    [Fact]
    public void Ndb_RoundTrip_Real()
    {
        string? path = RealSectorFiles.Path("NAVAIDS/itndb.ndb");
        if (path is null) return;
        var (o, w) = ParserTestHelpers.RoundTrip(new NdbParser(_warnings), new NdbSaver(), path);
        Assert.Equal(o, w);
    }

    // §21.2 — standard NDB record.
    [Fact]
    public void Ndb_StandardRecord()
    {
        var r = ParseNdb("AVI;390.0;N045.55.27.600;E012.25.42.600;\r\n").Records[0];
        Assert.Equal("AVI", r.Ident);
        Assert.Equal(390.0m, r.Frequency);
        Assert.Equal(45.924333, r.Position.LatitudeDeg, 5);
    }

    // §22.1 — round-trip the real ENR.fix.
    [Fact]
    public void Fix_RoundTrip_Real()
    {
        string? path = RealSectorFiles.Path("NAVAIDS/ENR.fix");
        if (path is null) return;
        var (o, w) = ParserTestHelpers.RoundTrip(new FixParser(_warnings), new FixSaver(), path);
        Assert.Equal(o, w);
    }

    // §22.2 — Field5 present → preserved verbatim.
    [Fact]
    public void Fix_Field5_Verbatim()
        => Assert.Equal("1", ParseFix("ABKON;N039.52.20.000;E010.48.24.000;0;1;\r\n").Records[0].ExtraField);

    // §22.3 — Field5 absent (4 fields) → warning, no record.
    [Fact]
    public void Fix_NoField5_Warning()
    {
        var pr = ParseFix("ABKON;N039.52.20.000;E010.48.24.000;0;\r\n");
        Assert.Empty(pr.Records);
        Assert.Equal(1, _warnings.Count);
    }

    // §22.4 — DisplayType = 2 preserved.
    [Fact]
    public void Fix_DisplayType()
        => Assert.Equal(2, ParseFix("ABADI;N040.45.19.000;E018.38.30.000;2;0;\r\n").Records[0].DisplayType);

    // Integration — fixes parsed into a NavaidSet resolve as fix references (ties §1.8 to real data).
    [Fact]
    public void Fix_PopulatesNavaidSet_ResolvesIdent()
    {
        var pr = ParseFix("ABDAB;N037.53.21.000;E010.37.43.000;0;1;\r\n");
        var navaids = new NavaidSet();
        foreach (var fix in pr.Records)
        {
            navaids.AddFix(fix);
        }

        var resolved = CoordinateConverter.Parse("ABDAB", navaids);
        Assert.Equal(37.889167, resolved.LatitudeDeg, 5);
        Assert.Equal(10.628611, resolved.LongitudeDeg, 5);
    }
}
