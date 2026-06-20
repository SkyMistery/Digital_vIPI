using Vipi.Application.Aor;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>Scenari di verità S1–S10 di SPEC_Logica_AoR §5 (la parte più critica del sistema).</summary>
public class AorServiceTests
{
    private readonly AorService _sut = new();

    // Fixture topologia Roma (semplificata):
    //   LIRR_NE_CTR ──< LIRP_APP ──< LIRP_TWR
    //               └─< LIRR_TS_CTR
    private static Topology RomaTopology() => new()
    {
        DefaultSectors = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["LIRR_NE_CTR"] = new[] { "NE" },
            ["LIRR_TS_CTR"] = new[] { "TS" },
            ["LIRP_APP"]    = new[] { "PISA" },
            ["LIRP_TWR"]    = new[] { "PISA_TWR" },
        },
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

        Assert.Equal(SectorState.Online, r.State["PISA"]);
        Assert.Equal(SectorState.Covered, r.State["NE"]);
        // PISA_TWR: APP online copre la TWR offline → Online (gestita da LIRP_APP via top-down)
        Assert.Equal(SectorState.Online, r.State["PISA_TWR"]);
    }

    [Fact] // S5 — sotto-settore TS "ruba" all'NE
    public void S5_SubSectorTs_StealsFromNe()
    {
        var r = _sut.Resolve(RomaTopology(), "LIRR_NE_CTR", Online("LIRR_NE_CTR", "LIRR_TS_CTR"));

        Assert.Equal(SectorState.Online, r.State["TS"]);
        Assert.Equal(SectorState.Covered, r.State["NE"]);
        Assert.Equal("LIRR_TS_CTR", r.Ownership["TS"]);
    }

    [Fact] // S6 — catena a tre livelli con "buco": TWR online, APP offline
    public void S6_TwrOnline_AppOffline()
    {
        var r = _sut.Resolve(RomaTopology(), "LIRR_NE_CTR", Online("LIRR_NE_CTR", "LIRP_TWR"));

        // TWR gestisce il proprio settore; il settore dell'APP (offline) ricade su NE (Covered).
        Assert.Equal(SectorState.Online, r.State["PISA_TWR"]);
        Assert.Equal(SectorState.Covered, r.State["PISA"]);
        Assert.Equal("LIRR_NE_CTR", r.Ownership["PISA"]);
    }

    [Fact] // Invariante: monotonia top-down — aggiungere un subordinato non riporta Online→Covered
    public void Invariant_TopDownMonotonic()
    {
        var before = _sut.Resolve(RomaTopology(), "LIRR_NE_CTR", Online("LIRR_NE_CTR"));
        var after  = _sut.Resolve(RomaTopology(), "LIRR_NE_CTR", Online("LIRR_NE_CTR", "LIRP_APP"));

        Assert.Equal(SectorState.Covered, before.State["PISA"]);
        Assert.Equal(SectorState.Online, after.State["PISA"]);
    }
}

/// <summary>S4 — split di settore ACC via UnificationRule.</summary>
public class AorUnificationTests
{
    private readonly AorService _sut = new();

    private static Topology SplitTopology() => new()
    {
        DefaultSectors = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["LIRR_SU_CTR"] = new[] { "SU", "ES" },   // SU possiede SU+ES "da solo"
            ["LIRR_ES_CTR"] = new[] { "ES" },
        },
        Parent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LIRR_ES_CTR"] = "LIRR_SU_CTR",
        },
        Rules = new[]
        {
            // Quando ES è online, ES passa a LIRR_ES_CTR.
            new UnificationRuleSpec("Split SU/ES", 10,
                RequiredOnline: new[] { "LIRR_ES_CTR" },
                Assignment: new Dictionary<string, string> { ["ES"] = "LIRR_ES_CTR" }),
        },
    };

    private static IReadOnlySet<string> Online(params string[] cs) =>
        new HashSet<string>(cs, StringComparer.OrdinalIgnoreCase);

    [Fact] // S4-A — solo SU online → SU copre SU ed ES
    public void S4_SuAlone_CoversBoth()
    {
        var r = _sut.Resolve(SplitTopology(), "LIRR_SU_CTR", Online("LIRR_SU_CTR"));

        Assert.Equal(SectorState.Covered, r.State["SU"]);
        Assert.Equal(SectorState.Covered, r.State["ES"]);
    }

    [Fact] // S4-B — ES online → regola di split attiva → ES diventa Online
    public void S4_EsOnline_SplitActive()
    {
        var r = _sut.Resolve(SplitTopology(), "LIRR_SU_CTR", Online("LIRR_SU_CTR", "LIRR_ES_CTR"));

        Assert.Equal(SectorState.Covered, r.State["SU"]);
        Assert.Equal(SectorState.Online, r.State["ES"]);
        Assert.Equal("LIRR_ES_CTR", r.Ownership["ES"]);
    }
}
