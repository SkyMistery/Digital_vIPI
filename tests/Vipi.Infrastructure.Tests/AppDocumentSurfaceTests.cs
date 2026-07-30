using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Superficie documentale dell'APP (doc 11 §3e): solo i NON remotizzati hanno un documento proprio. Prima
/// l'identità si risolveva per qualunque settore di tipo App, quindi l'editor creava documenti per gli APP
/// remotizzati che poi nessun viewer sapeva rendere.
/// </summary>
public class AppDocumentSurfaceTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfAppDerivationRepository _repo = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        await RomaStructureSeed.SeedAsync(_db);
        _repo = new EfAppDerivationRepository(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    [Fact]
    public async Task Standalone_App_Has_A_Document_Identity()
    {
        var id = await _repo.ResolveForDocumentAsync("LIRP_APP");

        Assert.NotNull(id);
        Assert.Equal("LIRP_APP", id!.Callsign);
    }

    [Fact]
    public async Task Remotized_App_Has_No_Document_Identity()
    {
        var papp = await _db.Sectors.FirstAsync(s => s.Callsign == "LIRP_APP");
        papp.ApproachKind = ApproachKind.Remotized;
        await _db.SaveChangesAsync();

        Assert.Null(await _repo.ResolveForDocumentAsync("LIRP_APP"));
    }

    [Fact]
    public async Task Identity_Does_Not_Require_IsPrimary()
    {
        // IsPrimary lo imposta la CREAZIONE del documento: pretenderlo qui bloccherebbe il primo documento
        // (catch-22 sull'ingresso UI).
        var papp = await _db.Sectors.FirstAsync(s => s.Callsign == "LIRP_APP");
        Assert.False(papp.IsPrimary);

        Assert.NotNull(await _repo.ResolveForDocumentAsync("LIRP_APP"));
    }
}
