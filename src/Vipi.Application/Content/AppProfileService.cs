using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Auth;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Use-case di authoring del profilo APP standalone: parti editoriali salvate (ACC-gated), parti derivate
/// calcolate live (frequenze dal sottoalbero, coordinamenti dai trasferimenti, poligono AoR dal catalogo).
/// Letture libere (servono al viewer); scritture gated via <see cref="IEditAuthorizationService"/>.
/// </summary>
public interface IAppProfileService
{
    Task<AppProfileData?> LoadForViewAsync(string appCallsign, CancellationToken ct = default);
    Task<AppProfileData?> LoadForEditAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Frequenze finali ordinate: catalogo del sottoalbero + link extra, con override d'ordine applicato.</summary>
    Task<IReadOnlyList<AppFreqRow>> DeriveFrequenciesAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Coordinamenti derivati dai trasferimenti del settore APP (verso ACC: dep+arr · verso torri: solo arr).</summary>
    Task<AppCoordination> DeriveCoordinationAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Poligono AoR proiettato a SVG (Fase 2). null = nessuna shape / non parsabile → placeholder a UI.</summary>
    Task<AppAorPolygon?> GetAorPolygonAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Poligoni delle TWR dello stesso aeroporto, proiettati, per l'overlay sulla mappa AoR. Vuoto = nessuna.</summary>
    Task<IReadOnlyList<AppAorPolygon>> GetTowerPolygonsAsync(string appCallsign, CancellationToken ct = default);

    Task<IReadOnlyList<LinkableFrequencyRow>> ListLinkableFrequenciesAsync(CancellationToken ct = default);

    Task SaveSeparationsAsync(string appCallsign, IReadOnlyList<AppSeparationRow> rows, CancellationToken ct = default);
    Task SaveVfrAsync(string appCallsign, string? vfrJson, CancellationToken ct = default);
    Task SaveSectionOrderAsync(string appCallsign, IReadOnlyList<string> order, CancellationToken ct = default);
    Task SaveHiddenSectionsAsync(string appCallsign, IReadOnlyList<string> hiddenKeys, CancellationToken ct = default);
    Task SaveFrequencyOrderAsync(string appCallsign, IReadOnlyList<AppFreqOrderOverride> overrides, CancellationToken ct = default);
    Task SaveFrequencyLinksAsync(string appCallsign, IReadOnlyList<int> sourceSectorIds, CancellationToken ct = default);
    Task SaveCustomSectionsAsync(string appCallsign, IReadOnlyList<AppCustomSection> sections, CancellationToken ct = default);
}

/// <inheritdoc cref="IAppProfileService"/>
public sealed class AppProfileService : IAppProfileService
{
    private readonly IAppProfileRepository _repo;
    private readonly IEditAuthorizationService _authz;
    private readonly ITopologyProvider _topology;
    private readonly ITransferService _transfers;

    public AppProfileService(IAppProfileRepository repo, IEditAuthorizationService authz,
        ITopologyProvider topology, ITransferService transfers)
    {
        _repo = repo;
        _authz = authz;
        _topology = topology;
        _transfers = transfers;
    }

    public Task<AppProfileData?> LoadForViewAsync(string appCallsign, CancellationToken ct = default) =>
        _repo.LoadAsync(Norm(appCallsign), ct);

    public async Task<AppProfileData?> LoadForEditAsync(string appCallsign, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(appCallsign, ct);
        return await _repo.LoadAsync(Norm(appCallsign), ct);
    }

    public Task<IReadOnlyList<LinkableFrequencyRow>> ListLinkableFrequenciesAsync(CancellationToken ct = default) =>
        _repo.ListLinkableFrequenciesAsync(ct);

    public async Task<IReadOnlyList<AppFreqRow>> DeriveFrequenciesAsync(string appCallsign, CancellationToken ct = default)
    {
        appCallsign = Norm(appCallsign);
        var topo = await _topology.BuildGlobalAsync(ct);
        var domain = topo.DomainOf(appCallsign);                                  // APP + sottoalbero (cross-ACC)
        var ancestors = topo.Ancestors(appCallsign).ToList();                     // genitori di copertura (vicino → lontano)
        var catalog = await _repo.DeriveCatalogFrequenciesAsync(appCallsign, domain, ancestors, ct);

        var profile = await _repo.LoadAsync(appCallsign, ct);
        var links = profile?.FrequencyLinks ?? Array.Empty<AppFreqRow>();
        var overrides = (profile?.FreqOrder ?? Array.Empty<AppFreqOrderOverride>())
            .ToDictionary(o => o.Callsign, o => o.Order, StringComparer.OrdinalIgnoreCase);

        // Ordine: indice di default del catalogo (già ATIS·DEL·GND·TWR·APP), poi i link; l'override per-callsign
        // (se presente) ha precedenza assoluta come chiave di sort, preservando per il resto l'ordine di default.
        var all = catalog.Concat(links).ToList();
        return all
            .Select((row, i) => (row, key: overrides.TryGetValue(row.Callsign, out var ov) ? ov : 1000 + i))
            .OrderBy(x => x.key)
            .Select(x => x.row)
            .ToList();
    }

