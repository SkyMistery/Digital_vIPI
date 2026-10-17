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

    // ---- Il centro della postazione (10 ottobre 2026) ----

    [Fact]
    public async Task Il_centro_si_scrive_all_apertura_e_poi_non_si_sposta()
    {
        // La prima volta che c'è, come posizione e frequenza: la riga dice dov'era la postazione quando si è aperta.
        await Giro(T0, Conn(100, T0, 60) with { Latitude = 41.80028, Longitude = 12.23889 });
        await Giro(T0.AddMinutes(30), Conn(100, T0, 1800) with { Latitude = 45.0, Longitude = 9.0 });
        _db.ChangeTracker.Clear();

        var s = await _db.AtcSessions.SingleAsync();
        Assert.Equal((41.80028, 12.23889), (s.Latitude, s.Longitude));
        Assert.Equal(1800, s.DurationSeconds);
    }

    [Fact]
    public async Task Una_sessione_aperta_senza_centro_lo_prende_al_primo_giro_che_lo_porta()
    {
        // È il giorno del carico: le sessioni già aperte sono nate senza la colonna, e si riempiono da sole.
        await Giro(T0, Conn(100, T0, 60));
        _db.ChangeTracker.Clear();
        Assert.Null((await _db.AtcSessions.AsNoTracking().SingleAsync()).Latitude);

        await Giro(T0.AddMinutes(1), Conn(100, T0, 120) with { Latitude = 41.80028, Longitude = 12.23889 });
        _db.ChangeTracker.Clear();

        var s = await _db.AtcSessions.SingleAsync();
        Assert.Equal((41.80028, 12.23889), (s.Latitude, s.Longitude));
    }

    [Fact]
    public async Task Mezza_coordinata_non_si_scrive()
    {
        // In coppia o niente: una latitudine senza longitudine è un punto che non esiste.
        await Giro(T0, Conn(100, T0, 60) with { Latitude = 41.80028 });
        _db.ChangeTracker.Clear();

        var s = await _db.AtcSessions.SingleAsync();
        Assert.Equal(((double?)null, (double?)null), (s.Latitude, s.Longitude));
    }

    [Fact]
    public async Task Quando_sparisce_dalla_frequenza_la_sessione_si_chiude()
    {
        await Giro(T0, Conn(100, T0, 60));
        await Giro(T0.AddHours(2));                  // nessuno online
        _db.ChangeTracker.Clear();

        // 🔴 T-032 (13-set-2026): si chiude all'ULTIMO AVVISTAMENTO (inizio + 60 s), non all'istante del giro che
        // non la vede più — due ore di fermo non sono due ore di frequenza.
        var s = await _db.AtcSessions.SingleAsync();
        Assert.Equal(T0.AddSeconds(60).UtcDateTime, s.EndUtc);
    }

    [Fact]
    public async Task Chi_si_riconnette_dopo_una_caduta_finisce_nello_stesso_turno()
    {
        await Giro(T0, Conn(100, T0, 3600));
        await Giro(T0.AddHours(1).AddMinutes(1));                     // cade: chiusa all'ultimo avvistamento (T0+1h)
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

    /// <summary>
    /// 🔴 U-026 (revisione totale 3): una sessione chiusa dal poller all'ultimo avvistamento (un whazzup che per
    /// venti minuti la omette) e ricomparsa con lo STESSO id dopo più di 15 minuti non è fra le «aperte o finite da
    /// poco»: il piano la dà per nuova, e la scrittura faceva `Add` su una chiave già in archivio. Il `SaveChanges`
    /// unico cadeva, e con lui le righe di TUTTI gli altri — ogni minuto, finché quel controllore restava connesso.
    /// </summary>
    [Fact]
    public async Task Una_sessione_che_ricompare_dopo_mezz_ora_si_riapre_e_non_ferma_le_altre()
    {
        await Giro(T0, Conn(100, T0, 60));
        await Giro(T0.AddMinutes(5));                                   // sparisce: chiusa a T0+60s
        await Giro(T0.AddMinutes(40),                                   // ricompare dopo 35 minuti
            Conn(100, T0, 40 * 60),
            Conn(200, T0.AddMinutes(39), 60, callsign: "LIRR_CTR", vid: 111111));
        _db.ChangeTracker.Clear();

        var s = await _db.AtcSessions.SingleAsync(x => x.SessionId == 100);
        Assert.Null(s.EndUtc);                          // riaperta
        Assert.Equal(40 * 60, s.DurationSeconds);
        Assert.Equal(100, s.ShiftKey);                  // il turno resta il suo
        Assert.True(await _db.AtcSessions.AnyAsync(x => x.SessionId == 200));   // e l'altra è scritta
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
