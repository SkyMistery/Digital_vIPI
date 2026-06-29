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
}
