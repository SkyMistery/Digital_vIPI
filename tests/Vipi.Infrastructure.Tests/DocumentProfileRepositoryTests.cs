using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>Round-trip degli override data-driven di un documento (DocumentProfile): get-or-create + serializzazione.</summary>
public class DocumentProfileRepositoryTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfDocumentProfileRepository _repo = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        await RomaStructureSeed.SeedAsync(_db);
        await RomaContentSeed.SeedAsync(_db);
        _repo = new EfDocumentProfileRepository(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    [Fact]
    public async Task Get_On_Missing_Returns_Empty()
    {
        var docId = await _db.Documents.Select(d => d.Id).FirstAsync();
        var data = await _repo.GetAsync(docId);
        Assert.Empty(data.FreqOrder);
        Assert.Empty(data.FreqLinkSectorIds);
    }

    [Fact]
    public async Task Save_Then_Get_RoundTrips_All_Overrides()
    {
        var docId = await _db.Documents.Select(d => d.Id).FirstAsync();

        await _repo.SaveFreqOrderAsync(docId, new[] { new AppFreqOrderOverride("LIRP_APP", 0), new AppFreqOrderOverride("LIRP_TWR", 1) });
        await _repo.SaveFreqLinksAsync(docId, new[] { 42, 7 });

        var data = await _repo.GetAsync(docId);
        Assert.Equal(new[] { 42, 7 }, data.FreqLinkSectorIds);
        Assert.Equal(2, data.FreqOrder.Count);
        Assert.Equal("LIRP_APP", data.FreqOrder[0].Callsign);

        // Una sola riga per documento (get-or-create, non duplica).
        Assert.Equal(1, await _db.DocumentProfiles.CountAsync(p => p.DocumentId == docId));
    }
}
