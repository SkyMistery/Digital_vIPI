using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Auth;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Anteprima release di un profilo APP: dati ricostruiti dallo snapshot + ciclo AIRAC della release.</summary>
public sealed record AppReleaseView(AppProfileData Data, string AiracCycle);

/// <summary>
/// Use-case di authoring del profilo APP standalone: parti editoriali salvate (ACC-gated), parti derivate
/// calcolate live (frequenze dal sottoalbero, coordinamenti dai trasferimenti, poligono AoR dal catalogo).
/// Letture libere (servono al viewer); scritture gated via <see cref="IEditAuthorizationService"/>.
/// </summary>
public interface IAppProfileService
{
    Task<AppProfileData?> LoadForViewAsync(string appCallsign, CancellationToken ct = default);
    Task<AppProfileData?> LoadForEditAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Anteprima di una specifica release: ricostruisce il profilo dallo snapshot congelato + ciclo AIRAC.
    /// Gated can-edit ACC; verifica che la release sia dell'APP indicato. Freq/coord/AoR restano derivati live. null se non corrisponde.</summary>
    Task<AppReleaseView?> LoadForReleaseAsync(string appCallsign, int releaseId, CancellationToken ct = default);

    /// <summary>Frequenze finali ordinate: catalogo del sottoalbero + link extra, con override d'ordine applicato.</summary>
    Task<IReadOnlyList<AppFreqRow>> DeriveFrequenciesAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Coordinamenti derivati dai trasferimenti del settore APP (verso ACC: dep+arr · verso torri: solo arr).</summary>
    /// <summary>Deriva la frase di coordinamento. <paramref name="templateOverride"/> non nullo/whitespace = usa quel
    /// template SENZA salvarlo (per l'anteprima live nell'editor); altrimenti usa l'override salvato o il default globale.</summary>
    Task<AppCoordination> DeriveCoordinationAsync(string appCallsign, string? templateOverride = null, CancellationToken ct = default);

    /// <summary>Poligono AoR proiettato a SVG (Fase 2). null = nessuna shape / non parsabile → placeholder a UI.</summary>
    Task<AppAorPolygon?> GetAorPolygonAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Poligoni delle TWR dello stesso aeroporto, proiettati, per l'overlay sulla mappa AoR. Vuoto = nessuna.</summary>
    Task<IReadOnlyList<AppAorPolygon>> GetTowerPolygonsAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Vista AoR dell'APP come mappa a settori toggleabili (chip on/off): l'APP + ciascuna TWR dello stesso
    /// aeroporto come "settore" con il proprio callsign. Riusa il modello di <see cref="AccAorView"/>.</summary>
    Task<AccAorView> GetAorViewAsync(string appCallsign, CancellationToken ct = default);

    Task<IReadOnlyList<LinkableFrequencyRow>> ListLinkableFrequenciesAsync(CancellationToken ct = default);

    Task SaveSeparationsAsync(string appCallsign, IReadOnlyList<AppSeparationRow> rows, CancellationToken ct = default);
    Task SaveVfrAsync(string appCallsign, string? vfrJson, CancellationToken ct = default);
    Task SaveSectionOrderAsync(string appCallsign, IReadOnlyList<string> order, CancellationToken ct = default);
    Task SaveHiddenSectionsAsync(string appCallsign, IReadOnlyList<string> hiddenKeys, CancellationToken ct = default);
    Task SaveFrequencyOrderAsync(string appCallsign, IReadOnlyList<AppFreqOrderOverride> overrides, CancellationToken ct = default);
    Task SaveFrequencyLinksAsync(string appCallsign, IReadOnlyList<int> sourceSectorIds, CancellationToken ct = default);
    Task SaveCustomSectionsAsync(string appCallsign, IReadOnlyList<AppCustomSection> sections, CancellationToken ct = default);

    /// <summary>Override per-documento del template della frase di coordinamento (null/vuoto = default globale).</summary>
    Task SaveCoordinationTemplateAsync(string appCallsign, string? template, CancellationToken ct = default);
}

/// <inheritdoc cref="IAppProfileService"/>
public sealed class AppProfileService : IAppProfileService
{
    private readonly IAppProfileRepository _repo;
    private readonly IEditAuthorizationService _authz;
    private readonly ITopologyProvider _topology;
    private readonly ITransferService _transfers;
    private readonly ICoordinationSentenceTemplate _sentence;
    private readonly IReleaseRepository _releases;
    private readonly IEditAuditWriter _audit;

    public AppProfileService(IAppProfileRepository repo, IEditAuthorizationService authz,
        ITopologyProvider topology, ITransferService transfers, ICoordinationSentenceTemplate sentence,
        IReleaseRepository releases, IEditAuditWriter audit)
    {
        _repo = repo;
        _authz = authz;
        _topology = topology;
        _transfers = transfers;
        _sentence = sentence;
        _releases = releases;
        _audit = audit;
    }

