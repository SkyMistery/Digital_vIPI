using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Aor;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// La riga automatica «APP di scalo solo militare → MIL_CTR fratello» arriva davvero nella topologia che usa la
/// ricaduta, letta dal database: categoria dello scalo e albero proiettato. Carta
/// <c>docs/feature/2026-09-24-mil-solo-traffico-militare.md</c>.
/// </summary>
public class RipiegoMilitareTopologiaTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();

        var acc = new Acc { Code = "LIBB", Name = "Brindisi", CountryPrefix = "LI" };
        var grottaglie = new Airport { Icao = "LIBG", Name = "Grottaglie", Acc = acc, Category = AirportCategory.MilitaryOnly };
        var bari = new Airport { Icao = "LIBD", Name = "Bari", Acc = acc, Category = AirportCategory.Civil };
        _db.AddRange(acc, grottaglie, bari);
        await _db.SaveChangesAsync();

        var es = Settore(acc.Id, "LIBB_ES_CTR", SectorType.Ctr, null, null);
        _db.Sectors.Add(es);
        await _db.SaveChangesAsync();
        _db.Sectors.AddRange(
            Settore(acc.Id, "LIBB_MIL_CTR", SectorType.Ctr, es.Id, null),
            Settore(acc.Id, "LIBG_APP", SectorType.App, es.Id, grottaglie.Id),
            Settore(acc.Id, "LIBD_APP", SectorType.App, es.Id, bari.Id));
        _db.SectorFallbacks.Add(new SectorFallback { SectorCallsign = "LIBG_APP", Order = 0, TargetCallsign = "LIBD_APP" });
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private static Sector Settore(int accId, string cs, SectorType tipo, int? padre, int? scalo) => new()
    {
        Callsign = cs, Name = cs, AccId = accId, Type = tipo, CoverageOrder = 10, IsActive = true,
        Kind = scalo is null ? SectorKind.Acc : SectorKind.Airport, AirportId = scalo, ParentSectorId = padre,
    };

    [Fact]
    public async Task L_APP_dello_scalo_solo_militare_ha_il_MIL_dopo_le_righe_scritte()
    {
        var topo = await new TopologyBuilder(_db).BuildGlobalAsync();

        var righe = topo.Fallbacks["LIBG_APP"];
        Assert.Equal(new[] { "LIBD_APP", "LIBB_MIL_CTR" }, righe.Select(r => r.TargetCallsign));
        Assert.False(righe[0].Automatica);
        Assert.True(righe[1].Automatica);
    }

    [Fact]
    public async Task L_APP_di_uno_scalo_civile_non_ha_niente()
    {
        var topo = await new TopologyBuilder(_db).BuildGlobalAsync();

        Assert.False(topo.Fallbacks.ContainsKey("LIBD_APP"));
    }
}
