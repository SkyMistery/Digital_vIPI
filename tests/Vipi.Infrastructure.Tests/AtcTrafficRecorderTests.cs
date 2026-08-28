using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Stats;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il giro dell'attribuzione dall'inizio alla fine, contro un database vero: fotografia della rete →
/// copertura → volumi → un aereo, una sessione → righe di traffico e contatori.
/// </summary>
public class AtcTrafficRecorderTests : IAsyncLifetime
{
    private const string Fir = "[[10,40],[14,40],[14,44],[10,44]]";       // 4°×4° attorno a Roma
    private const string Campo = "[[11.9,41.7],[12.4,41.7],[12.4,41.9],[11.9,41.9]]";

    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfAtcTrafficStore _traffico = default!;
    private EfAtcSessionStore _sessioni = default!;
    private AtcTrafficRecorder _recorder = default!;

    private static readonly DateTimeOffset T0 = new(2026, 8, 24, 18, 0, 0, TimeSpan.Zero);

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();

        var acc = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        var aeroporto = new Airport { Icao = "LIRF", Name = "Fiumicino", Acc = acc };
        _db.Accs.Add(acc);
        _db.Airports.Add(aeroporto);

        // Cataloghi: i volumi. La torre arriva a 3000 ft (regola di divisione), l'ACC è senza tetto.
        _db.AccSectors.Add(new AccSector
        {
            ComposePosition = "LIRR_NE1_CTR", CenterId = "LIRR", Position = "CTR",
            RegionMapPolygon = Fir, LowerLimit = 0, UpperLimit = null,
        });
        _db.AirportSectors.Add(new AirportSector
        {
            ComposePosition = "LIRF_TWR", AirportIcao = "LIRF", AccCode = "LIRR", Position = "TWR",
            RegionMapPolygon = Campo, LowerLimit = 0, UpperLimit = 3000,
        });
        _db.AirportSectors.Add(new AirportSector
        {
            ComposePosition = "LIRF_GND", AirportIcao = "LIRF", AccCode = "LIRR", Position = "GND",
        });
        await _db.SaveChangesAsync();

        // Proiezione: l'albero di copertura (è da qui che si scende, non dai cataloghi).
        var ctr = new Sector { Callsign = "LIRR_NE1_CTR", Name = "Roma NE1", AccId = acc.Id, Type = SectorType.Ctr, Kind = SectorKind.Acc };
        _db.Sectors.Add(ctr);
        await _db.SaveChangesAsync();

        var twr = new Sector
        {
            Callsign = "LIRF_TWR", Name = "Fiumicino Torre", AccId = acc.Id, Type = SectorType.Twr,
            Kind = SectorKind.Airport, AirportIcao = "LIRF", ParentSectorId = ctr.Id,
        };
        _db.Sectors.Add(twr);
        await _db.SaveChangesAsync();

        _db.Sectors.Add(new Sector
        {
            Callsign = "LIRF_GND", Name = "Fiumicino Ground", AccId = acc.Id, Type = SectorType.Gnd,
            Kind = SectorKind.Airport, AirportIcao = "LIRF", ParentSectorId = twr.Id,
        });
        await _db.SaveChangesAsync();

        _traffico = new EfAtcTrafficStore(_db);
        _sessioni = new EfAtcSessionStore(_db);
        _recorder = new AtcTrafficRecorder(new EfSectorVolumeCatalog(_db));
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private static SourceAtcConnection Atc(long id, string callsign) =>
        new(id, 704798, callsign, callsign.Split('_').Last(), "118.700", 4, T0, 600);

    private static SourcePilotFix Volo(string callsign, double lat, double lon, double ft,
        bool onGround = false, double gs = 420, string? stato = "En Route", double? distPartenza = 50,
        long? fp = 900, string dep = "LIRF", string arr = "LIRN") =>
        new(63000 + callsign.GetHashCode() % 1000, 785031, callsign, lat, lon, ft, gs, onGround, stato,
            distPartenza, fp, dep, arr, "B38M");

