namespace Vipi.Domain.Entities;

/// <summary>
/// Da dove viene il valore di UN campo di una radioassistenza. ⚠️ La provenienza è <b>per campo</b> e non per
/// riga, e non è un dettaglio: su MNL frequenza e canale li manda il sectorfile, su un ILS li scrive una
/// persona. Una colonna sola sulla riga mentirebbe su metà dei campi, e la regola «la fonte vince» non
/// saprebbe più a che cosa applicarsi.
/// </summary>
public enum NavaidFieldOrigin
{
    /// <summary>Nessuno l'ha ancora scritto.</summary>
    Empty,

    /// <summary>L'ha scritto una persona: si può correggere.</summary>
    Manual,

    /// <summary>Lo manda la sorgente (il sectorfile della divisione): <b>non si corregge a mano</b>.</summary>
    Source,
}

/// <summary>
/// Una radioassistenza dell'anagrafica di divisione: la scrivi una volta, esce uguale ovunque — nel vSOP di
/// Amendola e in quello di Gioia (carta <c>2026-08-27-vsop-militari.md</c> §12b).
///
/// <para>
/// ⚠️ <b>Il dato non nasce vuoto.</b> Il sectorfile lo porta già e noi lo buttavamo via: le righe di
/// <c>itvor.vor</c> sono <c>AEA;111.65;N040.38.17.400;E008.17.30.400;0;2;54Y;</c> — codice, frequenza,
/// coordinate <b>e canale</b> — e il parser teneva solo nome e coordinate. Misurato il 29 agosto 2026:
/// 128 VOR, 30 NDB, <b>26 col canale</b>, che sono i VORTAC/TACAN, cioè proprio i militari.
/// </para>
///
/// <para><b>Le decisioni del committente che questo modello mette in pietra</b> (§12b):</para>
/// <list type="bullet">
/// <item><b>Identità = <see cref="Code"/> + <see cref="Kind"/>.</b> Due <c>DEC</c>, uno VOR e uno NDB, sono
/// due righe.</item>
/// <item><b>La fonte vince sempre</b>: un campo <see cref="NavaidFieldOrigin.Source"/> non si corregge a
/// mano — il tentativo non va a buon fine, non «avvisa e passa».</item>
/// <item><b>Vince chi scrive per ultimo</b>, e il registro lo racconta col valore vecchio e quello nuovo.
/// ⚠️ Si scrivono <b>i campi toccati, non la riga</b>: altrimenti chi cambia la frequenza e chi cambia le
/// coordinate si sovrascrivono a vicenda <i>senza aver toccato la stessa cosa</i>, e il registro direbbe
/// una cosa falsa.</item>
/// </list>
///
/// <para>
/// ⚠️ <b>Il lock del documento qui non protegge niente</b>: due persone su due vSOP diversi hanno ognuna il
/// lock del proprio documento e scrivono sulla stessa radioassistenza. Il lock è del documento, l'anagrafica
/// è di tutti.
/// </para>
/// </summary>
public class Navaid
{
    public int Id { get; set; }

    /// <summary>Il codice come si scrive e come si dice in frequenza: <c>MNL</c>, <c>AEA</c>. Maiuscolo.</summary>
    public string Code { get; set; } = default!;

    /// <summary>
    /// La <b>famiglia</b>: <c>VHF</c> (108–118 MHz) o <c>NDB</c> (kHz). È la sola cosa che la sorgente sa
    /// davvero, e con <see cref="Code"/> e <see cref="Channel"/> forma l'identità della riga.
    ///
    /// <para>
    /// ⚠️ <b>Non è il tipo, e il nome del file non lo dice.</b> <c>itvor.vor</c> contiene VOR, TACAN
    /// <b>e</b> VORTAC insieme — Grosseto ci sta due volte, un VOR a 109.85 e un TACAN puro col solo canale
    /// 35Y — e nemmeno il canale distingue: <c>115.25</c> è la frequenza <i>appaiata</i> del canale 99Y, e
    /// ce l'ha anche un VOR/DME. Fino al 30 agosto 2026 questa colonna teneva «il file da cui la riga
    /// arriva» spacciandolo per la natura dell'impianto: era una classificazione inventata.
    /// </para>
    /// <para>Che tipo sia — VOR, TACAN, VORTAC, ILS — lo dice una <b>persona</b>: <see cref="Type"/>.</para>
    /// </summary>
    public string Kind { get; set; } = default!;

    /// <summary>
    /// L'identità in una stringa sola: <c>CODICE|FAMIGLIA|CANALE</c>. Esiste come colonna perché l'indice
    /// unico deve poter comprendere il <b>canale</b>, che è nullable — e in SQLite come in MySQL due NULL
    /// non si considerano uguali, quindi un indice su una colonna nullable <b>non</b> impedirebbe i
    /// doppioni proprio dove servono (le righe senza canale, che sono la maggioranza).
    /// <para>Stessa scelta di <c>GlossaryTerm.SourceKey</c>: la chiave si scrive, non si lascia decidere al
    /// confronto del database.</para>
    /// </summary>
    public string NaturalKey { get; set; } = default!;

    /// <summary>
    /// Il tipo: <c>VOR</c>, <c>TACAN</c>, <c>VORTACAN</c>, <c>ILS</c>, <c>NDB</c>… È <b>editoriale</b> e non
    /// entra nell'identità: lo scrive una persona, e la sorgente non lo tocca mai.
    ///
    /// <para>⚠️ <b>Null è una risposta legittima</b> — «nessuno l'ha ancora detto» — e in tabella si legge
    /// come un trattino. Metterci un ripiego (il nome del file, la banda) vorrebbe dire stampare su un SOP
    /// una classificazione che non ha fatto nessuno, con l'aria di essere un dato.</para>
    /// <para>Sulle righe in kHz nasce già a <c>NDB</c>: lì il tipo è uno solo, e quello la sorgente lo sa.</para>
    /// </summary>
    public string? Type { get; set; }

    /// <summary>La frequenza come si scrive: <c>115.25</c>, <c>390.0</c>. Null = non si sa.</summary>
    public string? Frequency { get; set; }

    /// <summary>Il canale TACAN/DME: <c>99Y</c>, <c>54Y</c>. Null = quella radioassistenza non ne ha uno.</summary>
    public string? Channel { get; set; }

    /// <summary>Latitudine in gradi decimali. ⚠️ Si <b>scrive</b> in sessagesimale e si <b>mostra</b> in
    /// sessagesimale: i gradi decimali sono la forma canonica in archivio, quella che una mappa sa usare.</summary>
    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public NavaidFieldOrigin FrequencyOrigin { get; set; }
    public NavaidFieldOrigin ChannelOrigin { get; set; }

    /// <summary>Provenienza delle coordinate. ⚠️ <b>Una sola per la coppia</b>: una latitudine senza la sua
    /// longitudine non è una posizione, e non ha senso che le due metà vengano da posti diversi.</summary>
    public NavaidFieldOrigin CoordinatesOrigin { get; set; }

    /// <summary>Quando la sorgente l'ha confermata l'ultima volta. Null = non l'ha mai mandata (riga nostra).</summary>
    public DateTime? ImportedUtc { get; set; }

    public DateTime? UpdatedUtc { get; set; }
    public int? UpdatedByUserId { get; set; }
}
