using Vipi.Application.Weather;
using Vipi.Ui.Shared;

namespace Vipi.Ui.Tests;

/// <summary>
/// Le caselle CROSS e TAIL del quadro vAWOS (U-225): con vento calmo o variabile non c'è una componente da
/// scrivere, e il primo disegno deve dire «--» come fa il JavaScript agli aggiornamenti, non «00».
/// </summary>
public class AwosVentoTests
{
    [Theory]
    [InlineData("LIRF 151250Z VRB15G25KT 9999 FEW030 21/12 Q1016")]
    [InlineData("LIRF 151250Z 00000KT 9999 FEW030 21/12 Q1016")]
    public void Vento_variabile_o_calmo_non_da_componenti(string metar)
    {
        var w = MetarParser.ParseMetar(metar).Wind;

        Assert.Null(AwosTesto.Componenti(w, 160));
        Assert.Equal("--", AwosTesto.Kt(AwosTesto.Componenti(w, 160)?.Cross));
    }

    [Fact]
    public void Vento_sull_asse_da_zero_e_zero()
    {
        var w = MetarParser.ParseMetar("LIRF 151250Z 16010KT 9999 FEW030 21/12 Q1016").Wind;

        var c = AwosTesto.Componenti(w, 160);

        Assert.Equal((0, 0), c);
        Assert.Equal("00", AwosTesto.Kt(c?.Tail));
    }

    [Fact]
    public void Vento_in_coda_si_misura()
    {
        var w = MetarParser.ParseMetar("LIRF 151250Z 34010KT 9999 FEW030 21/12 Q1016").Wind;

        Assert.Equal((0, 10), AwosTesto.Componenti(w, 160));
    }
}
