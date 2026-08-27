using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// C7c — un ACC estero che nasce DOPO il tappo one-shot <c>OptOutForeignAreasAsync</c> non deve entrare nel giro
/// delle 24h con le aree regolamentate accese: il default d'entità è <c>true</c> (giusto per i domestici) e i
/// chiamanti non lo toccavano. La regola sta in <see cref="Acc.NewForeign"/>, e queste prove la sorvegliano.
/// </summary>
public class AccEsteroNasceSpentoTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    [Fact]
    public void La_fabbrica_spegne_le_aree_e_ricava_il_prefisso()
    {
        var acc = Acc.NewForeign("LDZO", "Zagreb ACC");

        Assert.True(acc.IsForeign);
        Assert.False(acc.SpecialAreasEnabled);
        Assert.Equal("LD", acc.CountryPrefix);
    }

    [Fact]
    public async Task Import_confinanti_crea_l_ACC_estero_con_le_aree_spente()
    {
        var sut = new EfNeighbourRepository(_db, new Vipi.Domain.Services.AiracService());

        await sut.PersistForeignCatalogAsync(new[]
        {
            new ForeignAccImport("LDZO", "Zagreb ACC", new[]
            {
                new SourceSubcenter("LDZO_CTR", "LDZO", "CTR", null, "134.150", null),
            }),
        });

        var acc = await _db.Accs.SingleAsync(a => a.Code == "LDZO");
        Assert.True(acc.IsForeign);
        Assert.False(acc.SpecialAreasEnabled);
    }

    [Fact]
    public async Task Un_ACC_gia_acceso_a_mano_non_viene_rispento_dall_import()
    {
        _db.Accs.Add(new Acc { Code = "LDZO", Name = "Zagreb ACC", CountryPrefix = "LD", IsForeign = true, SpecialAreasEnabled = true });
        await _db.SaveChangesAsync();

        var sut = new EfNeighbourRepository(_db, new Vipi.Domain.Services.AiracService());
        await sut.PersistForeignCatalogAsync(new[]
        {
            new ForeignAccImport("LDZO", "Zagreb ACC", Array.Empty<SourceSubcenter>()),
        });

        var acc = await _db.Accs.SingleAsync(a => a.Code == "LDZO");
        Assert.True(acc.SpecialAreasEnabled);   // la scelta dell'admin sopravvive al giro periodico
    }
}
