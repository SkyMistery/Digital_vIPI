using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Import settori ATC d'aeroporto dalla sorgente: upsert per ComposePosition (incl. APP), idempotente,
/// con default limiti GND/19500, ereditando l'ACC dall'aeroporto e preservando IsHidden + limiti admin.
/// </summary>
public class AirportSectorImportTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfAirportSectorRepository _repo = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        // Un ACC + un aeroporto di competenza (l'import eredita l'ACC dall'aeroporto).
        var acc = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        _db.Accs.Add(acc);
        _db.Airports.Add(new Airport { Icao = "LIRN", Name = "Napoli", Acc = acc });
        await _db.SaveChangesAsync();

        _repo = new EfAirportSectorRepository(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    // Postazioni di Napoli, incluso un APP (che il vecchio percorso scartava) e ATIS/GND (senza limiti).
    private static IReadOnlyList<SourceAtcPosition> Positions() => new[]
    {
        new SourceAtcPosition("LIRN_ATIS", "127.000", "ATIS", null, null, null, null),
        new SourceAtcPosition("LIRN_GND", "121.900", "GND", null, "{\"poly\":9}", null, null),  // shape ignorata per GND
        new SourceAtcPosition("LIRN_TWR", "118.300", "TWR", null, "{\"poly\":1}", null, null),
        new SourceAtcPosition("LIRN_US0_APP", "124.350", "APP", "US0", null, null, null),
    };

    [Fact]
    public async Task Import_Creates_All_Sectors_Including_App_With_Per_Type_Limits()
    {
        var r = await _repo.ImportForAirportAsync("LIRN", Positions());

        Assert.Equal(4, r.Created);
        var sectors = await _repo.ListByAirportAsync("LIRN");
        Assert.Equal(4, sectors.Count);

        // APP incluso
        var app = sectors.Single(s => s.ComposePosition == "LIRN_US0_APP");
        Assert.Equal("APP", app.Position);
        Assert.Equal("US0", app.MiddleIdentifier);

        // ACC ereditato dall'aeroporto
        Assert.All(sectors, s => Assert.Equal("LIRR", s.AccCode));

        // TWR: GND(0)→3000 ft (le torri arrivano a 3000, non a FL195) + shape; APP: GND(0)→19500
        var twr = sectors.Single(s => s.ComposePosition == "LIRN_TWR");
        Assert.Equal(0, twr.LowerLimit);
        Assert.Equal(3000, twr.UpperLimit);
        Assert.True(twr.HasPolygon);
        Assert.Equal(0, app.LowerLimit);
        Assert.Equal(19500, app.UpperLimit);

        // GND e ATIS: niente limiti, niente shape (anche se la sorgente dà un poligono per la GND)
        foreach (var compose in new[] { "LIRN_GND", "LIRN_ATIS" })
        {
            var s = sectors.Single(x => x.ComposePosition == compose);
            Assert.Null(s.LowerLimit);
            Assert.Null(s.UpperLimit);
            Assert.False(s.HasPolygon);
        }
    }

    [Fact]
    public async Task Reimport_Is_Idempotent_And_Preserves_Hidden_And_Limits()
    {
        await _repo.ImportForAirportAsync("LIRN", Positions());
        var twr = (await _repo.ListByAirportAsync("LIRN")).Single(s => s.ComposePosition == "LIRN_TWR");

        await _repo.SetLimitsAsync(twr.Id, 0, 24500);
        await _repo.SetHiddenAsync(twr.Id, true);

        var r = await _repo.ImportForAirportAsync("LIRN", Positions());

        Assert.Equal(0, r.Created);
        Assert.Equal(4, r.Updated);

        var after = (await _repo.ListByAirportAsync("LIRN")).Single(s => s.ComposePosition == "LIRN_TWR");
        Assert.Equal(0, after.LowerLimit);
        Assert.Equal(24500, after.UpperLimit);
        Assert.True(after.IsHidden);
    }

    [Fact]
    public async Task Import_Sets_Default_Primary_Per_Type_And_SetPrimary_Is_Exclusive_Within_Type()
    {
        // Due TWR per verificare l'esclusiva per tipo.
        var positions = new[]
        {
            new SourceAtcPosition("LIRN_ATIS", "127.000", "ATIS", null, null, null, null),
            new SourceAtcPosition("LIRN_GND", "121.900", "GND", null, null, null, null),
            new SourceAtcPosition("LIRN_TWR", "118.300", "TWR", null, "{\"poly\":1}", null, null),
            new SourceAtcPosition("LIRN_N_TWR", "118.800", "TWR", "N", "{\"poly\":2}", null, null),
            new SourceAtcPosition("LIRN_US0_APP", "124.350", "APP", "US0", null, null, null),
        };
        await _repo.ImportForAirportAsync("LIRN", positions);
        var sectors = await _repo.ListByAirportAsync("LIRN");

        // default: una principale PER TIPO presente (GND, TWR, APP) = 3; ATIS mai principale
        Assert.Equal(3, sectors.Count(s => s.IsPrimary));
        Assert.False(sectors.Single(s => s.ComposePosition == "LIRN_ATIS").IsPrimary);
        Assert.Single(sectors, s => s.Position == "TWR" && s.IsPrimary);

        // scelta esplicita dell'altra TWR: esclusiva SOLO tra le TWR (GND/APP intatte)
        var otherTwr = sectors.Single(s => s.Position == "TWR" && !s.IsPrimary);
        await _repo.SetPrimaryAsync(otherTwr.Id);
        var after = await _repo.ListByAirportAsync("LIRN");
        Assert.Single(after, s => s.Position == "TWR" && s.IsPrimary);
        Assert.True(after.Single(s => s.Id == otherTwr.Id).IsPrimary);
        Assert.True(after.Single(s => s.ComposePosition == "LIRN_GND").IsPrimary);
        Assert.True(after.Single(s => s.ComposePosition == "LIRN_US0_APP").IsPrimary);

        // re-import preserva la scelta
        await _repo.ImportForAirportAsync("LIRN", positions);
        var reimported = await _repo.ListByAirportAsync("LIRN");
        Assert.True(reimported.Single(s => s.Id == otherTwr.Id).IsPrimary);
    }

    [Fact]
    public async Task Import_Unknown_Airport_Does_Nothing()
    {
        var r = await _repo.ImportForAirportAsync("ZZZZ", Positions());
        Assert.Equal(0, r.Created);
        Assert.Equal(0, r.Updated);
        Assert.Empty(await _repo.ListByAirportAsync("ZZZZ"));
    }

    [Fact]
    public async Task GetAccCode_Resolvers_Work()
    {
        await _repo.ImportForAirportAsync("LIRN", Positions());
        var s = (await _repo.ListByAirportAsync("LIRN")).First();

        Assert.Equal("LIRR", await _repo.GetAccCodeByIcaoAsync("LIRN"));
        Assert.Equal("LIRR", await _repo.GetAccCodeBySectorIdAsync(s.Id));
        Assert.Null(await _repo.GetAccCodeByIcaoAsync("ZZZZ"));
        Assert.Contains("LIRN", await _repo.ListAirportIcaosAsync());
    }
}
