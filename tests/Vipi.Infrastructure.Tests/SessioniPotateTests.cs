using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Stats;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// La potatura delle <b>sessioni</b> ATC oltre i dodici mesi, e il riassunto mensile che resta al loro posto.
///
/// <para>Fino al 26 agosto 2026 le sessioni non le cancellava nessuno: erano l'unica tabella che cresceva
/// senza fine (21 275 righe nei primi dodici mesi, misurate sull'archivio vero). Il punto delicato non è
/// cancellare — è che i numeri di un anno fa restino veri dopo: se le sessioni sparissero senza confluire
/// nel riassunto, le ore del mese scorso diventerebbero zero.</para>
/// </summary>
public class SessioniPotateTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfAtcTrafficStore _store = default!;

    private static readonly DateTimeOffset Adesso = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _store = new EfAtcTrafficStore(_db);
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private async Task<AtcSession> SessioneAsync(long id, DateTime inizio, int secondi = 3600,
        int userId = 704798, string callsign = "LIRF_TWR", bool aperta = false, int mosse = 12)
    {
        var s = new AtcSession
        {
            SessionId = id, UserId = userId, Callsign = callsign, Position = "TWR",
            StartUtc = inizio, EndUtc = aperta ? null : inizio.AddSeconds(secondi),
            DurationSeconds = secondi, Source = AtcSessionSource.Backfill,
            TrafficCount = mosse + 3, MovementCount = mosse, TrafficMinutes = 30,
            UpdatedAtUtc = inizio,
        };
        _db.AtcSessions.Add(s);
        await _db.SaveChangesAsync();
        return s;
    }

    [Fact]
    public async Task Una_sessione_vecchia_confluisce_nel_mensile_e_sparisce()
    {
        await SessioneAsync(1, new DateTime(2025, 3, 10, 8, 0, 0, DateTimeKind.Utc));

        var tolte = await _store.RollupAndPruneSessionsAsync(Adesso.AddDays(-366), 100);

        Assert.Equal(1, tolte);
        Assert.Empty(await _db.AtcSessions.ToListAsync());

        var r = Assert.Single(await _db.AtcMonthRollups.AsNoTracking().ToListAsync());
        Assert.Equal(new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc), r.Month);
        Assert.Equal(704798, r.UserId);
        Assert.Equal("LIRF_TWR", r.Callsign);
        Assert.Equal("TWR", r.Position);
        Assert.Equal(1, r.Sessions);
        Assert.Equal(3600, r.Seconds);
        Assert.Equal(12, r.TrafficMoved);
    }

    [Fact]
    public async Task Le_sessioni_dello_stesso_mese_si_sommano_in_una_riga_sola()
    {
        var marzo = new DateTime(2025, 3, 10, 8, 0, 0, DateTimeKind.Utc);
        await SessioneAsync(1, marzo);
        await SessioneAsync(2, marzo.AddDays(4), secondi: 1800, mosse: 5);

        await _store.RollupAndPruneSessionsAsync(Adesso.AddDays(-366), 100);

        var r = Assert.Single(await _db.AtcMonthRollups.AsNoTracking().ToListAsync());
        Assert.Equal(2, r.Sessions);
        Assert.Equal(5400, r.Seconds);
        Assert.Equal(17, r.TrafficMoved);
    }

    [Fact]
    public async Task Mesi_persone_e_callsign_diversi_restano_righe_diverse()
    {
        var marzo = new DateTime(2025, 3, 10, 8, 0, 0, DateTimeKind.Utc);
        await SessioneAsync(1, marzo);
        await SessioneAsync(2, marzo.AddMonths(1));                       // altro mese
        await SessioneAsync(3, marzo, userId: 111111);                    // altra persona
        await SessioneAsync(4, marzo, callsign: "LIRF_GND");              // altro callsign

        await _store.RollupAndPruneSessionsAsync(Adesso.AddDays(-366), 100);

        Assert.Equal(4, await _db.AtcMonthRollups.CountAsync());
    }

    [Fact]
    public async Task Uno_scaglione_alla_volta_non_conta_due_volte_lo_stesso_mese()
    {
        // ⚠️ È il difetto che il riassunto-poi-cancella dentro la stessa transazione esiste per evitare:
        // due giri sullo stesso mese devono sommare le righe NUOVE, non ripartire da capo.
        var marzo = new DateTime(2025, 3, 10, 8, 0, 0, DateTimeKind.Utc);
        for (var i = 1; i <= 4; i++) await SessioneAsync(i, marzo.AddDays(i));

        await _store.RollupAndPruneSessionsAsync(Adesso.AddDays(-366), 2);
        await _store.RollupAndPruneSessionsAsync(Adesso.AddDays(-366), 2);

        var r = Assert.Single(await _db.AtcMonthRollups.AsNoTracking().ToListAsync());
        Assert.Equal(4, r.Sessions);
        Assert.Equal(4 * 3600, r.Seconds);
    }

    [Fact]
    public async Task Le_sessioni_dentro_la_finestra_non_si_toccano()
    {
        await SessioneAsync(1, Adesso.UtcDateTime.AddDays(-30));

        Assert.Equal(0, await _store.RollupAndPruneSessionsAsync(Adesso.AddDays(-366), 100));
        Assert.Equal(1, await _db.AtcSessions.CountAsync());
        Assert.Empty(await _db.AtcMonthRollups.ToListAsync());
    }

    [Fact]
    public async Task Una_sessione_ancora_APERTA_non_si_pota_mai()
    {
        // Senza fine non c'è una durata definitiva: riassumerla congelerebbe un numero sbagliato. Una
        // connessione aperta da più di un anno è un guasto da guardare, non da cancellare.
        await SessioneAsync(1, new DateTime(2025, 3, 10, 8, 0, 0, DateTimeKind.Utc), aperta: true);

        Assert.Equal(0, await _store.RollupAndPruneSessionsAsync(Adesso.AddDays(-366), 100));
        Assert.Equal(1, await _db.AtcSessions.CountAsync());
    }

    [Fact]
    public async Task Il_traffico_di_dettaglio_cade_in_cascata_con_la_sessione()
    {
        var s = await SessioneAsync(1, new DateTime(2025, 3, 10, 8, 0, 0, DateTimeKind.Utc));
        _db.AtcSessionTraffic.Add(new AtcSessionTraffic
        {
            SessionId = s.SessionId, PilotCallsign = "AZA123", LegOrdinal = 1, PilotUserId = 5,
            FirstSeenUtc = s.StartUtc, LastSeenUtc = s.StartUtc.AddMinutes(20),
        });
        _db.AtcSessionRunways.Add(new AtcSessionRunway
        {
            SessionId = s.SessionId, FromUtc = s.StartUtc, Arrival = "16L", Departure = "25",
        });
        await _db.SaveChangesAsync();

        await _store.RollupAndPruneSessionsAsync(Adesso.AddDays(-366), 100);

        Assert.Empty(await _db.AtcSessionTraffic.ToListAsync());
        Assert.Empty(await _db.AtcSessionRunways.ToListAsync());
    }

    [Fact]
    public async Task Il_caso_d_uso_smaltisce_a_scaglioni_e_dice_se_resta_arretrato()
    {
        var marzo = new DateTime(2025, 3, 10, 8, 0, 0, DateTimeKind.Utc);
        for (var i = 1; i <= 5; i++) await SessioneAsync(i, marzo.AddDays(i));

        var caso = new AtcSessionRetentionUseCase(_store);

        var primo = await caso.RunAsync(Adesso, max: 3, batch: 2);
        Assert.Equal(3, primo.Removed);
        Assert.True(primo.MoreToGo);

        var secondo = await caso.RunAsync(Adesso, max: 100, batch: 2);
        Assert.Equal(2, secondo.Removed);
        Assert.False(secondo.MoreToGo);
        Assert.Empty(await _db.AtcSessions.ToListAsync());
    }

    [Fact]
    public async Task L_inizio_dell_archivio_non_si_accorcia_quando_le_sessioni_vengono_potate()
    {
        await SessioneAsync(1, new DateTime(2025, 3, 10, 8, 0, 0, DateTimeKind.Utc));
        await SessioneAsync(2, Adesso.UtcDateTime.AddDays(-30));

        var query = new EfAtcStatsQueries(_db);
        var prima = await query.ArchiveStartAsync(null);

        await _store.RollupAndPruneSessionsAsync(Adesso.AddDays(-366), 100);
        var dopo = await query.ArchiveStartAsync(null);

        // Senza il riassunto, «da quando esiste l'archivio» direbbe un mese fa: si accorcerebbe da solo ogni
        // notte mentre i numeri dei mesi vecchi ci sono ancora.
        Assert.NotNull(prima);
        Assert.NotNull(dopo);
        Assert.Equal(2025, dopo!.Value.Year);
        Assert.Equal(3, dopo.Value.Month);
    }
}
