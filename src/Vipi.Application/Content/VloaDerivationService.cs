using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Auth;
using Vipi.Domain;

namespace Vipi.Application.Content;

// =====================================================================================
//  vLOA data-driven: AoR / Frequenze / Coordinamenti DERIVATI unendo i due ACC (Home
//  italiano + Neighbour estero). Stato editoriale (settori/frequenze nascosti) nella side-entity DocumentProfile.
// =====================================================================================

/// <summary>Identità della coppia vLOA (per intestazioni/etichette).</summary>
public sealed record VloaPairMeta(string HomeAcc, string ForeignAcc, string HomeName, string ForeignName);

/// <summary>Chip toggle di un settore AoR: identità + colore (blu home / rosso estero) + stato nascosto.</summary>
public sealed record VloaAorSectorToggle(string Callsign, string Name, string Color, bool IsForeign, bool Hidden);

/// <summary>Vista AoR della vLOA: la mappa (settori NON nascosti, riusa <see cref="AccAorView"/>) + i chip di tutti i settori.</summary>
public sealed record VloaAorData(AccAorView Map, IReadOnlyList<VloaAorSectorToggle> Toggles)
{
    public static VloaAorData Empty { get; } = new(AccAorView.Empty, Array.Empty<VloaAorSectorToggle>());
}

/// <summary>Riga della tabella frequenze vLOA: la frequenza derivata + lato (home/estero) + stato nascosto.</summary>
public sealed record VloaFreqRow(AppFreqRow Row, bool IsForeign, bool Hidden);

/// <summary>Vista frequenze della vLOA: codici nazione/ACC dei due lati + tutte le frequenze con flag di visibilità.</summary>
public sealed record VloaFreqData(
    string HomeCountry, string HomeAcc, string ForeignCountry, string ForeignAcc, IReadOnlyList<VloaFreqRow> Rows)
{
    public static VloaFreqData Empty { get; } = new("", "", "", "", Array.Empty<VloaFreqRow>());
}

/// <summary>Coordinamenti della vLOA nelle due direzioni (home→estero, estero→home). Ogni direzione è un
/// <see cref="AccCoordination"/> (stessa gerarchia Settore→ACC→Aeroporto→Arrivi/Partenze della vIPI ACC): la
/// sezione è resa da <c>AccCoordinationView</c> in inglese, non più da una tabella piatta dedicata.</summary>
public sealed record VloaCoordination(
    string HomeAcc, string ForeignAcc,
    AccCoordination HomeToForeign, AccCoordination ForeignToHome)
{
    public static VloaCoordination Empty { get; } =
        new("", "", AccCoordination.Empty, AccCoordination.Empty);
}

/// <summary>Derivazione data-driven delle sezioni AoR/Frequenze/Coordinamenti della vLOA + toggle di visibilità.</summary>
public interface IVloaDerivationService
{
    Task<VloaPairMeta?> GetPairMetaAsync(int docId, CancellationToken ct = default);
    Task<VloaAorData> DeriveAorAsync(int docId, CancellationToken ct = default);
    Task<VloaFreqData> DeriveFrequenciesAsync(int docId, CancellationToken ct = default);
    Task<VloaCoordination> DeriveCoordinationAsync(int docId, CancellationToken ct = default);

    /// <summary>Inverte la visibilità di un settore nella mappa AoR (persistito). Authz: edit dell'ACC Home.</summary>
    Task ToggleAorSectorAsync(int docId, string callsign, CancellationToken ct = default);

    /// <summary>Inverte la visibilità di una frequenza nella tabella (persistito). Authz: edit dell'ACC Home.</summary>
    Task ToggleFrequencyAsync(int docId, string callsign, CancellationToken ct = default);

    /// <summary>Inverte la visibilità di una sezione (per titolo) nel documento pubblicato. Authz: edit dell'ACC Home.</summary>
    Task ToggleSectionAsync(int docId, string sectionTitle, CancellationToken ct = default);

    /// <summary>Titoli delle sezioni nascoste (per l'editor/viewer). Lettura senza authz.</summary>
    Task<IReadOnlyList<string>> GetHiddenSectionsAsync(int docId, CancellationToken ct = default);
}

/// <inheritdoc cref="IVloaDerivationService"/>
public sealed class VloaDerivationService : IVloaDerivationService
{
    private const string HomeColor = "#1f6feb";
    private const string ForeignColor = "#d1242f";

    private readonly IVloaDerivationRepository _repo;
    private readonly IAccDerivationRepository _accRepo;
    private readonly ITransferService _transfers;
    private readonly ICoordinationSentenceTemplate _sentence;
    private readonly IEditAuthorizationService _authz;
    private readonly NeighboursOptions _neighbours;

    public VloaDerivationService(IVloaDerivationRepository repo, IAccDerivationRepository accRepo, ITransferService transfers,
        ICoordinationSentenceTemplate sentence, IEditAuthorizationService authz, IOptions<NeighboursOptions> neighbours)
    {
        _repo = repo;
        _accRepo = accRepo;
        _transfers = transfers;
        _sentence = sentence;
        _authz = authz;
        _neighbours = neighbours.Value;
    }

