using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Stats;
using Vipi.Infrastructure.Persistence;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il giro completo di un poll, contro un database vero (SQLite in memoria): fotografia → piano → scrittura.
/// È qui che si vede se le sessioni si aprono, si aggiornano, si chiudono e si ritrovano in un turno solo.
/// </summary>
public class AtcSessionStoreTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfAtcSessionStore _store = default!;

    private static readonly DateTimeOffset T0 = new(2026, 8, 24, 18, 0, 0, TimeSpan.Zero);

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

    private static SourceAtcConnection Conn(long id, DateTimeOffset start, int secondi,
        string callsign = "LIRF_TWR", int vid = 704798) =>
        new(id, vid, callsign, "TWR", "118.700", 4, start, secondi);

    /// <summary>Un giro di poll come lo fa il servizio: leggi ciò che serve, decidi, scrivi.</summary>
    private async Task<int> Giro(DateTimeOffset ora, params SourceAtcConnection[] online)
    {
        var known = await _store.GetOpenOrRecentAsync(ora - AtcSessionSync.ShiftGap);
        return await _store.ApplyAsync(AtcSessionSync.Plan(online, known, ora));
    }

    [Fact]
    public async Task Un_giro_apre_la_sessione_e_il_secondo_la_aggiorna()
    {
        await Giro(T0, Conn(100, T0, 60));
        await Giro(T0.AddMinutes(30), Conn(100, T0, 1800));
        _db.ChangeTracker.Clear();

        var s = await _db.AtcSessions.SingleAsync();
        Assert.Equal(1800, s.DurationSeconds);      // aggiornata, non duplicata
        Assert.Null(s.EndUtc);
        Assert.Equal(100, s.ShiftKey);
        Assert.Equal("118.700", s.Frequency);
    }

    [Fact]
    public async Task Quando_sparisce_dalla_frequenza_la_sessione_si_chiude()
    {
        await Giro(T0, Conn(100, T0, 60));
        var fine = T0.AddHours(2);
        await Giro(fine);                            // nessuno online
        _db.ChangeTracker.Clear();

        var s = await _db.AtcSessions.SingleAsync();
        Assert.Equal(fine.UtcDateTime, s.EndUtc);
    }

    [Fact]
    public async Task Chi_si_riconnette_dopo_una_caduta_finisce_nello_stesso_turno()
    {
        await Giro(T0, Conn(100, T0, 60));
        await Giro(T0.AddHours(1));                                   // cade: sessione chiusa
        await Giro(T0.AddHours(1).AddMinutes(3),
                   Conn(101, T0.AddHours(1).AddMinutes(3), 60));      // rientra
        _db.ChangeTracker.Clear();

        var sessioni = await _db.AtcSessions.OrderBy(x => x.SessionId).ToListAsync();
        Assert.Equal(2, sessioni.Count);
        Assert.All(sessioni, s => Assert.Equal(100, s.ShiftKey));     // un turno solo
    }

    [Fact]
    public async Task Dopo_una_pausa_lunga_il_turno_e_un_altro()
    {
        await Giro(T0, Conn(100, T0, 60));
        await Giro(T0.AddHours(1));
        await Giro(T0.AddHours(5), Conn(101, T0.AddHours(5), 60));
        _db.ChangeTracker.Clear();

        var sessioni = await _db.AtcSessions.OrderBy(x => x.SessionId).ToListAsync();
        Assert.Equal(new long[] { 100, 101 }, sessioni.Select(s => s.ShiftKey));
    }

    [Fact]
    public async Task Una_sessione_chiusa_per_un_poll_perso_si_riapre_invece_di_sdoppiarsi()
    {
        // Il poller salta un giro (rete, riavvio): chiude. Poi l'ATC è ancora lì con lo stesso id IVAO.
        await Giro(T0, Conn(100, T0, 60));
        await Giro(T0.AddMinutes(2));                          // chiusa per sbaglio
        await Giro(T0.AddMinutes(3), Conn(100, T0, 180));      // era in frequenza tutto il tempo
        _db.ChangeTracker.Clear();

        var s = await _db.AtcSessions.SingleAsync();
        Assert.Null(s.EndUtc);                                  // riaperta
        Assert.Equal(180, s.DurationSeconds);
        Assert.Equal(100, s.ShiftKey);
    }

    [Fact]
    public async Task Il_giro_a_vuoto_non_scrive_niente()
    {
        Assert.Equal(0, await Giro(T0));
        Assert.Equal(0, await _db.AtcSessions.CountAsync());
    }

    [Fact]
    public async Task Le_sessioni_vecchie_non_vengono_rilette_a_ogni_giro()
    {
        // La lettura del poller guarda le aperte e le finite da poco: una sessione chiusa ieri non deve
        // rientrare, o il costo del giro cresce con l'archivio invece di restare costante.
        await Giro(T0, Conn(100, T0, 60));
        await Giro(T0.AddMinutes(10));

        var recenti = await _store.GetOpenOrRecentAsync(T0.AddDays(1) - AtcSessionSync.ShiftGap);
        Assert.Empty(recenti);
    }

    [Fact]
    public async Task Due_postazioni_insieme_sono_due_sessioni_e_due_turni()
    {
        await Giro(T0, Conn(100, T0, 60), Conn(101, T0, 60, callsign: "LIRF_GND", vid: 762032));
        _db.ChangeTracker.Clear();

        var sessioni = await _db.AtcSessions.OrderBy(x => x.SessionId).ToListAsync();
        Assert.Equal(2, sessioni.Count);
        Assert.Equal(new long[] { 100, 101 }, sessioni.Select(s => s.ShiftKey));
    }
}
