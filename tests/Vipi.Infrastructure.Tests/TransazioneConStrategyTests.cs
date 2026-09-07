using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Le scritture transazionali contro un provider che <b>ritenta</b>.
///
/// <para>Questo è il buco in cui è passato R-015 (revisione del 6 settembre 2026): tutti i test girano su
/// SQLite, che non ha nessuna execution strategy che ritenta, mentre MariaDB e Postgres — cioè produzione —
/// hanno <c>EnableRetryOnFailure</c>. Una transazione aperta a mano è verde qui e rossa là: la potatura
/// dell'archivio ATC non è mai avvenuta in produzione, e 5 543 test non se ne sono accorti.</para>
///
/// <para>Il rimedio non è ricordarsene: è <b>montare la strategy anche su SQLite</b>, così la differenza fra
/// i provider smette di essere cieca.</para>
/// </summary>
public class TransazioneConStrategyTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    private static readonly DateTimeOffset Adesso = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Una strategy che <b>dichiara di ritentare</b> come fanno Pomelo e Npgsql, senza ritentare davvero:
    /// quel che conta per questo difetto è <c>RetriesOnFailure</c>, non il ritentativo.
    /// </summary>
    private sealed class StrategyCheDichiaraDiRitentare : ExecutionStrategy
    {
        public StrategyCheDichiaraDiRitentare(ExecutionStrategyDependencies dipendenze)
            : base(dipendenze, maxRetryCount: 3, maxRetryDelay: TimeSpan.FromMilliseconds(1)) { }

        protected override bool ShouldRetryOn(Exception exception) => false;
    }

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>()
            .UseSqlite(_conn, b => b.ExecutionStrategy(d => new StrategyCheDichiaraDiRitentare(d)))
            .Options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private async Task SessioneVecchiaAsync(long id)
    {
        var inizio = Adesso.UtcDateTime.AddDays(-400);
        _db.AtcSessions.Add(new AtcSession
        {
            SessionId = id, UserId = 704798, Callsign = "LIRF_TWR", Position = "TWR",
            StartUtc = inizio, EndUtc = inizio.AddHours(1), DurationSeconds = 3600,
            Source = AtcSessionSource.Backfill,
            TrafficCount = 15, MovementCount = 12, TrafficMinutes = 30, UpdatedAtUtc = inizio,
        });
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// ⚠️ <b>La prova che il banco è vero.</b> Senza di questa, il test qui sotto sarebbe verde anche se la
    /// strategy non fosse montata — cioè verde per la ragione sbagliata. È anche la forma esatta che il
    /// codice aveva: il <c>BeginTransaction</c> da solo <b>non</b> solleva, è la coppia con
    /// <c>SaveChanges</c> a farlo.
    /// </summary>
    [Fact]
    public async Task La_transazione_aperta_a_mano_solleva_come_in_produzione()
    {
        await SessioneVecchiaAsync(1);

        var errore = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            _db.AtcSessions.RemoveRange(await _db.AtcSessions.ToListAsync());
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        });

        Assert.Contains("execution strategy", errore.Message);
    }

    /// <summary>La potatura, sullo stesso banco, deve passare: è ciò che in produzione non è mai successo.</summary>
    [Fact]
    public async Task La_potatura_delle_sessioni_passa_col_provider_che_ritenta()
    {
        await SessioneVecchiaAsync(1);
        await SessioneVecchiaAsync(2);

        var store = new EfAtcTrafficStore(_db, new EfUnitOfWork(_db));
        var tolte = await store.RollupAndPruneSessionsAsync(Adesso.AddDays(-366), batch: 100);

        Assert.Equal(2, tolte);
        Assert.Empty(await _db.AtcSessions.ToListAsync());

        var riassunto = Assert.Single(await _db.AtcMonthRollups.ToListAsync());
        Assert.Equal(2, riassunto.Sessions);
        Assert.Equal(7200, riassunto.Seconds);
    }
}
