namespace Aip.Aurora.Tests;

public sealed class AuroraTextTests
{
    [Theory]
    [InlineData(45.0, "N045.00.00.000")]
    [InlineData(45.503427777777778, "N045.30.12.340")]
    [InlineData(-33.5, "S033.30.00.000")]
    // 59,9996 secondi arrotondati: il riporto sale fino ai gradi.
    [InlineData(41.99999989, "N042.00.00.000")]
    // Un meno zero non è il sud.
    [InlineData(-0.0000000001, "N000.00.00.000")]
    public void Latitudine(double gradi, string attesa) => Assert.Equal(attesa, AuroraText.Latitude(gradi));

    [Theory]
    [InlineData(9.255711111111111, "E009.15.20.560")]
    [InlineData(-3.25, "W003.15.00.000")]
    [InlineData(179.5, "E179.30.00.000")]
    public void Longitudine(double gradi, string attesa) => Assert.Equal(attesa, AuroraText.Longitude(gradi));

    public static TheoryData<VerticalLimit, string> Limiti => new()
    {
        { new VerticalLimit { Text = "GND", Reference = VerticalReference.Ground }, "GND" },
        { new VerticalLimit { Text = "SFC", Reference = VerticalReference.Ground }, "SFC" },
        { new VerticalLimit { Text = "UNL", Reference = VerticalReference.Unlimited }, "UNL" },
        { new VerticalLimit { Text = "FL 195", Reference = VerticalReference.FlightLevel, Value = 195, Unit = VerticalUnit.Fl }, "FL195" },
        { new VerticalLimit { Text = "2500 FT AMSL", Reference = VerticalReference.Msl, Value = 2500, Unit = VerticalUnit.Ft }, "\"2500ft AMSL\"" },
        { new VerticalLimit { Text = "1000 FT AGL", Reference = VerticalReference.Agl, Value = 1000, Unit = VerticalUnit.Ft }, "\"1000ft AGL\"" },
        { new VerticalLimit { Text = "300 M AGL", Reference = VerticalReference.Agl, Value = 300, Unit = VerticalUnit.M }, "\"300m AGL\"" },
        // Una forma che l'importer non ha capito: il testo stampato, fra virgolette.
        { new VerticalLimit { Text = "1500 FT ASFC" }, "\"1500 FT ASFC\"" },
        { new VerticalLimit { Text = "TBN" }, "TBN" },
        // Un riferimento senza numero non si inventa: il testo.
        { new VerticalLimit { Text = "FL ?", Reference = VerticalReference.FlightLevel }, "\"FL ?\"" },
    };

    [Theory]
    [MemberData(nameof(Limiti))]
    public void LeQuoteComeLaCarta(VerticalLimit limite, string atteso) => Assert.Equal(atteso, AuroraText.Limit(limite));

    [Theory]
    [InlineData("D", "D")]
    [InlineData("COO APP", "\"COO APP\"")]
    [InlineData("dice \"no\"", "\"dice 'no'\"")]
    [InlineData("", "\"\"")]
    public void IValori(string testo, string atteso) => Assert.Equal(atteso, AuroraText.Value(testo));

    [Fact]
    public void LaSferaVaETorna()
    {
        var da = new Position(41.8, 12.25);
        var a = Sphere.Destination(da, 47.5, 12.3);

        Assert.Equal(12.3, Sphere.DistanceNm(da, a), 9);
        Assert.Equal(47.5, Sphere.BearingDeg(da, a), 9);
        // Un grado di meridiano: 60 NM a meno della sfera (60,04).
        Assert.Equal(60.04, Sphere.DistanceNm(new Position(41, 12), new Position(42, 12)), 2);
    }
}
