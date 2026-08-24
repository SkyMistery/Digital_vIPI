using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Le letture delle pagine, contro un database vero.
///
/// <para>⚠️ Questa classe esiste per un difetto: la prima versione di <c>ByPositionAsync</c> proiettava il
/// raggruppamento dentro un <c>record</c>, che EF non sa tradurre — e la cosa si è vista solo aprendo la
/// pagina, con un 500. Una query non provata contro un database vero è una query non scritta.</para>
/// </summary>
public class AtcStatsQueriesTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfAtcStatsQueries _q = default!;

    private static readonly DateTimeOffset T0 = new(2026, 8, 20, 18, 0, 0, TimeSpan.Zero);

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _q = new EfAtcStatsQueries(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private async Task Sessione(long id, int vid, string callsign, int secondi, int giorniFa = 0,
        long? turno = null, int movimenti = 0, int presenze = 0)
    {
        _db.AtcSessions.Add(new AtcSession
        {
            SessionId = id, UserId = vid, Callsign = callsign, Position = callsign.Split('_').Last(),
            StartUtc = T0.AddDays(-giorniFa).UtcDateTime,
            EndUtc = T0.AddDays(-giorniFa).AddSeconds(secondi).UtcDateTime,
            DurationSeconds = secondi, Source = AtcSessionSource.Backfill, ShiftKey = turno ?? id,
            MovementCount = movimenti, TrafficCount = presenze,
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    private static (DateTimeOffset Da, DateTimeOffset A) Anno => (T0.AddDays(-366), T0.AddDays(1));

    [Fact]
    public async Task I_totali_contano_i_TURNI_non_le_connessioni()
    {
        // Due spezzoni della stessa seduta: due sessioni, un turno.
        await Sessione(100, 704798, "LIRF_TWR", 3000, turno: 100, movimenti: 4, presenze: 6);
        await Sessione(101, 704798, "LIRF_TWR", 1800, turno: 100, movimenti: 2, presenze: 3);

        var t = await _q.TotalsAsync(704798, Anno.Da, Anno.A);

        Assert.Equal(2, t.Sessions);
        Assert.Equal(1, t.Shifts);
        Assert.Equal(4800, t.Seconds);
        Assert.Equal(6, t.Movements);
        Assert.Equal(9, t.Presences);
    }

    [Fact]
    public async Task Le_connessioni_sotto_il_minuto_non_fanno_numero()
    {
        await Sessione(100, 704798, "LIRF_TWR", 3600);
        await Sessione(101, 704798, "LIRF_TWR", 45);       // entrata e uscita

        var t = await _q.TotalsAsync(704798, Anno.Da, Anno.A);

        Assert.Equal(1, t.Sessions);
        Assert.Equal(3600, t.Seconds);
        Assert.Single(await _q.SessionsAsync(704798, Anno.Da, Anno.A));
    }

    [Fact]
    public async Task Per_postazione_si_ordina_per_ore()
    {
        await Sessione(100, 704798, "LIRF_TWR", 3600, movimenti: 5);
        await Sessione(101, 704798, "LIRF_TWR", 1800, movimenti: 3);
        await Sessione(102, 704798, "LIRN_APP", 7200, movimenti: 9);

        var righe = await _q.ByPositionAsync(704798, Anno.Da, Anno.A);

        Assert.Equal(new[] { "LIRN_APP", "LIRF_TWR" }, righe.Select(r => r.Key));
        var twr = righe.Single(r => r.Key == "LIRF_TWR");
        Assert.Equal(2, twr.Sessions);
        Assert.Equal(5400, twr.Seconds);
        Assert.Equal(8, twr.Movements);
    }

    [Fact]
    public async Task Per_mese_si_ordina_dal_piu_vecchio()
    {
        await Sessione(100, 704798, "LIRF_TWR", 3600, giorniFa: 0);
        await Sessione(101, 704798, "LIRF_TWR", 3600, giorniFa: 40);

        var righe = await _q.ByMonthAsync(704798, Anno.Da, Anno.A);

        Assert.Equal(2, righe.Count);
        Assert.Equal(new[] { "2026-07", "2026-08" }, righe.Select(r => r.Key));
    }

    [Fact]
    public async Task Le_statistiche_di_un_altro_non_entrano_nelle_mie()
    {
        await Sessione(100, 704798, "LIRF_TWR", 3600);
        await Sessione(101, 762032, "LIRF_TWR", 7200);

        Assert.Equal(3600, (await _q.TotalsAsync(704798, Anno.Da, Anno.A)).Seconds);
        Assert.Equal(10800, (await _q.TotalsAsync(null, Anno.Da, Anno.A)).Seconds);   // divisione: tutte
    }

    [Fact]
    public async Task Il_dettaglio_porta_la_sessione_e_i_suoi_aerei()
    {
        await Sessione(100, 704798, "LIRF_TWR", 3600, movimenti: 1, presenze: 2);
        _db.AtcSessionTraffic.Add(new AtcSessionTraffic
        {
            SessionId = 100, PilotCallsign = "AZA123", LegOrdinal = 1, PilotUserId = 785031,
            DepIcao = "LIRF", ArrIcao = "LIRN", AircraftIcao = "B38M",
            FirstSeenUtc = T0.UtcDateTime, LastSeenUtc = T0.AddMinutes(20).UtcDateTime,
            SeenMinutes = 12, SawMovement = true, Origin = TrafficOrigin.Aor,
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var d = await _q.SessionAsync(100);

        Assert.NotNull(d);
        Assert.Equal("LIRF_TWR", d!.Session.Callsign);
        var volo = Assert.Single(d.Traffic);
        Assert.Equal("AZA123", volo.PilotCallsign);
        Assert.Equal(12, volo.SeenMinutes);
        Assert.Equal(TrafficOrigin.Aor, volo.Origin);
    }

    [Fact]
    public async Task Una_sessione_inesistente_non_esplode()
    {
        Assert.Null(await _q.SessionAsync(999));
    }

    [Fact]
    public async Task La_classifica_ordina_per_ore_e_conta_i_turni()
    {
        await Sessione(100, 704798, "LIRF_TWR", 3600, turno: 100, movimenti: 2);
        await Sessione(101, 704798, "LIRF_TWR", 3600, turno: 100, movimenti: 3);
        await Sessione(102, 762032, "LIRN_APP", 9000, turno: 102, movimenti: 10);

        var classifica = await _q.TopControllersAsync(Anno.Da, Anno.A);

        Assert.Equal(new[] { 762032, 704798 }, classifica.Select(c => c.UserId));
        var mio = classifica.Single(c => c.UserId == 704798);
        Assert.Equal(1, mio.Shifts);          // due connessioni, un turno
        Assert.Equal(7200, mio.Seconds);
        Assert.Equal(5, mio.Movements);
    }

    [Fact]
    public async Task Fuori_dalla_finestra_non_si_conta()
    {
        await Sessione(100, 704798, "LIRF_TWR", 3600, giorniFa: 400);   // oltre la retention

        Assert.Equal(0, (await _q.TotalsAsync(704798, Anno.Da, Anno.A)).Sessions);
    }

    [Fact]
    public async Task Un_archivio_vuoto_da_zeri_e_non_eccezioni()
    {
        var t = await _q.TotalsAsync(704798, Anno.Da, Anno.A);

        Assert.Equal(0, t.Sessions);
        Assert.Equal(0, t.Seconds);
        Assert.Empty(await _q.ByPositionAsync(704798, Anno.Da, Anno.A));
        Assert.Empty(await _q.ByMonthAsync(704798, Anno.Da, Anno.A));
        Assert.Empty(await _q.TopControllersAsync(Anno.Da, Anno.A));
    }

    [Fact]
    public async Task La_copertura_si_misura_sul_periodo_di_cui_abbiamo_dati()
    {
        // ⚠️ Chiedere dodici mesi a un archivio che ne contiene uno darebbe «2%» in ogni casella: vero,
        // inutile e scoraggiante. La finestra si stringe alla prima sessione in archivio.
        await Sessione(100, 704798, "LIRF_TWR", 3600, giorniFa: 0);   // un'ora sola, in tutto l'archivio

        var g = await _q.CoverageAsync(null, Anno.Da, Anno.A);

        var piena = g.Where(c => c.CoveredMinutes > 0).ToList();
        Assert.NotEmpty(piena);
        Assert.All(piena, c => Assert.True(c.Ratio > 0.5, $"casella {c.DayOfWeek}/{c.Hour} al {c.Ratio:P0}"));
    }

    [Fact]
    public async Task Senza_sessioni_la_griglia_c_e_lo_stesso_ed_e_vuota()
    {
        var g = await _q.CoverageAsync(704798, Anno.Da, Anno.A);

        Assert.Equal(168, g.Count);
        Assert.All(g, c => Assert.Equal(0, c.CoveredMinutes));
    }

    [Fact]
    public async Task Gli_aeroporti_contano_partenza_E_arrivo_di_ogni_volo()
    {
        await Sessione(100, 704798, "LIRR_NE1_CTR", 3600);
        _db.AtcSessionTraffic.Add(new AtcSessionTraffic
        {
            SessionId = 100, PilotCallsign = "AZA123", LegOrdinal = 1, PilotUserId = 1,
            DepIcao = "LIRF", ArrIcao = "LIRN", AircraftIcao = "B38M",
            FirstSeenUtc = T0.UtcDateTime, LastSeenUtc = T0.UtcDateTime, SawMovement = true,
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var aeroporti = await _q.TopAirportsAsync(704798, Anno.Da, Anno.A);
        Assert.Equal(new[] { "LIRF", "LIRN" }, aeroporti.Select(a => a.Key).OrderBy(x => x));

        var tipi = await _q.TopAircraftAsync(704798, Anno.Da, Anno.A);
        Assert.Equal("B38M", Assert.Single(tipi).Key);
    }

    [Fact]
    public async Task La_striscia_conta_le_settimane_con_almeno_un_turno()
    {
        await Sessione(100, 704798, "LIRF_TWR", 3600);
        await Sessione(101, 704798, "LIRF_TWR", 3600, giorniFa: 7);
        await Sessione(102, 704798, "LIRF_TWR", 3600, giorniFa: 14);
        await Sessione(103, 704798, "LIRF_TWR", 3600, giorniFa: 60);      // vecchia: spezza

        var s = await _q.StreakAsync(704798, Anno.Da, Anno.A);

        Assert.Equal(3, s.CurrentWeeks);
        Assert.NotNull(s.LastSessionUtc);
    }

    [Fact]
    public async Task La_posizione_in_classifica_si_legge_anche_per_chi_e_in_fondo()
    {
        await Sessione(100, 111, "LIRF_TWR", 10_000);
        await Sessione(101, 222, "LIMC_TWR", 5_000);
        await Sessione(102, 333, "LIPZ_TWR", 1_000);

        var r = await _q.RankAsync(333, Anno.Da, Anno.A);

        Assert.Equal(3, r.Position);
        Assert.Equal(3, r.Total);
        Assert.Equal(100, r.TopPercent);

        var primo = await _q.RankAsync(111, Anno.Da, Anno.A);
        Assert.Equal(1, primo.Position);
        Assert.Equal(34, primo.TopPercent);
    }

    [Fact]
    public async Task Chi_non_ha_turni_nel_periodo_non_ha_posizione()
    {
        await Sessione(100, 111, "LIRF_TWR", 10_000);

        var r = await _q.RankAsync(999, Anno.Da, Anno.A);

        Assert.Equal(0, r.Position);
        Assert.Equal(0, r.TopPercent);
    }

    [Fact]
    public async Task L_inizio_dell_archivio_e_la_connessione_piu_vecchia()
    {
        await Sessione(100, 704798, "LIRF_TWR", 3600, giorniFa: 3);
        await Sessione(101, 704798, "LIRF_TWR", 3600, giorniFa: 40);

        var inizio = await _q.ArchiveStartAsync(704798);

        Assert.Equal(T0.AddDays(-40), inizio);
        Assert.Null(await _q.ArchiveStartAsync(999));
    }

    [Fact]
    public async Task Il_dettaglio_traduce_le_consegne_nei_callsign()
    {
        await Sessione(100, 704798, "LIRF_TWR", 3600);
        await Sessione(200, 704799, "LIRR_NE1_CTR", 3600);

        _db.AtcSessionTraffic.Add(new AtcSessionTraffic
        {
            SessionId = 100, PilotCallsign = "AZA123", LegOrdinal = 1, PilotUserId = 785031,
            DepIcao = "LIRF", ArrIcao = "LIMC", AircraftIcao = "A320",
            FirstSeenUtc = T0.UtcDateTime, LastSeenUtc = T0.AddMinutes(12).UtcDateTime, SeenMinutes = 12,
            SawMovement = true, SawAirborne = true, Origin = TrafficOrigin.Aor,
            FirstPhase = FlightPhase.Parked, LastPhase = FlightPhase.Airborne,
            EntryAltitudeFt = 0, ExitAltitudeFt = 12_000, MaxAltitudeFt = 12_000,
            HandoffToSessionId = 200,
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var d = await _q.SessionAsync(100);
        var riga = Assert.Single(d!.Traffic);

        Assert.Equal("LIRR_NE1_CTR", riga.HandoffTo);
        Assert.Null(riga.HandoffFrom);
        Assert.Equal(FlightPhase.Parked, riga.FirstPhase);
        Assert.True(riga.SawAirborne);
        Assert.Equal(12_000, riga.MaxAltitudeFt);
    }

    [Fact]
    public async Task Una_consegna_verso_una_sessione_potata_non_rompe_il_dettaglio()
    {
        // La potatura del dettaglio cancellera' righe vecchie: l'id resta, il callsign no.
        await Sessione(100, 704798, "LIRF_TWR", 3600);

        _db.AtcSessionTraffic.Add(new AtcSessionTraffic
        {
            SessionId = 100, PilotCallsign = "AZA123", LegOrdinal = 1, PilotUserId = 785031,
            FirstSeenUtc = T0.UtcDateTime, LastSeenUtc = T0.AddMinutes(3).UtcDateTime, SeenMinutes = 3,
            SawMovement = true, Origin = TrafficOrigin.Aor, HandoffToSessionId = 9_999,
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var d = await _q.SessionAsync(100);
        Assert.Null(Assert.Single(d!.Traffic).HandoffTo);
    }
}
