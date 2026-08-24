using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Stats;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il riempimento retroattivo del traffico d'aeroporto: quali sessioni prende, a chi assegna i movimenti
/// quando due postazioni erano insieme, e perché non ci riprova all'infinito.
/// </summary>
public class AirportTrafficBackfillTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfAtcTrafficStore _store = default!;

    private static readonly DateTimeOffset T0 = new(2026, 8, 20, 18, 0, 0, TimeSpan.Zero);

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

    private async Task Sessione(long id, string callsign, int daMinuto, int aMinuto, int traffico = 0)
    {
        _db.AtcSessions.Add(new AtcSession
        {
            SessionId = id, UserId = 704798, Callsign = callsign,
            StartUtc = T0.AddMinutes(daMinuto).UtcDateTime, EndUtc = T0.AddMinutes(aMinuto).UtcDateTime,
            DurationSeconds = (aMinuto - daMinuto) * 60, Source = AtcSessionSource.Backfill, ShiftKey = id,
            TrafficCount = traffico,
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    /// <summary>Sorgente finta: movimenti a piacere, gli stessi per qualunque aeroporto e finestra.</summary>
    private sealed class SorgenteFinta : IAirportTrafficSource
    {
        private readonly SourceAirportMovement[] _mov;
        public List<string> Interrogati { get; } = new();

        public SorgenteFinta(params SourceAirportMovement[] mov) =>
            _mov = mov.Length > 0 ? mov : new[]
            {
                new SourceAirportMovement(AirportMovementKind.Inbound, "AZA123", 785031, 900, "LEPA", "LIRF", "BCS3"),
                new SourceAirportMovement(AirportMovementKind.Outbound, "RYR456", 785032, 901, "LIRF", "EBBR", "B738"),
            };

        public Task<IReadOnlyList<SourceAirportMovement>> GetMovementsAsync(
            string icao, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
        {
            Interrogati.Add(icao);
            return Task.FromResult<IReadOnlyList<SourceAirportMovement>>(_mov);
        }
    }

    private (AirportTrafficBackfillUseCase Uc, SorgenteFinta Sorgente) Caso(params SourceAirportMovement[] mov)
    {
        var s = new SorgenteFinta(mov);
        return (new AirportTrafficBackfillUseCase(s, _store, new EfImportPolicyStore(_db)), s);
    }

    [Fact]
    public async Task I_movimenti_diventano_tratte_marcate_come_ricostruite()
    {
        await Sessione(100, "LIRF_TWR", 0, 120);
        var (uc, sorgente) = Caso();

        var esito = await uc.RunAsync(T0.AddDays(-1), 50, T0.AddDays(1));

        Assert.Equal(1, esito.Filled);
        Assert.Equal(2, esito.Movements);
        Assert.Equal(new[] { "LIRF" }, sorgente.Interrogati);
        _db.ChangeTracker.Clear();

        var righe = await _db.AtcSessionTraffic.ToListAsync();
        Assert.Equal(2, righe.Count);
        Assert.All(righe, r => Assert.Equal(TrafficOrigin.AirportApi, r.Origin));
        Assert.All(righe, r => Assert.True(r.SawMovement));
        // ⚠️ I minuti restano a zero: la sorgente dice CHE il volo c'è stato, non per quanto.
        Assert.All(righe, r => Assert.Equal(0, r.SeenMinutes));

        var s = await _db.AtcSessions.SingleAsync();
        Assert.Equal(2, s.TrafficCount);
        Assert.Equal(2, s.MovementCount);
        Assert.NotNull(s.TrafficFilledUtc);
    }

    [Fact]
    public async Task Fra_torre_e_ground_insieme_i_movimenti_vanno_alla_torre_una_volta_sola()
    {
        await Sessione(100, "LIRF_TWR", 0, 120);
        await Sessione(101, "LIRF_GND", 30, 90);
        var (uc, sorgente) = Caso();

        var esito = await uc.RunAsync(T0.AddDays(-1), 50, T0.AddDays(1));

        Assert.Equal(1, esito.Filled);      // una sola chiamata
        Assert.Equal(1, esito.Skipped);     // la GND cede alla torre
        Assert.Single(sorgente.Interrogati);
        _db.ChangeTracker.Clear();

        Assert.Equal(2, await _db.AtcSessionTraffic.CountAsync(t => t.SessionId == 100));
        Assert.Equal(0, await _db.AtcSessionTraffic.CountAsync(t => t.SessionId == 101));
        // ...ma anche la GND resta marcata: «provata, i movimenti erano di un altro».
        Assert.NotNull((await _db.AtcSessions.SingleAsync(s => s.SessionId == 101)).TrafficFilledUtc);
    }

    [Fact]
    public async Task Una_sessione_che_ha_gia_traffico_dal_vivo_non_si_tocca()
    {
        await Sessione(100, "LIRF_TWR", 0, 120, traffico: 5);
        var (uc, sorgente) = Caso();

        var esito = await uc.RunAsync(T0.AddDays(-1), 50, T0.AddDays(1));

        Assert.Equal(0, esito.Examined);
        Assert.Empty(sorgente.Interrogati);
    }

    [Fact]
    public async Task Gli_ACC_non_passano_di_qui()
    {
        await Sessione(100, "LIRR_NE1_CTR", 0, 120);
        var (uc, sorgente) = Caso();

        var esito = await uc.RunAsync(T0.AddDays(-1), 50, T0.AddDays(1));

        Assert.Equal(0, esito.Examined);      // il callsign non è una posizione d'aeroporto
        Assert.Empty(sorgente.Interrogati);
    }

    [Fact]
    public async Task Una_sessione_ancora_in_corso_aspetta_di_essere_finita()
    {
        _db.AtcSessions.Add(new AtcSession
        {
            SessionId = 100, UserId = 704798, Callsign = "LIRF_TWR", StartUtc = T0.UtcDateTime, EndUtc = null,
            DurationSeconds = 1200, Source = AtcSessionSource.Live, ShiftKey = 100,
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var (uc, sorgente) = Caso();
        Assert.Equal(0, (await uc.RunAsync(T0.AddDays(-1), 50, T0.AddDays(1))).Examined);
        Assert.Empty(sorgente.Interrogati);
    }

    [Fact]
    public async Task Riprovare_il_giro_dopo_non_richiama_le_sessioni_gia_riempite()
    {
        await Sessione(100, "LIRF_TWR", 0, 120);
        var (uc, sorgente) = Caso();

        await uc.RunAsync(T0.AddDays(-1), 50, T0.AddDays(1));
        var secondo = await uc.RunAsync(T0.AddDays(-1), 50, T0.AddDays(1));

        Assert.Equal(0, secondo.Examined);
        Assert.Single(sorgente.Interrogati);      // una volta e basta
    }

    [Fact]
    public async Task Una_sessione_lampo_non_vale_una_chiamata()
    {
        _db.AtcSessions.Add(new AtcSession
        {
            SessionId = 100, UserId = 704798, Callsign = "LIRF_TWR",
            StartUtc = T0.UtcDateTime, EndUtc = T0.AddSeconds(3).UtcDateTime, DurationSeconds = 3,
            Source = AtcSessionSource.Backfill, ShiftKey = 100,
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var (uc, sorgente) = Caso();
        Assert.Equal(0, (await uc.RunAsync(T0.AddDays(-1), 50, T0.AddDays(1))).Examined);
        Assert.Empty(sorgente.Interrogati);
    }

    [Fact]
    public async Task Col_tetto_per_giro_si_smaltisce_a_scaglioni()
    {
        for (var i = 0; i < 5; i++) await Sessione(100 + i, "LIRF_TWR", i * 200, i * 200 + 100);

        var (uc, sorgente) = Caso();
        var primo = await uc.RunAsync(T0.AddDays(-1), max: 2, now: T0.AddDays(1));

        Assert.Equal(2, primo.Examined);
        Assert.Equal(2, sorgente.Interrogati.Count);

        var secondo = await uc.RunAsync(T0.AddDays(-1), max: 2, now: T0.AddDays(1));
        Assert.Equal(2, secondo.Examined);
    }

    [Fact]
    public async Task Con_le_statistiche_escluse_dalla_policy_non_si_riempie_niente()
    {
        await Sessione(100, "LIRF_TWR", 0, 120);
        await new EfImportPolicyStore(_db).SaveAsync(
            ImportPolicySnapshot.AllImported with { AtcSessions = false }, updatedByUserId: 704798);
        _db.ChangeTracker.Clear();

        var (uc, sorgente) = Caso();
        Assert.Equal(0, (await uc.RunAsync(T0.AddDays(-1), 50, T0.AddDays(1))).Examined);
        Assert.Empty(sorgente.Interrogati);
    }

    [Fact]
    public async Task Il_pilota_che_rifila_il_piano_di_volo_resta_UN_movimento()
    {
        // Difetto visto sul dato vero: `AZA1430 LIRF→LICJ` compariva due volte nella stessa sessione di
        // LICJ_TWR — un solo atterraggio contato per due. Alla riconnessione il pilota deposita un piano
        // nuovo, quindi l'id del piano NON identifica la tratta: la identificano rotta e verso.
        await Sessione(100, "LICJ_TWR", 0, 120);
        var (uc, _) = Caso(
            new SourceAirportMovement(AirportMovementKind.Inbound, "AZA1430", 785031, 900, "LIRF", "LICJ", "MD82"),
            new SourceAirportMovement(AirportMovementKind.Inbound, "AZA1430", 785031, 917, "LIRF", "LICJ", "MD82"));

        var esito = await uc.RunAsync(T0.AddDays(-1), 50, T0.AddDays(1));

        Assert.Equal(1, esito.Movements);
        _db.ChangeTracker.Clear();
        Assert.Equal(1, await _db.AtcSessionTraffic.CountAsync());
        Assert.Equal(1, (await _db.AtcSessions.SingleAsync()).MovementCount);
    }

    [Fact]
    public async Task Atterrare_e_ripartire_restano_DUE_movimenti()
    {
        await Sessione(100, "LICJ_TWR", 0, 120);
        var (uc, _) = Caso(
            new SourceAirportMovement(AirportMovementKind.Inbound, "AZA1430", 785031, 900, "LIRF", "LICJ", "MD82"),
            new SourceAirportMovement(AirportMovementKind.Outbound, "AZA1430", 785031, 901, "LICJ", "LIRF", "MD82"));

        Assert.Equal(2, (await uc.RunAsync(T0.AddDays(-1), 50, T0.AddDays(1))).Movements);
    }
}
