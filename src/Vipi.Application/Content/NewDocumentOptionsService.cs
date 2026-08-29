using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Un ACC su cui chi guarda può creare, con quello che gli si può appendere.</summary>
/// <param name="Code">Codice ACC (LIRR, LIMM…).</param>
/// <param name="Name">Nome esteso, per la tendina.</param>
/// <param name="AreaSectors">Settori d'area (CTR e APP remotizzati): sono i candidati Home di una vLOA.</param>
/// <param name="StandaloneApps">APP non remotizzati: hanno un documento proprio.</param>
/// <param name="Airports">Aeroporti di competenza.</param>
public sealed record NewDocumentAcc(
    string Code, string Name,
    IReadOnlyList<NewDocumentTarget> AreaSectors,
    IReadOnlyList<NewDocumentTarget> StandaloneApps,
    IReadOnlyList<NewDocumentTarget> Airports);

/// <summary>Un bersaglio selezionabile. <paramref name="Key"/> è ciò che la rotta dell'editor vuole
/// (callsign o ICAO); <paramref name="Id"/> serve solo alle parti della vLOA, che si scelgono per Id.
///
/// <para>⚠️ I tre campi militari valgono <b>solo</b> per gli aeroporti e restano ai valori di riposo per i
/// settori: un settore non ha edizioni. Stanno qui e non in un secondo tipo perché la scheda «Aeroporto»
/// della pagina usa la stessa tendina delle altre — sdoppiare il tipo vorrebbe dire sdoppiare la tendina,
/// la selezione e il tasto per aggiungere due booleani.</para>
/// </summary>
/// <param name="HasDocument">La vIPI CIVILE esiste già (anche in bozza): allora il tasto dice «Apri».</param>
/// <param name="HasMilitaryPresence">C'è una base sul campo: allora, DOPO la civile, si può creare il vSOP
/// militare.</param>
/// <param name="IsMilitaryOnly">Nessun traffico civile: l'unica edizione è il vSOP militare, e la scheda
/// «Aeroporto» porta direttamente lì (carta vSOP militari §5-bis).</param>
/// <param name="HasMilDocument">Il vSOP MILITARE esiste già: sui campi solo militari è LUI a decidere se il
/// tasto dice «Crea» o «Apri».</param>
public sealed record NewDocumentTarget(int Id, string Key, string Label, bool HasDocument,
    bool HasMilitaryPresence = false, bool IsMilitaryOnly = false, bool HasMilDocument = false);

/// <summary>Un ACC estero, coi suoi settori d'area: sono i candidati Neighbour di una vLOA.</summary>
public sealed record NewDocumentForeignAcc(string Code, string Name, IReadOnlyList<NewDocumentTarget> AreaSectors);

/// <summary>Tutto quello che la pagina «Nuovo documento» deve mostrare, già filtrato per chi guarda.</summary>
public sealed record NewDocumentOptions(
    IReadOnlyList<NewDocumentAcc> MyAccs,
    IReadOnlyList<NewDocumentForeignAcc> ForeignAccs);

/// <summary>
/// Ciò che <c>/services/vsop/editor/new-document</c> può offrire a <b>questa</b> persona.
///
/// <para><b>Perché un servizio e non tre letture nella pagina.</b> Gli elenchi globali
/// (<c>ListSectorNodesAsync</c>, <c>ListAllAirportsAsync</c>) sono <c>EnsureAdmin</c>, e allentarli per far
/// entrare qui un Editor cambierebbe i permessi anche di <c>/services/vsop/admin/sector-structure</c> e
/// <c>/services/vsop/admin/airports</c> — dove si <b>scrive</b>. Qui la domanda si fa <b>una volta</b>, con
/// la stessa regola che poi rifiuterebbe.</para>
///
/// <para>⚠️ <b>Non è più «quali ACC sono suoi»</b>: dal 28 agosto 2026 l'Editor edita tutto, quindi o li
/// vede tutti o non ne vede nessuno. Il filtro per ACC che c'era qui è morto con le concessioni.</para>
///
/// <para>⚠️ Filtra, non autorizza: chi crea davvero passa comunque dal cancello del servizio. Una tendina è
/// una comodità, non una guardia — lezione di <c>/services/vsop/versions</c>.</para>
/// </summary>
public interface INewDocumentOptionsService
{
    Task<NewDocumentOptions> LoadAsync(CancellationToken ct = default);
}

