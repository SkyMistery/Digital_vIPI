using Vipi.Application.Aor;
using Vipi.Application.Content;
using Vipi.Application.Live;
using Vipi.Domain;

namespace Vipi.Application.Tests;

/// <summary>
/// Regola dei trasferimenti della vista live (doc refactor 12): «i miei, più quelli dei miei figli CHIUSI».
/// Non è il dominio topologico — un figlio online si tiene i propri — ma il mittente EFFETTIVO dopo la risalita.
/// </summary>
public class LiveStationPartsTests
{
    private static Topology Topo() => new()
    {
        Sectors = new[] { "LIRR_CTR", "LIRR_NE_CTR", "LIRF_APP", "LIRF_TWR" },
        Parent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LIRR_NE_CTR"] = "LIRR_CTR",
            ["LIRF_APP"] = "LIRR_NE_CTR",
            ["LIRF_TWR"] = "LIRF_APP",
        },
        Rules = Array.Empty<UnificationRuleSpec>(),
    };

    private static LiveStationParts Parts(FakeTransfers transfers) =>
        new(null!, transfers, new AorService(), null!);

    [Fact]
    public async Task Prende_solo_i_flussi_di_cui_e_mittente_effettivo()
    {
        var transfers = new FakeTransfers(
            Flow("LIRR_NE_CTR", "LIRR_NE_CTR"),     // mio
            Flow("LIRF_TWR", "LIRR_NE_CTR"),        // figlio chiuso: risale a me
            Flow("LIRF_APP", "LIRF_APP"));          // figlio online: se li tiene

        var mine = await Parts(transfers).TransfersAsync("LIRR", "LIRR_NE_CTR",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "LIRF_APP" }, Topo());

        Assert.Equal(2, mine.Count);
        Assert.All(mine, f => Assert.Equal("LIRR_NE_CTR", f.ResolvedOwnerCallsign));
    }

    [Fact]
    public async Task Risolve_come_se_la_postazione_guardata_fosse_online()
    {
        // Consultare una posizione offline (o la propria prima di collegarsi) non deve svuotare la pagina:
        // senza questo, i suoi flussi risalirebbero a un antenato e non comparirebbero mai.
        var transfers = new FakeTransfers(Flow("LIRR_NE_CTR", "LIRR_NE_CTR"));

        await Parts(transfers).TransfersAsync("LIRR", "LIRR_NE_CTR",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase), Topo());

        Assert.Contains("LIRR_NE_CTR", transfers.LastOnline!, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Non_mostra_i_punti_verso_un_mio_figlio_CHIUSO()
    {
        // Se il figlio è chiuso lo sto coprendo io: non c'è niente da passare, e il punto sparisce invece di
        // dire «passa a te stesso».
        var transfers = new FakeTransfers(Flow("LIRR_NE_CTR", "LIRR_NE_CTR", next: "LIRF_APP"));

        var mine = await Parts(transfers).TransfersAsync("LIRR", "LIRR_NE_CTR",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase), Topo());

        Assert.Empty(mine);
    }

    [Fact]
    public async Task Mostra_i_punti_verso_un_mio_figlio_APERTO()
    {
        var transfers = new FakeTransfers(Flow("LIRR_NE_CTR", "LIRR_NE_CTR", next: "LIRF_APP"));

        var mine = await Parts(transfers).TransfersAsync("LIRR", "LIRR_NE_CTR",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "LIRF_APP" }, Topo());

        Assert.Single(mine);
        Assert.Equal("LIRF_APP", mine[0].Points[0].Point.NextSectorCallsign);
    }

    [Fact]
    public async Task Un_ente_FUORI_dal_mio_dominio_resta_anche_se_chiuso()
    {
        // Fuori dal mio dominio la risalita è informazione utile: chi prende il traffico adesso, fino a UNICOM.
        var transfers = new FakeTransfers(Flow("LIRR_NE_CTR", "LIRR_NE_CTR", next: "LIMM_CTR"));

        var mine = await Parts(transfers).TransfersAsync("LIRR", "LIRR_NE_CTR",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase), Topo());

        Assert.Single(mine);
    }

    [Fact]
    public void Catena_di_copertura_dal_padre_alla_radice()
    {
        var chain = LiveStationParts.CoverageChain(Topo(), "LIRF_TWR");

        Assert.Equal(new[] { "LIRF_APP", "LIRR_NE_CTR", "LIRR_CTR" }, chain);
    }

    [Fact]
    public void Catena_di_copertura_di_una_radice_e_vuota()
    {
        Assert.Empty(LiveStationParts.CoverageChain(Topo(), "LIRR_CTR"));
    }

    [Fact]
    public void Catena_di_copertura_non_cicla_su_una_gerarchia_malata()
    {
        var malata = new Topology
        {
            Sectors = new[] { "A", "B" },
            Parent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["A"] = "B", ["B"] = "A" },
            Rules = Array.Empty<UnificationRuleSpec>(),
        };

        // Si ferma prima di reinserire il punto di partenza: nessuno è antenato di sé stesso.
        Assert.Equal(new[] { "B" }, LiveStationParts.CoverageChain(malata, "A"));
    }

    /// <summary>Flusso con un punto: <paramref name="next"/> è il settore ricevente configurato.</summary>
    private static ResolvedTransferFlow Flow(string owning, string resolvedOwner, string next = "LIMM_CTR") => new()
    {
        Flow = new TransferFlowRow
        {
            Id = 1, AccCode = "LIRR", OwningSectorId = 1, OwningSectorCallsign = owning,
            Kind = TransferFlowKind.Departure, Order = 0, Points = Array.Empty<TransferPointRow>(),
        },
        ResolvedOwnerCallsign = resolvedOwner,
        OwnerOnline = true,
        Points = new[] { Point(next) },
    };

    private static ResolvedTransferPoint Point(string next) => new()
    {
        Point = new TransferPointRow
        {
            Id = 1, Cop = "ABCDE", LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.AtOrBelow,
            LevelText = "FL240", NextSectorCallsign = next, Order = 0,
        },
        ResolvedHandler = next,
        IsOnline = true,
    };

    /// <summary>Registra l'insieme online ricevuto: è ciò che il secondo test deve poter osservare.</summary>
    /// <summary>Registra l'insieme online ricevuto: e' cio' che il secondo test deve poter osservare. Gli altri
    /// membri del servizio non li tocca nessuno da qui, e lanciano apposta — un finto che risponde a domande che
    /// il caso di prova non fa e' un finto che nasconde una dipendenza.</summary>
    private sealed class FakeTransfers : IAgreementService
    {
        private readonly IReadOnlyList<ResolvedTransferFlow> _flows;
        public IReadOnlySet<string>? LastOnline { get; private set; }

        public FakeTransfers(params ResolvedTransferFlow[] flows) => _flows = flows;

        public Task<IReadOnlyList<ResolvedTransferFlow>> ResolveForAccAsync(
            string accCode, IReadOnlySet<string> online, CancellationToken ct = default)
        {
            LastOnline = online;
            return Task.FromResult(_flows);
        }

        public Task<IReadOnlyList<AgreementRow>> ListByAccAsync(string a, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<TransferFlowRow>> ListFlowsByAccAsync(string a, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<int> AddAgreementAsync(string a, AgreementInput i, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task UpdateAgreementAsync(string a, int id, AgreementInput i, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task DeleteAgreementAsync(string a, int id, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<int> AddClauseAsync(string a, int id, Vipi.Domain.AgreementDirection d, AgreementClauseInput i, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task UpdateClauseAsync(string a, int id, AgreementClauseInput i, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task DeleteClauseAsync(string a, int id, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task MoveClauseAsync(string a, int id, bool up, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task MoveClauseToAsync(string a, int id, int t, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<int> AddAlternativeAsync(string a, int id, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<int> AddExceptionAsync(string a, int id, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<int> DuplicateVariantGroupAsync(string a, int id, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task DetachVariantAsync(string a, int id, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<int> CopyDirectionAsync(string a, int id, Vipi.Domain.AgreementDirection from, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<int> SetLevelAsync(string a, IReadOnlyList<int> ids, Vipi.Domain.ParsedLevel lv, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<int> SetConditionAsync(string a, IReadOnlyList<int> ids, string? area, string? custom, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<int> DeleteClausesAsync(string a, IReadOnlyList<int> ids, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<int> RestoreAgreementAsync(string a, AgreementSnapshot s, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<int> RestoreClausesAsync(string a, IReadOnlyList<AgreementClauseRestore> c, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
