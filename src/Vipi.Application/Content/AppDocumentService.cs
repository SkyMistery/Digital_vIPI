using System.Text.Json;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Authoring dell'APP standalone sul modello unificato <c>Document</c> (doc refactor 08e, strategia A). Ha sostituito
/// il vecchio <c>AppProfileService</c>/<c>AppProfile</c> (rimossi): le sezioni vivono in <c>DocumentSection</c>+
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

    /// <summary>Coordinamenti derivati dai trasferimenti del settore APP (frase dal template globale).</summary>
    Task<AppCoordination> DeriveCoordinationAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Vista AoR come mappa a settori (APP del dominio + shape extra scelte a mano). Riusa il modello di AccAorView.</summary>
    Task<AccAorView> GetAorViewAsync(string appCallsign, CancellationToken ct = default);

    Task<IReadOnlyList<LinkableFrequencyRow>> ListLinkableFrequenciesAsync(CancellationToken ct = default);

    /// <summary>Personalizzazione AoR salvata (shape extra + override colore per settore), sezione <c>aor</c>. Vuota se assente.</summary>
    Task<AorExtraShapes> GetAorCustomizationAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Salva la personalizzazione AoR (shape extra + colori) nella sezione <c>aor</c> (garantisce prima il documento; ACC-gated).</summary>
    Task SaveAorCustomizationAsync(string appCallsign, AorExtraShapes data, CancellationToken ct = default);

    /// <summary>Tutti i settori DB con poligono AoR, selezionabili come shape extra (picker globale, cerca per ente).</summary>
    Task<IReadOnlyList<SectorShapePick>> ListSelectableSectorShapesAsync(CancellationToken ct = default);

    /// <summary>Righe della sezione Separazioni (editoriale-strutturata), lette dal blocco keyed del Document. Vuoto se non migrato/assente.</summary>
    Task<IReadOnlyList<AppSeparationRow>> GetSeparationsAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Salva le righe Separazioni nel blocco keyed del Document (garantisce prima il documento; ACC-gated).</summary>
    Task SaveSeparationsAsync(string appCallsign, IReadOnlyList<AppSeparationRow> rows, CancellationToken ct = default);

    /// <summary>Contenuto della sezione VFR (prosa + tabella), letto dal blocco keyed del Document. Vuoto se non migrato/assente.</summary>
    Task<AppVfrContent> GetVfrAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Salva il contenuto VFR nel blocco keyed del Document (garantisce prima il documento; ACC-gated).</summary>
    Task SaveVfrAsync(string appCallsign, AppVfrContent content, CancellationToken ct = default);

    /// <summary>Identità dell'APP (settore, callsign, titolo IVAO, ACC, DocumentId se migrato). Null se il callsign non è un APP standalone.</summary>
    Task<AppDocumentIdentity?> GetIdentityAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Override editoriali del documento (sezioni nascoste, ordine/link frequenze, template coord). Vuoti se non migrato.</summary>
    Task<DocumentProfileData> GetOverridesAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Salva l'override d'ordine delle frequenze per callsign (ACC-gated).</summary>
    Task SaveFrequencyOrderAsync(string appCallsign, IReadOnlyList<AppFreqOrderOverride> overrides, CancellationToken ct = default);

    /// <summary>Salva i settori sorgente dei link frequenza extra (ACC-gated).</summary>
    Task SaveFrequencyLinksAsync(string appCallsign, IReadOnlyList<int> sourceSectorIds, CancellationToken ct = default);

    /// <summary>Settori APP selezionabili nelle configurazioni (i settori APP del dominio di copertura, primario incluso).</summary>
    Task<IReadOnlyList<AccSectorPick>> ListSectorsAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Configurazioni operative salvate (blocco keyed <c>configurations</c>). Vuoto se assente/non migrato.</summary>
    Task<IReadOnlyList<AccConfiguration>> GetConfigurationsAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Salva le configurazioni nel blocco keyed <c>configurations</c> del Document (garantisce prima il documento; ACC-gated).</summary>
    Task SaveConfigurationsAsync(string appCallsign, IReadOnlyList<AccConfiguration> configs, CancellationToken ct = default);

    /// <summary>Tabella accorpamento per ogni configurazione (settore unificato → assorbiti), derivata sul sottoalbero
    /// APP a partire dalle configurazioni della versione di LAVORO. Per l'editor: chi mostra un documento pubblicato
    /// deve passare le configurazioni di QUEL documento (overload sotto), o servirebbe la bozza al pubblico.</summary>
    Task<IReadOnlyList<AccConfigTableView>> DeriveConfigTableAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Come sopra ma sulle configurazioni date, non su quelle della versione di lavoro: le passa chi rende
    /// un documento (pubblico, bozza o anteprima release) leggendole dal documento che sta mostrando. Doc 13 §3g.</summary>
    Task<IReadOnlyList<AccConfigTableView>> DeriveConfigTableAsync(
        string appCallsign, IReadOnlyList<AccConfiguration> configs, CancellationToken ct = default);

    /// <summary>Selezione salvata delle aree regolamentate (sezione <c>regulated</c>). Vuota se assente: l'APP non ha
    /// aree di default (<c>OwnAuto</c> è sempre falso, a differenza del blocco Aerovia della vIPI ACC).</summary>
    Task<RegulatedSelection> GetRegulatedAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Salva la selezione aree regolamentate nella sezione <c>regulated</c> (garantisce prima il documento; ACC-gated).</summary>
    Task SaveRegulatedAsync(string appCallsign, RegulatedSelection selection, CancellationToken ct = default);

    /// <summary>Aree speciali dell'ACC a cui appartiene l'APP (picker editor).</summary>
    Task<IReadOnlyList<SpecialAreaPick>> ListSpecialAreasAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Aree speciali di TUTTI gli altri ACC (picker editor aree extra).</summary>
    Task<IReadOnlyList<SpecialAreaPick>> ListOtherAccSpecialAreasAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Risolve una selezione di aree regolamentate per il viewer (metadati + shape), in ordine (proprie poi
    /// extra). Prende la selezione invece del callsign perché il viewer la legge dalla VERSIONE che sta mostrando
    /// (pubblica/bozza/release), mentre i dettagli e le shape restano quelli correnti dei cataloghi.</summary>
    Task<IReadOnlyList<AccSpecialAreaView>> ResolveRegulatedAreasAsync(RegulatedSelection selection, CancellationToken ct = default);
}

