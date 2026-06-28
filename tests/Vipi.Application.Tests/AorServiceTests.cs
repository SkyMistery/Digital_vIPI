using Vipi.Application.Aor;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>Scenari di verità S1–S10 di SPEC_Logica_AoR §5 (la parte più critica del sistema).
/// Settore == posizione: ogni settore è identificato dal proprio callsign e possiede sé stesso.</summary>
public class AorServiceTests
{
    private readonly AorService _sut = new();

    // Fixture topologia Roma (albero di contenimento):
    //   LIRR_NE_CTR ──< LIRP_APP ──< LIRP_TWR
    //               └─< LIRR_TS_CTR
    private static Topology RomaTopology() => new()
    {
        Sectors = new[] { "LIRR_NE_CTR", "LIRR_TS_CTR", "LIRP_APP", "LIRP_TWR" },
        Parent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LIRR_TS_CTR"] = "LIRR_NE_CTR",
            ["LIRP_APP"]    = "LIRR_NE_CTR",
            ["LIRP_TWR"]    = "LIRP_APP",
        },
        Rules = Array.Empty<UnificationRuleSpec>(),
    };

    private static IReadOnlySet<string> Online(params string[] cs) =>
        new HashSet<string>(cs, StringComparer.OrdinalIgnoreCase);

    [Fact] // S1 — top-down completo: solo NE online → tutto Covered
    public void S1_OnlySelfOnline_AllCovered()
    {
        var r = _sut.Resolve(RomaTopology(), "LIRR_NE_CTR", Online("LIRR_NE_CTR"));

        Assert.All(r.State.Values, s => Assert.Equal(SectorState.Covered, s));
    }

    [Fact] // S2 — subordinato APP (Pisa) si connette → settori Pisa Online, resto Covered
    public void S2_SubordinateApp_Online()
    {
        var r = _sut.Resolve(RomaTopology(), "LIRR_NE_CTR", Online("LIRR_NE_CTR", "LIRP_APP"));

        Assert.Equal(SectorState.Online, r.State["LIRP_APP"]);
        Assert.Equal(SectorState.Covered, r.State["LIRR_NE_CTR"]);
        // LIRP_TWR: APP online copre la TWR offline → Online (gestita da LIRP_APP via top-down)
        Assert.Equal(SectorState.Online, r.State["LIRP_TWR"]);
    }

    [Fact] // S5 — sotto-settore TS "ruba" all'NE
    public void S5_SubSectorTs_StealsFromNe()
    {
        var r = _sut.Resolve(RomaTopology(), "LIRR_NE_CTR", Online("LIRR_NE_CTR", "LIRR_TS_CTR"));

        Assert.Equal(SectorState.Online, r.State["LIRR_TS_CTR"]);
        Assert.Equal(SectorState.Covered, r.State["LIRR_NE_CTR"]);
        Assert.Equal("LIRR_TS_CTR", r.Ownership["LIRR_TS_CTR"]);
    }

    [Fact] // S6 — catena a tre livelli con "buco": TWR online, APP offline
    public void S6_TwrOnline_AppOffline()
    {
        var r = _sut.Resolve(RomaTopology(), "LIRR_NE_CTR", Online("LIRR_NE_CTR", "LIRP_TWR"));

        // TWR gestisce il proprio settore; il settore dell'APP (offline) ricade su NE (Covered).
        Assert.Equal(SectorState.Online, r.State["LIRP_TWR"]);
        Assert.Equal(SectorState.Covered, r.State["LIRP_APP"]);
        Assert.Equal("LIRR_NE_CTR", r.Ownership["LIRP_APP"]);
    }

    [Fact] // Invariante: monotonia top-down — aggiungere un subordinato non riporta Online→Covered
    public void Invariant_TopDownMonotonic()
    {
        var before = _sut.Resolve(RomaTopology(), "LIRR_NE_CTR", Online("LIRR_NE_CTR"));
        var after  = _sut.Resolve(RomaTopology(), "LIRR_NE_CTR", Online("LIRR_NE_CTR", "LIRP_APP"));

        Assert.Equal(SectorState.Covered, before.State["LIRP_APP"]);
        Assert.Equal(SectorState.Online, after.State["LIRP_APP"]);
    }
}

/// <summary>S4 — split di settore ACC. Con la fusione settore/posizione lo split SU/ES è puro contenimento
/// (ES figlio di SU); la <see cref="UnificationRuleSpec"/> resta per riassegnazioni arbitrarie non esprimibili dall'albero.</summary>
public class AorUnificationTests
{
    private readonly AorService _sut = new();

    // SU root con figlio ES (lo split classico): nessuna regola necessaria.
    private static Topology SplitTopology() => new()
    {
        Sectors = new[] { "LIRR_SU_CTR", "LIRR_ES_CTR" },
        Parent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LIRR_ES_CTR"] = "LIRR_SU_CTR",
        },
        Rules = Array.Empty<UnificationRuleSpec>(),
    };

    private static IReadOnlySet<string> Online(params string[] cs) =>
        new HashSet<string>(cs, StringComparer.OrdinalIgnoreCase);

    [Fact] // S4-A — solo SU online → SU copre sé stesso ed ES (via contenimento)
    public void S4_SuAlone_CoversBoth()
    {
        var r = _sut.Resolve(SplitTopology(), "LIRR_SU_CTR", Online("LIRR_SU_CTR"));

        Assert.Equal(SectorState.Covered, r.State["LIRR_SU_CTR"]);
        Assert.Equal(SectorState.Covered, r.State["LIRR_ES_CTR"]);
    }

    [Fact] // S4-B — ES online → ES diventa Online (puro contenimento, nessuna regola)
    public void S4_EsOnline_SplitActive()
    {
        var r = _sut.Resolve(SplitTopology(), "LIRR_SU_CTR", Online("LIRR_SU_CTR", "LIRR_ES_CTR"));

        Assert.Equal(SectorState.Covered, r.State["LIRR_SU_CTR"]);
        Assert.Equal(SectorState.Online, r.State["LIRR_ES_CTR"]);
        Assert.Equal("LIRR_ES_CTR", r.Ownership["LIRR_ES_CTR"]);
    }

    // Riassegnazione arbitraria: quando LIRR_ES_CTR è online, prende anche TS (normalmente figlio di NE).
    private static Topology ReassignTopology() => new()
    {
        Sectors = new[] { "LIRR_NE_CTR", "LIRR_TS_CTR", "LIRR_ES_CTR" },
        Parent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LIRR_TS_CTR"] = "LIRR_NE_CTR",
            ["LIRR_ES_CTR"] = "LIRR_NE_CTR",
        },
        Rules = new[]
        {
            new UnificationRuleSpec("ES assorbe TS", 10,
                RequiredOnline: new[] { "LIRR_ES_CTR" },
                Assignment: new Dictionary<string, string> { ["LIRR_TS_CTR"] = "LIRR_ES_CTR" }),
        },
    };

    [Fact] // La regola riassegna TS a ES (che l'albero da solo non potrebbe esprimere)
    public void Rule_ReassignsSectorToNonParentOwner()
    {
        var r = _sut.Resolve(ReassignTopology(), "LIRR_NE_CTR", Online("LIRR_NE_CTR", "LIRR_ES_CTR"));

        Assert.Equal("LIRR_ES_CTR", r.Ownership["LIRR_TS_CTR"]);
        Assert.Equal(SectorState.Online, r.State["LIRR_TS_CTR"]);
    }
}
