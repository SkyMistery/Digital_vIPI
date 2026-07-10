using System.Text.Json;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Authoring dell'APP standalone sul modello unificato <c>Document</c> (doc refactor 08e, strategia A). Sostituisce
/// progressivamente <c>IAppProfileService</c>/<c>AppProfile</c>: le sezioni vivono in <c>DocumentSection</c>+
/// <c>ContentBlock</c>, gli override derivati in <c>DocumentProfile</c>. Le sezioni derivate (freq/coord/AoR) restano
/// calcolate live dai cataloghi; qui la logica di derivazione è identica a quella profile, con gli override presi
/// dal <c>DocumentProfile</c> del documento dell'APP.
/// </summary>
public interface IAppDocumentService
{
    /// <summary>Idempotente: garantisce il Document vIPI dell'APP (creato greenfield dalle sezioni di catalogo se
    /// mancante) e ne ritorna l'Id. ACC-gated.</summary>
    Task<int> EnsureAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Frequenze finali ordinate: catalogo del sottoalbero + link extra (da DocumentProfile), con override d'ordine.</summary>
    Task<IReadOnlyList<AppFreqRow>> DeriveFrequenciesAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Coordinamenti derivati dai trasferimenti del settore APP. <paramref name="templateOverride"/> non
    /// nullo/whitespace = usa quel template SENZA salvarlo (anteprima live); altrimenti l'override salvato o il default.</summary>
    Task<AppCoordination> DeriveCoordinationAsync(string appCallsign, string? templateOverride = null, CancellationToken ct = default);

    /// <summary>Vista AoR come mappa a settori (APP + torri dello stesso aeroporto). Riusa il modello di AccAorView.</summary>
    Task<AccAorView> GetAorViewAsync(string appCallsign, CancellationToken ct = default);

    Task<AppAorPolygon?> GetAorPolygonAsync(string appCallsign, CancellationToken ct = default);
    Task<IReadOnlyList<AppAorPolygon>> GetTowerPolygonsAsync(string appCallsign, CancellationToken ct = default);
    Task<IReadOnlyList<LinkableFrequencyRow>> ListLinkableFrequenciesAsync(CancellationToken ct = default);

    /// <summary>Righe della sezione Separazioni (editoriale-strutturata), lette dal blocco keyed del Document. Vuoto se non migrato/assente.</summary>
    Task<IReadOnlyList<AppSeparationRow>> GetSeparationsAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Salva le righe Separazioni nel blocco keyed del Document (garantisce prima il documento; ACC-gated).</summary>
    Task SaveSeparationsAsync(string appCallsign, IReadOnlyList<AppSeparationRow> rows, CancellationToken ct = default);

    /// <summary>Contenuto della sezione VFR (prosa + tabella), letto dal blocco keyed del Document. Vuoto se non migrato/assente.</summary>
    Task<AppVfrContent> GetVfrAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Salva il contenuto VFR nel blocco keyed del Document (garantisce prima il documento; ACC-gated).</summary>
    Task SaveVfrAsync(string appCallsign, AppVfrContent content, CancellationToken ct = default);

    /// <summary>Override editoriali del documento (sezioni nascoste, ordine/link frequenze, template coord). Vuoti se non migrato.</summary>
    Task<DocumentProfileData> GetOverridesAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Salva le chiavi delle sezioni nascoste dal pubblico (ACC-gated).</summary>
    Task SaveHiddenSectionsAsync(string appCallsign, IReadOnlyList<string> sectionKeys, CancellationToken ct = default);

    /// <summary>Salva l'override d'ordine delle frequenze per callsign (ACC-gated).</summary>
    Task SaveFrequencyOrderAsync(string appCallsign, IReadOnlyList<AppFreqOrderOverride> overrides, CancellationToken ct = default);

    /// <summary>Salva i settori sorgente dei link frequenza extra (ACC-gated).</summary>
    Task SaveFrequencyLinksAsync(string appCallsign, IReadOnlyList<int> sourceSectorIds, CancellationToken ct = default);

    /// <summary>Salva l'override per-documento del template della frase di coordinamento (null/vuoto = default; ACC-gated).</summary>
    Task SaveCoordinationTemplateAsync(string appCallsign, string? template, CancellationToken ct = default);
}

/// <inheritdoc cref="IAppDocumentService"/>
public sealed class AppDocumentService : IAppDocumentService
{
    private readonly IAppProfileRepository _apps;
    private readonly IEditingRepository _editing;
    private readonly IEditAuthorizationService _authz;
    private readonly ITopologyProvider _topology;
    private readonly ITransferService _transfers;
    private readonly ICoordinationSentenceTemplate _sentence;
    private readonly IDocumentProfileRepository _docProfiles;

