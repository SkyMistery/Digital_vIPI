using System.Drawing;
using Vipi.Sectorfile.Shared;
using Xunit;

namespace Vipi.Sectorfile.Shared.Tests;

/// <summary>
/// Phase 1 coverage for <see cref="ColorPalette"/> behaviour (Add / Resolve / TryResolve).
/// The .def-driven population tests (§24.x) arrive with DefParser in Phase 4.
/// </summary>
public class ColorPaletteTests
{
    [Fact]
    public void Resolve_KnownName_ReturnsDefinition()
    {
        var palette = new ColorPalette();
        palette.Add(new ColorDefinition("TAXIWAY", Color.FromArgb(80, 80, 80)));

        var def = palette.Resolve("TAXIWAY");
        Assert.Equal("TAXIWAY", def.Name);
        Assert.Equal(Color.FromArgb(80, 80, 80), def.Value);
    }

    [Fact]
    public void Resolve_IsCaseInsensitive()
    {
        var palette = new ColorPalette();
        palette.Add(new ColorDefinition("RUNWAY", Color.Gray));
        Assert.Equal(Color.Gray, palette.Resolve("runway").Value);
    }

    [Fact]
    public void Resolve_UnknownName_FallsBackToMagenta_NoThrow()
    {
        var palette = new ColorPalette();
        var def = palette.Resolve("UNKNOWN");
        Assert.Equal(Color.Magenta.ToArgb(), def.Value.ToArgb());
        Assert.Equal("UNKNOWN", def.Name);
    }

    [Fact]
    public void TryResolve_MissingName_ReturnsFalse()
    {
        var palette = new ColorPalette();
        Assert.False(palette.TryResolve("NOPE", out _));
    }

    [Fact]
    public void Add_LastWriteWins()
    {
        var palette = new ColorPalette();
        palette.Add(new ColorDefinition("APRON", Color.Red));
        palette.Add(new ColorDefinition("APRON", Color.Blue));
        Assert.Equal(Color.Blue, palette.Resolve("APRON").Value);
    }
}
