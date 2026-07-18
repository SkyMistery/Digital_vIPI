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

    /// <summary>Mappa callsign CTR → nome ramo di appartenenza (accorpamento freq #5). Vuota per blocchi non-Aerovia.</summary>
    Task<IReadOnlyDictionary<string, string>> GetFreqGroupMapAsync(string accCode, AccBlock block, string? rootCallsign = null, CancellationToken ct = default);

    /// <summary>Coordinamenti derivati del blocco (flussi posseduti dai membri + entranti): verso ACC/APP/torri.</summary>
    Task<AccCoordination> DeriveCoordinationAsync(string accCode, AccBlock block, string? rootCallsign = null, CancellationToken ct = default);

    /// <summary>Vista AoR del blocco: anelli per-settore (toggleabili) + configurazioni selezionabili. Una sola mappa.</summary>
    Task<AccAorView> DeriveAorViewAsync(string accCode, AccBlock block, string? rootCallsign = null, CancellationToken ct = default);

    /// <summary>Tabella accorpamento per ogni configurazione: settore unificato (aperto) → settori assorbiti (derivato via AorService) + CP/Range.</summary>
    Task<IReadOnlyList<AccConfigTableView>> DeriveConfigTableAsync(string accCode, AccBlock block, string? rootCallsign = null, CancellationToken ct = default);

    Task<IReadOnlyList<LinkableFrequencyRow>> ListLinkableFrequenciesAsync(CancellationToken ct = default);

    /// <summary>Aree speciali dell'ACC (picker editor #8).</summary>
    Task<IReadOnlyList<SpecialAreaPick>> ListSpecialAreasByAccAsync(string accCode, CancellationToken ct = default);

    /// <summary>Aree speciali attaccate al blocco, risolte per il viewer (metadati + shape proiettata), nell'ordine scelto.</summary>
    Task<IReadOnlyList<AccSpecialAreaView>> GetAttachedSpecialAreasAsync(AccBlock block, CancellationToken ct = default);

}

/// <inheritdoc cref="IAccDerivationService"/>
public sealed class AccDerivationService : IAccDerivationService
{
    private readonly IAccDerivationRepository _repo;
    private readonly ITransferService _transfers;
    private readonly ITopologyProvider _topology;
    private readonly Aor.IAorService _aor;
    private readonly ICoordinationSentenceTemplate _sentence;

    public AccDerivationService(IAccDerivationRepository repo, ITransferService transfers,
        ITopologyProvider topology, Aor.IAorService aor, ICoordinationSentenceTemplate sentence)
    {
        _repo = repo;
        _transfers = transfers;
        _topology = topology;
        _aor = aor;
        _sentence = sentence;
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
        var accNameMap = await _repo.GetSectorAccNameMapAsync(ct);
        var tpl = string.IsNullOrWhiteSpace(block.CoordinationSentenceTemplate)
            ? _sentence.Current
            : _sentence.Current.WithTemplate(block.CoordinationSentenceTemplate!);

        // Cuore condiviso (owned + entranti, direzione owner→next senza invert, frase composta).
        var entries = CoordinationDerivation.Build(flows, owners, types, nameMap, codeMap, airportMap, atcMap, tpl);

        // Un'unica gerarchia (condivisa con la vLOA): Settore → ACC → Aeroporto(arrivi/partenze) + Sorvoli/VFR/altro.
        var sectors = CoordinationDerivation.BuildAccTree(entries, codeMap, atcMap, airportMap, accNameMap, TransferFlowKindLabels.Label);
        return new AccCoordination { Sectors = sectors };
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
        var names = await NameMapAsync(accCode, ct);

        var sectors = new List<AccSectorAor>();
        var i = 0;
        foreach (var cs in callsigns)
        {
            if (!rawByCs.TryGetValue(cs, out var raw)) continue;
            var poly = Aor.AorPolygonProjector.Project(raw);
            if (poly is null) continue;
            var name = names.TryGetValue(cs, out var n) ? n : cs;
            sectors.Add(new AccSectorAor(cs, name, Aor.AorPalette.ColorAt(i), new[] { poly }));
            i++;
        }
        return new AccAorView(sectors, configs);
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

    public async Task<IReadOnlyDictionary<string, string>> GetFreqGroupMapAsync(string accCode, AccBlock block, string? rootCallsign = null, CancellationToken ct = default)
    {
        accCode = Norm(accCode);
        if (block.Kind != AccBlockKind.Aerovia) return new Dictionary<string, string>();
        // ACC-wide: unione delle mappe di ramo di tutti gli alberi CTR dell'ACC.
        var roots = (await _repo.ListTreeRootsAsync(accCode, ct)).Select(r => r.Callsign).ToList();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
            foreach (var kv in await _repo.GetCtrBranchMapAsync(accCode, root, ct))
                result[kv.Key] = kv.Value.Name;
        return result;
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

    public Task<IReadOnlyList<SpecialAreaPick>> ListSpecialAreasByAccAsync(string accCode, CancellationToken ct = default) =>
        _repo.ListSpecialAreasByAccAsync(Norm(accCode), ct);

    public async Task<IReadOnlyList<AccSpecialAreaView>> GetAttachedSpecialAreasAsync(AccBlock block, CancellationToken ct = default)
    {
        if (block.AttachedSpecialAreaIds.Count == 0) return Array.Empty<AccSpecialAreaView>();
        var details = await _repo.GetSpecialAreasByIdsAsync(block.AttachedSpecialAreaIds, ct);
        var byId = details.ToDictionary(d => d.IvaoId, StringComparer.OrdinalIgnoreCase);

        var result = new List<AccSpecialAreaView>();
        foreach (var id in block.AttachedSpecialAreaIds)   // preserva l'ordine scelto dallo staff
        {
            if (!byId.TryGetValue(id, out var d)) continue;
            var shape = string.IsNullOrWhiteSpace(d.RegionMapPolygon) ? null : Aor.AorPolygonProjector.Project(d.RegionMapPolygon);
            result.Add(new AccSpecialAreaView(d.IvaoId, d.Name, d.Type, d.Description, d.ActivationDetails, d.MinimumAlt, d.MaximumAlt, shape));
        }
        return result;
    }

    /// <summary>Membri effettivi del blocco: Aerovia con lista vuota = TUTTI i CTR dell'ACC (una vIPI per ACC, tutti
    /// gli alberi CTR insieme); altrimenti i callsign indicati. Il parametro <paramref name="rootCallsign"/> non
    /// restringe più a un sottoalbero: la vIPI ACC è unica e copre l'intero ACC.</summary>
    private async Task<IReadOnlyList<string>> MembersOfAsync(string accCode, AccBlock block, string? rootCallsign, CancellationToken ct)
    {
        if (block.MemberCallsigns.Count > 0) return block.MemberCallsigns;
        if (block.Kind == AccBlockKind.Aerovia)
            return (await _repo.ListCtrSectorsAsync(accCode, ct)).Select(s => s.Callsign).ToList();
        return Array.Empty<string>();
    }

    private static string Norm(string code) => (code ?? "").Trim().ToUpperInvariant();
}