    public AppDocumentService(IAppProfileRepository apps, IEditingRepository editing, IEditAuthorizationService authz,
        ITopologyProvider topology, ITransferService transfers, ICoordinationSentenceTemplate sentence,
        IDocumentProfileRepository docProfiles)
    {
        _apps = apps;
        _editing = editing;
        _authz = authz;
        _topology = topology;
        _transfers = transfers;
        _sentence = sentence;
        _docProfiles = docProfiles;
    }

    private static string Norm(string s) => (s ?? "").Trim().ToUpperInvariant();

    public async Task<int> EnsureAsync(string appCallsign, CancellationToken ct = default)
    {
        var id = await _apps.ResolveForDocumentAsync(Norm(appCallsign), ct)
            ?? throw new Aor.ValidationException($"APP {Norm(appCallsign)} inesistente.");
        if (id.DocumentId is int existing) return existing;   // già migrato

        await _authz.EnsureCanEditAccAsync(id.AccCode, ct);
        var sections = SectionCatalog.For(SectionProfile.App).Select(d => (d.Key, d.Title)).ToList();
        return await _editing.EnsureVipiDocumentAsync(id.SectorId, id.Title, Language.It, sections, _authz.CurrentUserId ?? 0, ct);
    }

    // Override derivati (link/ordine freq, template coord) dal DocumentProfile del documento dell'APP; vuoti se non migrato.
    private async Task<DocumentProfileData> LoadOverridesAsync(string appCallsign, CancellationToken ct)
    {
        var id = await _apps.ResolveForDocumentAsync(appCallsign, ct);
        return id?.DocumentId is int docId ? await _docProfiles.GetAsync(docId, ct) : new DocumentProfileData();
    }

    public async Task<IReadOnlyList<AppFreqRow>> DeriveFrequenciesAsync(string appCallsign, CancellationToken ct = default)
    {
        appCallsign = Norm(appCallsign);
        var topo = await _topology.BuildGlobalAsync(ct);
        var domain = topo.DomainOf(appCallsign);
        var ancestors = topo.Ancestors(appCallsign).ToList();
        var catalog = await _apps.DeriveCatalogFrequenciesAsync(appCallsign, domain, ancestors, ct);

        var overrides = await LoadOverridesAsync(appCallsign, ct);
        var links = await _apps.ResolveFreqLinksAsync(overrides.FreqLinkSectorIds, ct);
        var order = overrides.FreqOrder.ToDictionary(o => o.Callsign, o => o.Order, StringComparer.OrdinalIgnoreCase);

        var all = catalog.Concat(links).ToList();
        return all
            .Select((row, i) => (row, key: order.TryGetValue(row.Callsign, out var ov) ? ov : 1000 + i))
            .OrderBy(x => x.key)
            .Select(x => x.row)
            .ToList();
    }

