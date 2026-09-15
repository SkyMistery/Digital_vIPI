using Vipi.Application.Awos;
using Vipi.Application.Weather;
using Vipi.Ui.Shared;

namespace Vipi.Ui.Tests;

/// <summary>
/// Le celle RVR del quadro vAWOS (decisione del committente, 15 settembre 2026): senza nessun RVR nel bollettino
/// ogni cella dice P2000; con RVR per altre piste, quella che manca resta <c>///</c>.
/// </summary>
public class AwosRvrTests
{
    private static readonly AwosStrip Striscia =
        new(new AwosEnd("16R", 159, null), new AwosEnd("34L", 339, null));

    [Fact]
    public void Bollettino_senza_RVR_da_P2000_su_ogni_testata()
    {
        var m = MetarParser.ParseMetar("LIRF 151250Z 24008KT 9999 FEW030 21/12 Q1016 NOSIG");

        var celle = AwosTesto.Rvr(Striscia, m);

        Assert.Equal(new[] { "P2000", "P2000" }, celle.Select(c => c.Valore));
    }

    [Fact]
    public void RVR_per_altre_piste_lascia_barrata_quella_che_manca()
    {
        var m = MetarParser.ParseMetar("LIRF 151250Z 00000KT 0300 R16R/0350U R25/M0050D FG VV002 09/08 Q0998");

        var celle = AwosTesto.Rvr(Striscia, m);

        Assert.Equal(new[] { "350U", "///" }, celle.Select(c => c.Valore));
    }

    [Fact]
    public void Nessun_bollettino_e_barrato()
    {
        var celle = AwosTesto.Rvr(Striscia, null);

        Assert.Equal(new[] { "///", "///" }, celle.Select(c => c.Valore));
    }
}
