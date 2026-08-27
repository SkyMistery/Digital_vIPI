using Vipi.Application.Aor;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Anteprima release di una vIPI ACC: dati con blocchi congelati + ciclo AIRAC della release.</summary>
public sealed record AccReleaseView(AccVipiData Data, string AiracCycle);

/// <summary>
/// Use-case di authoring della vIPI ACC: documento a blocchi (Aerovia/CTR + gruppi APP). Le parti editoriali
/// (struttura blocchi, sezioni, configurazioni) si salvano in blocco; le parti derivate (AoR per configurazione,
/// frequenze dei membri, coordinamenti dai trasferimenti) si calcolano live. Scritture gated via authz ACC.
/// </summary>
public interface IAccDerivationService
{
    /// <summary>Radici degli alberi CTR dell'ACC (una vIPI per albero).</summary>
    Task<IReadOnlyList<AccTreeRoot>> ListTreeRootsAsync(string accCode, CancellationToken ct = default);

    /// <summary>Pool di settori selezionabili di un blocco: Aerovia → CTR del sottoalbero (root); gruppo-APP → i suoi membri APP.</summary>
    Task<IReadOnlyList<AccSectorPick>> GetBlockPoolAsync(string accCode, AccBlock block, string? rootCallsign = null, CancellationToken ct = default);

    /// <summary>Tutti i settori APP dell'ACC (per comporre i gruppi).</summary>
    Task<IReadOnlyList<AccSectorPick>> ListAppSectorsAsync(string accCode, CancellationToken ct = default);

    /// <summary>Frequenze derivate del blocco (membri + link), con override d'ordine applicato e accorpamento per ramo (Aerovia).</summary>
    Task<IReadOnlyList<AppFreqRow>> DeriveFrequenciesAsync(string accCode, AccBlock block, string? rootCallsign = null, CancellationToken ct = default);

    /// <summary>Coordinamenti derivati del blocco (flussi posseduti dai membri + entranti): verso ACC/APP/torri.</summary>
    Task<AccCoordination> DeriveCoordinationAsync(string accCode, AccBlock block, string? rootCallsign = null, CancellationToken ct = default);

    /// <summary>Mappe di risoluzione + template per l'anteprima live delle frasi nell'editor trasferimenti.
    /// Stesse mappe della derivazione reale → l'anteprima combacia con l'output pubblicato.</summary>
    Task<CoordinationPreviewContext> GetPreviewContextAsync(string accCode, CancellationToken ct = default);

    /// <summary>Vista AoR del blocco: anelli per-settore (toggleabili) + configurazioni selezionabili. Una sola mappa.</summary>
    Task<AccAorView> DeriveAorViewAsync(string accCode, AccBlock block, string? rootCallsign = null, CancellationToken ct = default);

    /// <summary>Carte MRVA del blocco, dal sectorfile: Aerovia → l'enroute dell'ACC; gruppo-APP → una carta per
    /// aeroporto membro che abbia il file. Vuota se la sorgente non è configurata o nessun membro ha il file.</summary>
    Task<MinimaView> DeriveMinimaAsync(string accCode, AccBlock block, string? rootCallsign = null, CancellationToken ct = default);

    /// <summary>Tabella accorpamento per ogni configurazione: settore unificato (aperto) → settori assorbiti (derivato via AorService) + CP/Range.</summary>
    Task<IReadOnlyList<AccConfigTableView>> DeriveConfigTableAsync(string accCode, AccBlock block, string? rootCallsign = null, CancellationToken ct = default);

    Task<IReadOnlyList<LinkableFrequencyRow>> ListLinkableFrequenciesAsync(CancellationToken ct = default);

    /// <summary>Tutti i settori DB con poligono AoR, selezionabili come shape extra (picker globale, cerca per ente).</summary>
    Task<IReadOnlyList<SectorShapePick>> ListSelectableSectorShapesAsync(CancellationToken ct = default);

    /// <summary>Aree speciali del proprio ACC (picker editor #8).</summary>
    Task<IReadOnlyList<SpecialAreaPick>> ListSpecialAreasByAccAsync(string accCode, CancellationToken ct = default);