    /// <summary>Un giro di poll completo: prima le sessioni (la chiave esterna), poi il traffico.</summary>
    private async Task<AtcTrafficRecorder.Result> Giro(DateTimeOffset ora,
        IReadOnlyList<SourceAtcConnection> atc, IReadOnlyList<SourcePilotFix> piloti)
    {
        var snapshot = new NetworkSnapshot { Atc = atc, Pilots = piloti, AsOf = ora };

        var known = await _sessioni.GetOpenOrRecentAsync(ora - AtcSessionSync.ShiftGap);
        await _sessioni.ApplyAsync(AtcSessionSync.Plan(atc, known, ora));

        return await _recorder.RecordAsync(snapshot, _traffico);
    }

    [Fact]
    public async Task Un_volo_in_crociera_finisce_alla_sessione_dell_ACC()
    {
        var esito = await Giro(T0, new[] { Atc(100, "LIRR_NE1_CTR") },
            new[] { Volo("AZA123", 42.0, 12.0, 35_000) });

        Assert.Equal(1, esito.Attributed);
        _db.ChangeTracker.Clear();

        var riga = await _db.AtcSessionTraffic.SingleAsync();
        Assert.Equal(100, riga.SessionId);
        Assert.Equal("AZA123", riga.PilotCallsign);
        Assert.Equal("LIRF", riga.DepIcao);
        Assert.True(riga.SawMovement);
        Assert.Equal(TrafficOrigin.Aor, riga.Origin);
    }

    [Fact]
    public async Task Con_la_torre_online_il_traffico_basso_e_suo_e_quello_alto_resta_all_ACC()
    {
        var atc = new[] { Atc(100, "LIRR_NE1_CTR"), Atc(101, "LIRF_TWR") };
        var piloti = new[]
        {
            Volo("AZA123", 41.8, 12.2, 2_000, gs: 160, stato: "Initial Climb"),   // sul campo, sotto i 3000
            Volo("RYR456", 42.5, 12.9, 35_000),                                   // in crociera
        };

        await Giro(T0, atc, piloti);
        _db.ChangeTracker.Clear();

        var righe = await _db.AtcSessionTraffic.ToListAsync();
        Assert.Equal(101, righe.Single(r => r.PilotCallsign == "AZA123").SessionId);
        Assert.Equal(100, righe.Single(r => r.PilotCallsign == "RYR456").SessionId);
    }

    [Fact]
    public async Task L_aereo_fermo_al_gate_va_alla_GND_che_non_ha_poligono_suo()
    {
        // La GND prende in prestito il volume della torre: senza, il traffico a terra andrebbe alla TWR.
        var atc = new[] { Atc(101, "LIRF_TWR"), Atc(102, "LIRF_GND") };
        var fermo = Volo("AZA123", 41.8, 12.25, 0, onGround: true, gs: 0, stato: "Boarding", distPartenza: 1.0);

        await Giro(T0, atc, new[] { fermo });
        _db.ChangeTracker.Clear();

        var riga = await _db.AtcSessionTraffic.SingleAsync();
        Assert.Equal(102, riga.SessionId);
        Assert.False(riga.SawMovement);     // è una presenza, non un movimento
    }

    [Fact]
    public async Task Un_aereo_fuori_dai_volumi_italiani_non_e_di_nessuno()
    {
        var esito = await Giro(T0, new[] { Atc(100, "LIRR_NE1_CTR") },
            new[] { Volo("AFR123", 48.8, 2.3, 35_000) });    // Parigi

        Assert.Equal(0, esito.Attributed);
        Assert.Equal(0, await _db.AtcSessionTraffic.CountAsync());
    }

    [Fact]
    public async Task I_contatori_della_sessione_distinguono_presenze_e_movimenti()
    {
        var atc = new[] { Atc(101, "LIRF_TWR") };
        var fermo = Volo("AZA123", 41.8, 12.25, 0, onGround: true, gs: 0, stato: "Boarding", distPartenza: 1.0);
        var inVolo = Volo("RYR456", 41.8, 12.25, 2_000, fp: 901, dep: "EBBR", arr: "LIRF");

        await Giro(T0, atc, new[] { fermo, inVolo });
        _db.ChangeTracker.Clear();

        var s = await _db.AtcSessions.SingleAsync(x => x.SessionId == 101);
        Assert.Equal(2, s.TrafficCount);        // due presenze
        Assert.Equal(1, s.MovementCount);       // un solo movimento
        Assert.Equal(1, s.TrafficMinutes);      // un giro con traffico
    }

