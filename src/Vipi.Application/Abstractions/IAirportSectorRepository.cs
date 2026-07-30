using Vipi.Application.Content;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Porta di persistenza per il catalogo settori ATC d'aeroporto importati dalla sorgente
/// (DEL/GND/TWR/APP…). Come per gli ACC, l'import fa upsert (mai creazione manuale) preservando
/// IsHidden e i limiti admin; l'ACC di competenza è ereditato dall'aeroporto. Impl. EF in Infrastructure.
/// </summary>
public interface IAirportSectorRepository
{
    /// <summary>
    /// Upsert dei settori ATC di un aeroporto dalla sorgente. Risolve l'ACC dall'aeroporto e lo scrive
    /// su ogni settore; preserva IsHidden e i limiti admin (default inf=0/GND, sup=19500 sui nuovi).
    /// Ritorna (creati, aggiornati). Se l'aeroporto non esiste, ritorna (0,0).
    /// </summary>
    Task<(int Created, int Updated)> ImportForAirportAsync(string icao, IReadOnlyList<SourceAtcPosition> positions, CancellationToken ct = default);

    /// <summary>Settori ATC di un aeroporto (anche nascosti).</summary>
    Task<IReadOnlyList<AirportSectorRow>> ListByAirportAsync(string icao, CancellationToken ct = default);

    /// <summary>Mostra/nasconde un settore ATC d'aeroporto.</summary>
    Task SetHiddenAsync(int id, bool hidden, CancellationToken ct = default);

    /// <summary>Imposta i limiti di quota (inferiore/superiore) di un settore ATC d'aeroporto (admin).</summary>
    Task SetLimitsAsync(int id, int? lower, int? upper, CancellationToken ct = default);

    /// <summary>Imposta il settore come frequenza principale dell'aeroporto (azzera gli altri dello stesso aeroporto).</summary>
    Task SetPrimaryAsync(int id, CancellationToken ct = default);

    /// <summary>Segnala se una posizione APP è "di ACC" (remotizzata) o no (doc proprio).</summary>
    Task SetIsAccAppAsync(int id, bool isAccApp, CancellationToken ct = default);

    /// <summary>Codice ACC di competenza di un aeroporto (per la guardia di autorizzazione); null se inesistente.</summary>
    Task<string?> GetAccCodeByIcaoAsync(string icao, CancellationToken ct = default);

    /// <summary>Codice ACC del settore d'aeroporto indicato (per la guardia di autorizzazione); null se inesistente.</summary>
    Task<string?> GetAccCodeBySectorIdAsync(int id, CancellationToken ct = default);

    /// <summary>ICAO di tutti gli aeroporti nel DB (per l'import automatico).</summary>
    Task<IReadOnlyList<string>> ListAirportIcaosAsync(CancellationToken ct = default);

    // --- Fallback shape tonda per le TWR senza poligono (Round 22) ---

    /// <summary>Tutti i settori TWR visibili con ICAO, coordinate aeroporto (se note) e poligono grezzo attuale.
    /// Il chiamante decide quali sono "vuoti/degeneri" (es. null, "[]") provando a proiettarli.</summary>
    Task<IReadOnlyList<TwrShapeRow>> ListTwrShapesAsync(CancellationToken ct = default);

    /// <summary>Scrive una shape SINTETICA (IsShapeSynthetic=true) su un settore. Mai chiamare su shape reali.</summary>
    Task SetSyntheticShapeAsync(int sectorId, string polygonJson, CancellationToken ct = default);

    /// <summary>Scrive una shape REALE (IsShapeSynthetic=false) su un settore, dalla sorgente GitHub (twrs.tfl).
    /// È un poligono vero (non un cerchio), quindi il fallback tondo non deve poi rimpiazzarlo.</summary>
    Task SetRealShapeAsync(int sectorId, string polygonJson, CancellationToken ct = default);

    /// <summary>Poligoni grezzi NON sintetici di tutti i settori d'aeroporto (per ICAO), per derivare un centro di
    /// ripiego dal poligono di un settore fratello (es. APP) quando le coordinate aeroporto non sono note.</summary>
    Task<IReadOnlyList<AirportPolygonRow>> ListNonSyntheticPolygonsAsync(CancellationToken ct = default);
}

/// <summary>Riga di lavoro per il fallback shape TWR: settore (+ callsign per il match GitHub) + coord aeroporto
/// (null = ignote) + poligono grezzo attuale + se la shape attuale è sintetica (cerchio di ripiego).</summary>
public sealed record TwrShapeRow(int SectorId, string ComposePosition, string AirportIcao, double? Latitude, double? Longitude, string? RawPolygon, bool IsShapeSynthetic);

/// <summary>Poligono grezzo di un settore d'aeroporto (per derivare un centro di ripiego).</summary>
public sealed record AirportPolygonRow(string AirportIcao, string RawPolygon);