    /// <summary>Aree speciali di altri ACC (picker editor aree extra #8).</summary>
    Task<IReadOnlyList<SpecialAreaPick>> ListOtherAccSpecialAreasAsync(string accCode, CancellationToken ct = default);

    /// <summary>Aree regolamentate del blocco risolte per il viewer (metadati + shape), in ordine (proprie poi extra).
    /// Aerovia in automatico = tutte le aree del proprio <paramref name="accCode"/> (dinamico); altrimenti il sottoinsieme
    /// scelto. Sempre in coda le aree extra di altri ACC.</summary>
    Task<IReadOnlyList<AccSpecialAreaView>> GetAttachedSpecialAreasAsync(string accCode, AccBlock block, CancellationToken ct = default);

}

/// <inheritdoc cref="IAccDerivationService"/>
public sealed class AccDerivationService : IAccDerivationService
{
    private readonly IAccDerivationRepository _repo;
    private readonly ISpecialAreaRepository _areas;
    private readonly IAgreementService _transfers;
    private readonly ITopologyProvider _topology;
    private readonly Aor.IAorService _aor;
    private readonly ICoordinationSentenceTemplate _sentence;
    private readonly IVectoringMinimaSource _minima;

    /// <summary>La lingua in cui comporre la prosa generata. Opzionale: senza, resta il comportamento di
    /// prima — italiano per ACC/APP, inglese per la vLOA.</summary>
    private readonly ReadingLanguageContext? _lingua;


    public AccDerivationService(IAccDerivationRepository repo, ISpecialAreaRepository areas, IAgreementService transfers,
        ITopologyProvider topology, Aor.IAorService aor, ICoordinationSentenceTemplate sentence,
        IVectoringMinimaSource minima, ReadingLanguageContext? lingua = null)
    {
        _repo = repo;
        _areas = areas;
        _transfers = transfers;
        _topology = topology;
        _aor = aor;
        _sentence = sentence;
        _minima = minima;
        _lingua = lingua;
    }

    public Task<IReadOnlyList<AccTreeRoot>> ListTreeRootsAsync(string accCode, CancellationToken ct = default) =>
        _repo.ListTreeRootsAsync(Norm(accCode), ct);

    public async Task<IReadOnlyList<AccSectorPick>> GetBlockPoolAsync(string accCode, AccBlock block, string? rootCallsign = null, CancellationToken ct = default)
    {
        accCode = Norm(accCode);
        // ACC-wide: il pool Aerovia è l'intero insieme dei CTR dell'ACC (tutti gli alberi). Una vIPI per ACC.
        if (block.Kind == AccBlockKind.Aerovia)
            return await _repo.ListCtrSectorsAsync(accCode, ct);

        // Gruppo-APP: i membri scelti (con nome dal catalogo APP dell'ACC).
        var apps = await _repo.ListAppSectorsAsync(accCode, ct);
        var members = new HashSet<string>(block.MemberCallsigns, StringComparer.OrdinalIgnoreCase);
        return apps.Where(a => members.Contains(a.Callsign)).ToList();
    }

    public Task<IReadOnlyList<AccSectorPick>> ListAppSectorsAsync(string accCode, CancellationToken ct = default) =>
        _repo.ListAppSectorsAsync(Norm(accCode), ct);

    public async Task<IReadOnlyList<AppFreqRow>> DeriveFrequenciesAsync(string accCode, AccBlock block, string? rootCallsign = null, CancellationToken ct = default)
    {
        accCode = Norm(accCode);
        var members = await MembersOfAsync(accCode, block, rootCallsign, ct);
        var rows = await _repo.DeriveFrequenciesForMembersAsync(members, block.FreqLinkCallsigns, ct);

        // Ordine override per callsign (dai tasti/drag).
        if (block.FreqOrder.Count > 0)
        {
            var overrides = block.FreqOrder.GroupBy(o => o.Callsign, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Last().Order, StringComparer.OrdinalIgnoreCase);
            rows = FrequencyOrdering.ApplyOrder(rows, overrides);
        }

        return rows;
    }