/// <inheritdoc cref="INewDocumentOptionsService"/>
public sealed class NewDocumentOptionsService : INewDocumentOptionsService
{
    private readonly IStructureEditingRepository _repo;
    private readonly IEditAuthorizationService _authz;

    public NewDocumentOptionsService(IStructureEditingRepository repo, IEditAuthorizationService authz)
    {
        _repo = repo;
        _authz = authz;
    }

    /// <summary>Prefisso ICAO degli ACC di casa: gli altri sono «esteri» e possono solo fare da Neighbour.</summary>
    private const string PrefissoNazionale = "LI";

    public async Task<NewDocumentOptions> LoadAsync(CancellationToken ct = default)
    {
        var accs = await _repo.ListAccsAsync(ct);
        var sectors = await _repo.ListSectorNodesAsync(ct);
        var airports = await _repo.ListAllAirportsAsync(ct);

        var miei = new List<NewDocumentAcc>();

        // ⚠️ La domanda si fa UNA volta e fuori dal ciclo: da quando l'Editor edita tutto, chiederla per ogni
        // ACC sarebbe chiedere la stessa cosa dieci volte per avere dieci volte la stessa risposta.
        foreach (var a in _authz.IsEditor
                     ? accs.Where(Nazionale).OrderBy(a => a.Code, StringComparer.OrdinalIgnoreCase)
                     : Enumerable.Empty<AccRow>())
        {
            var suoi = sectors.Where(s => Uguale(s.AccCode, a.Code)).ToList();
            miei.Add(new NewDocumentAcc(a.Code, a.Name,
                AreaSectors: suoi.Where(EArea).OrderBy(s => s.Callsign, StringComparer.Ordinal)
                    .Select(Bersaglio).ToList(),
                StandaloneApps: suoi.Where(EAppStandalone).OrderBy(s => s.Callsign, StringComparer.Ordinal)
                    .Select(Bersaglio).ToList(),
                Airports: airports.Where(x => Uguale(x.AccCode, a.Code))
                    .OrderBy(x => x.Icao, StringComparer.Ordinal)
                    .Select(x => new NewDocumentTarget(x.Id, x.Icao, $"{x.Icao} · {x.Name}", x.DocumentId is not null,
                        x.HasMilitaryPresence, x.IsMilitaryOnly, x.MilDocumentId is not null))
                    .ToList()));
        }

        // Gli esteri non si filtrano per permesso: non ci si crea niente sopra, fanno solo da controparte.
        var esteri = accs.Where(a => !Nazionale(a) && a.Sectors > 0)
            .OrderBy(a => a.Code, StringComparer.OrdinalIgnoreCase)
            .Select(a => new NewDocumentForeignAcc(a.Code, a.Name,
                sectors.Where(s => Uguale(s.AccCode, a.Code) && EArea(s))
                    .OrderBy(s => s.Callsign, StringComparer.Ordinal)
                    .Select(Bersaglio).ToList()))
            .Where(a => a.AreaSectors.Count > 0)
            .ToList();

        return new NewDocumentOptions(miei, esteri);
    }

    /// <summary>Il dato «ha già un documento» viene da chi lo possiede: il settore lo tiene in
    /// <c>DocumentId</c>, e non serve una seconda lettura dell'elenco documenti per dedurlo.</summary>
    private static NewDocumentTarget Bersaglio(GlobalSectorRow s) =>
        new(s.Id, s.Callsign, s.Callsign, s.DocumentId is not null);

    private static bool Nazionale(AccRow a) =>
        string.Equals(a.CountryPrefix, PrefissoNazionale, StringComparison.OrdinalIgnoreCase) && a.Sectors > 0;

    private static bool Uguale(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>Settore d'area = CTR o APP remotizzato: sono i candidati Home/Neighbour di una vLOA.</summary>
    private static bool EArea(GlobalSectorRow s) =>
        s.Type == SectorType.Ctr || (s.Type == SectorType.App && s.ApproachKind == ApproachKind.Remotized);

    /// <summary>APP non remotizzato (standalone): ha un documento proprio.</summary>
    private static bool EAppStandalone(GlobalSectorRow s) =>
        s.Type == SectorType.App && s.ApproachKind == ApproachKind.Standalone;
}
