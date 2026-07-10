using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Abstractions;

/// <summary>Identità di un APP per la migrazione su Document: id settore, titolo del documento, DocumentId se già creato.</summary>
public sealed record AppDocumentIdentity(int SectorId, string Callsign, string Title, string AccCode, int? DocumentId);

/// <summary>
/// Sorgente dati per la DERIVAZIONE delle sezioni live dell'APP standalone su Document (doc 08e): catalogo frequenze
/// del sottoalbero, poligoni AoR, mappe callsign→tipo/nome/codice per i coordinamenti, risoluzione link frequenza.
/// NON persiste editoriale (quello vive nel Document + <c>DocumentProfile</c>): sola lettura dai cataloghi/settori.
/// </summary>
public interface IAppProfileRepository
{
    /// <summary>Codice ACC del settore APP (per la guardia di autorizzazione). null = inesistente.</summary>
    Task<string?> GetAccCodeByAppAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Identità del settore APP per creare/risolvere il suo Document vIPI: id settore + titolo (nome IVAO/
    /// AtcCallsign, fallback al nome settore) + DocumentId se già migrato. null = callsign APP inesistente. Doc 08e.</summary>
    Task<AppDocumentIdentity?> ResolveForDocumentAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Poligono AoR grezzo (JSON IVAO) dal catalogo AirportSector del callsign APP. null = assente.</summary>
    Task<string?> GetAorPolygonRawAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Poligoni grezzi (JSON) delle TWR dello stesso aeroporto dell'APP (visibili, con shape). Per l'overlay AoR.</summary>
    Task<IReadOnlyList<string>> GetTowerPolygonsRawAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Come <see cref="GetTowerPolygonsRawAsync"/> ma con il callsign della torre (per le chip on/off della mappa AoR).</summary>
    Task<IReadOnlyList<(string Callsign, string Poly)>> GetTowerPolygonsWithCallsignRawAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Tutti i settori con frequenza (per il picker di link), con ICAO/callsign.</summary>
    Task<IReadOnlyList<LinkableFrequencyRow>> ListLinkableFrequenciesAsync(CancellationToken ct = default);

    /// <summary>Risolve gli id dei settori-sorgente dei link frequenza (da <c>DocumentProfile</c>) in righe frequenza
    /// (IsLink=true), preservando l'ordine degli id. Salta gli id senza frequenza/inesistenti. Doc 08e.</summary>
    Task<IReadOnlyList<AppFreqRow>> ResolveFreqLinksAsync(IReadOnlyList<int> sourceSectorIds, CancellationToken ct = default);

    /// <summary>
    /// Catalogo frequenze: posizioni (ATIS·DEL·GND·TWR·APP) degli aeroporti del sottoalbero (APP del callsign = ★),
    /// seguite dai GENITORI di copertura (<paramref name="ancestorCallsigns"/>, in ordine di vicinanza) coi loro CTR.
    /// </summary>
    Task<IReadOnlyList<AppFreqRow>> DeriveCatalogFrequenciesAsync(
        string appCallsign, IReadOnlySet<string> domainCallsigns,
        IReadOnlyList<string> ancestorCallsigns, CancellationToken ct = default);

    /// <summary>Mappa callsign→tipo di tutti i settori (per classificare i Next dei coordinamenti: ACC vs torre).</summary>
    Task<IReadOnlyDictionary<string, SectorType>> GetSectorTypeMapAsync(CancellationToken ct = default);

    /// <summary>Mappa callsign → codice settore (MiddleIdentifier, es. «WS2»/«ES»). Per la frase di coordinamento.</summary>
    Task<IReadOnlyDictionary<string, string>> GetSectorCodeMapAsync(CancellationToken ct = default);

    /// <summary>Mappa ICAO → nome aeroporto. Per la frase di coordinamento.</summary>
    Task<IReadOnlyDictionary<string, string>> GetAirportNameMapAsync(CancellationToken ct = default);

    /// <summary>Nome display del settore per callsign (Sector.Name), per il mittente della frase. Case-insensitive.</summary>
    Task<IReadOnlyDictionary<string, string>> GetSectorNameMapAsync(CancellationToken ct = default);

    /// <summary>Mappa callsign → nome IVAO del settore (AtcCallsign, es. «Roma Radar»), senza il codice. Per il mittente.</summary>
    Task<IReadOnlyDictionary<string, string>> GetSectorAtcNameMapAsync(CancellationToken ct = default);
}