    public async Task<AccCoordination> DeriveCoordinationAsync(string accCode, AccBlock block, string? rootCallsign = null, CancellationToken ct = default)
    {
        accCode = Norm(accCode);
        var owners = new HashSet<string>(await MembersOfAsync(accCode, block, rootCallsign, ct), StringComparer.OrdinalIgnoreCase);
        if (owners.Count == 0) return AccCoordination.Empty;

        var flows = await _transfers.ListFlowsByAccAsync(accCode, ct);
        var types = await _repo.GetSectorTypeMapAsync(ct);
        var nameMap = await NameMapAsync(accCode, ct);
        var codeMap = await _repo.GetSectorCodeMapAsync(ct);
        var airportMap = CoordinationDerivation.MergeAirportNames(await _repo.GetAirportNameMapAsync(ct), flows);
        var atcMap = await _repo.GetSectorAtcNameMapAsync(ct);
        var accRefMap = await _repo.GetSectorAccRefMapAsync(ct);
        var tpl = CoordinationSentenceTemplate.For(_lingua?.Corrente, _sentence.Current);

        // Cuore condiviso (owned + entranti, direzione owner→next senza invert, frase composta).
        var entries = CoordinationDerivation.Build(flows, owners, types, nameMap, codeMap, airportMap, atcMap, tpl);

        // Un'unica gerarchia (condivisa con la vLOA): Settore → ACC → Aeroporto(arrivi/partenze) + Sorvoli/VFR/altro.
        var sectors = CoordinationDerivation.BuildAccTree(entries, codeMap, atcMap, airportMap, accRefMap, TransferFlowKindLabels.Label);
        return new AccCoordination { Sectors = sectors };
    }

    public async Task<CoordinationPreviewContext> GetPreviewContextAsync(string accCode, CancellationToken ct = default)
    {
        accCode = Norm(accCode);
        // Stesse fonti di DeriveCoordinationAsync: così l'anteprima editor combacia con la derivazione reale.
        var flows = await _transfers.ListFlowsByAccAsync(accCode, ct);
        var types = await _repo.GetSectorTypeMapAsync(ct);
        var nameMap = await NameMapAsync(accCode, ct);
        var codeMap = await _repo.GetSectorCodeMapAsync(ct);
        var airportMap = CoordinationDerivation.MergeAirportNames(await _repo.GetAirportNameMapAsync(ct), flows);
        var atcMap = await _repo.GetSectorAtcNameMapAsync(ct);
        return new CoordinationPreviewContext(types, nameMap, codeMap, airportMap, atcMap,
            CoordinationSentenceTemplate.For(_lingua?.Corrente, _sentence.Current));
    }

    public async Task<AccAorView> DeriveAorViewAsync(string accCode, AccBlock block, string? rootCallsign = null, CancellationToken ct = default)
    {
        accCode = Norm(accCode);

        // Configurazioni selezionabili (RowIndices riempite in #2 dalla tabella config).
        var configs = block.Configurations.Count > 0
            ? block.Configurations.Select(c => new AccConfigSelection(c.Key, c.Name, c.OpenCallsigns.ToList())).ToList()
            : new List<AccConfigSelection> { new("all", "Tutti i settori", (await MembersOfAsync(accCode, block, rootCallsign, ct)).ToList()) };

        // Union dei settori referenziati; fallback ai membri del blocco.
        var callsigns = configs.SelectMany(c => c.OpenCallsigns).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (callsigns.Count == 0) callsigns = (await MembersOfAsync(accCode, block, rootCallsign, ct)).ToList();

        var rawByCs = await _repo.GetSectorPolygonsRawByCallsignAsync(callsigns, ct);
        var limits = await _repo.GetSectorLimitsByCallsignAsync(callsigns, ct);
        var names = await NameMapAsync(accCode, ct);

        var sectors = new List<AccSectorAor>();
        foreach (var cs in callsigns)
        {
            if (!rawByCs.TryGetValue(cs, out var raw)) continue;
            var poly = Aor.AorPolygonProjector.Project(raw);
            if (poly is null) continue;
            var name = names.TryGetValue(cs, out var n) ? n : cs;
            var (lo, hi) = FlBandOf(cs, limits);
            sectors.Add(new AccSectorAor(cs, name, Aor.AorColorScheme.Resolve(cs, block.AorColorOverrides), new[] { poly }, lo, hi));
        }

        // Shape extra scelte a mano (settori DB, anche esteri): appese come anelli toggleabili dopo i settori
        // principali, dedup su quanto già presente. Nome = da NameMap se noto, altrimenti il callsign.
        if (block.ExtraAorCallsigns.Count > 0)
        {
            var extraRaw = await _repo.GetSectorPolygonsRawByCallsignAsync(block.ExtraAorCallsigns, ct);
            var extraLimits = await _repo.GetSectorLimitsByCallsignAsync(block.ExtraAorCallsigns, ct);
            AppendExtraShapes(sectors, block.ExtraAorCallsigns, extraRaw, extraLimits, names, block.AorColorOverrides);
        }

        return new AccAorView(sectors, configs);
    }

