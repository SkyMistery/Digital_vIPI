using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il perimetro di shape di un documento: quali settori quella release può disegnare, e la forzatura.
/// ⚠️ Il perimetro è quello dell'ENTE (ACC o aeroporto), non l'elenco esatto delle configurazioni AoR: si
/// può avvisare di troppo, mai tacere per un settore che la mappa disegna davvero.
/// </summary>
public class ShapeGateScopeTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfShapeGateRepository _repo = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        var lirr = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        var limm = new Acc { Code = "LIMM", Name = "Milano", CountryPrefix = "LI" };
        _db.Accs.AddRange(lirr, limm);
        _db.Airports.AddRange(
            new Airport { Icao = "LIRA", Name = "Ciampino", Acc = lirr },
            new Airport { Icao = "LIMC", Name = "Malpensa", Acc = limm });
        await _db.SaveChangesAsync();

        _db.AccSectors.AddRange(
            new AccSector { ComposePosition = "LIRR_NE_CTR", CenterId = "LIRR", Position = "CTR", ShapeAiracCycle = "2610" },
            new AccSector { ComposePosition = "LIMM_W_CTR", CenterId = "LIMM", Position = "CTR" });
        _db.AirportSectors.AddRange(
            new AirportSector { ComposePosition = "LIRA_APP", AirportIcao = "LIRA", AccCode = "LIRR", Position = "APP" },
            new AirportSector { ComposePosition = "LIMC_APP", AirportIcao = "LIMC", AccCode = "LIMM", Position = "APP" });
        await _db.SaveChangesAsync();

        _repo = new EfShapeGateRepository(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    [Fact]
    public async Task La_chiave_della_vipi_acc_porta_i_settori_di_quella_acc()
    {
        var scope = await _repo.GetScopeAsync(ReleaseTargetType.AccVipi, "LIRR|LIRR_CTR");

        Assert.Equal("LIRR", scope.AccCode);
        Assert.Equal(new[] { "LIRA_APP", "LIRR_NE_CTR" }, scope.Rows.Select(r => r.Callsign).OrderBy(c => c));
    }

    [Fact]
    public async Task Il_perimetro_di_un_aeroporto_e_solo_il_suo_e_porta_la_acc_che_lo_governa()
    {
        var scope = await _repo.GetScopeAsync(ReleaseTargetType.Airport, "LIRA");

        Assert.Equal("LIRR", scope.AccCode);   // il permesso è ACC-scoped
        Assert.Equal("LIRA_APP", Assert.Single(scope.Rows).Callsign);
    }

    /// <summary>La chiave dell'APP è un callsign: l'aeroporto sono le prime quattro lettere.</summary>
    [Fact]
    public async Task La_chiave_dell_app_si_riporta_al_suo_aeroporto()
    {
        var scope = await _repo.GetScopeAsync(ReleaseTargetType.App, "LIMC_APP");

        Assert.Equal("LIMM", scope.AccCode);
        Assert.Equal("LIMC_APP", Assert.Single(scope.Rows).Callsign);
    }

    [Fact]
    public async Task Lo_stato_della_shape_arriva_intero_al_gate()
    {
        var scope = await _repo.GetScopeAsync(ReleaseTargetType.AccVipi, "LIRR|LIRR_CTR");

        var riga = scope.Rows.Single(r => r.Callsign == "LIRR_NE_CTR");
        Assert.Equal("2610", riga.Shape.FromCycle);
        Assert.False(riga.Shape.ForcePublished);
    }

    [Fact]
    public async Task La_forzatura_accende_solo_le_righe_indicate_e_lascia_il_differimento_scritto()
    {
        var id = (await _db.AccSectors.AsNoTracking().SingleAsync(x => x.ComposePosition == "LIRR_NE_CTR")).Id;

        Assert.Equal(1, await _repo.SetForcePublishedAsync(new[] { (SourceCatalog.Subcenter, id) }));

        var riga = await _db.AccSectors.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.True(riga.ShapeForcePublished);
        Assert.Equal("2610", riga.ShapeAiracCycle);   // ⚠️ «pubblicala lo stesso» ≠ «è in vigore»

        var altra = await _db.AccSectors.AsNoTracking().SingleAsync(x => x.ComposePosition == "LIMM_W_CTR");
        Assert.False(altra.ShapeForcePublished);
    }

    [Fact]
    public async Task Una_chiave_illeggibile_non_apre_nessun_perimetro()
    {
        Assert.Empty((await _repo.GetScopeAsync(ReleaseTargetType.Vloa, "boh")).Rows);
        Assert.Empty((await _repo.GetScopeAsync(ReleaseTargetType.App, "LI")).Rows);
        Assert.Empty((await _repo.GetScopeAsync(ReleaseTargetType.Airport, "  ")).Rows);
    }
}
