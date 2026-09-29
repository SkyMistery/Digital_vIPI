using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Identità della vIPI APP: l'<b>ente</b> che la possiede (S49, 29 settembre 2026), non più un settore.
/// </summary>
/// <param name="Code">Il codice dell'ente: chiave di pubblicazione e indirizzo pubblico (<c>?app=</c>). Per un APP
/// che non ha ancora un ente, il suo nominativo — sarà il codice dell'ente che nasce col documento.</param>
/// <param name="Seme">Da dove parte la derivazione: la posizione principale dell'ente (<c>LIRE_TWR</c>).</param>
/// <param name="UnitId">null finché l'ente non esiste (nasce col documento, alla prima apertura dell'editor).</param>
public sealed record AppDocumentIdentity(string Code, string Seme, string Title, string AccCode, int? DocumentId,
    int? UnitId = null);

/// <summary>
/// Sorgente dati per la DERIVAZIONE delle sezioni live dell'APP standalone su Document (doc 08e): catalogo frequenze
/// del sottoalbero, poligoni AoR, mappe callsign→tipo/nome/codice per i coordinamenti, risoluzione link frequenza.
/// NON persiste editoriale (quello vive nel Document + <c>DocumentProfile</c>): sola lettura dai cataloghi/settori.
/// </summary>
public interface IAppDerivationRepository
{
    /// <summary>Codice ACC del settore APP (per la guardia di autorizzazione). null = inesistente.</summary>
    Task<string?> GetAccCodeByAppAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>L'ente della vIPI APP, per codice o per una sua posizione; oppure un APP non remotizzato che un ente
    /// ancora non ce l'ha. null = né l'uno né l'altro, o un ente il cui contenuto vive nella vIPI dell'ACC.</summary>
    Task<AppDocumentIdentity?> ResolveForDocumentAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Crea l'ente (se non c'è) e la sua vIPI APP (se non c'è); ritorna l'id del documento. Idempotente.</summary>
    Task<int> EnsureDocumentAsync(AppDocumentIdentity identity, int authorUserId, CancellationToken ct = default);

    /// <summary>Poligono AoR grezzo (JSON IVAO) dal catalogo AirportSector del callsign APP. null = assente.</summary>
    Task<string?> GetAorPolygonRawAsync(string appCallsign, CancellationToken ct = default);

    /// <summary>Poligoni grezzi mappati per callsign (CTR + APP), per le shape AoR extra scelte a mano. Case-insensitive.</summary>
    Task<IReadOnlyDictionary<string, string>> GetSectorPolygonsRawByCallsignAsync(IReadOnlyList<string> callsigns, CancellationToken ct = default);

    /// <summary>Limiti di quota (Lower/Upper grezzi) mappati per callsign (CTR + APP/TWR), per l'estrusione 3D dell'AoR.
    /// Case-insensitive; assenti = non nel dizionario.</summary>
    Task<IReadOnlyDictionary<string, SectorFlLimits>> GetSectorLimitsByCallsignAsync(IReadOnlyList<string> callsigns, CancellationToken ct = default);

    /// <summary>Tutti i settori DB con poligono AoR (CTR + APP/torri), selezionabili come shape extra. Callsign + nome + ACC.</summary>
    Task<IReadOnlyList<SectorShapePick>> ListSelectableSectorShapesAsync(CancellationToken ct = default);

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
