using Vipi.Application.Content;

namespace Vipi.Application.Abstractions;

/// <summary>Porta di accesso ai contenuti pubblicati (impl. EF in Infrastructure).</summary>
public interface IContentRepository
{
    /// <summary>
    /// Carica la vIPI pubblicata di un aeroporto (per ICAO, es. "LIRF"): documento con scope su una
    /// posizione aeroportuale (torre) di quell'aeroporto. Null se non esiste.
    /// </summary>
    /// <param name="ignoreRelease">true = ignora la release AIRAC effettiva e torna lo stato pubblicato/live (anteprima bozza).</param>
    /// <param name="preferWorking">true = usa la versione di lavorazione più recente (bozza inclusa), non la pubblicata (anteprima bozza).</param>
    Task<RawDocument?> LoadAirportVipiAsync(string icao, bool ignoreRelease = false, bool preferWorking = false, CancellationToken ct = default);

    /// <summary>
    /// Carica la vIPI di un APP non remotizzato (per callsign, es. "LIRP_APP"): documento con settore primario
    /// APP standalone. Null se non esiste. Migrazione storage APP→Document (doc refactor 08e f4-b).
    /// </summary>
    /// <param name="ignoreRelease">true = ignora la release AIRAC effettiva e torna lo stato pubblicato/live (anteprima bozza).</param>
    /// <param name="preferWorking">true = usa la versione di lavorazione più recente (bozza inclusa, anche se non pubblicato). Solo anteprima bozza gated.</param>
    /// <summary>
    /// La vSOP <b>militare</b> di questo scalo (carta vSOP militari §1b): si passa da
    /// <c>Airport.MilDocumentId</c>, il gemello del legame civile.
    /// <para>⚠️ Non è lo stesso documento con un filtro: le due edizioni hanno release, cicli AIRAC e
    /// contenuti indipendenti.</para>
    /// </summary>
    Task<RawDocument?> LoadAirportMilVipiAsync(string icao, bool ignoreRelease = false, bool preferWorking = false, CancellationToken ct = default);

    Task<RawDocument?> LoadAppVipiAsync(string appCallsign, bool ignoreRelease = false, bool preferWorking = false, CancellationToken ct = default);

    /// <summary>Carica una specifica vLOA pubblicata per id documento (viewer multi-vLOA per ACC). Null se non esiste.</summary>
    /// <param name="ignoreRelease">true = ignora la release AIRAC effettiva e torna lo stato pubblicato/live (anteprima bozza).</param>
    /// <param name="preferWorking">true = usa la versione di lavorazione più recente (bozza inclusa, anche se il doc non è pubblicato). Solo anteprima bozza gated.</param>
    Task<RawDocument?> LoadVloaByIdAsync(int docId, bool ignoreRelease = false, bool preferWorking = false, CancellationToken ct = default);
}