    // Banda FL (Lower/Upper) di un settore per l'estrusione 3D, normalizzata; assente = default GND/UNL.
    private static (int? Lower, int? Upper) FlBandOf(string cs, IReadOnlyDictionary<string, SectorFlLimits> limits)
    {
        if (!limits.TryGetValue(cs, out var l)) return (null, null);
        var (bottom, top) = Aor.AorFlBand.Normalize(l.Lower, l.Upper);
        return (bottom, top);
    }

    // Appende gli anelli delle shape extra (settori DB) non già presenti; colore per tipo-ente + override; banda FL.
    private static void AppendExtraShapes(List<AccSectorAor> sectors, IReadOnlyList<string> extra,
        IReadOnlyDictionary<string, string> rawByCs, IReadOnlyDictionary<string, SectorFlLimits> limits,
        IReadOnlyDictionary<string, string> names, IReadOnlyDictionary<string, string> colorOverrides)
    {
        var present = new HashSet<string>(sectors.Select(s => s.Callsign), StringComparer.OrdinalIgnoreCase);
        foreach (var cs in extra.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (present.Contains(cs) || !rawByCs.TryGetValue(cs, out var raw)) continue;
            var poly = Aor.AorPolygonProjector.Project(raw);
            if (poly is null) continue;
            var name = names.TryGetValue(cs, out var n) ? n : cs;
            var (lo, hi) = FlBandOf(cs, limits);
            sectors.Add(new AccSectorAor(cs, name, Aor.AorColorScheme.Resolve(cs, colorOverrides), new[] { poly }, lo, hi));
            present.Add(cs);
        }
    }

    public async Task<IReadOnlyList<AccConfigTableView>> DeriveConfigTableAsync(string accCode, AccBlock block, string? rootCallsign = null, CancellationToken ct = default)
    {
        accCode = Norm(accCode);
        if (block.Configurations.Count == 0) return Array.Empty<AccConfigTableView>();

        var topo = await _topology.BuildByAccCodeAsync(accCode, ct);
        if (topo is null) return Array.Empty<AccConfigTableView>();

        // Radici su cui risolvere l'ownership + pool ammesso come righe, per natura del blocco:
        //  - Aerovia: radici = alberi CTR dell'ACC (domini disgiunti), pool = tutti i CTR;
        //  - gruppo-APP: radici = i callsign APP membri, pool = i settori APP dei loro domini.
        IReadOnlyList<string> roots;
        HashSet<string> pool;
        if (block.Kind == AccBlockKind.Aerovia)
        {
            roots = (await _repo.ListTreeRootsAsync(accCode, ct)).Select(r => r.Callsign).ToList();
            pool = new HashSet<string>((await _repo.ListCtrSectorsAsync(accCode, ct)).Select(s => s.Callsign), StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            var members = await MembersOfAsync(accCode, block, rootCallsign, ct);
            roots = members.ToList();
            var types = await _repo.GetSectorTypeMapAsync(ct);
            pool = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in members)
                foreach (var cs in topo.DomainOf(m))
                    if (types.TryGetValue(cs, out var t) && t == SectorType.App) pool.Add(cs);
        }

        return ConfigTableProjector.Build(_aor, topo, roots, pool, block.Configurations);
    }

