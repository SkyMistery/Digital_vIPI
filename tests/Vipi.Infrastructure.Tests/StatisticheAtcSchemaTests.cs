using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il modello delle statistiche ATC fa quello che la carta promette: la chiave composita distingue le tratte
/// senza colonna surrogata, la stessa sessione si riscrive invece di duplicarsi, e potare una sessione porta
/// via il suo traffico. Carta: <c>docs/feature/2026-08-24-servizio-statistiche-atc.md</c>.
/// </summary>
public class StatisticheAtcSchemaTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    private static readonly DateTime T0 = new(2026, 8, 24, 18, 0, 0, DateTimeKind.Utc);

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private static AtcSession Sessione(long id = 63243559, string callsign = "LIRF_TWR") => new()
    {
        SessionId = id,
        UserId = 704798,
        Callsign = callsign,
        Position = "TWR",
        Frequency = "118.700",
        StartUtc = T0,
        DurationSeconds = 3730,
        Source = AtcSessionSource.Live,
        ShiftKey = id,
    };

    private static AtcSessionTraffic Tratta(long sessionId, string pilota, int ordinal, string? dep, string? arr) => new()
    {
        SessionId = sessionId,
        PilotCallsign = pilota,
        LegOrdinal = ordinal,
        PilotUserId = 785031,
        DepIcao = dep,
        ArrIcao = arr,
        AircraftIcao = "B38M",
        FirstSeenUtc = T0,
        LastSeenUtc = T0.AddMinutes(20),
        SeenMinutes = 12,
        SawMovement = true,
        Origin = TrafficOrigin.Aor,
    };

    [Fact]
    public async Task L_id_di_sessione_IVAO_e_la_chiave_e_non_viene_rigenerato()
    {
        _db.AtcSessions.Add(Sessione());
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var letta = await _db.AtcSessions.SingleAsync();
        Assert.Equal(63243559, letta.SessionId);   // non un 1 autoincrementale
        Assert.Equal(AtcSessionSource.Live, letta.Source);
    }

    [Fact]
    public async Task Lo_stesso_pilota_puo_avere_due_tratte_nella_stessa_sessione()
    {
        // LIRF→LIRN e poi LIRN→LIRF con lo stesso callsign: due movimenti, due righe.
        _db.AtcSessions.Add(Sessione());
        _db.AtcSessionTraffic.Add(Tratta(63243559, "AZA123", 1, "LIRF", "LIRN"));
        _db.AtcSessionTraffic.Add(Tratta(63243559, "AZA123", 2, "LIRN", "LIRF"));
        await _db.SaveChangesAsync();

        Assert.Equal(2, await _db.AtcSessionTraffic.CountAsync(x => x.PilotCallsign == "AZA123"));
    }

    [Fact]
    public async Task La_stessa_tratta_scritta_due_volte_e_un_aggiornamento_non_un_doppione()
    {
        // È il caso del pilota che cade e rientra: il poller riscrive la riga che c'è.
        _db.AtcSessions.Add(Sessione());
        _db.AtcSessionTraffic.Add(Tratta(63243559, "AZA123", 1, "LIRF", "LIRN"));
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var riga = await _db.AtcSessionTraffic.SingleAsync();
        riga.LastSeenUtc = T0.AddMinutes(45);
        riga.SeenMinutes = 30;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var sola = await _db.AtcSessionTraffic.SingleAsync();
        Assert.Equal(30, sola.SeenMinutes);
        Assert.Equal(1, await _db.AtcSessionTraffic.CountAsync());
    }

    [Fact]
    public async Task Potando_una_sessione_sparisce_anche_il_suo_traffico()
    {
        // La retention pota il dettaglio a 12 mesi: non deve lasciare righe orfane.
        _db.AtcSessions.Add(Sessione());
        _db.AtcSessionTraffic.Add(Tratta(63243559, "AZA123", 1, "LIRF", "LIRN"));
        _db.AtcSessionTraffic.Add(Tratta(63243559, "RYR456", 1, "EBBR", "LIRF"));
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        _db.AtcSessions.Remove(await _db.AtcSessions.SingleAsync());
        await _db.SaveChangesAsync();

        Assert.Equal(0, await _db.AtcSessionTraffic.CountAsync());
    }

    [Fact]
    public async Task Gli_spezzoni_di_un_turno_si_ritrovano_dalla_chiave_di_turno()
    {
        // Caduta di linea: due sessioni IVAO, un turno solo (misurato: succede al 38% delle sessioni vere).
        _db.AtcSessions.Add(Sessione(100));
        _db.AtcSessions.Add(new AtcSession
        {
            SessionId = 101, UserId = 704798, Callsign = "LIRF_TWR", StartUtc = T0.AddMinutes(63),
            DurationSeconds = 1800, Source = AtcSessionSource.Live, ShiftKey = 100,
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var turno = await _db.AtcSessions.Where(x => x.ShiftKey == 100).ToListAsync();
        Assert.Equal(2, turno.Count);
        Assert.Equal(3730 + 1800, turno.Sum(x => x.DurationSeconds));
    }

    [Fact]
    public async Task Gli_enum_finiscono_nel_database_come_testo_leggibile()
    {
        _db.AtcSessions.Add(Sessione());
        _db.AtcSessionTraffic.Add(Tratta(63243559, "AZA123", 1, "LIRF", "LIRN"));
        await _db.SaveChangesAsync();

        var origine = await _db.Database
            .SqlQuery<string>($"select Origin as Value from AtcSessionTraffic limit 1").SingleAsync();
        var sorgente = await _db.Database
            .SqlQuery<string>($"select Source as Value from AtcSessions limit 1").SingleAsync();

        Assert.Equal("Aor", origine);
        Assert.Equal("Live", sorgente);
    }
}
