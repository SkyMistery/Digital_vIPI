using Vipi.Application.Aor;
using Vipi.Application.Content;
using Vipi.Application.Live;
using Vipi.Domain;

namespace Vipi.Application.Tests;

/// <summary>
/// Dispatch per tipo di ente della vista live (doc refactor 12). L'invariante che protegge l'unificazione:
/// per OGNI <see cref="SectorType"/> deve esserci esattamente un descrittore competente — se qualcuno aggiunge
/// un tipo al catalogo senza registrare il descrittore, la pagina risponderebbe «postazione sconosciuta».
/// </summary>
public class LiveStationRegistryTests
{
    private static LiveStationContext Ctx(SectorType type, ApproachKind? approach = null)
    {
        var sector = new SectorRow(1, "LIRR_NE_CTR", type, SectorKind.Acc, "Roma NE", "124.200", 0,
            approach, null, null, null, true, null, false);
        var topo = new Topology
        {
            Sectors = new[] { "LIRR_NE_CTR" },
            Parent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Rules = Array.Empty<UnificationRuleSpec>(),
        };
        var structure = new StructureData
        {
            AccId = 1, AccCode = "LIRR", AccName = "Roma",
            Airports = Array.Empty<AirportRow>(), Sectors = new[] { sector },
        };
        return new LiveStationContext("LIRR_NE_CTR", sector, new AccInfo("LIRR", "Roma"), structure, topo,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>I descrittori reali, senza dipendenze: qui si interroga solo <c>Matches</c>.</summary>
    private static IReadOnlyList<ILiveStationKind> Kinds() => new ILiveStationKind[]
    {
        new AreaLiveStation(null!, null!, null!),
        new ApproachLiveStation(null!, null!, null!, null!),
        new AirportLiveStation(null!, null!),
    };

    [Theory]
    [InlineData(SectorType.Ctr)]
    [InlineData(SectorType.App)]
    [InlineData(SectorType.Twr)]
    [InlineData(SectorType.ITwr)]
    [InlineData(SectorType.Gnd)]
    [InlineData(SectorType.Del)]
    public void Ogni_tipo_di_settore_ha_un_descrittore(SectorType type)
    {
        var matching = Kinds().Where(k => k.Matches(Ctx(type))).ToList();

        Assert.Single(matching);
    }

    [Fact]
    public void Tutti_i_tipi_del_catalogo_sono_coperti()
    {
        // Se domani si aggiunge un SectorType, questo test lo intercetta prima della pagina.
        var scoperti = Enum.GetValues<SectorType>()
            .Where(t => !Kinds().Any(k => k.Matches(Ctx(t))))
            .ToList();

        Assert.Empty(scoperti);
    }

    /// <summary>
    /// 🔴 S49 (29 settembre 2026): a Pratica di Mare la torre fa l'avvicinamento, e chi è su LIRE_TWR deve aprire la
    /// vIPI dell'ENTE. Il descrittore dell'avvicinamento viene prima di quello dell'aeroporto nel registry: basta
    /// che riconosca la posizione di un ente. Una torre qualunque resta all'aeroporto.
    /// </summary>
    [Fact]
    public void Una_torre_che_e_la_posizione_di_un_ente_apre_la_vipi_dell_ente()
    {
        var registry = new LiveStationRegistry(Kinds());

        Assert.IsType<ApproachLiveStation>(registry.For(Ctx(SectorType.Twr) with { UnitCode = "LIRE_APP" }));
        Assert.IsType<AirportLiveStation>(registry.For(Ctx(SectorType.Twr)));
    }

    /// <summary>S50: la posizione di un ente remotizzato (anche una torre) apre il suo gruppo nella vIPI dell'ACC —
    /// il descrittore dell'avvicinamento, che per un ente remotizzato prende la strada del gruppo APP.</summary>
    [Fact]
    public void La_posizione_di_un_ente_remotizzato_apre_il_gruppo_nella_vipi_acc()
    {
        var registry = new LiveStationRegistry(Kinds());

        Assert.IsType<ApproachLiveStation>(registry.For(Ctx(SectorType.Twr) with { UnitInAccVipi = true }));
    }

    [Fact]
    public void Il_registry_sceglie_per_priorita_la_prima_corrispondenza()
    {
        var registry = new LiveStationRegistry(new ILiveStationKind[]
        {
            new FakeKind(Priority: 50, Accepts: true),
            new FakeKind(Priority: 10, Accepts: true),
            new FakeKind(Priority: 1, Accepts: false),
        });

        var scelto = registry.For(Ctx(SectorType.Ctr));

        Assert.Equal(10, scelto!.Priority);
    }

    [Fact]
    public void Nessun_descrittore_competente_non_esplode()
    {
        var registry = new LiveStationRegistry(Array.Empty<ILiveStationKind>());

        Assert.Null(registry.For(Ctx(SectorType.Ctr)));
    }

    /// <summary>Revisione degli enti ATC (S52): un ente spostato nella vIPI ACC trova il SUO gruppo anche quando
    /// online c'è il codice dell'ente e non una posizione (Pratica, tolta la posizione LIRE_APP).</summary>
    [Fact]
    public void Il_gruppo_di_un_ente_si_trova_dall_ente_anche_senza_la_posizione()
    {
        var altro = new AccBlock { Kind = AccBlockKind.AppGroup, Title = "Altro", MemberCallsigns = { "LIRE_APP" } };
        var suo = new AccBlock { Kind = AccBlockKind.AppGroup, Title = "Pratica", MemberCallsigns = { "LIRE_TWR" }, UnitId = 7 };
        var blocchi = new[] { altro, suo };

        Assert.Same(suo, ApproachLiveStation.GruppoDi(blocchi, "LIRE_APP", unitId: 7));
        Assert.Same(altro, ApproachLiveStation.GruppoDi(blocchi, "LIRE_APP", unitId: null));
        Assert.Null(ApproachLiveStation.GruppoDi(blocchi, "LICJ_APP", unitId: null));
    }

    private sealed record FakeKind(int Priority, bool Accepts) : ILiveStationKind
    {
        public bool Matches(LiveStationContext ctx) => Accepts;
        public Task<LiveView> BuildAsync(LiveStationContext ctx, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
