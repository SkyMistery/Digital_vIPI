using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Abstractions;

/// <summary>
/// La porta di lettura e scrittura dell'<b>eliminazione</b>. Legge i <i>fatti</i> — chi cita cosa, chi
/// trattiene, quando la sorgente l'ha nominato l'ultima volta — e applica le <i>mosse</i> che le regole
/// hanno derivato da quei fatti (<see cref="DeletionRules"/>).
///
/// <para>⚠️ <b>La divisione non è un vezzo.</b> Se le regole vivessero nelle query, la finestra di conferma
/// e la transazione sarebbero due programmi diversi che si somigliano — ed è così che una promessa a schermo
/// e ciò che il database fa iniziano a divergere. Qui i fatti sono dati, le regole sono pure e testabili
/// senza database, e questa porta esegue soltanto ciò che le regole hanno già deciso.</para>
/// </summary>
public interface IDeletionRepository
{
    /// <summary>Tutto ciò che serve a decidere di un settore. <c>null</c> se non esiste.</summary>
    Task<SectorFacts?> SectorFactsAsync(int sectorId, CancellationToken ct = default);

    /// <summary>
    /// L'Id del settore <b>proiettato</b> che porta quel callsign, o <c>null</c> se non c'è.
    ///
    /// <para>⚠️ Serve perché l'albero della Struttura è fatto di righe di <b>catalogo</b>, non di settori: il
    /// numero che il nodo porta con sé è l'Id della riga di catalogo, e passarlo per un Id di settore
    /// eliminerebbe un settore a caso — o nessuno. Il callsign è l'unica chiave che i due mondi condividono.</para>
    /// </summary>
    Task<int?> SectorIdByCallsignAsync(string callsign, CancellationToken ct = default);

    /// <summary>Tutto ciò che serve a decidere di un aeroporto e dei suoi settori. <c>null</c> se non esiste.</summary>
    Task<AirportFacts?> AirportFactsAsync(int airportId, CancellationToken ct = default);

    /// <summary>Tutto ciò che serve a decidere di una ACC. <c>null</c> se non esiste.</summary>
    Task<AccFacts?> AccFactsAsync(string accCode, CancellationToken ct = default);

    /// <summary>Tutto ciò che serve a mostrare cosa si perde con un documento. <c>null</c> se non esiste.</summary>
    Task<DocumentFacts?> DocumentFactsAsync(int documentId, CancellationToken ct = default);

    /// <summary>
    /// <b>Tutti</b> i documenti in archivio, gestiti o no.
    ///
    /// <para>⚠️ Serve a trovare quelli che <b>nessuna pagina elenca</b>. Un documento entra negli elenchi solo
    /// se un descrittore lo riconosce — la vIPI ACC vuole un CTR <b>radice</b>, quella d'aeroporto un
    /// aeroporto, la vLOA le sue parti. Se l'aggancio cambia sotto (un import che sposta un settore sotto un
    /// padre, per dire) il documento resta in archivio e sparisce da ogni schermo: non si pubblica, non si
    /// elimina, e nessun rilievo lo nomina — perché anche i rilievi partono dall'elenco dei gestiti.</para>
    /// </summary>
    Task<IReadOnlyList<AffectedDoc>> AllDocumentsAsync(CancellationToken ct = default);

    /// <summary>
    /// Elimina un documento che <b>nessun descrittore riconosce</b>, con il suo audit. È la via di servizio
    /// per i documenti fuori elenco: quelli gestiti passano da <c>IDocumentAdminService</c>, che sa togliere
    /// anche le release — e un documento fuori elenco non ne ha, perché non ha una chiave sotto cui averle.
    /// </summary>
    Task DeleteUnmanagedDocumentAsync(int documentId, int actorUserId, CancellationToken ct = default);

    /// <summary>Tutto ciò che serve a decidere di un candidato confinante. <c>null</c> se non esiste.</summary>
    Task<NeighbourFacts?> NeighbourFactsAsync(int candidateId, CancellationToken ct = default);

    /// <summary>Tutto ciò che serve a decidere di un'area regolamentata. <c>null</c> se non esiste.</summary>
    Task<AreaFacts?> AreaFactsAsync(string ivaoId, CancellationToken ct = default);

    /// <summary>
    /// Quante pubblicazioni ha il bersaglio indicato. ⚠️ Le <c>DocRelease</c> non hanno FK verso il
    /// documento — si trovano per tipo e chiave — e quindi non compaiono in nessun cascade: se non le si
    /// conta a mano, la finestra tace proprio sulla cosa che il pubblico vedrebbe sparire.
    /// </summary>
    Task<int> ReleaseCountAsync(ReleaseTargetType tipo, string chiave, CancellationToken ct = default);

    /// <summary>
    /// Esegue le mosse in <b>una</b> transazione, nell'ordine che i vincoli impongono: i figli al nonno
    /// prima del <c>DELETE</c> (la FK sul padre è <c>Restrict</c>), i riferimenti sganciati prima delle
    /// righe che li portano, l'audit <b>prima</b> della cancellazione — dopo, il nome non è più leggibile e
    /// resterebbe un registro che dice «eliminato il settore 7».
    /// </summary>
    /// <param name="provaSorgente">
    /// Le tracce della domanda puntuale che ha autorizzato l'atto, quando è stata quella e non l'attesa dei
    /// due giri: finisce nel dettaglio dell'audit. <c>null</c> = eliminazione ordinaria, e il registro non ne
    /// parla. Senza, il registro mostrerebbe una cancellazione che le protezioni vietavano, e nessun modo di
    /// sapere perché è passata.
    /// </param>
    Task ApplyAsync(DeletionActions azioni, int actorUserId, string? provaSorgente = null,
        CancellationToken ct = default);
}
