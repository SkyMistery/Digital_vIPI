using Vipi.Application.Content;

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

    /// <summary>Tutto ciò che serve a decidere di un aeroporto e dei suoi settori. <c>null</c> se non esiste.</summary>
    Task<AirportFacts?> AirportFactsAsync(int airportId, CancellationToken ct = default);

    /// <summary>Tutto ciò che serve a decidere di una ACC. <c>null</c> se non esiste.</summary>
    Task<AccFacts?> AccFactsAsync(string accCode, CancellationToken ct = default);

    /// <summary>Tutto ciò che serve a mostrare cosa si perde con un documento. <c>null</c> se non esiste.</summary>
    Task<DocumentFacts?> DocumentFactsAsync(int documentId, CancellationToken ct = default);

    /// <summary>
    /// Esegue le mosse in <b>una</b> transazione, nell'ordine che i vincoli impongono: i figli al nonno
    /// prima del <c>DELETE</c> (la FK sul padre è <c>Restrict</c>), i riferimenti sganciati prima delle
    /// righe che li portano, l'audit <b>prima</b> della cancellazione — dopo, il nome non è più leggibile e
    /// resterebbe un registro che dice «eliminato il settore 7».
    /// </summary>
    Task ApplyAsync(DeletionActions azioni, int actorUserId, CancellationToken ct = default);
}
