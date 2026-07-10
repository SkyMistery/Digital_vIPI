using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Abstractions;

/// <summary>Identità di un APP per la migrazione su Document: id settore, titolo del documento, DocumentId se già creato.</summary>
public sealed record AppDocumentIdentity(int SectorId, string Callsign, string Title, string AccCode, int? DocumentId);

/// <summary>
/// Persistenza del profilo editoriale dell'APP standalone (separazioni, VFR, ordine sezioni, ordine/link frequenze),
/// ancorato 1:1 al Sector APP. Le sezioni derivate (frequenze sottoalbero, coordinamenti, AoR) non si persistono:
/// qui ci sono solo i dati grezzi per ricalcolarle (catalogo frequenze, mappa tipi, poligono).
/// Le scritture per-area sostituiscono l'intero valore (l'editor invia tutto). Get-or-create del profilo implicito.
/// </summary>
public interface IAppProfileRepository
{
    /// <summary>Carica il profilo (editoriale + link risolti). null = callsign APP inesistente.</summary>
    Task<AppProfileData?> LoadAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Vero se la vIPI APP standalone del callsign è nascosta dal pubblico.</summary>
    Task<bool> IsHiddenAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Costruisce l'<see cref="AppProfileData"/> dai blob CONGELATI di una release (i link-frequenza sono
    /// ri-risolti per callsign col catalogo corrente). Per la vista pubblica quando esiste una release effettiva.</summary>
    Task<AppProfileData?> BuildFromSnapshotAsync(string appCallsign, AppReleaseSnapshot snap, CancellationToken ct = default);

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

    /// <summary>Override per-documento del template frase (null = default globale). Lettura leggera per la derivazione.</summary>
    Task<string?> GetCoordinationTemplateAsync(string appCallsign, CancellationToken ct = default);

    Task SaveCoordinationTemplateAsync(string appCallsign, string? template, CancellationToken ct = default);

    Task SaveSeparationsAsync(string appCallsign, IReadOnlyList<AppSeparationRow> rows, CancellationToken ct = default);
    Task SaveVfrAsync(string appCallsign, string? vfrJson, CancellationToken ct = default);
    Task SaveSectionOrderAsync(string appCallsign, IReadOnlyList<string> order, CancellationToken ct = default);
    Task SaveHiddenSectionsAsync(string appCallsign, IReadOnlyList<string> hiddenKeys, CancellationToken ct = default);
    Task SaveFrequencyOrderAsync(string appCallsign, IReadOnlyList<AppFreqOrderOverride> overrides, CancellationToken ct = default);
    Task SaveFrequencyLinksAsync(string appCallsign, IReadOnlyList<int> sourceSectorIds, CancellationToken ct = default);
    Task SaveCustomSectionsAsync(string appCallsign, IReadOnlyList<AppCustomSection> sections, CancellationToken ct = default);
}
