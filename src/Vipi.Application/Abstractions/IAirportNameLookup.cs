namespace Vipi.Application.Abstractions;

/// <summary>Uno scalo trovato per codice: il nome, e se lo conosce il nostro archivio o la sola sorgente.</summary>
/// <param name="InArchivio">
/// Vero se lo scalo è fra i nostri. ⚠️ Serve a chi <b>scrive</b>: un alternato che non è in archivio si
/// porta dietro il nome nel documento, perché quel nome nessuno glielo ridarà al prossimo caricamento.
/// </param>
public sealed record AirportName(string Icao, string Name, bool InArchivio);

/// <summary>
/// Trova il nome di un aeroporto per codice ICAO, <b>prima in archivio e poi alla sorgente</b>.
///
/// <para>
/// Esiste per gli <b>aeroporti alternati</b> di un vSOP militare (carta §12f), che sono spesso <b>esteri</b>
/// — LGKR, LDDU — e quindi fuori dal nostro archivio, che tiene i soli scali italiani.
/// </para>
/// <para>
/// ⚠️ <b>Due metodi e non uno</b>, ed è la differenza fra leggere e scrivere. <see cref="NamesAsync"/> guarda
/// <b>solo l'archivio</b> e la usa chi mostra un documento: una pagina pubblica non deve dipendere da una
/// chiamata a IVAO per stampare una cella, e se la sorgente è muta o lenta il documento non deve accorgersene.
/// <see cref="FindAsync"/> interroga anche la sorgente e la usa chi <b>aggiunge</b> una riga: lì la chiamata
/// è una sola, l'ha chiesta una persona, e il nome trovato si salva nel documento.
/// </para>
/// </summary>
public interface IAirportNameLookup
{
    /// <summary>I nomi degli scali che <b>abbiamo in archivio</b>, per ICAO (maiuscolo). Gli sconosciuti
    /// semplicemente non compaiono nel risultato.</summary>
    Task<IReadOnlyDictionary<string, string>> NamesAsync(IReadOnlyList<string> icaos, CancellationToken ct = default);

    /// <summary>
    /// Lo scalo con questo codice: prima in archivio, poi alla sorgente. Null se non lo conosce nessuno dei
    /// due — o se la sorgente non è raggiungibile, che per chi chiede è la stessa cosa: non ha il nome.
    /// </summary>
    Task<AirportName?> FindAsync(string icao, CancellationToken ct = default);

    /// <summary>Gli scali che abbiamo in archivio, in ordine di codice: è l'elenco da cui si suggerisce
    /// mentre si scrive. ⚠️ Solo i nostri — la sorgente ne ha ventimila, e un elenco a discesa con dentro
    /// il mondo non aiuta nessuno a trovare Grottaglie.</summary>
    Task<IReadOnlyList<AirportName>> ListAsync(CancellationToken ct = default);
}
