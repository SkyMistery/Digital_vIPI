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
/// Editing topologia + effetto sul motore: aggiungere/togliere regole e relazioni cambia l'ownership
/// risolta da AorService (verifica che TopologyBuilder rilegga il DB aggiornato).
/// </summary>
public class TopologyEditingTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfTopologyEditingRepository _repo = default!;
    private readonly AorService _aor = new();

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        await RomaStructureSeed.SeedAsync(_db);
        _repo = new EfTopologyEditingRepository(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private async Task<AorResult> Resolve(string p, params string[] online)
    {
        var accId = await _db.Accs.Select(f => f.Id).FirstAsync();
        var topology = await new TopologyBuilder(_db).BuildAsync(accId);
        return _aor.Resolve(topology, p, new HashSet<string>(online, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Adding_Rule_Flips_Sector_To_Online()
    {
        // Default: P=NE possiede il settore NE → Covered (TS è subordinato ma non possiede NE).
        var before = await Resolve("LIRR_NE_CTR", "LIRR_NE_CTR", "LIRR_TS_CTR");
        Assert.Equal(SectorState.Covered, before.State["LIRR_NE_CTR"]);

        // Aggiungo una regola: quando TS è online, il settore NE passa a TS.
        await _repo.AddRuleAsync("LIRR", "NE→TS quando TS online", 5,
            """{"online":["LIRR_TS_CTR"]}""", """{"LIRR_NE_CTR":"LIRR_TS_CTR"}""");

        // Ora il motore (rileggendo il DB) assegna NE a TS online ≠ P → Online.
        var after = await Resolve("LIRR_NE_CTR", "LIRR_NE_CTR", "LIRR_TS_CTR");
        Assert.Equal(SectorState.Online, after.State["LIRR_NE_CTR"]);
        Assert.Equal("LIRR_TS_CTR", after.Ownership["LIRR_NE_CTR"]);
    }

    [Fact]
    public async Task Add_And_Toggle_Rule()
    {
        var id = await _repo.AddRuleAsync("LIRR", "Test", 5, """{"online":["LIRR_TS_CTR"]}""", """{"LIRR_TS_CTR":"LIRR_TS_CTR"}""");
        Assert.True(id > 0);

        var data = await _repo.LoadAsync("LIRR");
        Assert.Contains(data!.Rules, r => r.Id == id && r.IsActive);

        await _repo.SetRuleActiveAsync("LIRR", id, false);
        data = await _repo.LoadAsync("LIRR");
        Assert.Contains(data!.Rules, r => r.Id == id && !r.IsActive);
    }
}
