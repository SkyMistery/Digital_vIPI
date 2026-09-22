using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;
using Xunit;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>ApParser / ApSaver — TEST_MATRIX §2.1 … §2.18.</summary>
public sealed class ApParserTests
{
    private readonly CollectingWarnings _warnings = new();
    private ApParser Parser => new(_warnings);
    private static readonly ColorPalette Palette = new();

    private ParseResult<AirportInfo> Parse(string text)
        => Parser.Parse(ParserTestHelpers.Read(text), "itap.ap", Palette);

    // §2.1 — round-trip the real OTHER/itap.ap.
    [Fact]
    public void RoundTrip_Real_Itap()
    {
        string? path = RealSectorFiles.Path("OTHER/itap.ap");
        if (path is null) return;
        var (original, written) = ParserTestHelpers.RoundTrip(Parser, new ApSaver(), path);
        Assert.Equal(original, written);
    }

    // §2.2 — round-trip the real OTHER/lirr.ap.
    [Fact]
    public void RoundTrip_Real_Lirr()
    {
        string? path = RealSectorFiles.Path("OTHER/lirr.ap");
        if (path is null) return;
        var (original, written) = ParserTestHelpers.RoundTrip(Parser, new ApSaver(), path);
        Assert.Equal(original, written);
    }

    // §2.3 — full record (all 8 fields present).
    [Fact]
    public void Parse_FullRecord_AllFields()
    {
        var r = Parse("LIRF;15;6000;N041.48.01.000;E012.14.20.000;Fiumicino;1;1;\r\n").Records[0];

        Assert.Equal("LIRF", r.IcaoCode);
        Assert.Equal(15, r.ElevationFt);
        Assert.Equal(6000, r.TransitionAltFt);
        Assert.Equal(41.800278, r.Centre.LatitudeDeg, 6);
        Assert.Equal(12.238889, r.Centre.LongitudeDeg, 6);
        Assert.Equal("Fiumicino", r.Name);
        Assert.Equal(HideTag.Hidden, r.HideTag);
        Assert.Equal(InstallationType.Helipad, r.InstallationType);
        Assert.False(r.IsDisabled);
    }

    // §2.4 — disabled record (leading //) → IsDisabled = true.
    [Fact]
    public void Parse_DisabledRecord_SetsFlag()
    {
        var pr = Parse("//LIBB;0;0;N040.56.23.000;E016.26.04.000; Brindisi ACC;\r\n");
        var r = Assert.Single(pr.Records);
        Assert.True(r.IsDisabled);
        Assert.Equal("LIBB", r.IcaoCode);
        Assert.Equal(" Brindisi ACC", r.Name);   // verbatim, leading space preserved
    }

    // §2.5 — HideTag absent → null.
    [Fact]
    public void Parse_NoHideTag_Null()
    {
        var r = Parse("LIRF;15;0;N041.48.01.000;E012.14.20.000;Fiumicino;\r\n").Records[0];
        Assert.Null(r.HideTag);
        Assert.Equal(InstallationType.Airport, r.InstallationType);
    }

    // §2.6 / §2.7 — HideTag 1 → Hidden, 2 → Shown.
    [Theory]
    [InlineData("1", HideTag.Hidden)]
    [InlineData("2", HideTag.Shown)]
    public void Parse_HideTag(string field, HideTag expected)
    {
        var r = Parse($"LIRF;15;0;N041.48.01.000;E012.14.20.000;Name;{field};\r\n").Records[0];
        Assert.Equal(expected, r.HideTag);
    }

    // §2.8 — InstallationType = 1 → Helipad (HideTag field empty).
    [Fact]
    public void Parse_InstallationType_Helipad()
    {
        var r = Parse("LIRF;15;0;N041.48.01.000;E012.14.20.000;Name;;1;\r\n").Records[0];
        Assert.Null(r.HideTag);
        Assert.Equal(InstallationType.Helipad, r.InstallationType);
    }

