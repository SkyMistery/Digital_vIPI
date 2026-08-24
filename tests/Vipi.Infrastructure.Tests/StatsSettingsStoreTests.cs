using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Infrastructure.Persistence;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// L'interruttore della classifica pubblica: nasce spento, e accenderlo è un atto che si registra.
/// </summary>
public class StatsSettingsStoreTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfStatsSettingsStore _store = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _store = new EfStatsSettingsStore(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    [Fact]
    public async Task Senza_nessuna_decisione_la_classifica_e_spenta()
    {
        var s = await _store.GetAsync();

        Assert.False(s.PublicLeaderboard);
        Assert.Null(s.UpdatedUtc);
        Assert.Equal(0, s.UpdatedByUserId);      // 0 = nessuno l'ha mai decisa
    }

    [Fact]
    public async Task Accenderla_si_registra_con_chi_e_quando()
    {
        await _store.SaveAsync(publicLeaderboard: true, updatedByUserId: 704798);
        _db.ChangeTracker.Clear();

        var s = await _store.GetAsync();
        Assert.True(s.PublicLeaderboard);
        Assert.Equal(704798, s.UpdatedByUserId);
        Assert.NotNull(s.UpdatedUtc);

        var audit = await _db.AuditLogs.SingleAsync(a => a.EntityType == "StatsSettings");
        Assert.Equal(704798, audit.UserId);
        Assert.Contains("true", audit.DetailsJson ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rispegnerla_si_registra_a_sua_volta()
    {
        await _store.SaveAsync(true, 704798);
        await _store.SaveAsync(false, 762032);
        _db.ChangeTracker.Clear();

        Assert.False((await _store.GetAsync()).PublicLeaderboard);
        Assert.Equal(2, await _db.AuditLogs.CountAsync(a => a.EntityType == "StatsSettings"));
    }

    [Fact]
    public async Task Salvare_lo_stesso_valore_non_e_un_atto_e_non_si_scrive()
    {
        // Altrimenti «deciso da X oggi» finirebbe sopra la decisione di qualcun altro di mesi fa.
        await _store.SaveAsync(true, 704798);
        _db.ChangeTracker.Clear();
        var prima = (await _store.GetAsync()).UpdatedUtc;

        await _store.SaveAsync(true, 762032);
        _db.ChangeTracker.Clear();

        var dopo = await _store.GetAsync();
        Assert.Equal(prima, dopo.UpdatedUtc);
        Assert.Equal(704798, dopo.UpdatedByUserId);
        Assert.Equal(1, await _db.AuditLogs.CountAsync(a => a.EntityType == "StatsSettings"));
    }
}
