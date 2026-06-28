using Vipi.Application.Content;

namespace Vipi.Application.Abstractions;

/// <summary>Porta di accesso ai contenuti pubblicati (impl. EF in Infrastructure).</summary>
public interface IContentRepository
{
    /// <summary>
    /// Carica la vIPI pubblicata di un ACC (per codice ACC, es. "LIRR") come struttura grezza
    /// (albero sezioni + blocchi non filtrati). Null se non esiste.
    /// </summary>
    Task<RawDocument?> LoadAccVipiAsync(string accCode, CancellationToken ct = default);

    /// <summary>
    /// Carica la vIPI pubblicata di un aeroporto (per ICAO, es. "LIRF"): documento con scope su una
    /// posizione aeroportuale (torre) di quell'aeroporto. Null se non esiste.
    /// </summary>
    Task<RawDocument?> LoadAirportVipiAsync(string icao, CancellationToken ct = default);

    /// <summary>
    /// Carica la vLOA pubblicata la cui parte Home appartiene alla ACC indicata (es. "LIRR"). Null se non esiste.
    /// </summary>
    Task<RawDocument?> LoadVloaAsync(string accCode, CancellationToken ct = default);

    /// <summary>Carica una specifica vLOA pubblicata per id documento (viewer multi-vLOA per ACC). Null se non esiste.</summary>
    Task<RawDocument?> LoadVloaByIdAsync(int docId, CancellationToken ct = default);
}
