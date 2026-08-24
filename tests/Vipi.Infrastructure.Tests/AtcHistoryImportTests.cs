using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Stats;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il backfill dello storico: riempie i dodici mesi, mette la fine <b>vera</b> alle sessioni che il poller
/// aveva chiuso a occhio, e ricostruisce i turni su una sequenza che arriva alla rinfusa.
/// </summary>
public class AtcHistoryImportTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfAtcSessionStore _store = default!;

    private static readonly DateTimeOffset T0 = new(2026, 8, 20, 18, 0, 0, TimeSpan.Zero);

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _store = new EfAtcSessionStore(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private static SourceAtcSessionHistory Storica(long id, DateTimeOffset inizio, int secondi,
        DateTimeOffset? fine = null, string callsign = "LIRF_TWR", int vid = 704798) =>
        new(id, vid, callsign, 4, inizio, fine ?? inizio.AddSeconds(secondi), secondi);

    /// <summary>Sorgente finta che risponde solo sui prefissi che le si danno.</summary>
    private sealed class SorgenteFinta : IAtcHistorySource
    {
        private readonly Dictionary<string, List<SourceAtcSessionHistory>> _per;
        public int Chiamate { get; private set; }

        public SorgenteFinta(Dictionary<string, List<SourceAtcSessionHistory>> per) => _per = per;

        public Task<IReadOnlyList<SourceAtcSessionHistory>> GetAtcSessionsAsync(
            string callsignPrefix, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
        {
            Chiamate++;
            var trovate = _per.TryGetValue(callsignPrefix, out var v)
                ? v.Where(s => s.StartUtc >= from && s.StartUtc <= to).ToList()
                : new List<SourceAtcSessionHistory>();
            return Task.FromResult<IReadOnlyList<SourceAtcSessionHistory>>(trovate);
        }
    }

    private AtcHistoryImportUseCase UseCase(params (string Prefisso, SourceAtcSessionHistory[] Sessioni)[] dati) =>
        new(new SorgenteFinta(dati.ToDictionary(d => d.Prefisso, d => d.Sessioni.ToList())), _store);

    [Fact]
    public async Task Le_sessioni_storiche_arrivano_in_archivio_con_la_loro_fine()
    {
        var uc = UseCase(("LIR", new[] { Storica(100, T0, 3600) }));

        var esito = await uc.RunAsync(T0.AddDays(-1), T0.AddDays(1));

        Assert.Equal(1, esito.Created);
        _db.ChangeTracker.Clear();

        var s = await _db.AtcSessions.SingleAsync();
        Assert.Equal(AtcSessionSource.Backfill, s.Source);
        Assert.Equal(3600, s.DurationSeconds);
        Assert.Equal(T0.AddSeconds(3600).UtcDateTime, s.EndUtc);
        Assert.Equal("TWR", s.Position);        // ricavata dal callsign: la lista non porta atcSession
    }

    [Fact]
    public async Task Su_una_sessione_gia_vista_dal_vivo_lo_storico_corregge_solo_la_coda()
    {
        // Il poller l'aveva vista e chiusa a occhio al giro delle 18:40, con la posizione e la frequenza.
        _db.AtcSessions.Add(new AtcSession
        {
            SessionId = 100, UserId = 704798, Callsign = "LIRF_TWR", Position = "TWR", Frequency = "118.700",
            StartUtc = T0.UtcDateTime, EndUtc = T0.AddMinutes(40).UtcDateTime, DurationSeconds = 2400,
            Source = AtcSessionSource.Live, ShiftKey = 100, TrafficCount = 7, MovementCount = 5,
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        // La sorgente sa che ha staccato alle 19:00 dopo 3600 secondi.
        var esito = await UseCase(("LIR", new[] { Storica(100, T0, 3600) })).RunAsync(T0.AddDays(-1), T0.AddDays(1));

        Assert.Equal(1, esito.Updated);
        _db.ChangeTracker.Clear();

        var s = await _db.AtcSessions.SingleAsync();
        Assert.Equal(T0.AddSeconds(3600).UtcDateTime, s.EndUtc);      // coda corretta
        Assert.Equal(3600, s.DurationSeconds);
        Assert.Equal(AtcSessionSource.Live, s.Source);                // non declassata
        Assert.Equal("118.700", s.Frequency);                         // e non ha perso quel che sapeva
        Assert.Equal(7, s.TrafficCount);                              // né il traffico gia' attribuito
    }

    [Fact]
    public async Task I_turni_si_ricostruiscono_su_una_sequenza_arrivata_alla_rinfusa()
    {
        // Tre spezzoni dello stesso turno, consegnati in ordine sparso come fa la sorgente.
        var uc = UseCase(("LIR", new[]
        {
            Storica(102, T0.AddMinutes(105), 4500, T0.AddMinutes(180)),
            Storica(100, T0, 3000, T0.AddMinutes(50)),
            Storica(101, T0.AddMinutes(52), 2880, T0.AddMinutes(100)),
        }));

        var esito = await uc.RunAsync(T0.AddDays(-1), T0.AddDays(1));
        Assert.True(esito.ShiftsFixed > 0);
        _db.ChangeTracker.Clear();

        var sessioni = await _db.AtcSessions.OrderBy(s => s.SessionId).ToListAsync();
        Assert.All(sessioni, s => Assert.Equal(100, s.ShiftKey));
    }

    [Fact]
    public async Task Controllori_diversi_restano_turni_diversi()
    {
        var uc = UseCase(("LIR", new[]
        {
            Storica(100, T0, 3000, T0.AddMinutes(50), vid: 704798),
            Storica(101, T0.AddMinutes(52), 2880, T0.AddMinutes(100), vid: 762032),
        }));

        await uc.RunAsync(T0.AddDays(-1), T0.AddDays(1));
        _db.ChangeTracker.Clear();

        var sessioni = await _db.AtcSessions.OrderBy(s => s.SessionId).ToListAsync();
        Assert.Equal(new long[] { 100, 101 }, sessioni.Select(s => s.ShiftKey));
    }

    [Fact]
    public async Task Il_giro_interroga_tutti_i_prefissi_italiani_e_non_il_mondo()
    {
        var sorgente = new SorgenteFinta(new Dictionary<string, List<SourceAtcSessionHistory>>());
        await new AtcHistoryImportUseCase(sorgente, _store).RunAsync(T0.AddDays(-30), T0);

        Assert.Equal(23, sorgente.Chiamate);                       // LIA…LIZ
        Assert.Contains("LIR", AtcHistoryImportUseCase.ItalianPrefixes);
        Assert.DoesNotContain("LI", AtcHistoryImportUseCase.ItalianPrefixes);   // due lettere danno zero
    }

    [Fact]
    public async Task Rieseguire_lo_stesso_giro_non_duplica_niente()
    {
        var dati = new[] { Storica(100, T0, 3600), Storica(101, T0.AddHours(2), 1800) };

        await UseCase(("LIR", dati)).RunAsync(T0.AddDays(-1), T0.AddDays(1));
        var secondo = await UseCase(("LIR", dati)).RunAsync(T0.AddDays(-1), T0.AddDays(1));

        Assert.Equal(0, secondo.Created);
        Assert.Equal(2, secondo.Updated);
        Assert.Equal(2, await _db.AtcSessions.CountAsync());
    }

    [Fact]
    public async Task Una_sessione_fuori_finestra_non_entra()
    {
        var uc = UseCase(("LIR", new[] { Storica(100, T0.AddDays(-400), 3600) }));   // oltre la retention
        var esito = await uc.RunAsync(T0.AddDays(-365), T0);

        Assert.Equal(0, esito.Fetched);
        Assert.Equal(0, await _db.AtcSessions.CountAsync());
    }
}
