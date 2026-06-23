using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Aor;
using Vipi.Domain;
using Vipi.Infrastructure.Aor;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Verifica end-to-end DB→Topology→AoR: il seed strutturale di Roma + TopologyBuilder + AorService
/// riproducono gli scenari di SPEC_Logica_AoR §5 (S1/S2/S4/S5/S6) caricando dal database reale (SQLite).
/// </summary>
public class RomaAorIntegrationTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private int _firId;

    private readonly AorService _aor = new();

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync(); // tenere aperta la connessione = DB in-memory persistente
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _firId = await RomaStructureSeed.SeedAsync(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private async Task<AorResult> Resolve(string p, params string[] online)
    {
        var topology = await new TopologyBuilder(_db).BuildAsync(_firId);
        return _aor.Resolve(topology, p, new HashSet<string>(online, StringComparer.OrdinalIgnoreCase));
    }

    [Fact] // S1 — solo NE online → tutto Covered
    public async Task S1_OnlySelfOnline_AllCovered()
    {
        var r = await Resolve("LIRR_NE_CTR", "LIRR_NE_CTR");
        Assert.All(r.State.Values, s => Assert.Equal(SectorState.Covered, s));
    }

    [Fact] // S2 — Pisa APP online → settori Pisa Online, NE Covered
    public async Task S2_PisaApp_Online()
    {
        var r = await Resolve("LIRR_NE_CTR", "LIRR_NE_CTR", "LIRP_APP");
        Assert.Equal(SectorState.Online, r.State["LIRR-PISA"]);
        Assert.Equal(SectorState.Online, r.State["LIRR-PISA_TWR"]); // TWR offline → coperta da APP online
        Assert.Equal(SectorState.Covered, r.State["LIRR-NE"]);
    }

    [Fact] // S4 — split SU/ES via UnificationRule
    public async Task S4_SplitSuEs()
    {
        var alone = await Resolve("LIRR_SU_CTR", "LIRR_SU_CTR");
        Assert.Equal(SectorState.Covered, alone.State["LIRR-ES"]);

        var split = await Resolve("LIRR_SU_CTR", "LIRR_SU_CTR", "LIRR_ES_CTR");
        Assert.Equal(SectorState.Online, split.State["LIRR-ES"]);
        Assert.Equal(SectorState.Covered, split.State["LIRR-SU"]);
        Assert.Equal("LIRR_ES_CTR", split.Ownership["LIRR-ES"]);
    }

    [Fact] // S5 — TS ruba a NE
    public async Task S5_TsStealsFromNe()
    {
        var r = await Resolve("LIRR_NE_CTR", "LIRR_NE_CTR", "LIRR_TS_CTR");
        Assert.Equal(SectorState.Online, r.State["LIRR-TS"]);
        Assert.Equal(SectorState.Covered, r.State["LIRR-NE"]);
    }

    [Fact] // S6 — Pisa TWR online, APP offline → TWR Online, PISA (APP) resta Covered su NE
    public async Task S6_TwrOnline_AppOffline()
    {
        var r = await Resolve("LIRR_NE_CTR", "LIRR_NE_CTR", "LIRP_TWR");
        Assert.Equal(SectorState.Online, r.State["LIRR-PISA_TWR"]);
        Assert.Equal(SectorState.Covered, r.State["LIRR-PISA"]);
        Assert.Equal("LIRR_NE_CTR", r.Ownership["LIRR-PISA"]);
    }
}