    private async Task<IReadOnlyDictionary<string, string>> NameMapAsync(string accCode, CancellationToken ct)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in await _repo.ListCtrSectorsAsync(accCode, ct)) map[s.Callsign] = s.Name;
        foreach (var s in await _repo.ListAppSectorsAsync(accCode, ct)) map.TryAdd(s.Callsign, s.Name);
        return map;
    }

    public Task<IReadOnlyList<LinkableFrequencyRow>> ListLinkableFrequenciesAsync(CancellationToken ct = default) =>
        _repo.ListLinkableFrequenciesAsync(ct);

    public Task<IReadOnlyList<SectorShapePick>> ListSelectableSectorShapesAsync(CancellationToken ct = default) =>
        _repo.ListSelectableSectorShapesAsync(ct);

    public Task<IReadOnlyList<SpecialAreaPick>> ListSpecialAreasByAccAsync(string accCode, CancellationToken ct = default) =>
        _areas.ListSpecialAreasByAccAsync(Norm(accCode), ct);

    public Task<IReadOnlyList<SpecialAreaPick>> ListOtherAccSpecialAreasAsync(string accCode, CancellationToken ct = default) =>
        _areas.ListSpecialAreasExcludingAccAsync(Norm(accCode), ct);

    public async Task<IReadOnlyList<AccSpecialAreaView>> GetAttachedSpecialAreasAsync(string accCode, AccBlock block, CancellationToken ct = default)
    {
        // Aree del proprio ACC: automatico (Aerovia) = tutte; altrimenti il sottoinsieme scelto. Poi le extra di altri ACC.
        IReadOnlyList<string> ownIds = block.Kind == AccBlockKind.Aerovia && block.Regulated.OwnAuto
            ? (await _areas.ListSpecialAreasByAccAsync(Norm(accCode), ct)).Select(p => p.IvaoId).ToList()
            : block.Regulated.OwnIds;
        var orderedIds = ownIds.Concat(block.Regulated.ExtraIds).ToList();
        if (orderedIds.Count == 0) return Array.Empty<AccSpecialAreaView>();

        // Ordine preservato (proprie poi extra) dal proiettore condiviso con l'APP non remotizzata.
        return SpecialAreaProjection.Build(await _areas.GetSpecialAreasByIdsAsync(orderedIds, ct), orderedIds);
    }

    /// <summary>Membri effettivi del blocco: Aerovia con lista vuota = TUTTI i CTR dell'ACC (una vIPI per ACC, tutti
    /// gli alberi CTR insieme); altrimenti i callsign indicati. Il parametro <paramref name="rootCallsign"/> non
    /// restringe più a un sottoalbero: la vIPI ACC è unica e copre l'intero ACC.</summary>
    public async Task<MinimaView> DeriveMinimaAsync(string accCode, AccBlock block, string? rootCallsign = null, CancellationToken ct = default)
    {
        accCode = Norm(accCode);

        // Aerovia = l'enroute dell'ACC, che nel sectorfile è UN file (ENRMVA/{acc}.mva): dentro non c'è nulla che
        // leghi un'area a un settore — tutti i poligoni portano il codice dell'ACC e basta — quindi la carta è
        // dell'ente, come in Aurora. Non si prova a spartirla fra i CTR del blocco.
        if (block.Kind == AccBlockKind.Aerovia)
            return await MinimaCharts.ForAccAsync(_minima, accCode, ct);

        // Gruppo-APP: una carta per aeroporto membro. I file per-aeroporto sì che hanno un proprietario dichiarato,
        // ed è il nome del file.
        var members = await MembersOfAsync(accCode, block, rootCallsign, ct);
        return await MinimaCharts.ForPositionsAsync(_minima, members, ct);
    }

    private async Task<IReadOnlyList<string>> MembersOfAsync(string accCode, AccBlock block, string? rootCallsign, CancellationToken ct)
    {
        if (block.MemberCallsigns.Count > 0) return block.MemberCallsigns;
        if (block.Kind == AccBlockKind.Aerovia)
            return (await _repo.ListCtrSectorsAsync(accCode, ct)).Select(s => s.Callsign).ToList();
        return Array.Empty<string>();
    }

    private static string Norm(string code) => (code ?? "").Trim().ToUpperInvariant();
}
