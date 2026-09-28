using System.Drawing;
using Vipi.Sectorfile.Shared;
using Xunit;

namespace Vipi.Sectorfile.Shared.Tests;

/// <summary>
/// I valori di colore del sector, nelle quattro forme del manuale IVAO («Colour Definitions»), lotto «Subito» slice 4.
/// </summary>
public sealed class ColoreDelSectorTests
{
    [Theory]
    // Gli esempi del manuale, uno per forma.
    [InlineData("#18b76c", 0xFF, 0x18, 0xB7, 0x6C, FormaDelColore.Esadecimale)]
    [InlineData("#08466717", 0x08, 0x46, 0x67, 0x17, FormaDelColore.EsadecimaleConOpacita)]
    [InlineData("24,183,108", 0xFF, 24, 183, 108, FormaDelColore.Rgb)]
    [InlineData("%48:98:0", 0xFF, 48, 98, 0, FormaDelColore.Percento)]
    // Come stanno nel fork: colors.def e le teste dei .tfl.
    [InlineData("#406230", 0xFF, 0x40, 0x62, 0x30, FormaDelColore.Esadecimale)]
    [InlineData(" #2f2f2f ", 0xFF, 0x2F, 0x2F, 0x2F, FormaDelColore.Esadecimale)]
    [InlineData("#FF4455f0", 0xFF, 0x44, 0x55, 0xF0, FormaDelColore.EsadecimaleConOpacita)]
    public void LeggeLeQuattroFormeDelManuale(string testo, int a, int r, int g, int b, FormaDelColore forma)
    {
        Assert.True(ColoreDelSector.TryLeggi(testo, out Color colore, out FormaDelColore letta));
        Assert.Equal(Color.FromArgb(a, r, g, b).ToArgb(), colore.ToArgb());
        Assert.Equal(forma, letta);
    }

    [Theory]
    [InlineData("TAXIWAY")]        // un nome: lo risolve colors.def, non il valore
    [InlineData("")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("#GG0000")]
    [InlineData("256,0,0")]
    [InlineData("1,2")]
    [InlineData("%1:2")]
    [InlineData("-1,0,0")]
    public void UnNomeOUnTestoStortoNonSonoColori(string testo)
        => Assert.False(ColoreDelSector.TryLeggi(testo, out _));

    [Fact]
    public void ScriveMaiuscoloEConLOpacitaSoloSeServe()
    {
        Assert.Equal("#406230", ColoreDelSector.Scrivi(Color.FromArgb(0x40, 0x62, 0x30)));
        Assert.Equal("#80406230", ColoreDelSector.Scrivi(Color.FromArgb(0x80, 0x40, 0x62, 0x30)));
        Assert.Equal("#2F2F2F", ColoreDelSector.Scrivi(Color.FromArgb(0x2F, 0x2F, 0x2F)));
    }
}
