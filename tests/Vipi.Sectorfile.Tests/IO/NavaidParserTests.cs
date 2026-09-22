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

    // F2 slice 9 — the TACAN of Grosseto (itvor.vor:81): a channel and no frequency. A called it malformed; vIPI's
    // import reads it, and the concordance with vIPI found the gap.
    [Fact]
    public void Vor_Tacan_SenzaFrequenza()
    {
        const string riga = "GRO;;N042.45.37.200;E011.04.38.600;0;3;35Y";
        var letto = ParseVor(riga + "\r\n");

        var r = Assert.Single(letto.Records);
        Assert.Null(r.Frequency);
        Assert.Equal(42.760333, r.Position.LatitudeDeg, 5);
        Assert.Equal(0, _warnings.Count);

        // The frequency is written back empty. (The channel `35Y` is the 7th field, outside the model: the line as
        // a whole comes back through «riga come campi», FusioneDelRecordTests.)
        Assert.StartsWith("GRO;;N042.45.37.200;E011.04.38.600;0;3;", Assert.Single(new VorSaver().Serialize(r)));
    }

    // …but a frequency that is there and does not read is still malformed.
    [Fact]
    public void Vor_FrequenzaIllegibile_ResMalformata()
    {
        var letto = ParseVor("GRO;1O9.85;N042.45.39.200;E011.04.38.300;\r\n");

        Assert.Empty(letto.Records);
        Assert.Equal(1, _warnings.Count);
    }

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

    // §22.3 — ⚠️ OVERTURNED by F2 slice 5. In A a fix without Field5 was malformed: 2 032 real fixes have 4 fields
    // (`BC404;…;3;`, hidden) and 9 have 3 (`POE1;N045.28.15.000;E010.28.20.000;`). They are fixes.
    [Theory]
    [InlineData("BC404;N039.05.11.290;E017.03.27.750;3;", 3, null)]
    [InlineData("POE1;N045.28.15.000;E010.28.20.000;", null, null)]
    public void Fix_OptionalFields(string line, int? displayType, string? field5)
    {
        var fix = Assert.Single(ParseFix(line + "\r\n").Records);
        Assert.Equal(displayType, fix.DisplayType);
        Assert.Equal(field5, fix.ExtraField);
        Assert.Empty(_warnings.Snapshot());
        Assert.Equal(line, new FixSaver().Serialize(fix)[0]);
    }

    // A coordinate that does not read stays malformed: MIL.fix:96 has 72 seconds, APT.fix:294 a dash.
    [Theory]
    [InlineData("PL-BRAVO;N044.54.40.500;E010.34.072.00;3;")]
    [InlineData("MG763;N044.03.11.145;E008-11.31.443;3;")]
    public void Fix_BadCoordinate_Warning(string line)
    {
        Assert.Empty(ParseFix(line + "\r\n").Records);
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
