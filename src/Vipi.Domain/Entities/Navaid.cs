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
    /// La <b>natura</b>, e con <see cref="Code"/> è l'identità della riga: <c>VOR</c>, <c>NDB</c>,
    /// <c>ILS</c>, <c>TACAN</c>, <c>VORTACAN</c>…
    /// <para>⚠️ Per le righe che vengono dalla sorgente è il <b>file</b> da cui arrivano (<c>itvor</c> →
    /// <c>VOR</c>), quindi non si cambia: cambiarla vorrebbe dire cambiare identità, e il giro d'import
    /// successivo ricreerebbe la riga di prima. Chi deve <i>dire</i> che quel VOR è un VORTAC usa
    /// <see cref="DisplayType"/>.</para>
    /// </summary>
    public string Kind { get; set; } = default!;

    /// <summary>
    /// Il tipo <b>mostrato</b> in tabella, quando la natura della sorgente è più grossolana della realtà.
    /// Null = si mostra <see cref="Kind"/>.
    ///
    /// <para>⚠️ Esiste per un caso vero, non per completezza: MNL sta in <c>itvor.vor</c> col canale
    /// <c>99Y</c>, e sul SOP di Amendola si legge <b>VORTACAN</b>. Senza questo campo l'unico modo di
    /// scriverlo sarebbe cambiare la natura, cioè l'identità — e la riga tornerebbe VOR al primo import.
    /// ⚠️ E non si <i>deduce</i> dal canale: un canale su un VOR può essere quello del DME appaiato, non di
    /// un TACAN. Dedurlo darebbe una tabella sbagliata con l'aria di essere precisa.</para>
    /// </summary>
    public string? DisplayType { get; set; }

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
