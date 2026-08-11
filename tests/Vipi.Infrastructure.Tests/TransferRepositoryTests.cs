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
        Assert.Equal("FL130-", f.Points[0].LevelText);   // ≤ → «-»; nessuno stato verticale → nessuna freccia
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
            Parity = LevelParity.Odd, VerticalState = TransferVerticalState.Climbing, NextSectorId = _ftwrId,
        });

        var p = (await _repo.ListFlowsByAccAsync("LIRR")).Single().Points.Single();
        Assert.Equal(LevelParity.Odd, p.Parity);
        Assert.Equal(TransferVerticalState.Climbing, p.VerticalState);   // round-trip stato verticale (indipendente dal vincolo)
        Assert.Equal("FL290+ ↑ (dispari)", p.LevelText);   // ≥ → «+», stato salita → «↑», parità dispari
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
    public async Task MovePointToEnd_Reorders_To_Top_And_Bottom()
    {
        var flowId = await _repo.AddFlowAsync("LIRR", Flow());
        var a = await _repo.AddPointAsync("LIRR", flowId, Point("AAA", 100, _ftwrId));
        await _repo.AddPointAsync("LIRR", flowId, Point("BBB", 110, _ftwrId));
        var c = await _repo.AddPointAsync("LIRR", flowId, Point("CCC", 120, _ftwrId));

        // C in cima → CCC, AAA, BBB
        await _repo.MovePointToEndAsync("LIRR", c, top: true);
        var cops = (await _repo.ListFlowsByAccAsync("LIRR")).Single().Points.Select(p => p.Cop).ToArray();
        Assert.Equal(new[] { "CCC", "AAA", "BBB" }, cops);

        // A in fondo → CCC, BBB, AAA
        await _repo.MovePointToEndAsync("LIRR", a, top: false);
        cops = (await _repo.ListFlowsByAccAsync("LIRR")).Single().Points.Select(p => p.Cop).ToArray();
        Assert.Equal(new[] { "CCC", "BBB", "AAA" }, cops);
    }

    [Fact]
    public async Task Update_And_Delete_Point_And_Flow()
    {
        var flowId = await _repo.AddFlowAsync("LIRR", Flow());
        var pid = await _repo.AddPointAsync("LIRR", flowId, Point("VALMA", 130, _ftwrId));

        await _repo.UpdatePointAsync("LIRR", pid, Point("VALMA", 90, _tsId));
        var p = (await _repo.ListFlowsByAccAsync("LIRR")).Single().Points.Single();
        Assert.Equal("FL90-", p.LevelText);
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

    // ---- Faccetta trasferimento e velocità ----

    [Fact]
    public async Task Handoff_And_Speed_Roundtrip()
    {
        var flowId = await _repo.AddFlowAsync("LIRR", Flow());
        await _repo.AddPointAsync("LIRR", flowId, Point("CHI", 160, _ftwrId) with
        {
            LevelConstraint = LevelConstraint.AtOrAbove,
            VerticalState = TransferVerticalState.Descending,
            HandoffKind = TransferHandoffKind.AorBoundary,
            HandoffLevelValue = 110,
            HandoffLevelConstraint = LevelConstraint.Exact,
            SpeedValue = 250,
            SpeedConstraint = SpeedConstraint.AtOrBelow,
        });

        var p = Assert.Single((await _repo.ListFlowsByAccAsync("LIRR")).Single().Points);
        Assert.Equal(TransferHandoffKind.AorBoundary, p.HandoffKind);
        Assert.True(p.HasHandoff);
        // Il livello autorizzato e quello al trasferimento sono due testi distinti: è tutto il punto della faccetta.
        Assert.Equal("FL160+ ↓", p.LevelText);
        Assert.Equal("FL110", p.HandoffLevelText);
        Assert.Equal("≤250 kt", p.SpeedText);
    }

    [Fact]
    public async Task Handoff_Cleared_Wipes_Its_Companions()
    {
        var flowId = await _repo.AddFlowAsync("LIRR", Flow());
        var id = await _repo.AddPointAsync("LIRR", flowId, Point("CHI", 160, _ftwrId) with
        {
            HandoffKind = TransferHandoffKind.Point, HandoffLabel = "AVN", HandoffLevelValue = 110,
            SpeedValue = 250, SpeedConstraint = SpeedConstraint.AtOrBelow,
        });

        // Tornare a «il trasferimento coincide con l'ingresso» non deve lasciare un livello fantasma.
        await _repo.UpdatePointAsync("LIRR", id, Point("CHI", 160, _ftwrId));

        var p = Assert.Single((await _repo.ListFlowsByAccAsync("LIRR")).Single().Points);
        Assert.False(p.HasHandoff);
        Assert.Null(p.HandoffLabel);
        Assert.Null(p.HandoffLevelValue);
        Assert.Null(p.SpeedValue);
        Assert.Equal("", p.HandoffLevelText);
    }

    // ---- Varianti: il gruppo è un OUTLINE ----

    [Fact]
    public async Task Alternative_Is_Peer_And_Copies_Everything_But_The_Condition()
    {
        var flowId = await _repo.AddFlowAsync("LIRR", Flow());
        var srcId = await _repo.AddPointAsync("LIRR", flowId, Point("BIRSU", 150, _ftwrId) with
        {
            HandoffKind = TransferHandoffKind.AorBoundary, HandoffLevelValue = 110,
            ConditionLabel = "07", SpeedValue = 250, SpeedConstraint = SpeedConstraint.AtOrBelow,
        });
        await _repo.AddPointAsync("LIRR", flowId, Point("ELKAP", 150, _ftwrId));   // riga che deve scalare sotto

        await _repo.AddAlternativeAsync("LIRR", srcId);

        var points = (await _repo.ListFlowsByAccAsync("LIRR")).Single().Points;
        Assert.Equal(3, points.Count);
        var (src, alt, other) = (points[0], points[1], points[2]);

        Assert.Equal("BIRSU", alt.Cop);
        Assert.Equal("ELKAP", other.Cop);
        // Pari-grado: nessuna delle due è lo standard dell'altra (pista 07 · pista 25).
        Assert.Equal(0, src.VariantDepth);
        Assert.Equal(0, alt.VariantDepth);
        // Copia completa: livelli, faccetta trasferimento, velocità, ricevente.
        Assert.Equal(150, alt.LevelValue);
        Assert.Equal(TransferHandoffKind.AorBoundary, alt.HandoffKind);
        Assert.Equal(110, alt.HandoffLevelValue);
        Assert.Equal(250, alt.SpeedValue);
        Assert.Equal(src.NextSectorId, alt.NextSectorId);
        // Tranne la condizione, che è ciò che l'alternativa deve dire di diverso.
        Assert.Null(alt.ConditionLabel);
        Assert.NotNull(src.VariantGroup);
        Assert.Equal(src.VariantGroup, alt.VariantGroup);
        Assert.Null(other.VariantGroup);
    }

    [Fact]
    public async Task Exception_Nests_One_Level_Deeper()
    {
        var flowId = await _repo.AddFlowAsync("LIRR", Flow());
        var srcId = await _repo.AddPointAsync("LIRR", flowId, Point("BIRSU", 150, _ftwrId) with { ConditionLabel = "07" });

        var excId = await _repo.AddExceptionAsync("LIRR", srcId);
        var deeperId = await _repo.AddExceptionAsync("LIRR", excId);   // eccezione dell'eccezione

        var points = (await _repo.ListFlowsByAccAsync("LIRR")).Single().Points;
        Assert.Equal(new[] { 0, 1, 2 }, points.Select(p => p.VariantDepth));
        Assert.Equal(new[] { srcId, excId, deeperId }, points.Select(p => p.Id));
    }

    [Fact]
    public async Task An_Alternative_Lands_After_The_Whole_Subtree()
    {
        // Aggiungere «pista 25» accanto a «pista 07» non deve infilarsi in mezzo alle eccezioni della 07:
        // spezzerebbe in due un blocco già scritto, e le eccezioni rimaste sotto cambierebbero padrone.
        var flowId = await _repo.AddFlowAsync("LIRR", Flow());
        var rwy07 = await _repo.AddPointAsync("LIRR", flowId, Point("BIRSU", 150, _ftwrId) with { ConditionLabel = "07" });
        var exc = await _repo.AddExceptionAsync("LIRR", rwy07);
        await _repo.AddExceptionAsync("LIRR", exc);

        var rwy25 = await _repo.AddAlternativeAsync("LIRR", rwy07);

        var points = (await _repo.ListFlowsByAccAsync("LIRR")).Single().Points;
        Assert.Equal(rwy25, points[^1].Id);
        Assert.Equal(0, points[^1].VariantDepth);
    }

    [Fact]
    public async Task Group_Shares_Cop_And_Receiver_When_One_Row_Changes()
    {
        var flowId = await _repo.AddFlowAsync("LIRR", Flow());
        var srcId = await _repo.AddPointAsync("LIRR", flowId, Point("BIRSU", 150, _ftwrId));
        var altId = await _repo.AddAlternativeAsync("LIRR", srcId);

        // CoP e ricevente sono l'identità dell'accordo: cambiarli su una riga li cambia sul gruppo.
        await _repo.UpdatePointAsync("LIRR", altId, Point("PISIP", 130, _tsId));

        var points = (await _repo.ListFlowsByAccAsync("LIRR")).Single().Points;
        Assert.All(points, p => Assert.Equal("PISIP", p.Cop));
        Assert.All(points, p => Assert.Equal(_tsId, p.NextSectorId));
        // Il livello invece resta di ciascuna riga: è proprio ciò che l'alternativa differenzia.
        Assert.Equal(150, points[0].LevelValue);
        Assert.Equal(130, points[1].LevelValue);
    }

    [Fact]
    public async Task A_GroupWide_Row_Cannot_Be_Nested()
    {
        // «Vale per tutte le alternative» e «appartengo a questa» sono in contraddizione.
        var flowId = await _repo.AddFlowAsync("LIRR", Flow());
        var srcId = await _repo.AddPointAsync("LIRR", flowId, Point("BIRSU", 150, _ftwrId));
        var excId = await _repo.AddExceptionAsync("LIRR", srcId);

        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() =>
            _repo.UpdatePointAsync("LIRR", excId, Point("BIRSU", 130, _ftwrId) with { IsGroupWide = true }));
    }

    [Fact]
    public async Task GroupWide_Flag_Ignored_Outside_A_Group()
    {
        var flowId = await _repo.AddFlowAsync("LIRR", Flow());
        var id = await _repo.AddPointAsync("LIRR", flowId, Point("BIRSU", 150, _ftwrId) with { IsGroupWide = true });

        // Una riga singola non ha alternative da scavalcare.
        var p = Assert.Single((await _repo.ListFlowsByAccAsync("LIRR")).Single().Points);
        Assert.False(p.IsGroupWide);
        Assert.Null(p.VariantGroup);
        Assert.Equal(id, p.Id);
    }

    [Fact]
    public async Task Moving_A_Row_Carries_Its_Subtree()
    {
        // ⚠️ Il difetto che questo test presidia non darebbe nessun errore: la capofila si sposta, le sue
        // eccezioni restano indietro e passano ad altra alternativa continuando a dire quello che dicevano.
        var flowId = await _repo.AddFlowAsync("LIRR", Flow());
        var rwy07 = await _repo.AddPointAsync("LIRR", flowId, Point("BIRSU", 150, _ftwrId) with { ConditionLabel = "07" });
        var exc07 = await _repo.AddExceptionAsync("LIRR", rwy07);
        var rwy25 = await _repo.AddAlternativeAsync("LIRR", rwy07);

        await _repo.MovePointAsync("LIRR", rwy07, up: false);   // la 07 scende sotto la 25

        var points = (await _repo.ListFlowsByAccAsync("LIRR")).Single().Points;
        Assert.Equal(new[] { rwy25, rwy07, exc07 }, points.Select(p => p.Id));
        Assert.Equal(new[] { 0, 0, 1 }, points.Select(p => p.VariantDepth));
    }

    [Fact]
    public async Task Detaching_Takes_The_Subtree_And_Restarts_From_Zero()
    {
        var flowId = await _repo.AddFlowAsync("LIRR", Flow());
        var rwy07 = await _repo.AddPointAsync("LIRR", flowId, Point("BIRSU", 150, _ftwrId) with { ConditionLabel = "07" });
        var exc = await _repo.AddExceptionAsync("LIRR", rwy07);
        var deeper = await _repo.AddExceptionAsync("LIRR", exc);
        await _repo.AddAlternativeAsync("LIRR", rwy07);   // così il gruppo d'origine sopravvive

        await _repo.DetachVariantAsync("LIRR", exc);

        var points = (await _repo.ListFlowsByAccAsync("LIRR")).Single().Points;
        var staccate = points.Where(p => p.Id == exc || p.Id == deeper).ToList();
        // Il pezzo staccato riparte da zero e resta un gruppo suo: la sua struttura interna sopravvive.
        Assert.Equal(new[] { 0, 1 }, staccate.Select(p => p.VariantDepth));
        Assert.NotNull(staccate[0].VariantGroup);
        Assert.Equal(staccate[0].VariantGroup, staccate[1].VariantGroup);
        Assert.NotEqual(points.First(p => p.Id == rwy07).VariantGroup, staccate[0].VariantGroup);
    }

    [Fact]
    public async Task Detaching_Leaves_No_Group_Of_One()
    {
        var flowId = await _repo.AddFlowAsync("LIRR", Flow());
        var srcId = await _repo.AddPointAsync("LIRR", flowId, Point("BIRSU", 150, _ftwrId));
        var altId = await _repo.AddAlternativeAsync("LIRR", srcId);

        await _repo.DetachVariantAsync("LIRR", altId);

        var points = (await _repo.ListFlowsByAccAsync("LIRR")).Single().Points;
        Assert.All(points, p => Assert.Null(p.VariantGroup));   // sciolto anche il superstite
        Assert.All(points, p => Assert.Equal(0, p.VariantDepth));
    }

    [Fact]
    public async Task Deleting_The_Last_Sibling_Dissolves_The_Group()
    {
        var flowId = await _repo.AddFlowAsync("LIRR", Flow());
        var srcId = await _repo.AddPointAsync("LIRR", flowId, Point("BIRSU", 150, _ftwrId));
        var altId = await _repo.AddAlternativeAsync("LIRR", srcId);

        await _repo.DeletePointAsync("LIRR", altId);

        var p = Assert.Single((await _repo.ListFlowsByAccAsync("LIRR")).Single().Points);
        Assert.Null(p.VariantGroup);
    }

    // ---- Editor: trascinamento, duplicazione di gruppo, ricevente in blocco ----

    [Fact]
    public async Task Dragging_Lands_After_The_Target_Going_Down_And_Before_It_Going_Up()
    {
        var flowId = await _repo.AddFlowAsync("LIRR", Flow());
        var a = await _repo.AddPointAsync("LIRR", flowId, Point("AAA", 100, _ftwrId));
        var b = await _repo.AddPointAsync("LIRR", flowId, Point("BBB", 110, _ftwrId));
        var c = await _repo.AddPointAsync("LIRR", flowId, Point("CCC", 120, _ftwrId));

        // Scendendo si va DOPO il bersaglio: chi trascina A su C se lo aspetta sotto C, non sopra.
        await _repo.MovePointToAsync("LIRR", a, c);
        var cops = (await _repo.ListFlowsByAccAsync("LIRR")).Single().Points.Select(p => p.Cop).ToArray();
        Assert.Equal(new[] { "BBB", "CCC", "AAA" }, cops);

        // Salendo si va PRIMA: A su B torna in testa.
        await _repo.MovePointToAsync("LIRR", a, b);
        cops = (await _repo.ListFlowsByAccAsync("LIRR")).Single().Points.Select(p => p.Cop).ToArray();
        Assert.Equal(new[] { "AAA", "BBB", "CCC" }, cops);

        // Su sé stessa: niente da fare, e nessun ordine da riscrivere.
        await _repo.MovePointToAsync("LIRR", a, a);
        cops = (await _repo.ListFlowsByAccAsync("LIRR")).Single().Points.Select(p => p.Cop).ToArray();
        Assert.Equal(new[] { "AAA", "BBB", "CCC" }, cops);
    }

    [Fact]
    public async Task Dragging_Carries_The_Subtree_And_Refuses_To_Enter_Itself()
    {
        var flowId = await _repo.AddFlowAsync("LIRR", Flow());
        var rwy07 = await _repo.AddPointAsync("LIRR", flowId, Point("BIRSU", 150, _ftwrId) with { ConditionLabel = "07" });
        var exc07 = await _repo.AddExceptionAsync("LIRR", rwy07);
        var rwy25 = await _repo.AddAlternativeAsync("LIRR", rwy07);

        // Trascinare la 07 sulla 25 porta con sé la sua eccezione: lasciarla indietro la darebbe alla 25,
        // senza nessun errore e continuando a dire quello che diceva.
        await _repo.MovePointToAsync("LIRR", rwy07, rwy25);
        var points = (await _repo.ListFlowsByAccAsync("LIRR")).Single().Points;
        Assert.Equal(new[] { rwy25, rwy07, exc07 }, points.Select(p => p.Id));
        Assert.Equal(new[] { 0, 0, 1 }, points.Select(p => p.VariantDepth));

        // Dentro sé stessa non c'è dove andare: il bersaglio è nel blocco che si sta spostando.
        await _repo.MovePointToAsync("LIRR", rwy07, exc07);
        points = (await _repo.ListFlowsByAccAsync("LIRR")).Single().Points;
        Assert.Equal(new[] { rwy25, rwy07, exc07 }, points.Select(p => p.Id));
    }

    [Fact]
    public async Task Dragging_Between_Flows_Is_A_Noop()
    {
        // Un accordo appartiene al suo gruppo di traffico: spostarlo altrove è un'altra operazione, non un riordino.
        var arrivi = await _repo.AddFlowAsync("LIRR", Flow());
        var partenze = await _repo.AddFlowAsync("LIRR", new TransferFlowInput
        {
            OwningSectorId = _neId, Kind = TransferFlowKind.Departure, AirportIcao = "LIRF", Description = "test",
        });
        var a = await _repo.AddPointAsync("LIRR", arrivi, Point("AAA", 100, _ftwrId));
        var z = await _repo.AddPointAsync("LIRR", partenze, Point("ZZZ", 200, _ftwrId));

        await _repo.MovePointToAsync("LIRR", a, z);

        var flows = await _repo.ListFlowsByAccAsync("LIRR");
        Assert.Equal("AAA", Assert.Single(flows.Single(f => f.Id == arrivi).Points).Cop);
        Assert.Equal("ZZZ", Assert.Single(flows.Single(f => f.Id == partenze).Points).Cop);
    }

    [Fact]
    public async Task Duplicating_A_Group_Copies_The_Outline_Next_To_It()
    {
        var flowId = await _repo.AddFlowAsync("LIRR", Flow());
        var rwy07 = await _repo.AddPointAsync("LIRR", flowId, Point("BIRSU", 150, _ftwrId) with { ConditionLabel = "07" });
        await _repo.AddExceptionAsync("LIRR", rwy07);
        var trasversale = await _repo.AddAlternativeAsync("LIRR", rwy07);
        await _repo.UpdatePointAsync("LIRR", trasversale,
            Point("BIRSU", 130, _ftwrId) with { ConditionCustomLabel = "di notte", IsGroupWide = true });

        var copiate = await _repo.DuplicateVariantGroupAsync("LIRR", rwy07);

        Assert.Equal(3, copiate);
        var points = (await _repo.ListFlowsByAccAsync("LIRR")).Single().Points;
        var originali = points.Take(3).ToList();
        var copie = points.Skip(3).ToList();
        // La copia nasce ACCANTO all'originale, non dentro: gruppo nuovo, in coda.
        Assert.NotEqual(originali[0].VariantGroup, copie[0].VariantGroup);
        Assert.All(copie, p => Assert.Equal(copie[0].VariantGroup, p.VariantGroup));
        // Profondità e riga trasversale sono ciò che rende utile duplicare un gruppo invece delle sue righe.
        Assert.Equal(new[] { 0, 1, 0 }, copie.Select(p => p.VariantDepth));
        Assert.True(copie[^1].IsGroupWide);
        // ⚠️ Qui la condizione SI copia: è l'opposto dell'alternativa, e le due operazioni condividono CopyOf.
        Assert.Equal("07", copie[0].ConditionLabel);
        Assert.Equal("di notte", copie[^1].ConditionCustomLabel);
    }

    [Fact]
    public async Task Duplicating_Outside_A_Group_Does_Nothing()
    {
        var flowId = await _repo.AddFlowAsync("LIRR", Flow());
        var id = await _repo.AddPointAsync("LIRR", flowId, Point("BIRSU", 150, _ftwrId));

        Assert.Equal(0, await _repo.DuplicateVariantGroupAsync("LIRR", id));
        Assert.Single((await _repo.ListFlowsByAccAsync("LIRR")).Single().Points);
    }

    [Fact]
    public async Task Bulk_Receiver_Reaches_The_Whole_Group_Of_A_Selected_Row()
    {
        // Quando un settore cambia nome, il ricevente va cambiato su decine di righe: basta saltarne una perché
        // il documento dica due cose. E una selezione parziale non deve spaccare l'invariante del gruppo.
        var flowId = await _repo.AddFlowAsync("LIRR", Flow());
        var srcId = await _repo.AddPointAsync("LIRR", flowId, Point("BIRSU", 150, _ftwrId));
        var altId = await _repo.AddAlternativeAsync("LIRR", srcId);
        var sola = await _repo.AddPointAsync("LIRR", flowId, Point("ELKAP", 130, _ftwrId));
        var estranea = await _repo.AddPointAsync("LIRR", flowId, Point("OSTIA", 110, _ftwrId));

        var toccate = await _repo.SetReceiverAsync("LIRR", new[] { srcId, sola }, _tsId);

        Assert.Equal(2, toccate);   // le righe scelte, non quelle raggiunte per propagazione
        var points = (await _repo.ListFlowsByAccAsync("LIRR")).Single().Points;
        Assert.Equal(_tsId, points.First(p => p.Id == altId).NextSectorId);   // sorella, non selezionata
        Assert.Equal(_tsId, points.First(p => p.Id == sola).NextSectorId);
        Assert.Equal(_ftwrId, points.First(p => p.Id == estranea).NextSectorId);

        // Nessuno selezionato: niente da fare, e nessuna riga da svuotare per sbaglio.
        Assert.Equal(0, await _repo.SetReceiverAsync("LIRR", Array.Empty<int>(), null));
        Assert.All((await _repo.ListFlowsByAccAsync("LIRR")).Single().Points, p => Assert.NotNull(p.NextSectorId));
    }

    [Fact]
    public async Task Seed_Populates_Demo_Flows()
    {
        await RomaTransferSeed.SeedAsync(_db);
        var flows = await _repo.ListFlowsByAccAsync("LIRR");
        Assert.NotEmpty(flows);
        Assert.Contains(flows, f => f.OwningSectorCallsign == "LIRR_NE_CTR" && f.Kind == TransferFlowKind.Arrival
            && f.Points.Any(p => p.Cop == "VALMA" && p.LevelText == "FL130- ↓"));   // seed: ≤ → «-», stato backfill discesa → «↓»
    }
}
