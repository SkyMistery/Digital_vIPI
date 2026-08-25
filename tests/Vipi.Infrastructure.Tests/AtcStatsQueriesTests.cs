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

    /// <summary>Una tratta nell’archivio del traffico, già attribuita a una sessione.</summary>
    private async Task Tratta(long sessione, string callsign, string? dep, string? arr,
        bool movimento = true, int ordinale = 1)
    {
        _db.AtcSessionTraffic.Add(new AtcSessionTraffic
        {
            SessionId = sessione, PilotCallsign = callsign, LegOrdinal = ordinale, PilotUserId = 1,
            DepIcao = dep, ArrIcao = arr, AircraftIcao = "B38M",
            FirstSeenUtc = T0.UtcDateTime, LastSeenUtc = T0.UtcDateTime, SawMovement = movimento,
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    /// <summary>
    /// «Aeroporti gestiti» risponde a una domanda diversa da «aeroporti visti»: il campo è quello del
    /// PROPRIO callsign, e contano le tratte da o per lì.
    /// </summary>
    [Fact]
    public async Task Gli_aeroporti_gestiti_sono_quelli_del_proprio_callsign()
    {
        await Sessione(100, 704798, "LIRF_TWR", 3600);
        await Tratta(100, "AZA123", "LIRF", "LIRN");           // partita da casa
        await Tratta(100, "RYR456", "EGKK", "LIRF", ordinale: 2);   // arrivata a casa

        var gestiti = await _q.ManagedAirportsAsync(704798, Anno.Da, Anno.A);

        var solo = Assert.Single(gestiti);
        Assert.Equal("LIRF", solo.Key);
        Assert.Equal(2, solo.Sessions);

        // ⚠️ L’altra tabella dice ancora tutt’altro, ed è giusto così: lì i capi contano tutti e due.
        var visti = await _q.TopAirportsAsync(704798, Anno.Da, Anno.A);
        Assert.Equal(new[] { "EGKK", "LIRF", "LIRN" }, visti.Select(a => a.Key).OrderBy(x => x));
    }

    /// <summary>
    /// ⚠️ Un sorvolo vettorato mentre si copriva LIRF non è traffico «di» LIRF: fuori dall’elenco. Resta
    /// però fra gli aeroporti VISTI, perché gestito lo è stato.
    /// </summary>
    [Fact]
    public async Task Un_sorvolo_non_conta_per_il_campo_che_si_copriva()
    {
        await Sessione(100, 704798, "LIRF_APP", 3600);
        await Tratta(100, "AZA123", "LIRF", "LIRN");
        await Tratta(100, "DLH900", "EDDF", "LMML", ordinale: 2);   // passa e va

        var gestiti = await _q.ManagedAirportsAsync(704798, Anno.Da, Anno.A);
        Assert.Equal(1, Assert.Single(gestiti).Sessions);

        var visti = await _q.TopAirportsAsync(704798, Anno.Da, Anno.A);
        Assert.Contains("EDDF", visti.Select(a => a.Key));
    }

    /// <summary>Un circuito LIRF→LIRF è UNA tratta, non due: il campo sta a tutti e due i capi.</summary>
    [Fact]
    public async Task Un_volo_che_parte_e_torna_conta_una_volta_sola()
    {
        await Sessione(100, 704798, "LIRF_TWR", 3600);
        await Tratta(100, "IGAAA", "LIRF", "LIRF");

        Assert.Equal(1, Assert.Single(await _q.ManagedAirportsAsync(704798, Anno.Da, Anno.A)).Sessions);
    }

    /// <summary>⚠️ <c>AccSector.CenterId</c> è una FK verso <c>Acc.Code</c>: senza l’ACC il settore non entra.</summary>
    private async Task<Acc> AccRoma()
    {
        var acc = await _db.Accs.FirstOrDefaultAsync(a => a.Code == "LIRR");
        if (acc is not null) return acc;

        acc = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        _db.Accs.Add(acc);
        await _db.SaveChangesAsync();
        return acc;
    }

    /// <summary>Un settore d’area, col suo poligono: è lui a dire quali aeroporti sono «suoi».</summary>
    private async Task Area(string callsign, string poligono)
    {
        var acc = await AccRoma();
        _db.AccSectors.Add(new AccSector
        {
            ComposePosition = callsign, CenterId = acc.Code, Position = "CTR",
            RegionMapPolygon = poligono, LowerLimit = 0, UpperLimit = null,
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    private async Task Aeroporto(string icao, double? lat, double? lon)
    {
        var acc = await AccRoma();
        _db.Airports.Add(new Airport { Icao = icao, Name = icao, AccId = acc.Id, Latitude = lat, Longitude = lon });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    /// <summary>
    /// Un quadratone 9–16°E × 39–46°N: dentro ci sta l’Italia centro-meridionale, fuori Londra.
    /// ⚠️ Il JSON grezzo è <c>[lon,lat]</c> e <c>PolygonGeometry.ParsePoints</c> lo gira: leggerlo al
    /// contrario fa un riquadro in mezzo all’oceano Indiano, e il test passa a vuoto.
    /// </summary>
    private const string Riquadro = "[[9,39],[16,39],[16,46],[9,46]]";

    /// <summary>
    /// ⚠️ <c>LIRR_NE1_CTR</c> è una FIR, non un aeroporto: il campo non può uscire dal callsign. Per un
    /// settore d’area lo dice la GEOMETRIA — gli aeroporti dentro il suo poligono — e i capi fuori area
    /// restano fuori dal conto.
    /// </summary>
    [Fact]
    public async Task Un_settore_d_area_prende_gli_aeroporti_dentro_il_suo_poligono()
    {
        await Area("LIRR_NE1_CTR", Riquadro);
        await Aeroporto("LIRF", 41.8, 12.25);        // dentro
        await Aeroporto("LIRN", 40.9, 14.29);        // dentro
        await Aeroporto("EGLL", 51.47, -0.45);       // fuori: Londra

        await Sessione(100, 704798, "LIRR_NE1_CTR", 3600);
        await Tratta(100, "AZA123", "LIRF", "LIRN");
        await Tratta(100, "BAW789", "LIRF", "EGLL", ordinale: 2);

        var gestiti = await _q.ManagedAirportsAsync(704798, Anno.Da, Anno.A);

        Assert.Equal(new[] { "LIRF", "LIRN" }, gestiti.Select(a => a.Key).OrderBy(x => x));
        Assert.Equal(2, gestiti.Single(a => a.Key == "LIRF").Sessions);   // tutt’e due i voli partono da lì
        Assert.Equal(1, gestiti.Single(a => a.Key == "LIRN").Sessions);
    }

    /// <summary>Come per la torre: due capi in casa sono UNA tratta, non due.</summary>
    [Fact]
    public async Task Per_un_settore_d_area_un_volo_interno_conta_una_volta_per_ciascun_capo()
    {
        await Area("LIRR_NE1_CTR", Riquadro);
        await Aeroporto("LIRF", 41.8, 12.25);

        await Sessione(100, 704798, "LIRR_NE1_CTR", 3600);
        await Tratta(100, "IGAAA", "LIRF", "LIRF");      // circuito: stesso campo ai due capi

        Assert.Equal(1, Assert.Single(await _q.ManagedAirportsAsync(704798, Anno.Da, Anno.A)).Sessions);
    }

    /// <summary>
    /// ⚠️ Un settore senza poligono non può dire quali aeroporti copre, e non se li inventa dal prefisso:
    /// «LIRR» sarebbe una FIR spacciata per aeroporto.
    /// </summary>
    [Fact]
    public async Task Un_settore_d_area_senza_poligono_non_porta_aeroporti()
    {
        await Aeroporto("LIRF", 41.8, 12.25);
        await Sessione(100, 704798, "LIRR_NE1_CTR", 3600);
        await Tratta(100, "AZA123", "LIRF", "LIRN");

        Assert.Empty(await _q.ManagedAirportsAsync(704798, Anno.Da, Anno.A));
    }

    /// <summary>
    /// ⚠️ Nove aeroporti su 93 non hanno coordinate, e tre di quelli sono voci di FIR/TMA («Roma TMA»).
    /// Senza coordinate non si sa se stanno dentro: restano fuori, non entrano per difetto.
    /// </summary>
    [Fact]
    public async Task Un_aeroporto_senza_coordinate_non_entra_in_nessun_settore()
    {
        await Area("LIRR_NE1_CTR", Riquadro);
        await Aeroporto("LIRF", 41.8, 12.25);
        await Aeroporto("LIRR", null, null);            // «Roma TMA»: non è un aeroporto

        await Sessione(100, 704798, "LIRR_NE1_CTR", 3600);
        await Tratta(100, "AZA123", "LIRF", "LIRR");

        Assert.Equal("LIRF", Assert.Single(await _q.ManagedAirportsAsync(704798, Anno.Da, Anno.A)).Key);
    }

    /// <summary>Le due strade convivono nello stesso periodo: una torre e un’area, sommate per aeroporto.</summary>
    [Fact]
    public async Task Torre_e_area_finiscono_nella_stessa_tabella()
    {
        await Area("LIRR_NE1_CTR", Riquadro);
        await Aeroporto("LIRF", 41.8, 12.25);
        await Aeroporto("LIRN", 40.9, 14.29);

        await Sessione(100, 704798, "LIRF_TWR", 3600);
        await Tratta(100, "AZA123", "LIRF", "LIRN");

        await Sessione(200, 704798, "LIRR_NE1_CTR", 3600, giorniFa: 1);
        await Tratta(200, "AZA456", "LIRF", "LIRN");

        var gestiti = await _q.ManagedAirportsAsync(704798, Anno.Da, Anno.A);

        // LIRF: una tratta dalla torre + una dall’area. LIRN: solo quella dell’area (la torre è di LIRF).
        Assert.Equal(2, gestiti.Single(a => a.Key == "LIRF").Sessions);
        Assert.Equal(1, gestiti.Single(a => a.Key == "LIRN").Sessions);
    }

    /// <summary>Le posizioni che un campo lo dichiarano davvero, tutte e sei.</summary>
    [Theory]
    [InlineData("LIRF_TWR")]
    [InlineData("LIRF_GND")]
    [InlineData("LIRF_DEL")]
    [InlineData("LIRF_APP")]
    [InlineData("LIRF_W_DEP")]
    [InlineData("LIRF_AFIS")]
    public async Task Ogni_postazione_col_campo_nel_callsign_conta(string callsign)
    {
        await Sessione(100, 704798, callsign, 3600);
        await Tratta(100, "AZA123", "LIRF", "LIRN");

        Assert.Equal("LIRF", Assert.Single(await _q.ManagedAirportsAsync(704798, Anno.Da, Anno.A)).Key);
    }

    /// <summary>Il filtro delle connessioni-lampo vale anche qui: passa da <c>Contate</c> come le altre.</summary>
    [Fact]
    public async Task Una_connessione_lampo_non_porta_il_suo_aeroporto()
    {
        await Sessione(100, 704798, "LIRF_TWR", 45);
        await Tratta(100, "AZA123", "LIRF", "LIRN");

        Assert.Empty(await _q.ManagedAirportsAsync(704798, Anno.Da, Anno.A));
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
