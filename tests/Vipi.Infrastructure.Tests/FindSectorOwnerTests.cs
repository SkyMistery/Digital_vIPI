using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>Guard anti-collisione dell'aggiunta manuale di settori esteri: FindSectorOwnerAsync riconosce il
/// proprietario di un callsign già catalogato (AccSector o AirportSector) e ritorna null se è libero.</summary>
public class FindSectorOwnerTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        var lirr = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        _db.Accs.Add(lirr);
        _db.Accs.Add(new Acc { Code = "LGGG", Name = "Athinai", CountryPrefix = "LG", IsForeign = true });
        _db.Airports.Add(new Airport { Icao = "LIRP", Name = "Pisa", Acc = lirr });
        _db.AccSectors.Add(new AccSector { ComposePosition = "LGGG_N_CTR", CenterId = "LGGG", Position = "CTR" });
        _db.AirportSectors.Add(new AirportSector { ComposePosition = "LIRP_APP", AirportIcao = "LIRP", AccCode = "LIRR", Position = "APP", IsHidden = true });
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private EfNeighbourRepository Repo() => new(_db, new AiracService());

    [Fact]
    public async Task Returns_owner_from_AccSector()
    {
        var o = await Repo().FindSectorOwnerAsync("LGGG_N_CTR");
        Assert.NotNull(o);
        Assert.Equal("LGGG", o!.AccCode);
        Assert.False(o.IsHidden);
    }

    [Fact]
    public async Task Returns_owner_from_AirportSector_with_hidden_flag()
    {
        var o = await Repo().FindSectorOwnerAsync("LIRP_APP");
        Assert.NotNull(o);
        Assert.Equal("LIRR", o!.AccCode);   // ACC di competenza dell'aeroporto
        Assert.True(o.IsHidden);
    }

    [Fact]
    public async Task Returns_null_when_callsign_free()
    {
        Assert.Null(await Repo().FindSectorOwnerAsync("LGKR_APP"));
    }
}