    // Vista pubblica: se esiste una release AIRAC effettiva usa i blob CONGELATI dello snapshot (struttura/separazioni/
    // VFR/custom/sezioni nascoste). Le frequenze/coordinamenti/AoR restano derivati live. L'editor vede lo stato live.
    public async Task<AppProfileData?> LoadForViewAsync(string appCallsign, CancellationToken ct = default)
    {
        var callsign = Norm(appCallsign);
        if (await _repo.IsHiddenAsync(callsign, ct)) return null;   // nascosta dal pubblico (l'editor la vede)
        var rel = await _releases.GetEffectiveAsync(ReleaseTargetType.App, callsign, DateTime.UtcNow, ct);
        if (rel is not null)
        {
            var snap = System.Text.Json.JsonSerializer.Deserialize<AppReleaseSnapshot>(rel.PayloadJson);
            if (snap is not null) return await _repo.BuildFromSnapshotAsync(callsign, snap, ct);
        }
        return await _repo.LoadAsync(callsign, ct);
    }

    public async Task<AppProfileData?> LoadForEditAsync(string appCallsign, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(appCallsign, ct);
        return await _repo.LoadAsync(Norm(appCallsign), ct);
    }

    public async Task<AppReleaseView?> LoadForReleaseAsync(string appCallsign, int releaseId, CancellationToken ct = default)
    {
        var callsign = Norm(appCallsign);
        await EnsureCanEditAsync(callsign, ct);
        var rel = await _releases.GetByIdAsync(releaseId, ct);
        if (rel is null || rel.TargetType != ReleaseTargetType.App
            || !string.Equals(rel.TargetKey, callsign, StringComparison.OrdinalIgnoreCase))
            return null;   // release inesistente o non di questo APP → il viewer ricade su pubblica
        var snap = System.Text.Json.JsonSerializer.Deserialize<AppReleaseSnapshot>(rel.PayloadJson);
        if (snap is null) return null;
        var data = await _repo.BuildFromSnapshotAsync(callsign, snap, ct);
        return data is null ? null : new AppReleaseView(data, rel.ReleaseAiracCycle);
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

    public async Task<AppCoordination> DeriveCoordinationAsync(string appCallsign, string? templateOverride = null, CancellationToken ct = default)
    {
        appCallsign = Norm(appCallsign);
        var accCode = await _repo.GetAccCodeByAppAsync(appCallsign, ct);
        if (accCode is null) return AppCoordination.Empty;

        var flows = await _transfers.ListFlowsByAccAsync(accCode, ct);
        var types = await _repo.GetSectorTypeMapAsync(ct);
        var nameMap = await _repo.GetSectorNameMapAsync(ct);
        var codeMap = await _repo.GetSectorCodeMapAsync(ct);
        var airportMap = await _repo.GetAirportNameMapAsync(ct);
        var atcMap = await _repo.GetSectorAtcNameMapAsync(ct);
        // Anteprima: usa il template passato senza salvarlo; altrimenti l'override salvato o il default globale.
        var overrideTpl = string.IsNullOrWhiteSpace(templateOverride)
            ? await _repo.GetCoordinationTemplateAsync(appCallsign, ct)
            : templateOverride;
        var tpl = string.IsNullOrWhiteSpace(overrideTpl) ? _sentence.Current : _sentence.Current.WithTemplate(overrideTpl!);

        string? Compose(string ownerCs, string targetCs, string? airportIcao, LevelConstraint constraint, string levelText, string cop)
            => CoordinationSentences.Compose(tpl, types, nameMap, codeMap, airportMap, atcMap, ownerCs, targetCs, airportIcao, constraint, levelText, cop);

        var towardAcc = new Dictionary<string, List<AppCoordRow>>(StringComparer.OrdinalIgnoreCase);
        var towardTwr = new Dictionary<string, List<AppCoordRow>>(StringComparer.OrdinalIgnoreCase);

        // Flussi di PROPRIETÀ dell'APP: partenze/arrivi verso ACC, arrivi verso le torri.
        foreach (var flow in flows.Where(f => string.Equals(f.OwningSectorCallsign, appCallsign, StringComparison.OrdinalIgnoreCase)))
            foreach (var p in flow.Points)
            {
                var next = p.NextSectorCallsign;
                if (string.IsNullOrWhiteSpace(next)) continue;
                if (!types.TryGetValue(next, out var nextType)) continue;          // Next non risolvibile → salta

                var row = new AppCoordRow(p.Cop, p.LevelText, next, flow.Kind)
                {
                    OwnerCallsign = flow.OwningSectorCallsign,
                    AirportIcao = flow.AirportIcao,
                    Constraint = p.LevelConstraint,
                    Sentence = Compose(flow.OwningSectorCallsign, next, flow.AirportIcao, p.LevelConstraint, p.LevelText, p.Cop),
                };
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
                Bucket(towardAcc, owner).Add(new AppCoordRow(p.Cop, p.LevelText, owner, TransferFlowKind.Arrival)
                {
                    OwnerCallsign = owner,
                    AirportIcao = flow.AirportIcao,
                    Constraint = p.LevelConstraint,
                    // Mittente = ACC (owner), destinatario = questo APP.
                    Sentence = Compose(owner, appCallsign, flow.AirportIcao, p.LevelConstraint, p.LevelText, p.Cop),
                });
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

    // Palette anelli AoR (APP blu IVAO, poi varianti per le torri). Coerente con AccProfileService.
    private static readonly string[] AorPalette = { "#0D2C99", "#C77D3C", "#5B8C5A", "#8E5BA6", "#B0413E", "#3C55AC", "#7EA2D6" };

    public async Task<AccAorView> GetAorViewAsync(string appCallsign, CancellationToken ct = default)
    {
        var app = Norm(appCallsign);
        var sectors = new List<AccSectorAor>();

        var appPoly = Aor.AorPolygonProjector.Project(await _repo.GetAorPolygonRawAsync(app, ct));
        if (appPoly is not null)
            sectors.Add(new AccSectorAor(app, app, AorPalette[0], new[] { appPoly }));

        var towers = await _repo.GetTowerPolygonsWithCallsignRawAsync(app, ct);
        var i = 1;
        foreach (var (callsign, raw) in towers)
        {
            var poly = Aor.AorPolygonProjector.Project(raw);
            if (poly is null) continue;
            sectors.Add(new AccSectorAor(callsign, callsign, AorPalette[i % AorPalette.Length], new[] { poly }));
            i++;
        }
        return new AccAorView(sectors, Array.Empty<AccConfigSelection>());
    }

    public async Task SaveSeparationsAsync(string appCallsign, IReadOnlyList<AppSeparationRow> rows, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(appCallsign, ct);
        await _repo.SaveSeparationsAsync(Norm(appCallsign), rows, ct);
        await Audit(appCallsign, "separazioni", ct);
    }

    public async Task SaveVfrAsync(string appCallsign, string? vfrJson, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(appCallsign, ct);
        await _repo.SaveVfrAsync(Norm(appCallsign), vfrJson, ct);
        await Audit(appCallsign, "VFR", ct);
    }

    public async Task SaveSectionOrderAsync(string appCallsign, IReadOnlyList<string> order, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(appCallsign, ct);
        await _repo.SaveSectionOrderAsync(Norm(appCallsign), order, ct);
        await Audit(appCallsign, "ordine sezioni", ct);
    }

    public async Task SaveHiddenSectionsAsync(string appCallsign, IReadOnlyList<string> hiddenKeys, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(appCallsign, ct);
        await _repo.SaveHiddenSectionsAsync(Norm(appCallsign), hiddenKeys, ct);
        await Audit(appCallsign, "sezioni nascoste", ct);
    }

    public async Task SaveFrequencyOrderAsync(string appCallsign, IReadOnlyList<AppFreqOrderOverride> overrides, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(appCallsign, ct);
        await _repo.SaveFrequencyOrderAsync(Norm(appCallsign), overrides, ct);
        await Audit(appCallsign, "ordine frequenze", ct);
    }

    public async Task SaveFrequencyLinksAsync(string appCallsign, IReadOnlyList<int> sourceSectorIds, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(appCallsign, ct);
        await _repo.SaveFrequencyLinksAsync(Norm(appCallsign), sourceSectorIds, ct);
        await Audit(appCallsign, "link frequenze", ct);
    }

    public async Task SaveCustomSectionsAsync(string appCallsign, IReadOnlyList<AppCustomSection> sections, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(appCallsign, ct);
        foreach (var s in sections)
            if (string.IsNullOrWhiteSpace(s.Title)) throw new ValidationException("Titolo obbligatorio per ogni sezione custom.");
        await _repo.SaveCustomSectionsAsync(Norm(appCallsign), sections, ct);
        await Audit(appCallsign, "sezioni custom", ct);
    }

    public async Task SaveCoordinationTemplateAsync(string appCallsign, string? template, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(appCallsign, ct);
        await _repo.SaveCoordinationTemplateAsync(Norm(appCallsign), template, ct);
        await Audit(appCallsign, "template coordinamenti", ct);
    }

    private async Task EnsureCanEditAsync(string appCallsign, CancellationToken ct)
    {
        var acc = await _repo.GetAccCodeByAppAsync(Norm(appCallsign), ct)
            ?? throw new ValidationException($"APP {Norm(appCallsign)} inesistente.");
        await _authz.EnsureCanEditAccAsync(acc, ct);
    }

    // Storia modifiche (audit chi/quando) del profilo APP, deduplicata per sessione dal writer.
    private Task Audit(string appCallsign, string area, CancellationToken ct) =>
        _audit.RecordEditAsync("AppProfile", Norm(appCallsign), _authz.CurrentUserId ?? 0, area, ct);

    private static string Norm(string callsign) => (callsign ?? "").Trim().ToUpperInvariant();
}