/// <inheritdoc cref="IAppDocumentService"/>
public sealed class AppDocumentService : IAppDocumentService
{
    private readonly IAppDerivationRepository _apps;
    private readonly ISpecialAreaRepository _areas;
    private readonly IEditingRepository _editing;
    private readonly IEditAuthorizationService _authz;
    private readonly ITopologyProvider _topology;
    private readonly ITransferService _transfers;
    private readonly ICoordinationSentenceTemplate _sentence;
    private readonly IDocumentProfileRepository _docProfiles;
    private readonly Aor.IAorService _aor;

    public AppDocumentService(IAppDerivationRepository apps, ISpecialAreaRepository areas, IEditingRepository editing,
        IEditAuthorizationService authz, ITopologyProvider topology, ITransferService transfers,
        ICoordinationSentenceTemplate sentence, IDocumentProfileRepository docProfiles, Aor.IAorService aor)
    {
        _apps = apps;
        _areas = areas;
        _editing = editing;
        _authz = authz;
        _topology = topology;
        _transfers = transfers;
        _sentence = sentence;
        _docProfiles = docProfiles;
        _aor = aor;
    }

    private static string Norm(string s) => (s ?? "").Trim().ToUpperInvariant();

    // Sezioni "live" dell'APP (derivate o editoriali-strutturate rese da componenti dedicati): ricevono un blocco
    // placeholder alla creazione così restano visibili nel viewer anche senza contenuto memorizzato. Doc refactor 08e.
    // «minima» non c'è più (doc 13 §3b): è una sezione editoriale come le altre e un blocco tabella vuoto
    // le darebbe un editor di tabella che nessuno ha chiesto.
    private static readonly string[] LiveKeys =
        { "separations", "configurations", "aor", "frequencies", "vfr", "coordination", "regulated" };