    public async Task<AppCoordination> DeriveCoordinationAsync(string appCallsign, string? templateOverride = null, CancellationToken ct = default)
    {
        appCallsign = Norm(appCallsign);
        var accCode = await _apps.GetAccCodeByAppAsync(appCallsign, ct);
        if (accCode is null) return AppCoordination.Empty;

        var flows = await _transfers.ListFlowsByAccAsync(accCode, ct);
        var types = await _apps.GetSectorTypeMapAsync(ct);
        var nameMap = await _apps.GetSectorNameMapAsync(ct);
        var codeMap = await _apps.GetSectorCodeMapAsync(ct);
        var airportMap = await _apps.GetAirportNameMapAsync(ct);
        var atcMap = await _apps.GetSectorAtcNameMapAsync(ct);

        var saved = (await LoadOverridesAsync(appCallsign, ct)).CoordinationSentenceTemplate;
        var overrideTpl = string.IsNullOrWhiteSpace(templateOverride) ? saved : templateOverride;
        var tpl = string.IsNullOrWhiteSpace(overrideTpl) ? _sentence.Current : _sentence.Current.WithTemplate(overrideTpl!);

        string? Compose(string ownerCs, string targetCs, string? airportIcao, LevelConstraint constraint, string levelText, string cop)
            => CoordinationSentences.Compose(tpl, types, nameMap, codeMap, airportMap, atcMap, ownerCs, targetCs, airportIcao, constraint, levelText, cop);

        var towardAcc = new Dictionary<string, List<AppCoordRow>>(StringComparer.OrdinalIgnoreCase);
        var towardTwr = new Dictionary<string, List<AppCoordRow>>(StringComparer.OrdinalIgnoreCase);

        foreach (var flow in flows.Where(f => string.Equals(f.OwningSectorCallsign, appCallsign, StringComparison.OrdinalIgnoreCase)))
            foreach (var p in flow.Points)
            {
                var next = p.NextSectorCallsign;
                if (string.IsNullOrWhiteSpace(next)) continue;
                if (!types.TryGetValue(next, out var nextType)) continue;

                var row = new AppCoordRow(p.Cop, p.LevelText, next, flow.Kind)
                {
                    OwnerCallsign = flow.OwningSectorCallsign,
                    AirportIcao = flow.AirportIcao,
                    Constraint = p.LevelConstraint,
                    Sentence = Compose(flow.OwningSectorCallsign, next, flow.AirportIcao, p.LevelConstraint, p.LevelText, p.Cop),
                };
                if (nextType == SectorType.Ctr)
                    Bucket(towardAcc, next).Add(row);
                else if (nextType is SectorType.Twr or SectorType.ITwr && flow.Kind == TransferFlowKind.Arrival)
                    Bucket(towardTwr, next).Add(row);
            }

        foreach (var flow in flows)
        {
            if (flow.Kind != TransferFlowKind.Arrival) continue;
            var owner = flow.OwningSectorCallsign;
            if (string.Equals(owner, appCallsign, StringComparison.OrdinalIgnoreCase)) continue;
            if (!types.TryGetValue(owner, out var ownerType) || ownerType != SectorType.Ctr) continue;

            foreach (var p in flow.Points)
            {
                if (!string.Equals(p.NextSectorCallsign, appCallsign, StringComparison.OrdinalIgnoreCase)) continue;
                Bucket(towardAcc, owner).Add(new AppCoordRow(p.Cop, p.LevelText, owner, TransferFlowKind.Arrival)
                {
                    OwnerCallsign = owner,
                    AirportIcao = flow.AirportIcao,
                    Constraint = p.LevelConstraint,
                    Sentence = Compose(owner, appCallsign, flow.AirportIcao, p.LevelConstraint, p.LevelText, p.Cop),
                });
            }
        }

        return new AppCoordination { TowardAcc = ToGroups(towardAcc), TowardTowers = ToGroups(towardTwr) };

        static List<AppCoordRow> Bucket(Dictionary<string, List<AppCoordRow>> d, string key) =>
            d.TryGetValue(key, out var list) ? list : d[key] = new List<AppCoordRow>();

        static IReadOnlyList<AppCoordGroup> ToGroups(Dictionary<string, List<AppCoordRow>> d) =>
            d.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kv => new AppCoordGroup(kv.Key, kv.Value)).ToList();
    }

    // Palette anelli AoR (APP blu IVAO, poi varianti per le torri). Coerente con AppProfileService.
    private static readonly string[] AorPalette = { "#0D2C99", "#C77D3C", "#5B8C5A", "#8E5BA6", "#B0413E", "#3C55AC", "#7EA2D6" };

    public async Task<AccAorView> GetAorViewAsync(string appCallsign, CancellationToken ct = default)
    {
        var app = Norm(appCallsign);
        var sectors = new List<AccSectorAor>();

        var appPoly = Aor.AorPolygonProjector.Project(await _apps.GetAorPolygonRawAsync(app, ct));
        if (appPoly is not null)
            sectors.Add(new AccSectorAor(app, app, AorPalette[0], new[] { appPoly }));

        var towers = await _apps.GetTowerPolygonsWithCallsignRawAsync(app, ct);
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

    public async Task<AppAorPolygon?> GetAorPolygonAsync(string appCallsign, CancellationToken ct = default) =>
        Aor.AorPolygonProjector.Project(await _apps.GetAorPolygonRawAsync(Norm(appCallsign), ct));

    public async Task<IReadOnlyList<AppAorPolygon>> GetTowerPolygonsAsync(string appCallsign, CancellationToken ct = default)
    {
        var raws = await _apps.GetTowerPolygonsRawAsync(Norm(appCallsign), ct);
        return raws.Select(Aor.AorPolygonProjector.Project).Where(p => p is not null).Select(p => p!).ToList();
    }

    public Task<IReadOnlyList<LinkableFrequencyRow>> ListLinkableFrequenciesAsync(CancellationToken ct = default) =>
        _apps.ListLinkableFrequenciesAsync(ct);

    // --- Sezioni editoriali-strutturate su Document (doc 08e f3b-iii): separations/vfr in ContentBlock.BodyJson. ---

    private async Task<int?> ResolveDocIdAsync(string appCallsign, CancellationToken ct) =>
        (await _apps.ResolveForDocumentAsync(Norm(appCallsign), ct))?.DocumentId;

    public async Task<IReadOnlyList<AppSeparationRow>> GetSeparationsAsync(string appCallsign, CancellationToken ct = default)
    {
        if (await ResolveDocIdAsync(appCallsign, ct) is not int docId) return Array.Empty<AppSeparationRow>();
        var json = await _editing.GetSectionBlockJsonAsync(docId, "separations", ct);
        return Deserialize<List<AppSeparationRow>>(json) ?? new List<AppSeparationRow>();
    }

    public async Task SaveSeparationsAsync(string appCallsign, IReadOnlyList<AppSeparationRow> rows, CancellationToken ct = default)
    {
        var docId = await EnsureAsync(appCallsign, ct);   // ACC-gated + garantisce il Document
        var clean = (rows ?? Array.Empty<AppSeparationRow>())
            .Select(r => new AppSeparationRow((r.Vertical ?? "").Trim(), (r.Lateral ?? "").Trim(),
                string.IsNullOrWhiteSpace(r.Applicability) ? null : r.Applicability!.Trim()))
            .ToList();
        var json = clean.Count == 0 ? null : JsonSerializer.Serialize(clean);
        await _editing.SaveSectionBlockJsonAsync(docId, "separations", json, _authz.CurrentUserId ?? 0, ct);
    }

    public async Task<AppVfrContent> GetVfrAsync(string appCallsign, CancellationToken ct = default)
    {
        if (await ResolveDocIdAsync(appCallsign, ct) is not int docId) return AppVfrContent.Empty;
        var json = await _editing.GetSectionBlockJsonAsync(docId, "vfr", ct);
        return Deserialize<AppVfrContent>(json) ?? AppVfrContent.Empty;
    }

    public async Task SaveVfrAsync(string appCallsign, AppVfrContent content, CancellationToken ct = default)
    {
        var docId = await EnsureAsync(appCallsign, ct);   // ACC-gated + garantisce il Document
        var empty = content is null || (string.IsNullOrWhiteSpace(content.Intro) && content.Rows.Count == 0);
        var json = empty ? null : JsonSerializer.Serialize(content);
        await _editing.SaveSectionBlockJsonAsync(docId, "vfr", json, _authz.CurrentUserId ?? 0, ct);
    }

    // --- Override editoriali su DocumentProfile (doc 08e f4-a): sezioni nascoste, ordine/link freq, template coord. ---

    public Task<DocumentProfileData> GetOverridesAsync(string appCallsign, CancellationToken ct = default) =>
        LoadOverridesAsync(appCallsign, ct);

    public Task SaveHiddenSectionsAsync(string appCallsign, IReadOnlyList<string> sectionKeys, CancellationToken ct = default) =>
        WithDocumentAsync(appCallsign, (docId, c) => _docProfiles.SaveHiddenSectionsAsync(docId, sectionKeys ?? Array.Empty<string>(), c), ct);

    public Task SaveFrequencyOrderAsync(string appCallsign, IReadOnlyList<AppFreqOrderOverride> overrides, CancellationToken ct = default) =>
        WithDocumentAsync(appCallsign, (docId, c) => _docProfiles.SaveFreqOrderAsync(docId, overrides ?? Array.Empty<AppFreqOrderOverride>(), c), ct);

    public Task SaveFrequencyLinksAsync(string appCallsign, IReadOnlyList<int> sourceSectorIds, CancellationToken ct = default) =>
        WithDocumentAsync(appCallsign, (docId, c) => _docProfiles.SaveFreqLinksAsync(docId, sourceSectorIds ?? Array.Empty<int>(), c), ct);

    public Task SaveCoordinationTemplateAsync(string appCallsign, string? template, CancellationToken ct = default) =>
        WithDocumentAsync(appCallsign, (docId, c) => _docProfiles.SaveCoordinationTemplateAsync(docId, template, c), ct);

    // Garantisce il Document (ACC-gated via EnsureAsync) poi esegue l'azione sull'override, per documentId.
    private async Task WithDocumentAsync(string appCallsign, Func<int, CancellationToken, Task> action, CancellationToken ct)
    {
        var docId = await EnsureAsync(appCallsign, ct);
        await action(docId, ct);
    }

    private static T? Deserialize<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<T>(json); }
        catch (JsonException) { return null; }
    }
}
