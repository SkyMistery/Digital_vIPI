using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Stats;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// La potatura del dettaglio traffico, contro un database vero.
///
/// <para>⚠️ Il test che conta è il secondo: dopo la potatura le <b>ore e i movimenti di un anno fa devono
/// restare veri</b>. I contatori sulla riga sessione esistono esattamente per questo, e se un giorno
/// qualcuno cancellasse anche le sessioni, la pagina di chi controllava due anni fa direbbe zero.</para>
/// </summary>
public class TrafficRetentionTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfAtcTrafficStore _store = default!;

    private static readonly DateTimeOffset Oggi = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _store = new EfAtcTrafficStore(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    /// <summary>Una sessione con <paramref name="tratte"/> tratte, vecchia di <paramref name="giorniFa"/>.</summary>
    private async Task Sessione(long id, int giorniFa, int tratte)
    {
        var quando = Oggi.AddDays(-giorniFa);

        _db.AtcSessions.Add(new AtcSession
        {
            SessionId = id, UserId = 704798, Callsign = "LIRF_TWR",
            StartUtc = quando.UtcDateTime, EndUtc = quando.AddHours(2).UtcDateTime,
            DurationSeconds = 7200, Source = AtcSessionSource.Backfill, ShiftKey = id,
            TrafficCount = tratte, MovementCount = tratte, TrafficMinutes = tratte * 3,
        });

        for (var i = 0; i < tratte; i++)
            _db.AtcSessionTraffic.Add(new AtcSessionTraffic
            {
                SessionId = id, PilotCallsign = $"AZA{i:000}", LegOrdinal = 1,
                FirstSeenUtc = quando.UtcDateTime, LastSeenUtc = quando.AddMinutes(20).UtcDateTime,
                SeenMinutes = 20, SawMovement = true, Origin = TrafficOrigin.Aor,
            });

        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Toglie_il_dettaglio_vecchio_e_lascia_quello_recente()
    {
        await Sessione(1, giorniFa: 400, tratte: 5);     // oltre i dodici mesi
        await Sessione(2, giorniFa: 30, tratte: 4);      // dentro

        var esito = await new TrafficRetentionUseCase(_store).RunAsync(Oggi, max: 1000);

        Assert.Equal(5, esito.Removed);
        Assert.False(esito.MoreToGo);
        Assert.Equal(4, await _db.AtcSessionTraffic.CountAsync());
        Assert.All(await _db.AtcSessionTraffic.ToListAsync(), t => Assert.Equal(2, t.SessionId));
    }

    /// <summary>
    /// ⚠️ Il motivo per cui i contatori sono denormalizzati sulla riga sessione: dopo la potatura le ore e
    /// i movimenti di un anno fa devono valere ancora. Senza, la prima potatura azzererebbe la storia.
    /// </summary>
    [Fact]
    public async Task Le_sessioni_e_i_loro_numeri_restano_intatti()
    {
        await Sessione(1, giorniFa: 400, tratte: 5);

        await new TrafficRetentionUseCase(_store).RunAsync(Oggi, max: 1000);

        var s = await _db.AtcSessions.SingleAsync(x => x.SessionId == 1);
        Assert.Equal(5, s.TrafficCount);
        Assert.Equal(5, s.MovementCount);
        Assert.Equal(15, s.TrafficMinutes);
        Assert.Equal(7200, s.DurationSeconds);
        Assert.Empty(await _db.AtcSessionTraffic.ToListAsync());
    }

    /// <summary>Il tetto per giro si rispetta, e il giro dice che c'è ancora arretrato.</summary>
    [Fact]
    public async Task Il_tetto_per_giro_ferma_la_potatura_e_lo_dice()
    {
        await Sessione(1, giorniFa: 400, tratte: 10);

        var esito = await new TrafficRetentionUseCase(_store).RunAsync(Oggi, max: 4, batch: 3);

        Assert.Equal(4, esito.Removed);
        Assert.True(esito.MoreToGo);
        Assert.Equal(6, await _db.AtcSessionTraffic.CountAsync());
    }

    [Fact]
    public async Task Senza_niente_di_vecchio_non_cancella_niente()
    {
        await Sessione(1, giorniFa: 10, tratte: 3);

        var esito = await new TrafficRetentionUseCase(_store).RunAsync(Oggi, max: 1000);

        Assert.Equal(0, esito.Removed);
        Assert.False(esito.MoreToGo);
        Assert.Equal(3, await _db.AtcSessionTraffic.CountAsync());
    }
}