    public async Task<int> EnsureAsync(string appCallsign, CancellationToken ct = default)
    {
        var id = await _apps.ResolveForDocumentAsync(Norm(appCallsign), ct)
            ?? throw new Aor.ValidationException($"{Norm(appCallsign)} non è un APP non remotizzato.");
        // Authz PRIMA dell'uscita anticipata: sui documenti già migrati il metodo non verificava nulla.
        await _authz.EnsureCanEditAccAsync(id.AccCode, ct);
        if (id.DocumentId is int existing) return existing;   // già migrato
        var sections = SectionCatalog.For(SectionProfile.App).Select(d => (d.Key, d.Title)).ToList();
        return await _editing.EnsureVipiDocumentAsync(id.SectorId, id.Title, Language.It, sections,
            _authz.CurrentUserId ?? 0, LiveKeys, ct);
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
        return FrequencyOrdering.ApplyOrder(all, order);
    }

    public async Task<AppCoordination> DeriveCoordinationAsync(string appCallsign, CancellationToken ct = default)
    {
        appCallsign = Norm(appCallsign);
        var accCode = await _apps.GetAccCodeByAppAsync(appCallsign, ct);
        if (accCode is null) return AppCoordination.Empty;

        // Il doc copre l'intero dominio di gerarchia (primario + figli APP), come le frequenze: i coordinamenti
        // sono l'union dei flussi di tutti i settori del dominio (semantica del derive ACC su MemberCallsigns).
        var domain = (await _topology.BuildGlobalAsync(ct)).DomainOf(appCallsign);

        var flows = await _transfers.ListFlowsByAccAsync(accCode, ct);
        var types = await _apps.GetSectorTypeMapAsync(ct);
        var nameMap = await _apps.GetSectorNameMapAsync(ct);
        var codeMap = await _apps.GetSectorCodeMapAsync(ct);
        var airportMap = CoordinationDerivation.MergeAirportNames(await _apps.GetAirportNameMapAsync(ct), flows);
        var atcMap = await _apps.GetSectorAtcNameMapAsync(ct);

        var tpl = _sentence.Current;

        // Cuore condiviso (owned + entranti, direzione owner→next senza invert, frase composta).
        var domainSet = domain as IReadOnlySet<string> ?? new HashSet<string>(domain, StringComparer.OrdinalIgnoreCase);
        var entries = CoordinationDerivation.Build(flows, domainSet, types, nameMap, codeMap, airportMap, atcMap, tpl);

        var towardAcc = new Dictionary<string, List<AppCoordRow>>(StringComparer.OrdinalIgnoreCase);
        var towardTwr = new Dictionary<string, List<AppCoordRow>>(StringComparer.OrdinalIgnoreCase);
        var overflights = new Dictionary<string, List<AppCoordRow>>(StringComparer.OrdinalIgnoreCase);

        foreach (var e in entries)
        {
            // Sorvoli/VFR/altro (senza aeroporto) → gruppo dedicato per etichetta di tipo.
            if (e.Kind is not (TransferFlowKind.Arrival or TransferFlowKind.Departure))
                Bucket(overflights, TransferFlowKindLabels.Label(e.Kind)).Add(e.Row);
            // Arrivi/partenze verso un ACC (CTR) → verso ACC; il counterpart è la chiave del gruppo.
            else if (e.CounterpartType == SectorType.Ctr)
                Bucket(towardAcc, e.CounterpartCallsign).Add(e.Row);
            // Arrivi verso una torre → verso torri (le partenze verso torre non si mostrano).
            else if (e.CounterpartType is SectorType.Twr or SectorType.ITwr && e.Kind == TransferFlowKind.Arrival)
                Bucket(towardTwr, e.CounterpartCallsign).Add(e.Row);
        }

        return new AppCoordination { TowardAcc = ToGroups(towardAcc), TowardTowers = ToGroups(towardTwr), Overflights = ToGroups(overflights) };

        static List<AppCoordRow> Bucket(Dictionary<string, List<AppCoordRow>> d, string key) =>
            d.TryGetValue(key, out var list) ? list : d[key] = new List<AppCoordRow>();

        static IReadOnlyList<AppCoordGroup> ToGroups(Dictionary<string, List<AppCoordRow>> d) =>
            d.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kv => new AppCoordGroup(kv.Key, kv.Value)).ToList();
    }

    public async Task<AccAorView> GetAorViewAsync(string appCallsign, CancellationToken ct = default)
    {
        var app = Norm(appCallsign);
        var sectors = new List<AccSectorAor>();
        var custom = await GetAorCustomizationAsync(app, ct);   // shape extra + override colore

        // Settori APP del dominio di copertura (primario + figli standalone), coerente con le frequenze che usano DomainOf.
        var topo = await _topology.BuildGlobalAsync(ct);
        var domain = topo.DomainOf(app);
        var types = await _apps.GetSectorTypeMapAsync(ct);
        var appCallsigns = domain
            .Where(cs => types.TryGetValue(cs, out var t) && t == SectorType.App)
            .OrderBy(cs => string.Equals(cs, app, StringComparison.OrdinalIgnoreCase) ? 0 : 1)   // primario per primo
            .ThenBy(cs => cs, StringComparer.OrdinalIgnoreCase);

        var appCsList = appCallsigns.ToList();
        var limits = await _apps.GetSectorLimitsByCallsignAsync(appCsList, ct);
        foreach (var cs in appCsList)
        {
            var poly = Aor.AorPolygonProjector.Project(await _apps.GetAorPolygonRawAsync(cs, ct));
            if (poly is null) continue;
            var (lo, hi) = FlBandOf(cs, limits);
            sectors.Add(new AccSectorAor(cs, cs, Aor.AorColorScheme.Resolve(cs, custom.Colors), new[] { poly }, lo, hi));
        }

        // Shape extra scelte a mano (settori DB, anche torri/esteri): sostituiscono l'overlay torri automatico.
        // Appese come anelli toggleabili dopo i settori APP, dedup su quanto già presente.
        if (custom.Callsigns.Count > 0)
        {
            var rawByCs = await _apps.GetSectorPolygonsRawByCallsignAsync(custom.Callsigns, ct);
            var extraLimits = await _apps.GetSectorLimitsByCallsignAsync(custom.Callsigns, ct);
            var present = new HashSet<string>(sectors.Select(s => s.Callsign), StringComparer.OrdinalIgnoreCase);
            var names = await _apps.GetSectorNameMapAsync(ct);
            foreach (var cs in custom.Callsigns.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (present.Contains(cs) || !rawByCs.TryGetValue(cs, out var raw)) continue;
                var poly = Aor.AorPolygonProjector.Project(raw);
                if (poly is null) continue;
                var (lo, hi) = FlBandOf(cs, extraLimits);
                sectors.Add(new AccSectorAor(cs, names.GetValueOrDefault(cs, cs), Aor.AorColorScheme.Resolve(cs, custom.Colors), new[] { poly }, lo, hi));
                present.Add(cs);
            }
        }

        // Configurazioni selezionabili sulla mappa: le salvate (settori aperti = APP aperti), altrimenti «tutti».
        var configs = await GetConfigurationsAsync(app, ct);
        var selections = configs.Count > 0
            ? configs.Select(c => new AccConfigSelection(c.Key, c.Name, c.OpenCallsigns.ToList())).ToList()
            : new List<AccConfigSelection> { new("all", "Tutti i settori", sectors.Select(s => s.Callsign).ToList()) };
        return new AccAorView(sectors, selections);
    }

    // Banda FL (Lower/Upper) normalizzata per l'estrusione 3D; callsign assente dai limiti = default GND/UNL.
    private static (int? Lower, int? Upper) FlBandOf(string cs, IReadOnlyDictionary<string, SectorFlLimits> limits)
    {
        if (!limits.TryGetValue(cs, out var l)) return (null, null);
        var (bottom, top) = Aor.AorFlBand.Normalize(l.Lower, l.Upper);
        return (bottom, top);
    }

    public Task<IReadOnlyList<LinkableFrequencyRow>> ListLinkableFrequenciesAsync(CancellationToken ct = default) =>
        _apps.ListLinkableFrequenciesAsync(ct);

    public Task<IReadOnlyList<SectorShapePick>> ListSelectableSectorShapesAsync(CancellationToken ct = default) =>
        _apps.ListSelectableSectorShapesAsync(ct);

    public async Task<AorExtraShapes> GetAorCustomizationAsync(string appCallsign, CancellationToken ct = default)
    {
        if (await ResolveDocIdAsync(appCallsign, ct) is not int docId) return new AorExtraShapes();
        var json = await _editing.GetSectionBlockJsonAsync(docId, "aor", ct);
        return Deserialize<AorExtraShapes>(json) ?? new AorExtraShapes();
    }

    public async Task SaveAorCustomizationAsync(string appCallsign, AorExtraShapes data, CancellationToken ct = default)
    {
        var docId = await EnsureAsync(appCallsign, ct);   // ACC-gated + garantisce il Document
        var clean = AorCustomizationCleaner.Clean(data);
        var empty = clean.Callsigns.Count == 0 && clean.Colors.Count == 0;
        var json = empty ? null : JsonSerializer.Serialize(clean);
        await _editing.SaveSectionBlockJsonAsync(docId, "aor", json, _authz.CurrentUserId ?? 0, ct);
    }

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

    // --- Aree regolamentate (blocco keyed "regulated"): stessa selezione della vIPI ACC, senza il modo automatico.
    // Sull'APP non esistono aree di default: l'ACC di appartenenza ne ha decine e quasi nessuna tocca un singolo
    // avvicinamento, quindi si scelgono tutte a mano (OwnIds) più le eventuali extra di altri ACC (ExtraIds). ---

    public async Task<RegulatedSelection> GetRegulatedAsync(string appCallsign, CancellationToken ct = default)
    {
        if (await ResolveDocIdAsync(appCallsign, ct) is not int docId) return NoAuto(null);
        var json = await _editing.GetSectionBlockJsonAsync(docId, "regulated", ct);
        return NoAuto(RegulatedSelectionJson.Parse(json));
    }

    public async Task SaveRegulatedAsync(string appCallsign, RegulatedSelection selection, CancellationToken ct = default)
    {
        var docId = await EnsureAsync(appCallsign, ct);   // ACC-gated + garantisce il Document
        var clean = NoAuto(selection);
        var empty = clean.OwnIds.Count == 0 && clean.ExtraIds.Count == 0;
        var json = empty ? null : JsonSerializer.Serialize(clean);
        await _editing.SaveSectionBlockJsonAsync(docId, "regulated", json, _authz.CurrentUserId ?? 0, ct);
    }

    // Normalizza a selezione MANUALE (OwnAuto sempre falso): il modo automatico è del solo blocco Aerovia ACC, e un
    // JSON che lo portasse (scritto a mano, o copiato da un blocco ACC) farebbe comparire aree mai scelte.
    private static RegulatedSelection NoAuto(RegulatedSelection? sel) => new()
    {
        OwnAuto = false,
        OwnIds = sel?.OwnIds ?? new List<string>(),
        ExtraIds = sel?.ExtraIds ?? new List<string>(),
    };

    public async Task<IReadOnlyList<SpecialAreaPick>> ListSpecialAreasAsync(string appCallsign, CancellationToken ct = default)
    {
        var acc = await _apps.GetAccCodeByAppAsync(Norm(appCallsign), ct);
        return acc is null ? Array.Empty<SpecialAreaPick>() : await _areas.ListSpecialAreasByAccAsync(acc, ct);
    }

    public async Task<IReadOnlyList<SpecialAreaPick>> ListOtherAccSpecialAreasAsync(string appCallsign, CancellationToken ct = default)
    {
        var acc = await _apps.GetAccCodeByAppAsync(Norm(appCallsign), ct);
        return acc is null ? Array.Empty<SpecialAreaPick>() : await _areas.ListSpecialAreasExcludingAccAsync(acc, ct);
    }

    public async Task<IReadOnlyList<AccSpecialAreaView>> ResolveRegulatedAreasAsync(RegulatedSelection selection, CancellationToken ct = default)
    {
        var sel = NoAuto(selection);
        var orderedIds = sel.OwnIds.Concat(sel.ExtraIds).ToList();
        if (orderedIds.Count == 0) return Array.Empty<AccSpecialAreaView>();
        return SpecialAreaProjection.Build(await _areas.GetSpecialAreasByIdsAsync(orderedIds, ct), orderedIds);
    }

    // --- Configurazioni (blocco keyed "configurations"): storage editoriale + accorpamento derivato. ---

    // Settori APP del dominio di copertura (primario incluso), con nome: pool dei picker e delle righe accorpamento.
    private async Task<IReadOnlyList<(string Callsign, string Name)>> AppSectorsOfAsync(string app, CancellationToken ct)
    {
        var domain = (await _topology.BuildGlobalAsync(ct)).DomainOf(app);
        var types = await _apps.GetSectorTypeMapAsync(ct);
        var names = await _apps.GetSectorNameMapAsync(ct);
        return domain
            .Where(cs => types.TryGetValue(cs, out var t) && t == SectorType.App)
            .OrderBy(cs => string.Equals(cs, app, StringComparison.OrdinalIgnoreCase) ? 0 : 1)   // primario per primo
            .ThenBy(cs => cs, StringComparer.OrdinalIgnoreCase)
            .Select(cs => (cs, names.GetValueOrDefault(cs, cs)))
            .ToList();
    }

    public async Task<IReadOnlyList<AccSectorPick>> ListSectorsAsync(string appCallsign, CancellationToken ct = default)
    {
        var app = Norm(appCallsign);
        return (await AppSectorsOfAsync(app, ct)).Select(s => new AccSectorPick(s.Callsign, s.Name)).ToList();
    }

    public async Task<IReadOnlyList<AccConfiguration>> GetConfigurationsAsync(string appCallsign, CancellationToken ct = default)
    {
        if (await ResolveDocIdAsync(appCallsign, ct) is not int docId) return Array.Empty<AccConfiguration>();
        var json = await _editing.GetSectionBlockJsonAsync(docId, "configurations", ct);
        return ConfigTableProjector.Deserialize(json);
    }

    public async Task SaveConfigurationsAsync(string appCallsign, IReadOnlyList<AccConfiguration> configs, CancellationToken ct = default)
    {
        var docId = await EnsureAsync(appCallsign, ct);   // ACC-gated + garantisce il Document
        var json = (configs?.Count ?? 0) == 0 ? null : JsonSerializer.Serialize(configs);
        await _editing.SaveSectionBlockJsonAsync(docId, "configurations", json, _authz.CurrentUserId ?? 0, ct);
    }

    public async Task<IReadOnlyList<AccConfigTableView>> DeriveConfigTableAsync(string appCallsign, CancellationToken ct = default) =>
        await DeriveConfigTableAsync(appCallsign, await GetConfigurationsAsync(Norm(appCallsign), ct), ct);

    public async Task<IReadOnlyList<AccConfigTableView>> DeriveConfigTableAsync(
        string appCallsign, IReadOnlyList<AccConfiguration> configs, CancellationToken ct = default)
    {
        var app = Norm(appCallsign);
        if (configs is null || configs.Count == 0) return Array.Empty<AccConfigTableView>();

        var topo = await _topology.BuildGlobalAsync(ct);
        var sectors = await AppSectorsOfAsync(app, ct);
        var pool = new HashSet<string>(sectors.Select(s => s.Callsign), StringComparer.OrdinalIgnoreCase);
        return ConfigTableProjector.Build(_aor, topo, new[] { app }, pool, configs);
    }

    // --- Override editoriali su DocumentProfile (doc 08e f4-a): sezioni nascoste, ordine/link freq, template coord. ---

    public Task<AppDocumentIdentity?> GetIdentityAsync(string appCallsign, CancellationToken ct = default) =>
        _apps.ResolveForDocumentAsync(Norm(appCallsign), ct);

    public Task<DocumentProfileData> GetOverridesAsync(string appCallsign, CancellationToken ct = default) =>
        LoadOverridesAsync(appCallsign, ct);

    public Task SaveFrequencyOrderAsync(string appCallsign, IReadOnlyList<AppFreqOrderOverride> overrides, CancellationToken ct = default) =>
        WithDocumentAsync(appCallsign, (docId, c) => _docProfiles.SaveFreqOrderAsync(docId, overrides ?? Array.Empty<AppFreqOrderOverride>(), c), ct);

    public Task SaveFrequencyLinksAsync(string appCallsign, IReadOnlyList<int> sourceSectorIds, CancellationToken ct = default) =>
        WithDocumentAsync(appCallsign, (docId, c) => _docProfiles.SaveFreqLinksAsync(docId, sourceSectorIds ?? Array.Empty<int>(), c), ct);

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
