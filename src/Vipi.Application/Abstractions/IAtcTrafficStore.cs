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
}