    public async Task<AppCoordination> DeriveCoordinationAsync(string appCallsign, CancellationToken ct = default)
    {
        appCallsign = Norm(appCallsign);
        var accCode = await _repo.GetAccCodeByAppAsync(appCallsign, ct);
        if (accCode is null) return AppCoordination.Empty;

        var flows = await _transfers.ListFlowsByAccAsync(accCode, ct);
        var types = await _repo.GetSectorTypeMapAsync(ct);

        var towardAcc = new Dictionary<string, List<AppCoordRow>>(StringComparer.OrdinalIgnoreCase);
        var towardTwr = new Dictionary<string, List<AppCoordRow>>(StringComparer.OrdinalIgnoreCase);

        // Flussi di PROPRIETÀ dell'APP: partenze/arrivi verso ACC, arrivi verso le torri.
        foreach (var flow in flows.Where(f => string.Equals(f.OwningSectorCallsign, appCallsign, StringComparison.OrdinalIgnoreCase)))
            foreach (var p in flow.Points)
            {
                var next = p.NextSectorCallsign;
                if (string.IsNullOrWhiteSpace(next)) continue;
                if (!types.TryGetValue(next, out var nextType)) continue;          // Next non risolvibile → salta

                var row = new AppCoordRow(p.Cop, p.LevelText, next, flow.Kind);
                if (nextType == SectorType.Ctr)
                    Bucket(towardAcc, next).Add(row);                              // verso ACC: partenze + arrivi
                else if (nextType is SectorType.Twr or SectorType.ITwr && flow.Kind == TransferFlowKind.Arrival)
                    Bucket(towardTwr, next).Add(row);                             // verso torri: solo arrivi
            }

        // Flussi ENTRANTI nell'APP: arrivi che un ACC consegna a questo APP (flusso di proprietà dell'ACC, Next = APP).
        // Per l'APP sono coordinamenti "verso ACC" in arrivo; il referente è l'ACC che possiede il flusso.
        foreach (var flow in flows)
        {
            if (flow.Kind != TransferFlowKind.Arrival) continue;
            var owner = flow.OwningSectorCallsign;
            if (string.Equals(owner, appCallsign, StringComparison.OrdinalIgnoreCase)) continue;   // già trattato sopra
            if (!types.TryGetValue(owner, out var ownerType) || ownerType != SectorType.Ctr) continue;

            foreach (var p in flow.Points)
            {
                if (!string.Equals(p.NextSectorCallsign, appCallsign, StringComparison.OrdinalIgnoreCase)) continue;
                Bucket(towardAcc, owner).Add(new AppCoordRow(p.Cop, p.LevelText, owner, TransferFlowKind.Arrival));
            }
        }

        return new AppCoordination
        {
            TowardAcc = ToGroups(towardAcc),
            TowardTowers = ToGroups(towardTwr),
        };

        static List<AppCoordRow> Bucket(Dictionary<string, List<AppCoordRow>> d, string key) =>
            d.TryGetValue(key, out var list) ? list : d[key] = new List<AppCoordRow>();

        static IReadOnlyList<AppCoordGroup> ToGroups(Dictionary<string, List<AppCoordRow>> d) =>
            d.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kv => new AppCoordGroup(kv.Key, kv.Value)).ToList();
    }

    public async Task<AppAorPolygon?> GetAorPolygonAsync(string appCallsign, CancellationToken ct = default)
    {
        var raw = await _repo.GetAorPolygonRawAsync(Norm(appCallsign), ct);
        return Aor.AorPolygonProjector.Project(raw);
    }

    public async Task<IReadOnlyList<AppAorPolygon>> GetTowerPolygonsAsync(string appCallsign, CancellationToken ct = default)
    {
        var raws = await _repo.GetTowerPolygonsRawAsync(Norm(appCallsign), ct);
        return raws.Select(Aor.AorPolygonProjector.Project).Where(p => p is not null).Select(p => p!).ToList();
    }

    public async Task SaveSeparationsAsync(string appCallsign, IReadOnlyList<AppSeparationRow> rows, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(appCallsign, ct);
        await _repo.SaveSeparationsAsync(Norm(appCallsign), rows, ct);
    }

    public async Task SaveVfrAsync(string appCallsign, string? vfrJson, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(appCallsign, ct);
        await _repo.SaveVfrAsync(Norm(appCallsign), vfrJson, ct);
    }

    public async Task SaveSectionOrderAsync(string appCallsign, IReadOnlyList<string> order, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(appCallsign, ct);
        await _repo.SaveSectionOrderAsync(Norm(appCallsign), order, ct);
    }

    public async Task SaveHiddenSectionsAsync(string appCallsign, IReadOnlyList<string> hiddenKeys, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(appCallsign, ct);
        await _repo.SaveHiddenSectionsAsync(Norm(appCallsign), hiddenKeys, ct);
    }

    public async Task SaveFrequencyOrderAsync(string appCallsign, IReadOnlyList<AppFreqOrderOverride> overrides, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(appCallsign, ct);
        await _repo.SaveFrequencyOrderAsync(Norm(appCallsign), overrides, ct);
    }

    public async Task SaveFrequencyLinksAsync(string appCallsign, IReadOnlyList<int> sourceSectorIds, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(appCallsign, ct);
        await _repo.SaveFrequencyLinksAsync(Norm(appCallsign), sourceSectorIds, ct);
    }

    public async Task SaveCustomSectionsAsync(string appCallsign, IReadOnlyList<AppCustomSection> sections, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(appCallsign, ct);
        foreach (var s in sections)
            if (string.IsNullOrWhiteSpace(s.Title)) throw new ValidationException("Titolo obbligatorio per ogni sezione custom.");
        await _repo.SaveCustomSectionsAsync(Norm(appCallsign), sections, ct);
    }

    private async Task EnsureCanEditAsync(string appCallsign, CancellationToken ct)
    {
        var acc = await _repo.GetAccCodeByAppAsync(Norm(appCallsign), ct)
            ?? throw new ValidationException($"APP {Norm(appCallsign)} inesistente.");
        await _authz.EnsureCanEditAccAsync(acc, ct);
    }

    private static string Norm(string callsign) => (callsign ?? "").Trim().ToUpperInvariant();
}
