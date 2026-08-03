using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Riconciliazione one-shot delle aree regolamentate: gli ACC esteri escono dall'import periodico e le loro aree
/// lasciano l'archivio. Gira UNA volta sola — altrimenti a ogni riavvio ricancellerebbe quelle di un ente estero
/// appena riabilitato a mano.
/// </summary>
public class SpecialAreaMaintenanceTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfSpecialAreaMaintenance _sut = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _sut = new EfSpecialAreaMaintenance(_db, new EfImportStateStore(_db));

        _db.Accs.AddRange(
            new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" },
            new Acc { Code = "LFZZ", Name = "France Military", CountryPrefix = "LF", IsForeign = true },
            new Acc { Code = "LSAS", Name = "Switzerland", CountryPrefix = "LS", IsForeign = true });
        await _db.SaveChangesAsync();

        Area("solo-estera", "LFZZ");
        Area("condivisa", "LFZZ", "LIRR");   // elencata anche da un ACC domestico
        Area("italiana", "LIRR");
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private void Area(string ivaoId, params string[] centers)
    {
        _db.SpecialAreas.Add(new SpecialArea
        {
            IvaoId = ivaoId, Name = ivaoId,
            Centers = centers.Select(c => new SpecialAreaCenter { IvaoId = ivaoId, CenterId = c }).ToList(),
        });
    }

    [Fact]
    public async Task Foreign_accs_are_switched_off_and_their_areas_freed()
    {
        var freed = await _sut.OptOutForeignAreasAsync();

        Assert.Equal(2, freed);                                                   // i due legami di LFZZ
        Assert.All(await _db.Accs.Where(a => a.IsForeign).ToListAsync(), a => Assert.False(a.SpecialAreasEnabled));
        Assert.True((await _db.Accs.SingleAsync(a => a.Code == "LIRR")).SpecialAreasEnabled);   // i domestici non si toccano

        var left = await _db.SpecialAreas.Select(a => a.IvaoId).OrderBy(x => x).ToListAsync();
        Assert.Equal(new[] { "condivisa", "italiana" }, left);                    // la condivisa resta a LIRR
    }

    [Fact]
    public async Task It_does_not_undo_an_acc_the_admin_re_enabled()
    {
        await _sut.OptOutForeignAreasAsync();

        // L'admin accende la Svizzera e ne importa le aree.
        var lsas = await _db.Accs.SingleAsync(a => a.Code == "LSAS");
        lsas.SpecialAreasEnabled = true;
        Area("svizzera", "LSAS");
        await _db.SaveChangesAsync();

        var freed = await _sut.OptOutForeignAreasAsync();   // riavvio dell'app

        Assert.Equal(0, freed);
        Assert.True((await _db.Accs.SingleAsync(a => a.Code == "LSAS")).SpecialAreasEnabled);
        Assert.Contains(await _db.SpecialAreas.Select(a => a.IvaoId).ToListAsync(), x => x == "svizzera");
    }
}
