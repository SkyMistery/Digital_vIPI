using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>CRUD coordinamenti: flussi (settore proprio) + punti (CoP/livello strutturato/Next).</summary>
public class TransferRepositoryTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfTransferRepository _repo = default!;
    private int _neId, _ftwrId, _tsId;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        await RomaStructureSeed.SeedAsync(_db);
        _repo = new EfTransferRepository(_db);

        var sectors = await _db.Sectors.ToListAsync();
        _neId = sectors.First(s => s.Callsign == "LIRR_NE_CTR").Id;
        _ftwrId = sectors.First(s => s.Callsign == "LIRF_TWR").Id;
        _tsId = sectors.First(s => s.Callsign == "LIRR_TS_CTR").Id;
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private TransferFlowInput Flow() => new()
    {
        OwningSectorId = _neId, Kind = TransferFlowKind.Arrival, AirportIcao = "LIRF", Description = "test",
    };

    private TransferPointInput Point(string cop, int level, int? next) => new()
    {
        Cop = cop, LevelValue = level, LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.AtOrBelow,
        NextSectorId = next,
    };

    [Fact]
    public async Task Add_Flow_With_Points_Roundtrips()
    {
        var flowId = await _repo.AddFlowAsync("LIRR", Flow());
        await _repo.AddPointAsync("LIRR", flowId, Point("VALMA", 130, _ftwrId));
        await _repo.AddPointAsync("LIRR", flowId, Point("ELKAP", 150, _ftwrId));

        var flows = await _repo.ListFlowsByAccAsync("LIRR");
        var f = Assert.Single(flows);
        Assert.Equal("LIRR_NE_CTR", f.OwningSectorCallsign);
        Assert.Equal(2, f.Points.Count);
        Assert.Equal("FL130↓", f.Points[0].LevelText);
        Assert.Equal("LIRF_TWR", f.Points[0].NextSectorCallsign);
    }

    [Fact]
    public async Task Add_Overflight_Without_Airport_And_No_Points_Roundtrips()
    {
        var flowId = await _repo.AddFlowAsync("LIRR", new TransferFlowInput
        {
            OwningSectorId = _neId, Kind = TransferFlowKind.Overflight, AirportIcao = null, Description = null,
        });

        var flows = await _repo.ListFlowsByAccAsync("LIRR");
        var f = Assert.Single(flows);
        Assert.Equal(flowId, f.Id);
        Assert.Equal(TransferFlowKind.Overflight, f.Kind);
        Assert.Null(f.AirportIcao);
        Assert.Empty(f.Points);
    }

    [Fact]
    public async Task Point_Parity_Roundtrips_And_Shows_In_LevelText()
    {
        var flowId = await _repo.AddFlowAsync("LIRR", Flow());
        await _repo.AddPointAsync("LIRR", flowId, new TransferPointInput
        {
            Cop = "ELB", LevelValue = 290, LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.AtOrAbove,
            Parity = LevelParity.Odd, NextSectorId = _ftwrId,
        });

        var p = (await _repo.ListFlowsByAccAsync("LIRR")).Single().Points.Single();
        Assert.Equal(LevelParity.Odd, p.Parity);
        Assert.Equal("FL290↑ (dispari)", p.LevelText);
    }

    [Fact]
    public async Task Special_Level_Renders_Text()
    {
        var flowId = await _repo.AddFlowAsync("LIRR", Flow());
        await _repo.AddPointAsync("LIRR", flowId, new TransferPointInput
        {
            Cop = "ELB", LevelConstraint = LevelConstraint.Special, LevelSpecial = "per aerovia",
            LevelUnit = LevelUnit.Fl, NextSectorId = null,
        });

        var p = (await _repo.ListFlowsByAccAsync("LIRR")).Single().Points.Single();
        Assert.Equal("per aerovia", p.LevelText);
        Assert.Null(p.LevelValue);
        Assert.Null(p.NextSectorCallsign);
    }

    [Fact]
    public async Task Point_Condition_Roundtrips()
    {
        var flowId = await _repo.AddFlowAsync("LIRR", Flow());
        await _repo.AddPointAsync("LIRR", flowId, new TransferPointInput
        {
            Cop = "VALMA", LevelValue = 195, LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.AtOrBelow,
            NextSectorId = _ftwrId, ConditionLabel = "RWY 16", ConditionRefId = 42,
        });

        var p = (await _repo.ListFlowsByAccAsync("LIRR")).Single().Points.Single();
        Assert.Equal("RWY 16", p.ConditionLabel);
        Assert.Equal(42, p.ConditionRefId);
    }

    [Fact]
    public async Task Point_Condition_Independent_Columns_Persist()
    {
        // Le tre dimensioni (pista/area/personalizzata) sono indipendenti e coesistono su una riga.
        var flowId = await _repo.AddFlowAsync("LIRR", Flow());
        await _repo.AddPointAsync("LIRR", flowId, new TransferPointInput
        {
            Cop = "ELB", LevelValue = 150, LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.AtOrBelow,
            NextSectorId = null, ConditionLabel = "16R / 16L", ConditionRefId = 7,
            ConditionAreaLabel = "R41", ConditionCustomLabel = "notte",
        });

        var p = (await _repo.ListFlowsByAccAsync("LIRR")).Single().Points.Single();
        Assert.Equal("16R / 16L", p.ConditionLabel);
        Assert.Equal(7, p.ConditionRefId);
        Assert.Equal("R41", p.ConditionAreaLabel);
        Assert.Equal("notte", p.ConditionCustomLabel);
    }

    [Fact]
    public async Task Point_Condition_Ref_Kept_Only_With_Runway()
    {
        // Il soft-ref pista è tenuto solo se c'è una pista; senza pista viene azzerato anche se passato.
        var flowId = await _repo.AddFlowAsync("LIRR", Flow());
        await _repo.AddPointAsync("LIRR", flowId, new TransferPointInput
        {
            Cop = "OSTIA", LevelValue = 120, LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.AtOrBelow,
            NextSectorId = _ftwrId, ConditionLabel = null, ConditionRefId = 99, ConditionAreaLabel = "R41",
        });

        var p = (await _repo.ListFlowsByAccAsync("LIRR")).Single().Points.Single();
        Assert.Null(p.ConditionLabel);
        Assert.Null(p.ConditionRefId);
        Assert.Equal("R41", p.ConditionAreaLabel);
    }

    [Fact]
    public async Task MovePoint_Swaps_Order_And_Noop_At_Ends()
    {
        var flowId = await _repo.AddFlowAsync("LIRR", Flow());
        var p1 = await _repo.AddPointAsync("LIRR", flowId, Point("VALMA", 130, _ftwrId));
        var p2 = await _repo.AddPointAsync("LIRR", flowId, Point("ELKAP", 150, _ftwrId));
        var p3 = await _repo.AddPointAsync("LIRR", flowId, Point("OSTIA", 170, _ftwrId));

        // Ordine iniziale: VALMA, ELKAP, OSTIA
        var order0 = (await _repo.ListFlowsByAccAsync("LIRR")).Single().Points.Select(p => p.Cop).ToList();
        Assert.Equal(new[] { "VALMA", "ELKAP", "OSTIA" }, order0);

        // Sposta ELKAP su → VALMA, ELKAP scambiati
        await _repo.MovePointAsync("LIRR", p2, up: true);
        var order1 = (await _repo.ListFlowsByAccAsync("LIRR")).Single().Points.Select(p => p.Cop).ToList();
        Assert.Equal(new[] { "ELKAP", "VALMA", "OSTIA" }, order1);

        // Sposta OSTIA giù = estremo → no-op
        await _repo.MovePointAsync("LIRR", p3, up: false);
        var order2 = (await _repo.ListFlowsByAccAsync("LIRR")).Single().Points.Select(p => p.Cop).ToList();
        Assert.Equal(new[] { "ELKAP", "VALMA", "OSTIA" }, order2);

        // Primo su = estremo → no-op (p1 ora è VALMA in mezzo; usa ELKAP=p2 ora in testa)
        await _repo.MovePointAsync("LIRR", p2, up: true);
        var order3 = (await _repo.ListFlowsByAccAsync("LIRR")).Single().Points.Select(p => p.Cop).ToList();
        Assert.Equal(new[] { "ELKAP", "VALMA", "OSTIA" }, order3);
    }

    [Fact]
    public async Task Update_And_Delete_Point_And_Flow()
    {
        var flowId = await _repo.AddFlowAsync("LIRR", Flow());
        var pid = await _repo.AddPointAsync("LIRR", flowId, Point("VALMA", 130, _ftwrId));

        await _repo.UpdatePointAsync("LIRR", pid, Point("VALMA", 90, _tsId));
        var p = (await _repo.ListFlowsByAccAsync("LIRR")).Single().Points.Single();
        Assert.Equal("FL90↓", p.LevelText);
        Assert.Equal("LIRR_TS_CTR", p.NextSectorCallsign);

        await _repo.DeletePointAsync("LIRR", pid);
        Assert.Empty((await _repo.ListFlowsByAccAsync("LIRR")).Single().Points);

        await _repo.DeleteFlowAsync("LIRR", flowId);
        Assert.Empty(await _repo.ListFlowsByAccAsync("LIRR"));
    }

    [Fact]
    public async Task Global_Topology_Resolves_Receiver_Up_Hierarchy()
    {
        // LIRF_TWR è figlio di LIRR_NE_CTR (seed). TWR offline, NE online → il ricevente risolto risale al padre.
        var topo = await new Vipi.Infrastructure.Aor.TopologyBuilder(_db).BuildGlobalAsync();
        var chain = new[] { "LIRF_TWR" }.Concat(topo.Ancestors("LIRF_TWR")).ToList();
        Assert.Contains("LIRR_NE_CTR", chain);

        var online = new HashSet<string>(new[] { "LIRR_NE_CTR" }, StringComparer.OrdinalIgnoreCase);
        var (handler, isOnline) = TransferOnlineResolver.Resolve(chain, online);
        Assert.True(isOnline);
        Assert.Equal("LIRR_NE_CTR", handler);

        // Nessuno online lungo la catena → UNICOM.
        var (h2, on2) = TransferOnlineResolver.Resolve(chain, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        Assert.False(on2);
        Assert.Equal("UNICOM", h2);
    }

    [Fact]
    public async Task Seed_Populates_Demo_Flows()
    {
        await RomaTransferSeed.SeedAsync(_db);
        var flows = await _repo.ListFlowsByAccAsync("LIRR");
        Assert.NotEmpty(flows);
        Assert.Contains(flows, f => f.OwningSectorCallsign == "LIRR_NE_CTR" && f.Kind == TransferFlowKind.Arrival
            && f.Points.Any(p => p.Cop == "VALMA" && p.LevelText == "FL130↓"));
    }
}
