using Vipi.Application.Aor;
using Vipi.Application.Content;
using Vipi.AuroraBridge.Contracts;
using Vipi.Domain;

namespace Vipi.Application.Tests;

/// <summary>
/// Matching del bridge Aurora: dal contesto di un volo ai punti di trasferimento candidati.
/// Casi modellati sui dati reali della ACC LIBB (CoP fix, «ALL», «ALL to GR», range di aerovie «Y01-Y12»,
/// parità semicircolare quasi ovunque).
/// </summary>
public class TransferMatcherTests
{
    private const string Me = "LIBB_ES_CTR";

    private static Topology Topo() => new()
    {
        Sectors = new[] { "LIBB_ES_CTR", "LIBB_CTR", "LIRR_NC_CTR", "LIRR_CTR", "LIRN_US0_APP", "LIBP_APP", "LIBP_TWR" },
        Parent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LIBB_ES_CTR"] = "LIBB_CTR",
            ["LIRR_NC_CTR"] = "LIRR_CTR",
            ["LIRN_US0_APP"] = "LIRR_CTR",
            ["LIBP_APP"] = "LIBB_ES_CTR",
            ["LIBP_TWR"] = "LIBP_APP",
        },
        Rules = Array.Empty<UnificationRuleSpec>(),
    };

    private static HashSet<string> Online(params string[] cs) => new(cs, StringComparer.OrdinalIgnoreCase);

    private static TransferPointRow Point(
        int id, string cop, int? level = 210, LevelConstraint constraint = LevelConstraint.AtOrBelow,
        LevelParity parity = LevelParity.Any, string? next = "LIRR_NC_CTR", LevelUnit unit = LevelUnit.Fl,
        string? special = null, string? conditionLabel = null, string? areaLabel = null) => new()
        {
            Id = id,
            Cop = cop,
            LevelValue = level,
            LevelUnit = unit,
            LevelConstraint = constraint,
            LevelSpecial = special,
            Parity = parity,
            LevelText = LevelFormatting.Format(level, unit, constraint, special, parity),
            NextSectorCallsign = next,
            ConditionLabel = conditionLabel,
            ConditionAreaLabel = areaLabel,
            Order = id,
        };

    private static TransferFlowRow Flow(
        int id, TransferFlowKind kind, string? airport, params TransferPointRow[] points) => new()
        {
            Id = id,
            AccCode = "LIBB",
            OwningSectorId = 1,
            OwningSectorCallsign = Me,
            Kind = kind,
            AirportIcao = airport,
            Order = id,
            Points = points,
        };

    private static TransferResolveRequest Request(
        string owner = Me, string? dep = "LIBD", string? arr = "LIRF", int? cruise = 350,
        string? route = "PISIP UM984 ASPIR", params string[] fixes) => new()
        {
            OwnerCallsign = owner,
            Departure = dep,
            Arrival = arr,
            CruiseLevel = cruise,
            Route = route,
            RouteFixes = fixes.Select(f => new RouteFix(f, null)).ToList(),
        };

    private static TransferResolveResponse Run(
        TransferResolveRequest req, IReadOnlyList<TransferFlowRow> flows, IReadOnlySet<string>? online = null) =>
        TransferMatcher.Match(req, flows, Topo(), online ?? Online(), "LIBB",
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

    // --- accoppiamento flusso ↔ volo ---

    [Fact]
    public void Arrivo_sceglie_il_flusso_dell_aeroporto_di_destinazione()
    {
        var flows = new[]
        {
            Flow(1, TransferFlowKind.Arrival, "LIRF", Point(10, "ASPIR")),
            Flow(2, TransferFlowKind.Arrival, "LIRN", Point(11, "AMSOR")),
        };

        var res = Run(Request(arr: "LIRF"), flows);

        Assert.Equal("ASPIR", res.Candidates[0].Cop);
        Assert.Contains("arrivo a LIRF", res.Candidates[0].Reasons);
    }

    [Fact]
    public void Partenza_e_arrivo_non_si_confondono()
    {
        var flows = new[]
        {
            Flow(1, TransferFlowKind.Departure, "LIBD", Point(10, "PISIP", level: 140, constraint: LevelConstraint.Exact)),
            Flow(2, TransferFlowKind.Arrival, "LIBD", Point(11, "PISIP")),
        };

        var res = Run(Request(dep: "LIBD", arr: "EDDF"), flows);

        Assert.Equal(10, res.Candidates[0].PointId);
        Assert.Equal("Departure", res.Candidates[0].FlowKind);
    }

    [Fact]
    public void Sorvolo_senza_aeroporto_e_sempre_candidato_ma_sotto_l_aeroporto_giusto()
    {
        var flows = new[]
        {
            Flow(1, TransferFlowKind.Overflight, null, Point(10, "ALL")),
            Flow(2, TransferFlowKind.Arrival, "LIRF", Point(11, "ASPIR")),
        };

        var res = Run(Request(arr: "LIRF", fixes: "ASPIR"), flows);

        Assert.Equal(11, res.Candidates[0].PointId);
        Assert.Contains(res.Candidates, c => c.PointId == 10);
    }

    // --- CoP ---

    [Fact]
    public void CoP_presente_nei_fix_di_Aurora_batte_quello_solo_nel_piano()
    {
        var flows = new[]
        {
            Flow(1, TransferFlowKind.Arrival, "LIRF", Point(10, "ASPIR"), Point(11, "PISIP")),
        };

        // ASPIR è nei fix risolti da Aurora, PISIP compare solo nella stringa di rotta.
        var res = Run(Request(route: "PISIP UM984 ASPIR", fixes: "ASPIR"), flows);

        Assert.Equal(10, res.Candidates[0].PointId);
    }

    [Fact]
    public void CoP_riporta_l_ETO_quando_Aurora_lo_conosce()
    {
        var req = Request(fixes: "ASPIR");
        req.RouteFixes = new List<RouteFix> { new("ASPIR", "0925") };
        var flows = new[] { Flow(1, TransferFlowKind.Arrival, "LIRF", Point(10, "ASPIR")) };

        var res = Run(req, flows);

        Assert.Equal("0925", res.Candidates[0].CopEto);
        Assert.Contains(res.Candidates[0].Reasons, r => r.Contains("ETO 0925"));
    }

    [Fact]
    public void CoP_assente_dalla_rotta_resta_candidato_ma_penalizzato()
    {
        var flows = new[] { Flow(1, TransferFlowKind.Arrival, "LIRF", Point(10, "XOLTA")) };

        var res = Run(Request(route: "PISIP UM984 ASPIR"), flows);

        var c = Assert.Single(res.Candidates);
        Assert.Contains(c.Reasons, r => r.Contains("non trovato in rotta"));
        Assert.True(c.Score < 1.0);
    }

    [Fact]
    public void Jolly_ALL_vale_per_tutti_e_ALL_to_GR_avvisa()
    {
        var flows = new[]
        {
            Flow(1, TransferFlowKind.Overflight, null, Point(10, "ALL"), Point(11, "ALL to GR")),
        };

        var res = Run(Request(), flows);

        Assert.Contains(res.Candidates, c => c.PointId == 10 && c.Reasons.Any(r => r.Contains("tutti i punti")));
        Assert.Contains(res.Candidates, c => c.PointId == 11 && c.Reasons.Any(r => r.Contains("verificata a mano")));
    }

    [Fact]
    public void Range_di_aerovie_riconosce_l_aerovia_in_rotta()
    {
        var flows = new[] { Flow(1, TransferFlowKind.Arrival, "LIBP", Point(10, "Y01-Y12")) };

        var res = Run(Request(arr: "LIBP", route: "PISIP Y08 ASPIR"), flows);

        Assert.Contains(res.Candidates[0].Reasons, r => r.Contains("aerovia Y08"));
    }

    [Fact]
    public void Range_di_aerovie_fuori_intervallo_non_conta()
    {
        var flows = new[] { Flow(1, TransferFlowKind.Arrival, "LIBP", Point(10, "Y01-Y12")) };

        var res = Run(Request(arr: "LIBP", route: "PISIP Y44 ASPIR"), flows);

        Assert.Contains(res.Candidates[0].Reasons, r => r.Contains("nessuna aerovia"));
    }

    // --- parità semicircolare ---

    [Fact]
    public void Parita_coerente_premia_e_parita_opposta_affonda()
    {
        var flows = new[]
        {
            Flow(1, TransferFlowKind.Arrival, "LIRF",
                Point(10, "ASPIR", parity: LevelParity.Odd),
                Point(11, "ASPIR", parity: LevelParity.Even)),
        };

        // FL350 → dispari.
        var res = Run(Request(cruise: 350, fixes: "ASPIR"), flows);

        Assert.Equal(10, res.Candidates[0].PointId);
        Assert.Contains(res.Candidates[0].Reasons, r => r.Contains("dispari"));
        Assert.True(res.Candidates[0].Score > res.Candidates[1].Score);
    }

    // --- condizioni ---

    [Fact]
    public void Condizione_pista_soddisfatta_quando_coincide_con_CTRLRWY()
    {
        var req = Request(arr: "LIRF", fixes: "ASPIR");
        req.RunwaysInUse["LIRF"] = new RunwayConfig { Departure = { "25" }, Arrival = { "16L", "16R" } };
        var flows = new[]
        {
            Flow(1, TransferFlowKind.Arrival, "LIRF",
                Point(10, "ASPIR", conditionLabel: "RWY 16L"),
                Point(11, "ASPIR", conditionLabel: "RWY 25")),
        };

        var res = Run(req, flows);

        Assert.Equal(10, res.Candidates[0].PointId);
        Assert.Equal("matched", res.Candidates[0].Condition.Match);
        Assert.Equal("unmatched", res.Candidates.Single(c => c.PointId == 11).Condition.Match);
    }

    [Fact]
    public void Condizione_di_area_resta_non_verificabile_e_avvisa()
    {
        var flows = new[]
        {
            Flow(1, TransferFlowKind.Arrival, "LIRF", Point(10, "ASPIR", areaLabel: "LOTAR attiva")),
        };

        var res = Run(Request(fixes: "ASPIR"), flows);

        Assert.Equal("unknown", res.Candidates[0].Condition.Match);
        Assert.Contains(res.Warnings, w => w.Contains("non sono verificabili"));
    }

    [Fact]
    public void Senza_configurazione_piste_la_condizione_e_unknown_non_unmatched()
    {
        var flows = new[]
        {
            Flow(1, TransferFlowKind.Arrival, "LIRF", Point(10, "ASPIR", conditionLabel: "RWY 16L")),
        };

        var res = Run(Request(fixes: "ASPIR"), flows);

        Assert.Equal("unknown", res.Candidates[0].Condition.Match);
    }

    // --- copertura top-down ---

    [Fact]
    public void I_flussi_di_un_APP_chiuso_che_sto_coprendo_sono_miei()
    {
        var flows = new[]
        {
            new TransferFlowRow
            {
                Id = 1, AccCode = "LIBB", OwningSectorId = 2, OwningSectorCallsign = "LIBP_APP",
                Kind = TransferFlowKind.Arrival, AirportIcao = "LIBP", Order = 1,
                Points = new[] { Point(10, "ASPIR") },
            },
        };

        var res = Run(Request(arr: "LIBP", fixes: "ASPIR"), flows, Online());

        Assert.Single(res.Candidates);
    }

    [Fact]
    public void I_flussi_di_un_APP_online_non_sono_piu_miei()
    {
        var flows = new[]
        {
            new TransferFlowRow
            {
                Id = 1, AccCode = "LIBB", OwningSectorId = 2, OwningSectorCallsign = "LIBP_APP",
                Kind = TransferFlowKind.Arrival, AirportIcao = "LIBP", Order = 1,
                Points = new[] { Point(10, "ASPIR") },
            },
        };

        var res = Run(Request(arr: "LIBP", fixes: "ASPIR"), flows, Online("LIBP_APP"));

        Assert.Empty(res.Candidates);
        Assert.Contains(res.Warnings, w => w.Contains("Nessun flusso"));
    }

    // --- ente successivo ---

    [Fact]
    public void Ente_successivo_offline_risale_la_gerarchia()
    {
        var flows = new[] { Flow(1, TransferFlowKind.Arrival, "LIRF", Point(10, "ASPIR", next: "LIRR_NC_CTR")) };

        var res = Run(Request(fixes: "ASPIR"), flows, Online("LIRR_CTR"));

        Assert.Equal("LIRR_CTR", res.Candidates[0].ResolvedHandler);
        Assert.True(res.Candidates[0].HandlerOnline);
    }

    [Fact]
    public void Nessuno_online_porta_a_UNICOM()
    {
        var flows = new[] { Flow(1, TransferFlowKind.Arrival, "LIRF", Point(10, "ASPIR")) };

        var res = Run(Request(fixes: "ASPIR"), flows);

        Assert.Equal("UNICOM", res.Candidates[0].ResolvedHandler);
        Assert.False(res.Candidates[0].HandlerOnline);
    }

    [Fact]
    public void Next_ATC_gia_impostato_in_Aurora_premia_il_punto_coerente()
    {
        var req = Request(fixes: "ASPIR");
        req.NextStation = "LIRN_US0_APP";
        var flows = new[]
        {
            Flow(1, TransferFlowKind.Arrival, "LIRF",
                Point(10, "ASPIR", next: "LIRR_NC_CTR"),
                Point(11, "ASPIR", next: "LIRN_US0_APP")),
        };

        var res = Run(req, flows);

        Assert.Equal(11, res.Candidates[0].PointId);
    }

    // --- valore per Aurora ---

    [Fact]
    public void Il_livello_FL_diventa_il_numero_nudo_per_default()
    {
        var flows = new[] { Flow(1, TransferFlowKind.Arrival, "LIRF", Point(10, "ASPIR", level: 210)) };

        var res = Run(Request(fixes: "ASPIR"), flows);

        Assert.Equal("210", res.Candidates[0].AuroraValue);
        Assert.True(res.Candidates[0].Writable);
    }

    [Fact]
    public void Con_la_convenzione_FL_il_valore_e_prefissato()
    {
        var flows = new[] { Flow(1, TransferFlowKind.Arrival, "LIRF", Point(10, "ASPIR", level: 210)) };

        var res = TransferMatcher.Match(Request(fixes: "ASPIR"), flows, Topo(), Online(), "LIBB",
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
            new TransferMatchOptions(AuroraLabelConvention.FlPrefixed));

        Assert.Equal("FL210", res.Candidates[0].AuroraValue);
    }

    [Fact]
    public void I_piedi_restano_numerici_anche_con_la_convenzione_FL()
    {
        var flows = new[]
        {
            Flow(1, TransferFlowKind.Arrival, "LIRF", Point(10, "ASPIR", level: 5000, unit: LevelUnit.Feet)),
        };

        var res = TransferMatcher.Match(Request(fixes: "ASPIR"), flows, Topo(), Online(), "LIBB",
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
            new TransferMatchOptions(AuroraLabelConvention.FlPrefixed));

        Assert.Equal("5000", res.Candidates[0].AuroraValue);
    }

    [Fact]
    public void Livello_speciale_e_scrivibile_ma_ripulito_dal_separatore()
    {
        var flows = new[]
        {
            Flow(1, TransferFlowKind.Arrival, "LIRF",
                Point(10, "ASPIR", level: null, constraint: LevelConstraint.Special, special: "per aerovia; come da LoA")),
        };

        var res = Run(Request(fixes: "ASPIR"), flows);

        Assert.True(res.Candidates[0].Writable);
        Assert.DoesNotContain(";", res.Candidates[0].AuroraValue);
    }

    [Fact]
    public void Livello_mancante_non_e_scrivibile()
    {
        var flows = new[]
        {
            Flow(1, TransferFlowKind.Arrival, "LIRF", Point(10, "ASPIR", level: null, constraint: LevelConstraint.Exact)),
        };

        var res = Run(Request(fixes: "ASPIR"), flows);

        Assert.False(res.Candidates[0].Writable);
        Assert.Null(res.Candidates[0].AuroraValue);
        Assert.Contains(res.Warnings, w => w.Contains("livello scrivibile"));
    }

    // --- casi degeneri ---

    [Fact]
    public void Callsign_ignoto_non_propone_nulla_e_lo_dice()
    {
        var flows = new[] { Flow(1, TransferFlowKind.Arrival, "LIRF", Point(10, "ASPIR")) };

        var res = Run(Request(owner: "LIZZ_AEW_CTR"), flows);

        Assert.Empty(res.Candidates);
        Assert.Contains(res.Warnings, w => w.Contains("non riconosciuto"));
    }

    [Fact]
    public void Callsign_abbreviato_viene_ricondotto_al_settore()
    {
        var flows = new[] { Flow(1, TransferFlowKind.Arrival, "LIRF", Point(10, "ASPIR")) };

        var res = Run(Request(owner: "LIBB_ES", fixes: "ASPIR"), flows);

        Assert.Equal("LIBB_ES_CTR", res.ResolvedOwner);
        Assert.Single(res.Candidates);
    }

    [Fact]
    public void Il_numero_di_candidati_e_limitato()
    {
        var points = Enumerable.Range(1, 20).Select(i => Point(i, "ALL")).ToArray();
        var flows = new[] { Flow(1, TransferFlowKind.Overflight, null, points) };

        var res = TransferMatcher.Match(Request(), flows, Topo(), Online(), "LIBB",
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, new TransferMatchOptions(MaxCandidates: 5));

        Assert.Equal(5, res.Candidates.Count);
    }
}
