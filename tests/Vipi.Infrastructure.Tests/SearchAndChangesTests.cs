using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>Ricerca full-text + "Cosa è cambiato" sul DB seedato.</summary>
public class SearchAndChangesTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfSearchRepository _search = default!;
    private EfChangesRepository _changes = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        await RomaStructureSeed.SeedAsync(_db);
        await RomaContentSeed.SeedAsync(_db);
        await RomaAirportSeed.SeedAsync(_db);
        await RomaVloaSeed.SeedAsync(_db);
        _search = new EfSearchRepository(_db);
        _changes = new EfChangesRepository(_db);
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    [Fact]
    public async Task Search_Finds_Cop_In_Vipi()
    {
        var hits = await _search.SearchAsync("VALMA", SearchScope.All, 50);
        Assert.NotEmpty(hits);
        Assert.Contains(hits, h => h.Url.Contains("/vipi") && h.Snippet.Contains("VALMA", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Search_Scope_Filters()
    {
        // ESEBA è nei CoP della vLOA → deve comparire solo nello scope vLOA, non in vIPI.
        var vloa = await _search.SearchAsync("ESEBA", SearchScope.Vloa, 50);
        Assert.NotEmpty(vloa);
        Assert.All(vloa, h => Assert.Contains("/vloa", h.Url));

        var vipi = await _search.SearchAsync("ESEBA", SearchScope.Vipi, 50);
        Assert.DoesNotContain(vipi, h => h.Url.Contains("/vloa"));
    }

    [Fact]
    public async Task Search_NoMatch_Empty()
    {
        var hits = await _search.SearchAsync("zzxxqqnotfound", SearchScope.All, 50);
        Assert.Empty(hits);
    }

    /// <summary>
    /// Un blocco immagine ha per testo il suo alternativo e la didascalia. Il BodyJson porta lo sha: se finisse
    /// nell'indice, cercare una sequenza qualsiasi pescherebbe immagini a caso e il risultato mostrerebbe JSON.
    /// </summary>
    [Fact]
    public async Task Search_Indexes_Image_Alt_And_Caption_Not_The_Json()
    {
        const string sha = "beef56789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0";
        var section = await _db.DocumentSections.FirstAsync();
        _db.ContentBlocks.Add(new Vipi.Domain.Entities.ContentBlock
        {
            DocumentVersionId = section.DocumentVersionId,
            SectionId = section.Id,
            Order = 99,
            Format = Vipi.Domain.BlockFormat.Image,
            Tier = Vipi.Domain.BlockTier.Extended,
            Visibility = Vipi.Domain.BlockVisibility.Always,
            Body = "Vista del piazzale",
            BodyJson = MediaRef.Serialize(new MediaRef(sha, "Hangar sud", 1600, 900)),
        });
        await _db.SaveChangesAsync();

        var perAlt = await _search.SearchAsync("Hangar sud", SearchScope.All, 50);
        Assert.Contains(perAlt, h => h.Snippet.Contains("Hangar sud"));
        Assert.DoesNotContain(perAlt, h => h.Snippet.Contains("mediaId"));

        var perDidascalia = await _search.SearchAsync("piazzale", SearchScope.All, 50);
        Assert.Contains(perDidascalia, h => h.Snippet.Contains("Vista del piazzale"));

        // Lo sha non è testo: cercarne un pezzo non deve pescare l'immagine.
        Assert.Empty(await _search.SearchAsync("beef5678", SearchScope.All, 50));
    }

    [Fact]
    public async Task Changed_Lists_Documents_In_Current_Cycle()
    {
        var cycle = new AiracService().GetCycle(System.DateTime.UtcNow);
        var rows = await _changes.ListChangedAsync(cycle);

        Assert.NotEmpty(rows);
        Assert.Contains(rows, r => r.DocTitle.Contains("Roma ACC"));
        // versioni uniche dei seed → "Nuovo" (v1), prev counts a 0
        Assert.All(rows, r => Assert.True(r.CurrBlocks > 0));
    }

    [Fact]
    public async Task Changed_OtherCycle_Empty()
    {
        var rows = await _changes.ListChangedAsync("9999");
        Assert.Empty(rows);
    }
}
