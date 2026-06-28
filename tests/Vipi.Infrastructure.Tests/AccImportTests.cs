using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Import ACC dalla sorgente: upsert ACC (dedup per centerId) + settori CTR, idempotente,
/// con preservazione di IsHidden. Verifica anche che EfStationDirectory escluda gli ACC nascosti.
/// </summary>
public class AccImportTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfAccAdminRepository _repo = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _repo = new EfAccAdminRepository(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private static IReadOnlyList<SourceCenter> Sample() => new[]
    {
        new SourceCenter("LIRR_N_CTR", "LIRR", "Roma Control", false, "124.000"),
        new SourceCenter("LIRR_S_CTR", "LIRR", "Roma Control", false, "125.000"),
        new SourceCenter("LIMM_CTR",   "LIMM", "Milano Control", false, "133.250"),
        new SourceCenter("LIRM_CTR",   "LIRM", "Martina Control", true, "130.000"),  // militare
    };

    private static IReadOnlyList<SourceSubcenter> Subs() => new[]
    {
        new SourceSubcenter("LIRR_N_CTR", "LIRR", "CTR", "N", "124.000", "{\"poly\":1}"),
        new SourceSubcenter("LIRR_S_CTR", "LIRR", "CTR", "S", "125.000", null),
        new SourceSubcenter("LIRR_FSS",   "LIRR", "FSS", null, "123.000", null),   // FSS → default GND-19000
        new SourceSubcenter("LIBB_X_CTR", "LIBB", "CTR", "X", "126.000", null),   // ACC inesistente → skip
    };

    [Fact]
    public async Task Import_Creates_One_Acc_Per_CenterId()
    {
        var r = await _repo.ImportAsync(Sample());

        Assert.Equal(3, r.Created);       // LIRR (dedup da 2 righe), LIMM, LIRM

        var accs = await _repo.ListAccsAsync();
        Assert.Equal(3, accs.Count);
        Assert.Single(accs, a => a.Code == "LIRR");
        Assert.True(Assert.Single(accs, a => a.Code == "LIRM").IsMilitary);
        Assert.All(accs, a => Assert.False(a.IsHidden));   // tutti attivi di default
    }

    [Fact]
    public async Task Reimport_Is_Idempotent_And_Preserves_Hidden()
    {
        await _repo.ImportAsync(Sample());
        var limm = (await _repo.ListAccsAsync()).Single(a => a.Code == "LIMM");
        await _repo.SetHiddenAsync(limm.Id, true);

        var r = await _repo.ImportAsync(Sample());   // secondo import

        Assert.Equal(0, r.Created);
        Assert.Equal(3, r.Updated);

        Assert.Equal(3, (await _repo.ListAccsAsync()).Count);    // nessun duplicato
        Assert.True((await _repo.ListAccsAsync()).Single(a => a.Code == "LIMM").IsHidden);  // hide preservato
    }

    [Fact]
    public async Task Import_Subcenters_Skips_Unknown_Acc_And_Preserves_Admin_Limits()
    {
        await _repo.ImportAsync(Sample());        // crea LIRR, LIMM, LIRM (non LIBB)
        var r = await _repo.ImportSubcentersAsync(Subs());

        Assert.Equal(3, r.Created);               // LIRR_N/S/FSS; LIBB_X scartato (nessun ACC LIBB)
        var subs = await _repo.ListSubcentersAsync();
        Assert.Equal(3, subs.Count);
        Assert.True(subs.Single(s => s.ComposePosition == "LIRR_N_CTR").HasPolygon);
        // default limiti CTR: inferiore 0 (GND), superiore null = UNL
        var s2 = subs.Single(s => s.ComposePosition == "LIRR_S_CTR");
        Assert.Equal(0, s2.LowerLimit);
        Assert.Null(s2.UpperLimit);
        // default limiti FSS: GND (0) → 19000
        var fss = subs.Single(s => s.ComposePosition == "LIRR_FSS");
        Assert.Equal(0, fss.LowerLimit);
        Assert.Equal(19000, fss.UpperLimit);

        // admin imposta limiti + nasconde
        var n = subs.Single(s => s.ComposePosition == "LIRR_N_CTR");
        await _repo.SetSubcenterLimitsAsync(n.Id, 0, 24500);
        await _repo.SetSubcenterHiddenAsync(n.Id, true);

        // re-import (sorgente senza limiti) preserva i valori admin
        var r2 = await _repo.ImportSubcentersAsync(Subs());
        Assert.Equal(0, r2.Created);
        Assert.Equal(3, r2.Updated);
        var after = (await _repo.ListSubcentersAsync()).Single(s => s.ComposePosition == "LIRR_N_CTR");
        Assert.Equal(0, after.LowerLimit);
        Assert.Equal(24500, after.UpperLimit);
        Assert.True(after.IsHidden);
    }

    [Fact]
    public async Task Hiding_Acc_Marks_Its_Sectors_AccHidden()
    {
        await _repo.ImportAsync(Sample());
        await _repo.ImportSubcentersAsync(Subs());
        var lirr = (await _repo.ListAccsAsync()).Single(a => a.Code == "LIRR");

        await _repo.SetHiddenAsync(lirr.Id, true);

        var subs = await _repo.ListSubcentersAsync();
        Assert.All(subs.Where(s => s.CenterId == "LIRR"), s => Assert.True(s.AccHidden));
    }

    [Fact]
    public async Task StationDirectory_Excludes_Hidden_Accs()
    {
        await _repo.ImportAsync(Sample());
        var lirm = (await _repo.ListAccsAsync()).Single(a => a.Code == "LIRM");
        await _repo.SetHiddenAsync(lirm.Id, true);

        var nav = new EfStationDirectory(_db).ListAccs();

        Assert.DoesNotContain(nav, a => a.Code == "LIRM");
        Assert.Contains(nav, a => a.Code == "LIRR");
    }
}
