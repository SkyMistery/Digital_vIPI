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

    /// <summary>
    /// 🔴 U-093/U-095 (revisione totale 3; regola del committente, 27 settembre 2026): senza gruppi RVR il P2000 vale
    /// solo con visibilità ≥ 1500 m (o CAVOK). Sotto, l'RVR sarebbe dovuto e la sua assenza è un buco — una stazione
    /// senza trasmissometro in nebbia —, e «oltre 2000 m» accanto a «LVP» e 400 m di visibilità era un dato inventato.
    /// </summary>
    [Theory]
    [InlineData("LIBD 270550Z 00000KT 0400 FG VV001 08/08 Q1022", "///")]
    [InlineData("LIRL 270550Z 00000KT 0200 FG VV001 06/06 Q1027", "///")]
    [InlineData("LIRF 270550Z 00000KT 1400 BR BKN004 08/08 Q1022", "///")]
    [InlineData("LIRF 270550Z 00000KT 1500 BR BKN004 08/08 Q1022", "P2000")]
    [InlineData("LIRF 270550Z 24008KT CAVOK 21/12 Q1016", "P2000")]
    public void Senza_RVR_il_P2000_vale_solo_da_1500_metri(string raw, string atteso)
    {
        var celle = AwosTesto.Rvr(Striscia, MetarParser.ParseMetar(raw));

        Assert.All(celle, c => Assert.Equal(atteso, c.Valore));
    }

    /// <summary>🔴 U-089: il cielo oscurato si scrive, invece di sparire dalla riga delle nubi.</summary>
    [Theory]
    [InlineData("LIMC 270620Z 00000KT 0900 FG VV/// 08/08 Q1025", "VV ///")]
    [InlineData("LIMC 270620Z 00000KT 0900 FG OVC/// 08/08 Q1025", "OVC ///")]
    public void Il_cielo_oscurato_si_scrive_nelle_nubi(string raw, string riga)
    {
        Assert.Contains(riga, AwosTesto.Nubi(MetarParser.ParseMetar(raw)));
    }

    [Fact]
    public void Nessun_bollettino_e_barrato()
    {
        var celle = AwosTesto.Rvr(Striscia, null);

        Assert.Equal(new[] { "///", "///" }, celle.Select(c => c.Valore));
    }
}
