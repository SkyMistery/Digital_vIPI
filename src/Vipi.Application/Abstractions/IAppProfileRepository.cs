using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Abstractions;

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

    /// <summary>Codice ACC del settore APP (per la guardia di autorizzazione). null = inesistente.</summary>
    Task<string?> GetAccCodeByAppAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Poligono AoR grezzo (JSON IVAO) dal catalogo AirportSector del callsign APP. null = assente.</summary>
    Task<string?> GetAorPolygonRawAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Poligoni grezzi (JSON) delle TWR dello stesso aeroporto dell'APP (visibili, con shape). Per l'overlay AoR.</summary>
    Task<IReadOnlyList<string>> GetTowerPolygonsRawAsync(string appCallsign, CancellationToken ct = default);

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

    Task SaveSeparationsAsync(string appCallsign, IReadOnlyList<AppSeparationRow> rows, CancellationToken ct = default);
    Task SaveVfrAsync(string appCallsign, string? vfrJson, CancellationToken ct = default);
    Task SaveSectionOrderAsync(string appCallsign, IReadOnlyList<string> order, CancellationToken ct = default);
    Task SaveHiddenSectionsAsync(string appCallsign, IReadOnlyList<string> hiddenKeys, CancellationToken ct = default);
    Task SaveFrequencyOrderAsync(string appCallsign, IReadOnlyList<AppFreqOrderOverride> overrides, CancellationToken ct = default);
    Task SaveFrequencyLinksAsync(string appCallsign, IReadOnlyList<int> sourceSectorIds, CancellationToken ct = default);
    Task SaveCustomSectionsAsync(string appCallsign, IReadOnlyList<AppCustomSection> sections, CancellationToken ct = default);
}
