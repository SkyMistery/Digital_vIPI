using Vipi.Application.Content;

namespace Vipi.Application.Abstractions;

/// <summary>Persistenza dei settori orfani (proiettati e non più confermati dai cataloghi). Impl. EF.</summary>
public interface IOrphanSectorRepository
{
    /// <summary>Gli orfani, con i documenti che li raccontano e chi ne impedisce la rimozione. Con
    /// <paramref name="sogliaTimbro"/> valorizzata entrano anche i settori <b>attivi</b> la cui riga di
    /// catalogo la sorgente non manda più (le rinomine).</summary>
    Task<IReadOnlyList<OrphanSectorRow>> ListOrphansAsync(string? accCode, DateTime? sogliaTimbro, CancellationToken ct = default);

    /// <summary>Un singolo orfano (per i controlli prima di rimuoverlo). null se non è orfano né stantìo.</summary>
    Task<OrphanSectorRow?> GetOrphanAsync(int sectorId, DateTime? sogliaTimbro, CancellationToken ct = default);

    /// <summary>Settori attivi dello stesso ACC a cui si può riappendere il documento dell'orfano.</summary>
    Task<IReadOnlyList<ReattachTargetRow>> ReattachTargetsAsync(int orphanSectorId, CancellationToken ct = default);

    /// <summary>Sposta documento e ruolo di primario dall'orfano al bersaglio.</summary>
    Task ReattachAsync(int orphanSectorId, int targetSectorId, CancellationToken ct = default);

    /// <summary>
    /// Righe di catalogo che la sorgente non manda più: timbro d'import più vecchio di
    /// <paramref name="sogliaUtc"/>, escluse quelle <b>aggiunte a mano</b> (che la sorgente non ha mai
    /// mandato) e quelle nascoste (hanno già un loro segnale).
    /// </summary>
    Task<IReadOnlyList<StaleCatalogRow>> ListStaleCatalogRowsAsync(DateTime sogliaUtc, CancellationToken ct = default);

    /// <summary>Quante righe di catalogo ci sono in tutto (escluse le manuali e le nascoste): è il
    /// denominatore della guardia di massa.</summary>
    Task<int> CountCatalogRowsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gli <b>aeroporti</b> che la sorgente non nomina da prima della soglia: il gemello di
    /// <see cref="ListStaleCatalogRowsAsync"/> per gli scali. Fino al 26 agosto 2026 non era una domanda che
    /// si potesse porre — <c>Airport</c> non aveva nessun timbro d'import.
    /// <para>Chi non è mai stato timbrato resta fuori: è il caso degli aeroporti più vecchi del timbro
    /// stesso, e finché la sorgente non passa almeno una volta «non lo sappiamo» non è «è sparito».</para>
    /// </summary>
    Task<IReadOnlyList<StaleAirportRow>> ListStaleAirportsAsync(DateTime sogliaUtc, CancellationToken ct = default);

    /// <summary>
    /// Il possibile <b>sostituto</b> di un callsign non più elencato: stessa posizione, stesso perimetro
    /// (aeroporto o ACC), timbro recente. null se non ce n'è uno solo — con zero o due candidati la
    /// proposta sarebbe una scommessa, e questa è una domanda a cui deve rispondere una persona.
    /// </summary>
    Task<string?> FindRenameCandidateAsync(StaleCatalogRow stantia, DateTime sogliaUtc, CancellationToken ct = default);

    /// <summary>Codice ACC del settore, per l'autorizzazione. null se non risolvibile.</summary>
    Task<string?> GetAccCodeAsync(int sectorId, CancellationToken ct = default);
}
