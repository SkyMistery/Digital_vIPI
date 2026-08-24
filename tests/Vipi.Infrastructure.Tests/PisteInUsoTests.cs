using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Stats;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Le piste in uso, dalla frase dell'ATIS alla riga in archivio.
///
/// <para>⚠️ È una <b>sequenza</b>, non un valore: le configurazioni cambiano durante il turno, e scrivere
/// quella del primo giro come «la pista della sessione» sarebbe falso per metà turno (nota del committente).
/// Le frasi qui sotto sono quelle vere del whazzup del 24 agosto 2026.</para>
/// </summary>
public class PisteInUsoTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfAtcSessionStore _store = default!;

    private static readonly DateTimeOffset T0 = new(2026, 8, 24, 13, 0, 0, TimeSpan.Zero);

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _store = new EfAtcSessionStore(_db);

        _db.AtcSessions.Add(new AtcSession
        {
            SessionId = 100, UserId = 704798, Callsign = "LIRF_TWR", StartUtc = T0.UtcDateTime,
            DurationSeconds = 3600, Source = AtcSessionSource.Live, ShiftKey = 100,
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    /// <summary>Un giro di poll: legge l'ATIS e scrive solo se la configurazione è cambiata.</summary>
    private async Task<bool> Giro(string atis, int minuti)
    {
        var piste = AtisRunways.Leggi(new[] { atis });
        return await _store.AppendRunwayAsync(100, piste.Arrival, piste.Departure, T0.AddMinutes(minuti));
    }

    private const string Configurazione16 =
        "This is Fiumicino ATIS arrival and departure information CHARLIE at 1310. " +
        "Arrival runway 16L 16R departure runway 25 Transition level 70";

    private const string Configurazione34 =
        "This is Fiumicino ATIS arrival and departure information GOLF at 1520. " +
        "Arrival runway 34L departure runway 34R Transition level 70";

    [Fact]
    public async Task Il_primo_giro_scrive_la_configurazione()
    {
        Assert.True(await Giro(Configurazione16, 0));
        _db.ChangeTracker.Clear();

        var r = await _db.AtcSessionRunways.SingleAsync();
        Assert.Equal("16L/16R", r.Arrival);
        Assert.Equal("25", r.Departure);
        Assert.Equal(T0.UtcDateTime, r.FromUtc);
    }

    [Fact]
    public async Task I_giri_successivi_uguali_non_scrivono_niente()
    {
        await Giro(Configurazione16, 0);
        Assert.False(await Giro(Configurazione16, 1));
        Assert.False(await Giro(Configurazione16, 2));

        // Un turno di tre ore non deve lasciare 180 righe: ne lascia una.
        Assert.Equal(1, await _db.AtcSessionRunways.CountAsync());
    }

    [Fact]
    public async Task Il_cambio_di_pista_a_turno_in_corso_lascia_la_sua_riga()
    {
        await Giro(Configurazione16, 0);
        await Giro(Configurazione16, 60);
        Assert.True(await Giro(Configurazione34, 130));
        _db.ChangeTracker.Clear();

        var righe = await _db.AtcSessionRunways.OrderBy(r => r.FromUtc).ToListAsync();
        Assert.Equal(2, righe.Count);
        Assert.Equal("16L/16R", righe[0].Arrival);
        Assert.Equal("34L", righe[1].Arrival);
        Assert.Equal(T0.AddMinutes(130).UtcDateTime, righe[1].FromUtc);
    }

    [Fact]
    public async Task Una_configurazione_che_TORNA_e_un_cambio_e_si_registra()
    {
        // 16 → 34 → 16 sono tre righe: la sequenza racconta il turno, non l'insieme delle piste viste.
        await Giro(Configurazione16, 0);
        await Giro(Configurazione34, 60);
        Assert.True(await Giro(Configurazione16, 120));

        Assert.Equal(3, await _db.AtcSessionRunways.CountAsync());
    }

    [Fact]
    public async Task Il_dettaglio_sessione_mostra_la_sequenza_in_ordine()
    {
        await Giro(Configurazione16, 0);
        await Giro(Configurazione34, 90);
        _db.ChangeTracker.Clear();

        var d = await new EfAtcStatsQueries(_db).SessionAsync(100);

        Assert.NotNull(d);
        Assert.Equal(2, d!.Runways.Count);
        Assert.Equal("16L/16R", d.Runways[0].Arrival);
        Assert.Equal("34L", d.Runways[1].Arrival);
        Assert.True(d.Runways[1].FromUtc > d.Runways[0].FromUtc);
    }

    [Fact]
    public async Task Una_sessione_che_non_esiste_non_si_porta_dietro_righe_orfane()
    {
        // Capita davvero: il poller vede l'ATIS nel giro in cui la sessione nasce, prima che sia scritta.
        var piste = AtisRunways.Leggi(new[] { Configurazione16 });
        Assert.False(await _store.AppendRunwayAsync(999, piste.Arrival, piste.Departure, T0));
        Assert.Equal(0, await _db.AtcSessionRunways.CountAsync());
    }

    [Fact]
    public async Task Potando_la_sessione_spariscono_anche_le_sue_piste()
    {
        await Giro(Configurazione16, 0);
        _db.ChangeTracker.Clear();

        _db.AtcSessions.Remove(await _db.AtcSessions.SingleAsync());
        await _db.SaveChangesAsync();

        Assert.Equal(0, await _db.AtcSessionRunways.CountAsync());
    }
}