    // §2.9 — free-text InstallationType → Custom, original text preserved.
    [Fact]
    public void Parse_InstallationType_FreeText_Custom()
    {
        var r = Parse("LIRF;15;0;N041.48.01.000;E012.14.20.000;Name;;MIL ZONE;\r\n").Records[0];
        Assert.Equal(InstallationType.Custom, r.InstallationType);
        Assert.Equal("MIL ZONE", r.CustomInstallationTypeText);
    }

    // §2.10 — TransitionAlt = 0 is a legitimate value, no warning.
    [Fact]
    public void Parse_TransitionAltZero_NoWarning()
    {
        var r = Parse("LIRF;15;0;N041.48.01.000;E012.14.20.000;Name;\r\n").Records[0];
        Assert.Equal(0, r.TransitionAltFt);
        Assert.Equal(0, _warnings.Count);
    }

    // §2.11 — interspersed comments → record count correct; adjacent comments are leading.
    [Fact]
    public void Parse_InterspersedComments_RecordCountAndLeading()
    {
        var pr = Parse(
            "//file header\r\n" +
            "//AD\r\n" +
            "LIRF;15;0;N041.48.01.000;E012.14.20.000;Fiumicino;\r\n" +
            "\r\n" +
            "//orphan\r\n" +
            "LICC;39;0;N037.28.00.000;E015.03.50.000;Catania;\r\n");

        Assert.Equal(2, pr.Records.Count);
        var firstChunk = (RecordChunk<AirportInfo>)pr.Chunks.First(c => c is RecordChunk<AirportInfo>);
        Assert.Equal(new[] { "//file header", "//AD" }, firstChunk.LeadingComments);
    }

    // §2.12 — compact-DMS coordinate parses identically to dotted.
    [Fact]
    public void Parse_CompactDmsCoordinate()
    {
        var r = Parse("LIRF;15;0;N0414801000;E0121420000;Name;\r\n").Records[0];
        Assert.Equal(41.800278, r.Centre.LatitudeDeg, 6);
        Assert.Equal(12.238889, r.Centre.LongitudeDeg, 6);
    }

    // §2.13 — comments-only file → no records, no exception.
    [Fact]
    public void Parse_CommentsOnly_NoRecords()
    {
        var pr = Parse("//a\r\n//b\r\n");
        Assert.Empty(pr.Records);
    }

    // §2.14 — empty file → no records.
    [Fact]
    public void Parse_Empty_NoRecords()
    {
        var pr = Parse(string.Empty);
        Assert.Empty(pr.Records);
    }

    // §2.15 — line with fewer than 5 fields → skipped with a warning, kept in a RawChunk.
    [Fact]
    public void Parse_TooFewFields_SkippedWithWarning()
    {
        var pr = Parse("LIRF;15;0;\r\n");
        Assert.Empty(pr.Records);
        Assert.Equal(1, _warnings.Count);
        Assert.Contains(pr.Chunks, c => c is RawChunk<AirportInfo>);
    }

    // §2.16 — saver writes a disabled record starting with //.
    [Fact]
    public void Saver_Disabled_StartsWithSlashes()
    {
        var r = new AirportInfo { IcaoCode = "LIRF", Name = "Fiumicino", IsDisabled = true };
        Assert.StartsWith("//LIRF;", new ApSaver().Serialize(r)[0]);
    }

    // §2.17 — saver always emits dotted DMS coordinates.
    [Fact]
    public void Saver_CoordinatesDottedDms()
    {
        var r = new AirportInfo { IcaoCode = "LIRF", Centre = new Coordinate(41.80027778, 12.23888889), Name = "X" };
        string line = new ApSaver().Serialize(r)[0];
        Assert.Contains("N041.48.01.000", line);
        Assert.Contains("E012.14.20.000", line);
    }

    // §2.18 — saver omits HideTag when null (no empty placeholder for a plain record).
    [Fact]
    public void Saver_NullHideTag_FieldOmitted()
    {
        var r = new AirportInfo { IcaoCode = "LIRF", ElevationFt = 15, TransitionAltFt = 0,
                                  Centre = new Coordinate(41.8, 12.2), Name = "X" };
        Assert.Equal("LIRF;15;0;N041.48.00.000;E012.12.00.000;X;", new ApSaver().Serialize(r)[0]);
    }
}
