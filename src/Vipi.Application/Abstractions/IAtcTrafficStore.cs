using Vipi.Application.Stats;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Archivio delle tratte gestite. Come per le sessioni: qui c'è solo l'I/O, le decisioni stanno in
/// <see cref="TrafficLedger"/>.
/// </summary>
public interface IAtcTrafficStore
{
    /// <summary>
    /// Tratte già scritte per queste sessioni, più i minuti «occupato» già contati. Serve a rimettere in
    /// memoria lo stato dopo un riavvio: senza, una sessione ancora in corso ripartirebbe da zero.
    /// </summary>
    Task<IReadOnlyDictionary<long, (IReadOnlyList<TrafficLegRow> Legs, int TrafficMinutes)>> GetLegsAsync(
        IReadOnlyCollection<long> sessionIds, CancellationToken ct = default);

    /// <summary>Scrive tratte e contatori (upsert). Ritorna quante righe di tratta ha toccato.</summary>
    Task<int> SaveAsync(TrafficFlush flush, CancellationToken ct = default);

    /// <summary>
    /// Sessioni d'aeroporto <b>chiuse</b> e senza traffico che non sono ancora state riempite a posteriori,
    /// dalla più recente, al massimo <paramref name="max"/>. Insieme a loro le sessioni dello stesso
    /// aeroporto che si sovrappongono nel tempo, che servono a decidere di chi è il movimento.
    /// </summary>
    Task<(IReadOnlyList<AirportSessionWindow> ToFill, IReadOnlyList<AirportSessionWindow> Concurrent)>
        GetAirportSessionsToFillAsync(DateTimeOffset notBefore, int max, CancellationToken ct = default);

    /// <summary>
    /// Scrive i movimenti ricostruiti di una sessione e la marca come riempita (anche quando i movimenti
    /// sono zero: «provato, non c'era nessuno» è un fatto, e senza marca si riproverebbe per sempre).
    /// </summary>
    Task<int> FillAirportMovementsAsync(
        long sessionId, IReadOnlyList<SourceAirportMovement> movements, DateTimeOffset filledAtUtc,
        CancellationToken ct = default);

    /// <summary>
    /// Cancella il <b>dettaglio</b> delle tratte più vecchio di <paramref name="notAfter"/>, a scaglioni di
    /// <paramref name="batch"/> righe. Ritorna quante ne ha tolte.
    ///
    /// <para>⚠️ Tocca <b>solo</b> <c>AtcSessionTraffic</c>: le sessioni restano per sempre, e con loro i
    /// contatori denormalizzati che le riassumono. Quei contatori esistono esattamente perché la potatura
    /// non azzeri le ore di un anno fa — cancellare anche le sessioni farebbe sparire le statistiche
    /// insieme al dettaglio.</para>
    ///
    /// <para>⚠️ A scaglioni e non in un colpo: una <c>DELETE</c> da mezzo milione di righe tiene il lock
    /// sulla tabella per il tempo che ci mette, e questa gira mentre l'applicazione serve pagine.</para>
    /// </summary>
    Task<int> PruneTrafficAsync(DateTimeOffset notAfter, int batch, CancellationToken ct = default);

    /// <summary>
    /// Riassume e poi <b>toglie</b> uno scaglione di sessioni più vecchie della soglia: le righe confluiscono
    /// nel riassunto mensile (<c>AtcMonthRollup</c>) e poi spariscono. Ritorna quante ne ha tolte.
    ///
    /// <para>⚠️ Riassumere e cancellare devono stare nella <b>stessa transazione</b>. Separate, un'interruzione
    /// fra le due lascerebbe il riassunto già incrementato e le sessioni ancora lì: il giro dopo le
    /// conterebbe una seconda volta, e le ore di un mese diventerebbero il doppio senza che nulla lo dica.</para>
    /// </summary>
    Task<int> RollupAndPruneSessionsAsync(DateTimeOffset notAfter, int batch, CancellationToken ct = default);
}
