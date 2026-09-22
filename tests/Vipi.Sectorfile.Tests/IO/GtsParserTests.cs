using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;
using Xunit;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>GtsParser / GtsSaver — TEST_MATRIX §10.1 … §10.5.</summary>
public sealed class GtsParserTests
{
    private readonly CollectingWarnings _warnings = new();
    private GtsParser Parser => new(_warnings);
    private ParseResult<Stand> Parse(string text) => Parser.Parse(ParserTestHelpers.Read(text), "lirf.gts", new ColorPalette());

    // §10.1 — round-trip the real lirf.gts (includes disabled records).
    [Fact]
    public void RoundTrip_Real()
    {
        string? path = RealSectorFiles.Path("lirf.gts");
        if (path is null) return;
        var (original, written) = ParserTestHelpers.RoundTrip(Parser, new GtsSaver(), path);
        Assert.Equal(original, written);
    }

    // §10.2 — standard record → Stand fields correct.
    [Fact]
    public void Parse_StandardRecord()
    {
        var r = Parse("101;LIRF;N041.48.16.944;E012.16.18.365;\r\n").Records[0];
        Assert.Equal("101", r.Number);
        Assert.Equal("LIRF", r.IcaoCode);
        Assert.False(r.IsDisabled);
        Assert.Equal(41.804707, r.Position.LatitudeDeg, 5);
    }

    // §10.3 — disabled record (leading //) → IsDisabled = true.
    [Fact]
    public void Parse_Disabled()
    {
        var r = Assert.Single(Parse("//19;LICZ;N037.24.21.887;E014.54.58.014;\r\n").Records);
        Assert.True(r.IsDisabled);
        Assert.Equal("19", r.Number);
    }

    // §10.4 — saver writes a disabled stand starting with //.
    [Fact]
    public void Saver_Disabled_StartsWithSlashes()
    {
        var s = new Stand { Number = "19", IcaoCode = "LICZ", IsDisabled = true };
        Assert.StartsWith("//19;LICZ;", new GtsSaver().Serialize(s)[0]);
    }

    // §10.5 — mix of enabled and disabled round-trips byte-for-byte.
    [Fact]
    public void Parse_Mix_RoundTrip()
    {
        const string text = "101;LIRF;N041.48.16.944;E012.16.18.365;\r\n//19;LICZ;N037.24.21.887;E014.54.58.014;\r\n";
        var pr = Parse(text);
        string tmp = Path.Combine(Path.GetTempPath(), "asd_gts_" + Guid.NewGuid().ToString("N") + ".gts");
        try
        {
            new FileSaverOrchestrator().Save(pr, new HashSet<Stand>(), new GtsSaver(), tmp);
            Assert.Equal(System.Text.Encoding.UTF8.GetBytes(text), File.ReadAllBytes(tmp));
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }
}