    [Fact]
    public async Task Fra_un_checkpoint_e_l_altro_il_database_non_viene_riscritto()
    {
        var atc = new[] { Atc(100, "LIRR_NE1_CTR") };
        var volo = new[] { Volo("AZA123", 42.0, 12.0, 35_000) };

        await Giro(T0, atc, volo);                       // tratta nuova: scrive
        var secondo = await Giro(T0.AddMinutes(1), atc, volo);
        var terzo = await Giro(T0.AddMinutes(2), atc, volo);

        Assert.Equal(0, secondo.WrittenLegs);             // niente di nuovo: si aspetta il checkpoint
        Assert.Equal(0, terzo.WrittenLegs);
        _db.ChangeTracker.Clear();

        var riga = await _db.AtcSessionTraffic.SingleAsync();
        Assert.Equal(1, riga.SeenMinutes);                // in archivio c'è ancora il primo giro
    }

    [Fact]
    public async Task Al_checkpoint_i_minuti_accumulati_arrivano_in_archivio()
    {
        var atc = new[] { Atc(100, "LIRR_NE1_CTR") };
        var volo = new[] { Volo("AZA123", 42.0, 12.0, 35_000) };

        await Giro(T0, atc, volo);                                              // scrive: tratta nuova
        for (var m = 1; m <= 11; m++) await Giro(T0.AddMinutes(m), atc, volo);
        _db.ChangeTracker.Clear();

        // Al decimo minuto il checkpoint ha versato in archivio gli 11 giri fino a quel momento; gli ultimi
        // due sono ancora in memoria — ed è il patto: il database non vede ogni minuto, e chi legge le
        // statistiche di una sessione IN CORSO vede l'ultimo checkpoint, non l'istante.
        var riga = await _db.AtcSessionTraffic.SingleAsync();
        Assert.Equal(11, riga.SeenMinutes);

        await _recorder.FlushAsync(_traffico, T0.AddMinutes(11));
        _db.ChangeTracker.Clear();
        Assert.Equal(12, (await _db.AtcSessionTraffic.SingleAsync()).SeenMinutes);
    }

    [Fact]
    public async Task Quando_la_sessione_sparisce_l_ultimo_tratto_viene_salvato()
    {
        var atc = new[] { Atc(100, "LIRR_NE1_CTR") };
        var volo = new[] { Volo("AZA123", 42.0, 12.0, 35_000) };

        await Giro(T0, atc, volo);
        await Giro(T0.AddMinutes(1), atc, volo);
        await Giro(T0.AddMinutes(2), new[] { Atc(200, "LIMM_WS2_CTR") }, Array.Empty<SourcePilotFix>());
        _db.ChangeTracker.Clear();

        var riga = await _db.AtcSessionTraffic.SingleAsync();
        Assert.Equal(2, riga.SeenMinutes);     // i due giri, salvati alla chiusura invece che persi
    }

    [Fact]
    public async Task Dopo_un_riavvio_i_minuti_non_tornano_indietro()
    {
        var atc = new[] { Atc(100, "LIRR_NE1_CTR") };
        var volo = new[] { Volo("AZA123", 42.0, 12.0, 35_000) };

        await Giro(T0, atc, volo);
        for (var m = 1; m <= 11; m++) await Giro(T0.AddMinutes(m), atc, volo);
        await _recorder.FlushAsync(_traffico, T0.AddMinutes(11));                // 12 minuti in archivio

        // Riavvio: registratore nuovo, memoria vuota, stesso archivio.
        _recorder = new AtcTrafficRecorder(new EfSectorVolumeCatalog(_db));
        await Giro(T0.AddMinutes(12), atc, volo);
        await _recorder.FlushAsync(_traffico, T0.AddMinutes(12));
        _db.ChangeTracker.Clear();

        var riga = await _db.AtcSessionTraffic.SingleAsync();
        Assert.Equal(13, riga.SeenMinutes);     // 12 + il giro dopo il riavvio, non 1
        Assert.Equal(1, riga.LegOrdinal);       // e non ha aperto una seconda tratta
    }

