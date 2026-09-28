using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// 🔴 U-104/U-121 (revisione totale 3; regole scelte dal committente il 28 settembre 2026): la storia del ponte RFO
/// teneva una copia intera del documento — fino a 1 MB — a ogni scrittura, per sempre. Ora per evento restano le
/// ultime <see cref="RfoLimits.StoriaPerEvento"/> versioni, e <see cref="RfoLimits.GiorniDiStoria"/> giorni dopo
/// l'ultima scrittura la storia dell'evento se ne va tutta. Il documento corrente resta.
/// </summary>
public class StoriaDelPonteRfoTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfRfoSharedStateStore _store = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _store = new EfRfoSharedStateStore(_db);
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private async Task ScriviAsync(string evento, int volte)
    {
        var versione = (await _store.LoadAsync(evento))?.Version ?? 0;
        for (var i = 0; i < volte; i++)
        {
            var esito = await _store.WriteAsync(evento, versione, $"{{\"n\":{i}}}", "TEST");
            Assert.Equal(Vipi.Application.Abstractions.RfoWriteOutcome.Scritto, esito.Outcome);
            versione = esito.Current!.Version;
        }
    }

    private Task<int> StoriaAsync(string evento) =>
        _db.RfoSharedStateHistory.AsNoTracking().CountAsync(h => h.EventId == evento);

    [Fact]
    public async Task Per_evento_restano_le_ultime_cento_versioni()
    {
        await ScriviAsync("lirn-20260919", RfoLimits.StoriaPerEvento + 5);
        await ScriviAsync("limc-20261010", 3);

        Assert.Equal(RfoLimits.StoriaPerEvento, await StoriaAsync("lirn-20260919"));
        Assert.Equal(3, await StoriaAsync("limc-20261010"));   // l'altro evento non si tocca

        // Le più VECCHIE se ne vanno: resta dalla sesta in poi.
        var prima = await _db.RfoSharedStateHistory.AsNoTracking()
            .Where(h => h.EventId == "lirn-20260919").MinAsync(h => h.Version);
        Assert.Equal(6, prima);
    }

    [Fact]
    public async Task Trenta_giorni_dopo_l_ultima_scrittura_la_storia_se_ne_va_tutta()
    {
        await ScriviAsync("lirn-20260919", 3);
        await ScriviAsync("limc-20261010", 2);
        // L'evento di LIRN è finito da un pezzo: la sua ultima scrittura si retrodata.
        await _db.Database.ExecuteSqlRawAsync(
            "UPDATE rfo_shared_state_history SET updated_at = {0} WHERE event_id = 'lirn-20260919'",
            DateTime.UtcNow.AddDays(-40));

        var tolte = await _store.PotaStoriaAsync(DateTime.UtcNow.AddDays(-RfoLimits.GiorniDiStoria));

        Assert.Equal(3, tolte);
        Assert.Equal(0, await StoriaAsync("lirn-20260919"));
        Assert.Equal(2, await StoriaAsync("limc-20261010"));
        Assert.NotNull(await _store.LoadAsync("lirn-20260919"));   // il documento resta
    }
}
