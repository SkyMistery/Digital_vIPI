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

    /// <summary>
    /// Scrive le sessioni arrivate dallo <b>storico</b>. Ritorna (create, aggiornate).
    ///
    /// <para>⚠️ Sulle righe che il poller ha già scritto, lo storico è verità <b>solo per la coda</b>: fine e
    /// durata definitive. Non tocca i campi che il poller conosce meglio (posizione e frequenza, che nella
    /// lista dello storico non ci sono affatto) e non declassa a <c>Backfill</c> una riga vista dal vivo.</para>
    /// </summary>
    Task<(int Created, int Updated)> UpsertHistoryAsync(
        IReadOnlyList<SourceAtcSessionHistory> sessions, CancellationToken ct = default);

    /// <summary>
    /// Ricalcola i turni nella finestra data e scrive quelli cambiati; ritorna quante righe ha corretto.
    ///
    /// <para>Serve dopo il backfill: le sessioni storiche arrivano alla rinfusa, e il turno si riconosce solo
    /// guardando la sequenza completa di un controllore su una postazione. Usa lo stesso raggruppatore puro
    /// del poller, così i due percorsi non possono dare risposte diverse.</para>
    /// </summary>
    Task<int> RecomputeShiftsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}
