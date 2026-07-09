namespace Vipi.Application.Content;

/// <summary>
/// Use-case di gestione dei settori ATC d'aeroporto importati dalla sorgente. L'import scarica le
/// postazioni ATC (porta neutra <see cref="Abstractions.IAirportDetailProvider"/>) — TUTTE, inclusi gli APP —
/// e fa upsert: il sito resta agnostico dalla sorgente e contiene SOLO ciò che la sorgente fornisce.
/// Letture libere (servono all'editor in sola lettura); scritture ACC-gated via
/// <see cref="Auth.IEditAuthorizationService"/>.
/// </summary>
public interface IAirportSectorService
{
    /// <summary>Settori ATC di un aeroporto (anche nascosti). Lettura libera.</summary>
    Task<IReadOnlyList<AirportSectorRow>> ListByAirportAsync(string icao, CancellationToken ct = default);

    /// <summary>Importa/aggiorna dalla sorgente i settori ATC dell'aeroporto (incl. APP). ACC-gated.</summary>
    Task<AirportSectorImportResult> ImportFromSourceAsync(string icao, CancellationToken ct = default);

    /// <summary>Mostra/nasconde un settore ATC d'aeroporto. ACC-gated.</summary>
    Task SetHiddenAsync(int id, bool hidden, CancellationToken ct = default);

    /// <summary>Imposta i limiti di quota di un settore ATC d'aeroporto. ACC-gated.</summary>
    Task SetLimitsAsync(int id, int? lower, int? upper, CancellationToken ct = default);

    /// <summary>Imposta il settore come frequenza principale dell'aeroporto (esclusiva). ACC-gated.</summary>
    Task SetPrimaryAsync(int id, CancellationToken ct = default);

    /// <summary>Segnala se una posizione APP è "di ACC" (remotizzata) o no (doc proprio). ACC-gated; riproietta. </summary>
    Task SetAccAppAsync(int id, bool isAccApp, CancellationToken ct = default);
}
