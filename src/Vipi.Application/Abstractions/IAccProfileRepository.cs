using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Abstractions;

/// <summary>Identità del Document della vIPI ACC (doc refactor 08e-acc): il settore CTR radice primario che lo chiavizza
/// (<see cref="SectorId"/>/<see cref="RootCallsign"/>), il codice/nome ACC e l'eventuale <see cref="DocumentId"/> se già
/// migrato. Analogo ACC di <see cref="AppDocumentIdentity"/>.</summary>
public sealed record AccDocumentIdentity(int SectorId, string RootCallsign, string AccCode, string AccName, int? DocumentId);

/// <summary>
/// Persistenza della vIPI ACC (documento a blocchi, 1:1 con l'Acc) + primitive di derivazione live
/// (poligoni AoR, frequenze dei membri, mappa tipi settore). Mirror in chiave ACC di <see cref="IAppProfileRepository"/>.
/// </summary>
public interface IAccProfileRepository
{
    /// <summary>Nome dell'ACC dal codice; null se l'ACC non esiste.</summary>
    Task<string?> GetAccNameByCodeAsync(string accCode, CancellationToken ct = default);

    /// <summary>Risolve l'identità del Document vIPI ACC: settore CTR radice primario (che lo chiavizza) + eventuale
    /// DocumentId. Null se l'ACC non esiste o non ha radici CTR. Doc refactor 08e-acc.</summary>
    Task<AccDocumentIdentity?> ResolveAccDocumentIdentityAsync(string accCode, CancellationToken ct = default);

    /// <summary>Radici degli alberi CTR dell'ACC (settori CTR senza genitore, attivi). Una vIPI per radice.</summary>
    Task<IReadOnlyList<AccTreeRoot>> ListTreeRootsAsync(string accCode, CancellationToken ct = default);

    /// <summary>Callsign dei CTR del sottoalbero della radice indicata (radice inclusa). Vuoto se radice inesistente.</summary>
    Task<IReadOnlyList<string>> ListSubtreeCtrCallsignsAsync(string accCode, string rootCallsign, CancellationToken ct = default);

    /// <summary>Mappa CTR del sottoalbero → ramo di appartenenza (nome + ordine): radice o suo figlio diretto. Per l'accorpamento freq (#5).</summary>
    Task<IReadOnlyDictionary<string, (string Name, int Order)>> GetCtrBranchMapAsync(string accCode, string rootCallsign, CancellationToken ct = default);

    /// <summary>Settori CTR (di aerovia) dell'ACC: pool del blocco Aerovia. (Callsign, Name).</summary>
    Task<IReadOnlyList<AccSectorPick>> ListCtrSectorsAsync(string accCode, CancellationToken ct = default);

    /// <summary>Settori APP dell'ACC: candidati per i gruppi APP. (Callsign, Name).</summary>
    Task<IReadOnlyList<AccSectorPick>> ListAppSectorsAsync(string accCode, CancellationToken ct = default);

    /// <summary>Poligoni grezzi (JSON) dei settori indicati: cerca sia nel catalogo CTR (AccSector) sia APP (AirportSector).</summary>
    Task<IReadOnlyList<string>> GetAorPolygonsRawAsync(IReadOnlyList<string> callsigns, CancellationToken ct = default);

    /// <summary>Poligoni grezzi mappati per callsign (per anelli AoR toggleabili singolarmente). Chiave case-insensitive.</summary>
    Task<IReadOnlyDictionary<string, string>> GetSectorPolygonsRawByCallsignAsync(IReadOnlyList<string> callsigns, CancellationToken ct = default);

    /// <summary>Poligoni grezzi delle TWR degli aeroporti dei settori APP indicati (overlay torri sull'AoR APP).</summary>
    Task<IReadOnlyList<string>> GetTowerPolygonsRawForAppsAsync(IReadOnlyList<string> appCallsigns, CancellationToken ct = default);

    /// <summary>Frequenze derivate per un insieme di settori membri (+ link extra), ordinate. Espande gli APP col catalogo aeroporto.</summary>
    Task<IReadOnlyList<AppFreqRow>> DeriveFrequenciesForMembersAsync(
        IReadOnlyList<string> memberCallsigns, IReadOnlyList<string> linkCallsigns, CancellationToken ct = default);

    /// <summary>Mappa callsign → tipo settore (per classificare i Next dei trasferimenti). Riuso del pattern APP.</summary>
    Task<IReadOnlyDictionary<string, SectorType>> GetSectorTypeMapAsync(CancellationToken ct = default);

    /// <summary>Mappa callsign → codice settore (MiddleIdentifier, es. «WS2»/«ES»). Per la frase di coordinamento.</summary>
    Task<IReadOnlyDictionary<string, string>> GetSectorCodeMapAsync(CancellationToken ct = default);

    /// <summary>Mappa ICAO → nome aeroporto. Per la frase di coordinamento.</summary>
    Task<IReadOnlyDictionary<string, string>> GetAirportNameMapAsync(CancellationToken ct = default);

    /// <summary>Mappa callsign → nome IVAO del settore (AtcCallsign, es. «Roma Radar»), senza il codice. Per il mittente.</summary>
    Task<IReadOnlyDictionary<string, string>> GetSectorAtcNameMapAsync(CancellationToken ct = default);

    /// <summary>Mappa callsign → nome dell'ACC di appartenenza del settore. Per raggruppare gli avvicinamenti per ACC.</summary>
    Task<IReadOnlyDictionary<string, string>> GetSectorAccNameMapAsync(CancellationToken ct = default);

    /// <summary>Tutte le frequenze linkabili (per il picker dei link extra).</summary>
    Task<IReadOnlyList<LinkableFrequencyRow>> ListLinkableFrequenciesAsync(CancellationToken ct = default);

    /// <summary>Aree speciali dell'ACC (picker editor), ordinate per nome.</summary>
    Task<IReadOnlyList<SpecialAreaPick>> ListSpecialAreasByAccAsync(string accCode, CancellationToken ct = default);

    /// <summary>Aree speciali per IvaoId (per il viewer), con shape grezza.</summary>
    Task<IReadOnlyList<SpecialAreaDetail>> GetSpecialAreasByIdsAsync(IReadOnlyList<string> ivaoIds, CancellationToken ct = default);
}
