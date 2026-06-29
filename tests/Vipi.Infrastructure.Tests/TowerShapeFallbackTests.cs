using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Fallback shape tonda 5 NM per le TWR senza poligono: genera SOLO sulle TWR vuote ("[]"), marca sintetica, mai
/// sovrascrive una shape reale. Il centro è la coord aeroporto persistita all'import dal dettaglio postazione
/// (blocco "airport"); ripiego sul centro del poligono di un settore fratello se la coord manca.
/// </summary>
public class TowerShapeFallbackTests : IAsyncLifetime
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

        var acc = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        _db.Accs.Add(acc);
        _db.Airports.Add(new Airport { Icao = "LIRN", Name = "Napoli", Acc = acc });
        _db.Airports.Add(new Airport { Icao = "LIRP", Name = "Pisa", Acc = acc });
        await _db.SaveChangesAsync();
        _repo = new EfAirportSectorRepository(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    // SourceAtcPosition: (Callsign, Frequency, Position, MiddleIdentifier, RegionMapPolygon, Lower, Upper, AirportLat, AirportLon)
    [Fact]
    public async Task Applies_Circle_Only_To_Empty_Twr_And_Persists_Airport_Coords_From_Detail()
    {
        // LIRN_TWR con "[]" e coord aeroporto dal dettaglio; LIRP_TWR con shape reale.
        await _repo.ImportForAirportAsync("LIRN", new[]
        {
            new SourceAtcPosition("LIRN_TWR", "118.300", "TWR", null, "[]", null, null, 40.886, 14.291),
            new SourceAtcPosition("LIRN_GND", "121.900", "GND", null, null, null, null, 40.886, 14.291),
        });
        await _repo.ImportForAirportAsync("LIRP", new[]
        {
            new SourceAtcPosition("LIRP_TWR", "118.300", "TWR", null, "[[10.0,43.0],[10.1,43.0],[10.1,43.1]]", null, null, 43.68, 10.39),
        });

        var svc = new TowerShapeFallbackService(_repo);
        Assert.Equal(1, await svc.ApplyAsync());

        var lirn = await _db.AirportSectors.AsNoTracking().SingleAsync(s => s.ComposePosition == "LIRN_TWR");
        Assert.True(lirn.IsShapeSynthetic);
        Assert.False(string.IsNullOrWhiteSpace(lirn.RegionMapPolygon));

        var lirp = await _db.AirportSectors.AsNoTracking().SingleAsync(s => s.ComposePosition == "LIRP_TWR");
        Assert.False(lirp.IsShapeSynthetic);
        Assert.Equal("[[10.0,43.0],[10.1,43.0],[10.1,43.1]]", lirp.RegionMapPolygon);

        // Coord aeroporto persistite dall'import (dal blocco "airport" del dettaglio).
        var napoli = await _db.Airports.AsNoTracking().SingleAsync(a => a.Icao == "LIRN");
        Assert.Equal(40.886, napoli.Latitude!.Value, 3);
        Assert.Equal(14.291, napoli.Longitude!.Value, 3);
    }

    [Fact]
    public async Task Falls_Back_To_Sibling_Polygon_Center_When_No_Airport_Coords()
    {
        // Nessuna coord aeroporto (dettaglio senza "airport"): centro dal poligono dell'APP (settore fratello).
        await _repo.ImportForAirportAsync("LIRN", new[]
        {
            new SourceAtcPosition("LIRN_TWR", "118.300", "TWR", null, "[]", null, null),
            new SourceAtcPosition("LIRN_US0_APP", "124.350", "APP", "US0",
                "[[14.2,40.8],[14.4,40.8],[14.4,41.0],[14.2,41.0]]", null, null),
        });

        var svc = new TowerShapeFallbackService(_repo);
        Assert.Equal(1, await svc.ApplyAsync());

        var twr = await _db.AirportSectors.AsNoTracking().SingleAsync(s => s.ComposePosition == "LIRN_TWR");
        Assert.True(twr.IsShapeSynthetic);
        Assert.False(string.IsNullOrWhiteSpace(twr.RegionMapPolygon));
    }

    [Fact]
    public async Task Is_Idempotent_No_Double_Generation()
    {
        await _repo.ImportForAirportAsync("LIRN", new[]
        {
            new SourceAtcPosition("LIRN_TWR", "118.300", "TWR", null, "[]", null, null, 40.886, 14.291),
        });
        var svc = new TowerShapeFallbackService(_repo);

        Assert.Equal(1, await svc.ApplyAsync());
        Assert.Equal(0, await svc.ApplyAsync());   // ora ha già la shape → niente di vuoto
    }
}
