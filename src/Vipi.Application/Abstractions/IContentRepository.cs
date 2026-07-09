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
    /// <param name="ignoreRelease">true = ignora la release AIRAC effettiva e torna lo stato pubblicato/live (anteprima bozza).</param>
    Task<RawDocument?> LoadAirportVipiAsync(string icao, bool ignoreRelease = false, CancellationToken ct = default);

    /// <summary>
    /// Carica la vLOA pubblicata la cui parte Home appartiene alla ACC indicata (es. "LIRR"). Null se non esiste.
    /// </summary>
    Task<RawDocument?> LoadVloaAsync(string accCode, CancellationToken ct = default);

    /// <summary>Carica una specifica vLOA pubblicata per id documento (viewer multi-vLOA per ACC). Null se non esiste.</summary>
    /// <param name="ignoreRelease">true = ignora la release AIRAC effettiva e torna lo stato pubblicato/live (anteprima bozza).</param>
    /// <param name="preferWorking">true = usa la versione di lavorazione più recente (bozza inclusa, anche se il doc non è pubblicato). Solo anteprima bozza gated.</param>
    Task<RawDocument?> LoadVloaByIdAsync(int docId, bool ignoreRelease = false, bool preferWorking = false, CancellationToken ct = default);

    /// <summary>Carica la vLOA pubblicata della coppia (Home=<paramref name="homeAccCode"/>, Neighbour=<paramref name="foreignAccCode"/>).
    /// Una sola vLOA per coppia ACC↔ACC. Null se non esiste.</summary>
    /// <param name="ignoreRelease">true = ignora la release AIRAC effettiva e torna lo stato pubblicato/live (anteprima bozza).</param>
    /// <param name="preferWorking">true = usa la versione di lavorazione più recente (bozza inclusa). Solo anteprima bozza gated.</param>
    Task<RawDocument?> LoadVloaByPairAsync(string homeAccCode, string foreignAccCode, bool ignoreRelease = false, bool preferWorking = false, CancellationToken ct = default);
}