    [Fact]
    public async Task Chi_sale_dalla_torre_all_ACC_lascia_una_consegna_su_tutte_e_due()
    {
        var atc = new[] { Atc(100, "LIRR_NE1_CTR"), Atc(101, "LIRF_TWR") };

        // Primo giro: basso sul campo, è della torre. Secondo giro: salito, è dell'ACC.
        await Giro(T0, atc, new[] { Volo("AZA123", 41.8, 12.2, 2_000, gs: 160, stato: "Initial Climb") });
        await Giro(T0.AddMinutes(1), atc, new[] { Volo("AZA123", 42.0, 12.4, 10_000) });
        _db.ChangeTracker.Clear();

        var righe = await _db.AtcSessionTraffic.ToListAsync();
        Assert.Equal(100, righe.Single(r => r.SessionId == 101).HandoffToSessionId);
        Assert.Equal(101, righe.Single(r => r.SessionId == 100).HandoffFromSessionId);
    }

    [Fact]
    public async Task Un_poller_fermo_a_lungo_non_inventa_una_consegna()
    {
        // ⚠️ Fra i due giri passa mezz'ora: il passaggio non l'abbiamo visto, e «prima era tuo e adesso è
        // suo» non è una consegna.
        var atc = new[] { Atc(100, "LIRR_NE1_CTR"), Atc(101, "LIRF_TWR") };

        await Giro(T0, atc, new[] { Volo("AZA123", 41.8, 12.2, 2_000, gs: 160, stato: "Initial Climb") });
        await Giro(T0.AddMinutes(30), atc, new[] { Volo("AZA123", 42.0, 12.4, 10_000) });
        _db.ChangeTracker.Clear();

        var righe = await _db.AtcSessionTraffic.ToListAsync();
        Assert.All(righe, r => Assert.Null(r.HandoffToSessionId));
        Assert.All(righe, r => Assert.Null(r.HandoffFromSessionId));
    }

    [Fact]
    public async Task Le_fasi_e_le_quote_arrivano_fino_all_archivio()
    {
        var atc = new[] { Atc(100, "LIRR_NE1_CTR") };

        await Giro(T0, atc, new[] { Volo("AZA123", 42.0, 12.0, 35_000) });
        await Giro(T0.AddMinutes(1), atc, new[] { Volo("AZA123", 42.0, 12.0, 24_000) });

        // ⚠️ Senza il flush l'archivio si ferma alla prima scrittura: il secondo giro cambia solo minuti e
        // quote, e quelli aspettano il checkpoint (dieci minuti) o lo spegnimento.
        await _recorder.FlushAsync(_traffico, T0.AddMinutes(1));
        _db.ChangeTracker.Clear();

        var riga = await _db.AtcSessionTraffic.SingleAsync();
        Assert.Equal(FlightPhase.Airborne, riga.FirstPhase);
        Assert.Equal(FlightPhase.Airborne, riga.LastPhase);
        Assert.True(riga.SawAirborne);
        Assert.Equal(35_000, riga.EntryAltitudeFt);
        Assert.Equal(24_000, riga.ExitAltitudeFt);
        Assert.Equal(35_000, riga.MaxAltitudeFt);
    }

    [Fact]
    public async Task Una_postazione_fuori_divisione_si_archivia_ma_non_prende_traffico()
    {
        // Dal 28 agosto 2026 il poller registra anche il resto del mondo. La sessione si scrive — è archivio
        // — ma l'attribuzione non la deve nemmeno guardare: l'AoR che abbiamo è italiana, e provarci a ogni
        // giro vorrebbe dire idratare dal database centinaia di sessioni che non avranno mai una tratta.
        var estera = Atc(300, "EDDF_TWR") with { IsOutsideDivision = true };

        var esito = await Giro(T0, new[] { estera },
            new[] { Volo("DLH400", 50.03, 8.57, 3_000) });

        Assert.Equal(0, esito.Attributed);
        Assert.Equal(0, esito.WrittenLegs);

        _db.ChangeTracker.Clear();
        var riga = Assert.Single(await _db.AtcSessions.ToListAsync());
        Assert.Equal("EDDF_TWR", riga.Callsign);
        Assert.True(riga.IsOutsideDivision);
        Assert.Empty(await _db.AtcSessionTraffic.ToListAsync());
    }
}
