using Vipi.Application.Awos;
using Vipi.Ui.Shared;

namespace Vipi.Ui.Tests;

/// <summary>
/// Da dove viene la pista in uso è meccanica di servizio: allo staff sì, al pubblico no — come nella vIPI
/// (decisione del committente, 15 settembre 2026).
/// </summary>
public class AwosProvenienzaTests
{
    private static readonly AwosActive DaRegola =
        new(new[] { "34R" }, new[] { "34L" }, AwosRunwaySource.Regola, "34 asciutta");

    [Fact]
    public void Al_pubblico_la_riga_dice_solo_le_piste()
    {
        Assert.Equal("RWY IN USE: 34R DEP · 34L ARR", AwosTesto.RigaAttiva(DaRegola, meccanicaVisibile: false));
        Assert.Equal("RWY IN USE: 34R DEP · 34L ARR · from rule 34 asciutta",
                     AwosTesto.RigaAttiva(DaRegola, meccanicaVisibile: true));
    }

    [Fact]
    public void Al_pubblico_il_nome_della_regola_non_resta_nella_vista_serializzata()
    {
        var vista = new AwosView("LIRF", "Fiumicino", "LIRR", true, false, null, null, null, null, null, null,
            Array.Empty<AwosStrip>(), DaRegola, null,
            new AwosLvp(Vipi.Application.Content.LvpValutatore.DaMetar(null, null, false), null),
            DateTimeOffset.UtcNow);

        Assert.Null(AwosTesto.PerChiGuarda(vista, meccanicaVisibile: false).Attiva.Dettaglio);
        Assert.Equal("34 asciutta", AwosTesto.PerChiGuarda(vista, meccanicaVisibile: true).Attiva.Dettaglio);
    }
}
