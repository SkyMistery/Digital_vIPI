using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// La lettura delle API dei settori (S119): quali centri esistono per chi chiama, e i settori coi padri giusti.
/// </summary>
public class SettoriPerApiTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private Acc Centro(string code, bool nascosto = false, bool estero = false, bool militare = false)
    {
        var a = new Acc { Code = code, Name = code, CountryPrefix = estero ? "LJ" : "LI", IsHidden = nascosto, IsForeign = estero, IsMilitary = militare };
        _db.Accs.Add(a);
        return a;
    }

    private Sector Settore(Acc acc, string callsign, SectorType tipo, int ordine, Sector? padre = null, bool attivo = true) =>
        _db.Sectors.Add(new Sector
        {
            Acc = acc, Callsign = callsign, Name = callsign, Type = tipo,
            Kind = tipo == SectorType.Ctr ? SectorKind.Acc : SectorKind.Airport,
            AirportIcao = tipo == SectorType.Ctr ? null : callsign[..4],
            CoverageOrder = ordine, ParentSector = padre, IsActive = attivo, IsProjected = true,
        }).Entity;

    /// <summary>Un centro nascosto non esiste per il sito, uno estero è un vicino di cui non scriviamo la struttura.</summary>
    [Fact]
    public async Task Solo_i_centri_italiani_visibili()
    {
        Centro("LIRR"); Centro("LIMM"); Centro("LIXX", nascosto: true); Centro("LJLA", estero: true);
        Centro("LIMV", militare: true);
        await _db.SaveChangesAsync();
        var porta = new EfSettoriPerApi(_db);

        Assert.Equal(new[] { "LIMM", "LIMV", "LIRR" }, (await porta.CentriAsync()).Select(c => c.Code));
        Assert.True((await porta.CentroAsync(" limv "))!.IsMilitary);
        Assert.Null(await porta.CentroAsync("LIXX"));
        Assert.Null(await porta.CentroAsync("LJLA"));
        Assert.Empty(await porta.SettoriAsync("LIXX"));
    }

    /// <summary>
    /// I settori del centro, attivi, in ordine di copertura; il padre anche in un altro centro (l'APP di confine
    /// sotto il CTR del vicino). Un padre spento non si dà: chi lo leggesse cercherebbe un settore che non c'è.
    /// </summary>
    [Fact]
    public async Task I_settori_attivi_col_padre_anche_fuori_dal_centro()
    {
        var roma = Centro("LIRR");
        var napoli = Centro("LIRN");
        var ne = Settore(roma, "LIRR_NE_CTR", SectorType.Ctr, 1);
        var spento = Settore(roma, "LIRR_XX_CTR", SectorType.Ctr, 1, attivo: false);
        var app = Settore(roma, "LIRF_APP", SectorType.App, 2, padre: ne);
        Settore(roma, "LIRF_TWR", SectorType.Twr, 3, padre: app);
        Settore(roma, "LIRZ_APP", SectorType.App, 2, padre: Settore(napoli, "LIRN_CTR", SectorType.Ctr, 1));
        Settore(roma, "LIRL_APP", SectorType.App, 2, padre: spento);
        await _db.SaveChangesAsync();

        var settori = await new EfSettoriPerApi(_db).SettoriAsync("lirr");

        Assert.Equal(new[] { "LIRR_NE_CTR", "LIRF_APP", "LIRL_APP", "LIRZ_APP", "LIRF_TWR" }, settori.Select(s => s.Callsign));
        var perNome = settori.ToDictionary(s => s.Callsign);
        Assert.Null(perNome["LIRR_NE_CTR"].ParentCallsign);
        Assert.Equal("LIRF_APP", perNome["LIRF_TWR"].ParentCallsign);
        Assert.Equal("LIRN_CTR", perNome["LIRZ_APP"].ParentCallsign);
        Assert.Null(perNome["LIRL_APP"].ParentCallsign);
        Assert.Equal("LIRF", perNome["LIRF_APP"].AirportIcao);
    }
}
