using Vipi.Domain;

namespace Vipi.Domain.Entities;

/// <summary>
/// Un fatto a monte che tocca un documento: «il settore LIRR_TS_CTR non è più nei cataloghi», «l'area
/// LI-R51 è cambiata», «la copia pubblicata è indietro». Carta
/// <c>docs/feature/2026-08-25-documenti-da-rivedere.md</c>.
///
/// <para><b>Perché una tabella e non un flag.</b> Fino al 25 agosto 2026 la segnalazione era un solo
/// <c>NeedsReviewUtc</c> + <c>ReviewReason</c> sul <see cref="Document"/>: il secondo evento sovrascriveva
/// il primo, che spariva senza lasciare traccia. Con più rivelatori quel modello perde informazione al
/// primo giro.</para>
///
/// <para><b>Perché è ancorato al documento e non al bersaglio di release.</b> La chiave di release di una
/// vIPI ACC è <c>{acc}|{callsign del settore primario}</c> e quella di un APP <b>è</b> il callsign: le
/// spostano un settore riparentato, un primario che cambia, una rinomina in sorgente — cioè proprio gli
/// eventi che questa tabella registra. Un impatto ancorato lì diventerebbe un impatto che parla di un
/// documento e non sa più quale. Il <see cref="DocumentId"/> è stabile, ha una FK e si porta via le sue
/// righe in cascata. La UI il bersaglio ce l'ha già (<c>ManagedDoc</c> porta entrambi).</para>
///
/// <para>⚠️ <see cref="ClearedUtc"/> è <b>NOT NULL</b> con la sentinella <see cref="Aperto"/> per «ancora
/// aperto»: l'unicità che impedisce i doppioni deve valere <b>solo fra le righe aperte</b>, e MariaDB non
/// ha indici unici parziali. La sentinella è il modo per esprimerlo con un indice normale su
/// <c>(DocumentId, Kind, SourceKey, ClearedUtc)</c> — e serve davvero: gli impatti si scrivono da dentro
/// la proiezione, che ha tredici chiamanti e gira anche in concorrenza.</para>
/// </summary>
public class DocumentImpact
{
    /// <summary>
    /// Sentinella di «aperto» per <see cref="ClearedUtc"/>. Non è una data: è l'assenza di una data, scritta
    /// in modo che un indice unico normale possa distinguerla.
    ///
    /// <para>⚠️ <b>Non è <c>DateTime.MinValue</c>, e non può esserlo</b>: il <c>DATETIME</c> di MariaDB parte
    /// da <c>1000-01-01</c>, quindi <c>0001-01-01</c> — la scelta ovvia — in <c>sql_mode</c> stretto viene
    /// <b>rifiutata</b> (errore 1292) e in modalità permissiva diventa una data zero. Su SQLite passerebbe:
    /// la suite sarebbe verde e la produzione romperebbe alla prima segnalazione. L'epoca Unix è dentro
    /// l'intervallo di tutti e tre i provider e non può essere scambiata per una chiusura vera — nel 1970
    /// non c'era niente da chiudere.</para>
    /// </summary>
    public static readonly DateTime Aperto = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public int Id { get; set; }

    public int DocumentId { get; set; }
    public Document? Document { get; set; }

    public ImpactKind Kind { get; set; }

    /// <summary>Che cosa, a monte, ha prodotto l'impatto: un callsign (<c>LIRR_TS_CTR</c>), un'area
    /// (<c>area:44120</c>), oppure vuoto per gli impatti che riguardano il documento nel suo insieme.
    /// Entra nella chiave di deduplicazione: due volte lo stesso fatto = una riga sola.</summary>
    public string SourceKey { get; set; } = "";

    /// <summary>Chiave di localizzazione della frase mostrata. ⚠️ La frase <b>non</b> si salva: una riga scritta
    /// in italiano si ripresenterebbe in italiano a chi legge in inglese, e il circuito Blazor cambia lingua
    /// senza ricaricare. È lo stesso pattern di <c>ConsistencyFinding</c>.</summary>
    public string ReasonKey { get; set; } = default!;

    /// <summary>Argomenti della frase, serializzati (array JSON di stringhe). null = frase senza argomenti.</summary>
    public string? ReasonArgsJson { get; set; }

    /// <summary>Vero se il documento ha una sezione <b>Live</b> alimentata dalla famiglia di questo impatto:
    /// il cambio è <b>già in pubblico</b>, senza passare da una ripubblicazione. Alza la severità della riga,
    /// non ne cambia la natura.</summary>
    public bool IsPublicNow { get; set; }

    public DateTime RaisedUtc { get; set; }

    /// <summary>Quando è stato chiuso; <see cref="Aperto"/> finché è aperto.</summary>
    public DateTime ClearedUtc { get; set; } = Aperto;

    /// <summary>Chi l'ha chiuso. <b>0</b> = l'ha richiuso il calcolo, non una persona.</summary>
    public int ClearedByUserId { get; set; }

    /// <summary>Comodità di lettura: la riga è ancora aperta?</summary>
    public bool IsOpen => ClearedUtc == Aperto;
}
