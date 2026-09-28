using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Stats;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// La passata una tantum che rifà lo storico delle statistiche (revisione 3, U-218 e U-228; scelta del committente
/// del 28 settembre 2026): turni ricuciti sulle sovrapposizioni brevi, giorni aeroporto rimessi in coda senza
/// cancellarli, e dalla seconda volta niente.
/// </summary>
public class StoricoStatisticheTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private EfStatsMaintenance Manutenzione() =>
        new(_db, new EfAtcSessionStore(_db), new EfImportStateStore(_db));

    private static AtcSession Sessione(long id, DateTime inizio, DateTime fine) => new()
    {
        SessionId = id, UserId = 704798, Callsign = "LIRF_TWR", StartUtc = inizio, EndUtc = fine,
        DurationSeconds = (int)(fine - inizio).TotalSeconds, ShiftKey = id, UpdatedAtUtc = DateTime.UtcNow,
    };

    [Fact]
    public async Task Rifa_i_turni_e_rimette_in_coda_i_giorni_una_volta_sola()
    {
        var t0 = DateTime.UtcNow.Date.AddDays(-10).AddHours(18);
        // La riconnessione parte due secondi prima che la sorgente chiudesse la caduta: due turni per sempre.
        _db.AtcSessions.AddRange(Sessione(1, t0, t0.AddMinutes(30)), Sessione(2, t0.AddMinutes(30).AddSeconds(-2), t0.AddHours(1)));
        var giorno = DateTime.UtcNow.Date.AddDays(-10);
        _db.AirportDayTraffic.Add(new AirportDayTraffic
        {
            Icao = "LIRF", Day = giorno, Inbound = 7, Outbound = 5, FetchedUtc = giorno.AddDays(1).AddHours(3),
        });
        await _db.SaveChangesAsync();

        var fatto = await Manutenzione().RifaiStoricoAsync();

        Assert.Equal(new StoricoRifatto(1, 1), fatto);
        _db.ChangeTracker.Clear();
        Assert.Equal(1, (await _db.AtcSessions.SingleAsync(s => s.SessionId == 2)).ShiftKey);
        var riga = await _db.AirportDayTraffic.SingleAsync();
        Assert.Equal(7, riga.Inbound);                                   // la riga resta, col conto di prima
        Assert.True(riga.FetchedUtc < giorno.AddDays(1) + AirportRollupPlanner.Assestamento);   // e si ripassa

        // Seconda consegna: il registro la ferma.
        _db.AirportDayTraffic.Single().FetchedUtc = giorno.AddDays(2);
        await _db.SaveChangesAsync();
        Assert.Equal(StoricoRifatto.Niente, await Manutenzione().RifaiStoricoAsync());
        _db.ChangeTracker.Clear();
        Assert.Equal(giorno.AddDays(2), (await _db.AirportDayTraffic.SingleAsync()).FetchedUtc);
    }
}
