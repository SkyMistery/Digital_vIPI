using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>Instradamento: un documento il cui settore primario è un APP standalone è marcato IsStandaloneApp (non IsAirport), scope = callsign.</summary>
public class AppRoutingTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        await RomaStructureSeed.SeedAsync(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    [Fact]
    public async Task Standalone_App_Doc_Routes_To_App_Editor()
    {
        // Crea un documento e collega LIRP_APP come settore primario.
        var doc = new Document
        {
            Type = DocumentType.Vipi, Title = "vIPI — LIRP_APP", Language = Language.It,
            Status = DocumentStatus.Published, LastUpdatedUtc = DateTime.UtcNow, LastUpdatedAiracCycle = "2606",
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

        var app = await _db.Sectors.FirstAsync(s => s.Callsign == "LIRP_APP");
        app.DocumentId = doc.Id;
        app.IsPrimary = true;
        await _db.SaveChangesAsync();

        var repo = new EfEditingRepository(_db, new AiracService(), new EfMediaMaintenance(_db));
        var summaries = await repo.ListDocumentsAsync();
        var s = summaries.First(x => x.Id == doc.Id);

        Assert.True(s.IsStandaloneApp);
        Assert.False(s.IsAirport);                 // NON instradato all'editor aeroporto
        Assert.Equal("LIRP_APP", s.Scope);         // scope = callsign (chiave editor APP)
        Assert.Equal("LIRR", s.AccCode);
    }
}