    /// <summary>Settori EFFETTIVAMENTE confinanti (home/estero) calcolati per geometria dai poligoni di confine dei
    /// due ACC (non tutti i settori delle FIR). Deterministico, indipendente dallo stato del candidato.</summary>
    private async Task<(List<string> Home, List<string> Foreign)> ComputeConfiningAsync(VloaPairInfo pair, CancellationToken ct)
    {
        var homeRings = (await _repo.GetBoundaryPolygonsAsync(pair.HomeAcc, ct))
            .Select(p => (p.Callsign, Ring: PolygonGeometry.ToRing(p.Raw))).Where(x => x.Ring is not null).ToList();
        var foreignRings = (await _repo.GetBoundaryPolygonsAsync(pair.ForeignAcc, ct))
            .Select(p => (p.Callsign, Ring: PolygonGeometry.ToRing(p.Raw))).Where(x => x.Ring is not null).ToList();

        var threshold = _neighbours.AdjacencyThresholdNm;
        var home = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var foreign = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in homeRings)
            foreach (var f in foreignRings)
                if (PolygonGeometry.AreAdjacent(h.Ring, f.Ring, threshold))
                {
                    home.Add(h.Callsign);
                    foreign.Add(f.Callsign);
                }
        return (home.ToList(), foreign.ToList());
    }

    public async Task<VloaPairMeta?> GetPairMetaAsync(int docId, CancellationToken ct = default)
    {
        var pair = await _repo.GetPairAsync(docId, ct);
        return pair is null ? null : new VloaPairMeta(pair.HomeAcc, pair.ForeignAcc, pair.HomeName, pair.ForeignName);
    }

    public async Task<VloaAorData> DeriveAorAsync(int docId, CancellationToken ct = default)
    {
        var pair = await _repo.GetPairAsync(docId, ct);
        if (pair is null) return VloaAorData.Empty;
        var profile = await _repo.LoadEditorialAsync(docId, ct);
        var hidden = new HashSet<string>(profile.HiddenAorSectors, StringComparer.OrdinalIgnoreCase);
        var atc = await _accRepo.GetSectorAtcNameMapAsync(ct);
        var (homeConf, foreignConf) = await ComputeConfiningAsync(pair, ct);

        var all = homeConf.Concat(foreignConf).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var raw = await _accRepo.GetSectorPolygonsRawByCallsignAsync(all, ct);

        var toggles = new List<VloaAorSectorToggle>();
        var mapSectors = new List<AccSectorAor>();

        void Add(string cs, bool isForeign)
        {
            var name = atc.GetValueOrDefault(cs, cs);
            var color = isForeign ? ForeignColor : HomeColor;
            var isHidden = hidden.Contains(cs);
            toggles.Add(new VloaAorSectorToggle(cs, name, color, isForeign, isHidden));
            if (isHidden || !raw.TryGetValue(cs, out var poly)) return;
            var projected = AorPolygonProjector.Project(poly);
            if (projected is not null) mapSectors.Add(new AccSectorAor(cs, name, color, new[] { projected }));
        }

        foreach (var cs in homeConf) Add(cs, false);
        foreach (var cs in foreignConf) Add(cs, true);

        var map = new AccAorView(mapSectors, Array.Empty<AccConfigSelection>());
        return new VloaAorData(map, toggles);
    }

    public async Task<VloaFreqData> DeriveFrequenciesAsync(int docId, CancellationToken ct = default)
    {
        var pair = await _repo.GetPairAsync(docId, ct);
        if (pair is null) return VloaFreqData.Empty;
        var profile = await _repo.LoadEditorialAsync(docId, ct);
        var hidden = new HashSet<string>(profile.HiddenFrequencies, StringComparer.OrdinalIgnoreCase);
        var empty = Array.Empty<string>();

        // Solo i settori EFFETTIVAMENTE confinanti su ENTRAMBI i lati (non tutti i CTR degli ACC), coerente con l'AoR.
        var (homeConf, foreignConf) = await ComputeConfiningAsync(pair, ct);
        var homeRows = await _accRepo.DeriveFrequenciesForMembersAsync(homeConf, empty, ct);
        var foreignRows = await _accRepo.DeriveFrequenciesForMembersAsync(foreignConf, empty, ct);

        var rows = homeRows.Select(r => new VloaFreqRow(r, false, hidden.Contains(r.Callsign)))
            .Concat(foreignRows.Select(r => new VloaFreqRow(r, true, hidden.Contains(r.Callsign))))
            .ToList();
        return new VloaFreqData("IT", pair.HomeAcc, pair.ForeignCountry, pair.ForeignAcc, rows);
    }

    public async Task<VloaCoordination> DeriveCoordinationAsync(int docId, CancellationToken ct = default)
    {
        var pair = await _repo.GetPairAsync(docId, ct);
        if (pair is null) return VloaCoordination.Empty;

        var types = await _accRepo.GetSectorTypeMapAsync(ct);
        var codeMap = await _accRepo.GetSectorCodeMapAsync(ct);
        var atcMap = await _accRepo.GetSectorAtcNameMapAsync(ct);
        var accNameMap = await _accRepo.GetSectorAccNameMapAsync(ct);
        // Le vLOA sono documenti bilaterali in INGLESE: frasi di coordinamento col template EN.
        var tpl = CoordinationSentenceTemplate.English;

        var homeSet = new HashSet<string>(pair.HomeAll, StringComparer.OrdinalIgnoreCase);
        var foreignSet = new HashSet<string>(pair.ForeignAll, StringComparer.OrdinalIgnoreCase);

        var flows = (await _transfers.ListFlowsByAccAsync(pair.HomeAcc, ct))
            .Concat(await _transfers.ListFlowsByAccAsync(pair.ForeignAcc, ct)).ToList();
        var airportMap = CoordinationDerivation.MergeAirportNames(await _accRepo.GetAirportNameMapAsync(ct), flows);

        // Solo i trasferimenti che attraversano il confine, per direzione (owner→next, senza inversione):
        // home→estero (H2F) e estero→home (F2H). Ogni riga diventa una CoordinationEntry per l'albero ACC condiviso.
        var h2f = new List<CoordinationEntry>();
        var f2h = new List<CoordinationEntry>();

        foreach (var flow in flows)
        {
            var owner = flow.OwningSectorCallsign;
            var ownerHome = homeSet.Contains(owner);
            var ownerForeign = foreignSet.Contains(owner);
            if (!ownerHome && !ownerForeign) continue;

            foreach (var p in flow.Points)
            {
                var next = p.NextSectorCallsign;
                if (string.IsNullOrWhiteSpace(next) || !types.TryGetValue(next!, out var nextType)) continue;
                var nextHome = homeSet.Contains(next!);
                var nextForeign = foreignSet.Contains(next!);
                var isH2F = ownerHome && nextForeign;
                var isF2H = ownerForeign && nextHome;
                if (!isH2F && !isF2H) continue;

                var sentence = CoordinationSentences.Compose(tpl, types, atcMap, codeMap, airportMap, atcMap,
                    owner, next!, flow.AirportIcao, p.LevelConstraint, p.LevelValue, p.LevelUnit, p.LevelSpecial, p.Parity, p.Cop, flow.Kind,
                    p.ConditionLabel, p.ConditionAreaLabel, p.ConditionCustomLabel, p.VerticalState);
                var row = new AppCoordRow(p.Cop, p.LevelText, next!, flow.Kind)
                {
                    OwnerCallsign = owner,
                    AirportIcao = flow.AirportIcao,
                    Constraint = p.LevelConstraint,
                    Sentence = sentence,
                    ConditionLabel = p.ConditionDisplay,
                };
                var entry = new CoordinationEntry(owner, next!, nextType, flow.AirportIcao, flow.Kind, IsIncoming: false, row);
                (isH2F ? h2f : f2h).Add(entry);
            }
        }

        AccCoordination Tree(List<CoordinationEntry> es) => new()
        {
            Sectors = CoordinationDerivation.BuildAccTree(es, codeMap, atcMap, airportMap, accNameMap, TransferFlowKindLabels.LabelEn),
        };

        return new VloaCoordination(pair.HomeAcc, pair.ForeignAcc, Tree(h2f), Tree(f2h));
    }

    public Task ToggleAorSectorAsync(int docId, string callsign, CancellationToken ct = default) =>
        ToggleAsync(docId, callsign, Target.Aor, ct);

    public Task ToggleFrequencyAsync(int docId, string callsign, CancellationToken ct = default) =>
        ToggleAsync(docId, callsign, Target.Freq, ct);

    public Task ToggleSectionAsync(int docId, string sectionTitle, CancellationToken ct = default) =>
        ToggleAsync(docId, sectionTitle, Target.Section, ct);

    public async Task<IReadOnlyList<string>> GetHiddenSectionsAsync(int docId, CancellationToken ct = default) =>
        (await _repo.LoadEditorialAsync(docId, ct)).HiddenSections;

    private enum Target { Aor, Freq, Section }

    private async Task ToggleAsync(int docId, string key, Target which, CancellationToken ct)
    {
        var homeAcc = await _repo.GetHomeAccCodeAsync(docId, ct)
            ?? throw new Aor.ValidationException("vLOA inesistente.");
        await _authz.EnsureCanEditAccAsync(homeAcc, ct);

        var state = await _repo.LoadEditorialAsync(docId, ct);
        var hiddenAor = new HashSet<string>(state.HiddenAorSectors, StringComparer.OrdinalIgnoreCase);
        var hiddenFreq = new HashSet<string>(state.HiddenFrequencies, StringComparer.OrdinalIgnoreCase);
        var hiddenSec = new HashSet<string>(state.HiddenSections, StringComparer.OrdinalIgnoreCase);
        var target = which switch { Target.Aor => hiddenAor, Target.Freq => hiddenFreq, _ => hiddenSec };
        if (!target.Add(key)) target.Remove(key);   // toggle

        await _repo.SaveEditorialAsync(docId,
            new VloaEditorialState(hiddenAor.ToList(), hiddenFreq.ToList(), hiddenSec.ToList()), ct);
    }

}
