using System.Drawing;
using Vipi.Sectorfile.IO;
using Xunit;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>DefParser — DEVELOPMENT_PLAN / TEST_MATRIX §24.1 … §24.4.</summary>
public sealed class DefParserTests
{
    private readonly CollectingWarnings _warnings = new();
    private DefParser Parser => new(_warnings);

    // §24.1 — a colour line (with whitespace around ';') → ColorDefinition with name + value.
    [Fact]
    public void Parse_ColourLine_BuildsDefinition()
    {
        var palette = Parser.Parse(ParserTestHelpers.Read("TAXIWAY ; #FFAA00 ;\r\n"), "test.def");

        Assert.True(palette.TryResolve("TAXIWAY", out var def));
        Assert.Equal("TAXIWAY", def.Name);
        Assert.Equal(Color.FromArgb(0xFF, 0xAA, 0x00).ToArgb(), def.Value.ToArgb());
    }

    // §24.2 — Resolve a known name → its definition.
    [Fact]
    public void Resolve_KnownName_ReturnsDefinition()
    {
        var palette = Parser.Parse(ParserTestHelpers.Read("GRASS;#406230;\r\n"), "test.def");
        Assert.Equal(Color.FromArgb(0x40, 0x62, 0x30).ToArgb(), palette.Resolve("GRASS").Value.ToArgb());
    }

    // §24.3 — Resolve an unknown name → magenta fallback, no exception.
    [Fact]
    public void Resolve_UnknownName_FallsBackToMagenta()
    {
        var palette = Parser.Parse(ParserTestHelpers.Read("GRASS;#406230;\r\n"), "test.def");
        var def = palette.Resolve("UNKNOWN");
        Assert.Equal("UNKNOWN", def.Name);
        Assert.Equal(Color.Magenta.ToArgb(), def.Value.ToArgb());
    }

    [Fact]
    public void Parse_SkipsCommentsAndBlanks()
    {
        var palette = Parser.Parse(ParserTestHelpers.Read("//header\r\n\r\nGRASS;#406230;\r\n"), "test.def");
        Assert.Single(palette.Entries);
        Assert.Equal(0, _warnings.Count);
    }

    [Fact]
    public void Parse_MalformedColourValue_WarnsAndSkips()
    {
        var palette = Parser.Parse(ParserTestHelpers.Read("GRASS;notacolour;\r\n"), "test.def");
        Assert.Empty(palette.Entries);
        Assert.Equal(1, _warnings.Count);
    }

    // Lotto «Subito» slice 4: ogni forma del manuale IVAO vale anche in [DEFINE] (gli esempi sono del manuale).
    [Theory]
    [InlineData("GRASS;%48:98:0;", 0xFF, 48, 98, 0)]
    [InlineData("ASPHALT;#FF4455f0;", 0xFF, 0x44, 0x55, 0xF0)]
    [InlineData("VETRO;#80406230;", 0x80, 0x40, 0x62, 0x30)]
    [InlineData("LABEL;24,183,108;", 0xFF, 24, 183, 108)]
    public void Parse_EveryManualColourForm(string line, int a, int r, int g, int b)
    {
        var palette = Parser.Parse(ParserTestHelpers.Read(line + "\r\n"), "test.def");

        Assert.Equal(0, _warnings.Count);
        Assert.Equal(Color.FromArgb(a, r, g, b).ToArgb(), palette.Resolve(line.Split(';')[0]).Value.ToArgb());
    }

    // §24.4 — parse the real colours.def: every colour line is covered (no warnings).
    [Fact]
    public void Parse_RealDefFile_AllLinesCovered()
    {
        string? path = RealSectorFiles.Path("COLORS/colors.def");
        if (path is null)
        {
            return;   // real SectorFiles tree not present (CI)
        }

        var palette = Parser.Parse(path);
        Assert.True(palette.Entries.Count >= 10);
        Assert.True(palette.TryResolve("GRASS", out _));
        Assert.True(palette.TryResolve("RUNWAY", out _));
        Assert.Equal(0, _warnings.Count);
    }
}
