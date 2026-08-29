using System.Text.Json.Serialization;
using Vipi.Domain.Entities;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Una riga dell'anagrafica delle radioassistenze, come la vede chi la mostra o la modifica.
/// </summary>
/// <param name="Type">Il tipo da <b>stampare</b>: quello scelto a mano se c'è, altrimenti la natura.</param>
public sealed record NavaidRow(
    int Id, string Code, string Kind, string? DisplayType,
    string? Frequency, string? Channel, double? Latitude, double? Longitude,
    NavaidFieldOrigin FrequencyOrigin, NavaidFieldOrigin ChannelOrigin, NavaidFieldOrigin CoordinatesOrigin,
    DateTime? UpdatedUtc, int? UpdatedByUserId)
{
    // ⚠️ Fuori dal JSON: questa riga finisce negli SNAPSHOT di release, e una proprietà calcolata scritta
    // dentro è un valore che al ricarico non torna indietro — cioè due verità sulla stessa cosa.
    [JsonIgnore]
    public string Type => string.IsNullOrWhiteSpace(DisplayType) ? Kind : DisplayType!;

    /// <summary>Vero se questa riga la manda il sectorfile, anche solo per un campo: è la riga che non si
    /// può eliminare a mano, perché il giro dopo tornerebbe.</summary>
    [JsonIgnore]
    public bool DallaSorgente =>
        FrequencyOrigin == NavaidFieldOrigin.Source || ChannelOrigin == NavaidFieldOrigin.Source
        || CoordinatesOrigin == NavaidFieldOrigin.Source;
}

/// <summary>Esito della scrittura di UN campo dell'anagrafica.</summary>
public enum NavaidWrite
{
    /// <summary>Scritto.</summary>
    Ok,

    /// <summary>
    /// Rifiutato: quel campo lo manda la <b>sorgente</b>, e la sorgente vince sempre (decisione del
    /// committente, carta §12b). ⚠️ Non è «avvisa e scrivi lo stesso»: la modifica <b>non</b> va a buon fine,
    /// o al primo giro d'import sparirebbe senza spiegazioni.
    /// </summary>
    DallaSorgente,

    /// <summary>Il valore non ha la forma giusta (una frequenza che non è una frequenza, un sessagesimale
    /// che non si legge). La riga resta com'era.</summary>
    NonValido,

    /// <summary>La riga non c'è (o non c'è più: qualcuno l'ha eliminata mentre la si modificava).</summary>
    NonTrovata,

    /// <summary>Niente da fare: il valore era già quello. ⚠️ Il non-evento non si scrive nel registro —
    /// altrimenti «modificata da X oggi» finirebbe sopra una decisione presa da un altro mesi fa.</summary>
    Invariato,
}

/// <summary>Che cosa ha fatto un giro d'import delle radioassistenze.</summary>
public sealed record NavaidImportOutcome(int Create, int Aggiornate, int Invariate);

/// <summary>
/// L'anagrafica delle radioassistenze di divisione (carta vSOP militari §12b): <b>scritta una volta, esce
/// uguale ovunque</b>.
///
/// <para><b>Le regole che questa porta fa rispettare</b>, e che non si deducono dai nomi dei metodi:</para>
/// <list type="number">
/// <item><b>La fonte vince sempre.</b> Un campo che arriva dal sectorfile non si modifica a mano: il metodo
/// torna <see cref="NavaidWrite.DallaSorgente"/> e non scrive.</item>
/// <item><b>L'assenza non cancella.</b> Un giro d'import che non porta un campo <b>lascia il nostro dov'è</b>.
/// La regola è già stata pagata cara altrove: gli upsert scrivevano il <c>[]</c> della sorgente sopra le
/// shape e azzerarono 83 poligoni su 83.</item>
/// <item><b>Si scrivono i campi toccati, non la riga.</b> Per questo i metodi di scrittura sono uno per
/// campo: se si salvasse tutta la riga, chi cambia la frequenza e chi cambia le coordinate si
/// sovrascriverebbero a vicenda <i>senza aver toccato la stessa cosa</i>, e il registro racconterebbe una
/// modifica che nessuno ha fatto.</item>
/// <item><b>Vince chi arriva per ultimo</b> — decisione del committente — e il registro porta il valore
/// <b>vecchio e nuovo</b>: «Tizio ha modificato MNL» non permette né di accorgersene né di rimettere a posto.</item>
/// </list>
///
/// <para>⚠️ <b>Il lock del documento qui non protegge niente</b>: due persone su due vSOP diversi hanno
/// ognuna il lock del proprio documento e scrivono sulla stessa radioassistenza.</para>
/// </summary>
public interface INavaidCatalog
{
    /// <summary>Tutta l'anagrafica, in ordine di codice. È l'elenco da cui si sceglie in tabella.</summary>
    Task<IReadOnlyList<NavaidRow>> ListAsync(CancellationToken ct = default);

    /// <summary>Le righe con queste identità (codice + natura), nell'ordine chiesto; le sconosciute si
    /// saltano. È la lettura che serve a una tabella di documento, che porta le identità e non gli id.</summary>
    Task<IReadOnlyList<NavaidRow>> GetManyAsync(IReadOnlyList<NavaidKey> keys, CancellationToken ct = default);

    /// <summary>
    /// Crea una radioassistenza scritta a mano, o restituisce quella che c'è già con la stessa identità.
    /// ⚠️ Idempotente apposta: due porte che creano la stessa cosa devono fare la stessa domanda, e la
    /// domanda giusta è quella del dominio (codice + natura) — la lezione delle due vLOA sulla stessa coppia.
    /// </summary>
    Task<NavaidRow> CreateAsync(string code, string kind, int userId, CancellationToken ct = default);

    Task<NavaidWrite> SetDisplayTypeAsync(int id, string? tipo, int userId, CancellationToken ct = default);
    Task<NavaidWrite> SetFrequencyAsync(int id, string? frequenza, int userId, CancellationToken ct = default);
    Task<NavaidWrite> SetChannelAsync(int id, string? canale, int userId, CancellationToken ct = default);

    /// <summary>Le coordinate, scritte in <b>sessagesimale</b> (<c>N41°32'05.07''E015°43'42.47''</c>): è
    /// l'unica forma accettata, per decisione del committente. Testo vuoto = si cancellano.</summary>
    Task<NavaidWrite> SetCoordinatesAsync(int id, string? sessagesimale, int userId, CancellationToken ct = default);

    /// <summary>
    /// Il giro d'import dal sectorfile. Crea le righe nuove, aggiorna i campi che la sorgente manda e
    /// <b>non tocca</b> quelli che non manda. Non elimina mai: gli ILS e i TACAN non stanno nel sectorfile,
    /// e una potatura li porterebbe via al primo giro.
    /// </summary>
    Task<NavaidImportOutcome> ImportFromSourceAsync(IReadOnlyList<SourceNavaid> navaids, CancellationToken ct = default);
}

/// <summary>L'identità di una radioassistenza: codice + natura (decisione del committente, §12b).</summary>
public readonly record struct NavaidKey(string Code, string Kind);

/// <summary>Una radioassistenza come la manda la sorgente. I campi che la sorgente non conosce sono null, e
/// null qui vuol dire <b>«non lo dico»</b>, mai «cancellalo».</summary>
public sealed record SourceNavaid(string Code, string Kind, string? Frequency, string? Channel,
    double? Latitude, double? Longitude);
