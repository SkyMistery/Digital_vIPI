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
}
