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
    Task ApplyAsync(DeletionActions azioni, int actorUserId, CancellationToken ct = default);
}
