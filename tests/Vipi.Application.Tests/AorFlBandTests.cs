using Vipi.Application.Aor;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>Normalizzazione banda FL per l'estrusione 3D: null→GND/UNL, piedi(&gt;660)→FL, garanzia Top&gt;Bottom.</summary>
public class AorFlBandTests
{
    [Fact]
    public void Nulls_Map_To_Ground_And_Unlimited()
    {
        Assert.Equal((0, 660), AorFlBand.Normalize(null, null));
        Assert.Equal((0, 195), AorFlBand.Normalize(null, 195));
        Assert.Equal((245, 660), AorFlBand.Normalize(245, null));
    }

    [Fact]
    public void Values_Above_660_Are_Feet_Divided_By_100()
    {
        Assert.Equal((0, 195), AorFlBand.Normalize(null, 19500));   // 19500 ft (default AirportSector) → FL195
        Assert.Equal((25, 195), AorFlBand.Normalize(2500, 19500));  // 2500 ft → FL25
    }

    [Fact]
    public void Values_At_Or_Below_660_Are_Already_Fl()
    {
        Assert.Equal((245, 355), AorFlBand.Normalize(245, 355));
        Assert.Equal((0, 660), AorFlBand.Normalize(0, 660));
    }

    [Fact]
    public void Degenerate_Band_Gets_Minimum_Thickness()
    {
        Assert.Equal((100, 101), AorFlBand.Normalize(100, 100));    // top==bottom → top+1
        Assert.Equal((300, 301), AorFlBand.Normalize(300, 200));    // top<bottom → bottom+1
    }

    [Fact]
    public void Negative_Values_Clamp_To_Ground()
    {
        Assert.Equal((0, 100), AorFlBand.Normalize(-50, 100));
    }
}
