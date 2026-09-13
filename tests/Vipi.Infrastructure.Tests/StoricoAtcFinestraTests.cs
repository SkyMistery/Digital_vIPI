using Vipi.Infrastructure.Ivao;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// 🔴 T-031 (revisione del 13 settembre 2026): la finestra del ripasso dello storico ATC. Era sempre di
/// <c>RefreshDays</c> giorni fissi, e dopo un fermo più lungo il buco restava per sempre.
/// </summary>
public class StoricoAtcFinestraTests
{
    private static readonly DateTimeOffset Adesso = new(2026, 9, 13, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Primo_giro_recupera_tutto_il_backfill() =>
        Assert.Equal(Adesso.AddDays(-366), AtcHistoryImportHostedService.InizioFinestra(Adesso, null, 366, 2));

    [Fact]
    public void Giro_di_ieri_ripassa_i_giorni_di_sempre() =>
        Assert.Equal(Adesso.AddDays(-2),
            AtcHistoryImportHostedService.InizioFinestra(Adesso, Adesso.AddDays(-1).UtcDateTime, 366, 2));

    [Fact]
    public void Dopo_un_fermo_di_una_settimana_riparte_dall_ultimo_giro_riuscito() =>
        Assert.Equal(Adesso.AddDays(-8),
            AtcHistoryImportHostedService.InizioFinestra(Adesso, Adesso.AddDays(-7).UtcDateTime, 366, 2));

    [Fact]
    public void Un_fermo_oltre_il_backfill_si_ferma_al_tetto() =>
        Assert.Equal(Adesso.AddDays(-366),
            AtcHistoryImportHostedService.InizioFinestra(Adesso, Adesso.AddDays(-500).UtcDateTime, 366, 2));
}
