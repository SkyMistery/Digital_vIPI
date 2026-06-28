using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>CRUD trasferimenti + round-trip della catena handler (array JSON ordinato).</summary>
public class TransferRepositoryTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfTransferRepository _repo = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        await RomaStructureSeed.SeedAsync(_db);
        _repo = new EfTransferRepository(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private static TransferInput Input(string[] chain) => new()
    {
        RelationKey = "LIRR-LIMM", RelationLabel = "Roma ↔ Milano", Phase = TransferPhase.Arrival,
        AirportIcao = "LIMC", Cop = "DEVOX", FlRule = "FL250↑", HandlerChain = chain, StandardFallback = "UNICOM",
    };

    [Fact]
    public async Task Add_Preserves_Ordered_Chain()
    {
        await _repo.AddAsync("LIRR", Input(new[] { "ES2", "WS2" }));

        var rows = await _repo.ListByAccAsync("LIRR");
        var row = Assert.Single(rows);
        Assert.Equal(new[] { "ES2", "WS2" }, row.HandlerChain); // ordine preservato
        Assert.Equal("UNICOM", row.StandardFallback);
    }

    [Fact]
    public async Task Update_And_Delete()
    {
        var id = await _repo.AddAsync("LIRR", Input(new[] { "WS2" }));

        await _repo.UpdateAsync("LIRR", id, Input(new[] { "ES2", "WS2", "CE1" }));
        var updated = (await _repo.ListByAccAsync("LIRR")).Single();
        Assert.Equal(3, updated.HandlerChain.Count);

        await _repo.DeleteAsync("LIRR", id);
        Assert.Empty(await _repo.ListByAccAsync("LIRR"));
    }

    [Fact]
    public async Task Seed_Populates_Demo_Transfers()
    {
        await RomaTransferSeed.SeedAsync(_db);
        var rows = await _repo.ListByAccAsync("LIRR");
        Assert.NotEmpty(rows);
        Assert.Contains(rows, r => r.Cop == "DEVOX" && r.HandlerChain.SequenceEqual(new[] { "ES2", "WS2" }));
    }
}
