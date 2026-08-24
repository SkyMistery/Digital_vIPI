using Vipi.Application.Stats;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Archivio delle sessioni ATC per le statistiche. Porta sottile di proposito: la logica di cosa scrivere sta
/// in <see cref="AtcSessionSync"/>, che è pura e testabile; qui c'è solo l'I/O.
/// </summary>
public interface IAtcSessionStore
{
    /// <summary>
    /// Le sessioni che servono a decidere il prossimo giro: quelle <b>aperte</b> (per chiuderle se sono
    /// sparite) e quelle <b>finite da poco</b> (per riconoscere il turno di chi si riconnette).
    /// Non tutto l'archivio: è una lettura che gira ogni minuto.
    /// </summary>
    Task<IReadOnlyList<KnownAtcSession>> GetOpenOrRecentAsync(DateTimeOffset since, CancellationToken ct = default);

    /// <summary>Applica il piano: crea/aggiorna le sessioni viste e chiude quelle sparite. Ritorna quante righe ha toccato.</summary>
    Task<int> ApplyAsync(AtcSessionPlan plan, CancellationToken ct = default);
}
