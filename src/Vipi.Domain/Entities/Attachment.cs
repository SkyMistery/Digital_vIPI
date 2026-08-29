namespace Vipi.Domain.Entities;

/// <summary>
/// Che <b>genere</b> di documento è un allegato. Uno dei due assi della biblioteca (l'altro è
/// <see cref="AttachmentScope"/>): «le LoA di Roma» sono due filtri, non una cartella.
/// <para>⚠️ Non è una cartella per un motivo pratico: un albero a 50+ file si riempie di roba archiviata
/// male e nessuno la ritrova. Due assi non hanno un «posto sbagliato» dove finire.</para>
/// </summary>
public enum AttachmentKind
{
    /// <summary>Lettera d'accordo firmata fra due enti.</summary>
    Loa,

    /// <summary>Circolare della divisione.</summary>
    Circular,

    /// <summary>Carta: aerodromo, avvicinamento, minime.</summary>
    Chart,

    /// <summary>Manuale.</summary>
    Manual,

    /// <summary>Tutto il resto. È lo zero dell'enum, quindi una voce nasce così finché non la si classifica.</summary>
    Other,
}

/// <summary>
/// A che <b>perimetro</b> appartiene un allegato: il secondo asse della biblioteca.
/// <para>La chiave del perimetro sta a parte, in <see cref="Attachment.ScopeKey"/>: <c>Acc</c> + <c>LIRR</c>,
/// <c>Airport</c> + <c>LIMC</c>. Per <see cref="Division"/> la chiave è vuota.</para>
/// </summary>
public enum AttachmentScope
{
    /// <summary>Vale per tutta la divisione. Zero dell'enum: è il caso senza chiave, quindi anche il default sicuro.</summary>
    Division,

    /// <summary>Di un ACC: la chiave è il codice (<c>LIRR</c>).</summary>
    Acc,

    /// <summary>Di uno scalo: la chiave è l'ICAO (<c>LIMC</c>).</summary>
    Airport,
}

/// <summary>
/// <b>Dove stanno i byte.</b> Non è un dettaglio decorativo: è la colonna che rende reversibile un vincolo
/// che non controlliamo.
///
/// <para>I PDF non possono stare da noi per due ragioni entrambe esterne — il piano di hosting non ammette
/// il formato (vincolo <b>contrattuale</b>, quindi non si aggira mettendo i byte in MariaDB) e IVAO HQ
/// indica di tenere i documenti sul Drive di divisione. Il deposito di oggi è quindi
/// <see cref="Drive"/>.</para>
///
/// <para>⚠️ Il nome è <c>Provider</c> e non <c>DriveFileId</c> proprio perché il giorno che il deposito
/// cambia — Cloudflare R2, che è già davanti al sito; un repo GitHub; di nuovo in casa se il piano di
/// hosting cambia — non si deve toccare <b>un solo documento</b>: si aggiunge un valore qui. Un nome che
/// dice «Drive» richiuderebbe la porta che <c>/vsop/files/{slug}</c> tiene aperta.</para>
/// </summary>
public enum AttachmentProvider
{
    /// <summary>Google Drive, cartella dentro il <b>Drive condiviso</b> di divisione (confermato il 29 agosto 2026).
    /// I byte appartengono quindi all'organizzazione e non a una persona: sopravvivono a un cambio d'incarico.</summary>
    Drive,
}

/// <summary>
/// Una voce della biblioteca allegati: una LoA firmata, una circolare, una carta, un manuale — caricata
/// <b>in un posto solo</b> e citata dai documenti che la nominano
/// (carta <c>docs/feature/2026-08-25-biblioteca-allegati.md</c>).
///
/// <para><b>L'identità è lo <see cref="Slug"/>, e non cambia mai.</b> È ciò che i documenti citano
/// (<c>[LoA Marseille](allegato:loa-lirr-lfmm)</c>, o un blocco <c>Attachment</c>). Se citassero il file,
/// ogni sostituzione di un PDF vorrebbe dire <b>riaprire tutti i documenti che lo citano</b>; citando lo
/// slug, sostituire è spostare un puntatore.</para>
///
/// <para><b>Qui dentro non c'è nessun riferimento ai byte</b>, ed è voluto: dove stia il file lo dice
/// <see cref="AttachmentVersion"/>, una riga per versione, e la <i>corrente</i> è quella col
/// <see cref="AttachmentVersion.Number"/> più alto. La carta metteva l'id del file anche qui; tenerlo in due
/// posti vuol dire che un giorno i due posti dicono cose diverse, e il posto sbagliato è quello che serve il
/// link. Una voce senza nemmeno una versione non esiste: nasce con la v1.</para>
///
/// <para>⚠️ <b>Tutto ciò che entra in biblioteca è pubblico.</b> Il file sul Drive è condiviso «chiunque
/// abbia il link», quindi allegati riservati allo staff <b>non esistono</b>: un controllo d'accesso davanti a
/// un URL pubblico sarebbe teatro. Confermato dal committente che non servono.</para>
///
/// <para>Nessuna FK verso documenti o sezioni, come per <see cref="MediaAsset"/> e per la stessa ragione: un
/// allegato sopravvive al blocco che lo cita — una release pubblicata continua a citarne lo slug — e chi lo
/// cita si <b>ricava</b> leggendo i blocchi, mai da una tabella di join che si desincronizza in silenzio.</para>
/// </summary>
public class Attachment
{
    public int Id { get; set; }

    /// <summary>
    /// L'identità: <c>loa-lirr-lfmm</c>. È ciò che finisce dentro i documenti, quindi <b>non si cambia mai</b>
    /// — cambiarlo spegne ogni citazione già scritta. Unico.
    /// </summary>
    public string Slug { get; set; } = default!;

    /// <summary>Quel che si legge nel link: «LoA Roma–Marseille».</summary>
    public string Title { get; set; } = default!;

    /// <summary>Primo asse della biblioteca.</summary>
    public AttachmentKind Kind { get; set; }

    /// <summary>Secondo asse della biblioteca.</summary>
    public AttachmentScope Scope { get; set; }

    /// <summary>
    /// La chiave del perimetro: <c>LIRR</c> per un ACC, <c>LIMC</c> per uno scalo, <c>null</c> per la
    /// divisione. ⚠️ Stringa e non FK verso <c>Acc</c>/<c>Airport</c>: una LoA resta valida — e resta da
    /// leggere — anche il giorno che l'anagrafica non ha più quella riga.
    /// </summary>
    public string? ScopeKey { get; set; }

    /// <summary>Note libere dello staff. Non le legge nessun motore.</summary>
    public string? Notes { get; set; }

    public DateTime CreatedUtc { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public int UpdatedByUserId { get; set; }

    /// <summary>La storia, dalla v1 alla corrente. La corrente è quella col <c>Number</c> più alto.</summary>
    public List<AttachmentVersion> Versions { get; set; } = new();
}

/// <summary>
/// Una versione di un <see cref="Attachment"/>: <b>dove stanno i byte adesso</b>, più chi li ha messi lì,
/// quando e perché.
///
/// <para><b>Il link serve sempre la versione corrente</b>, release pubblicate comprese. Non si congela: la
/// regola di casa è già scritta in <c>DocRelease</c> — la release congela le <i>scelte editoriali</i>, non i
/// cataloghi esterni, e una LoA firmata è un catalogo esterno come una frequenza. Congelare avrebbe anche un
/// difetto pratico grave: una scansione sbagliata già pubblicata si correggerebbe solo <b>ripubblicando
/// tutti</b> i documenti che la citano.</para>
///
/// <para>⚠️ <b>Questa riga non promette di riscaricare la v1.</b> Su Drive le revisioni «purgabili» durano
/// una trentina di giorni (meno se il file ne accumula cento), e i byte vecchi se ne vanno: quel che resta è
/// <b>chi, quando e perché</b>, che è la tracciatura chiesta. La revisione <i>di testa</i> invece non viene
/// mai purgata, quindi la versione corrente — l'unica che i documenti servono — è al sicuro.</para>
/// </summary>
public class AttachmentVersion
{
    public int Id { get; set; }

    public int AttachmentId { get; set; }
    public Attachment? Attachment { get; set; }

    /// <summary>Progressivo <b>per allegato</b>, da 1. Il più alto è la versione corrente.</summary>
    public int Number { get; set; }

    /// <summary>Dove stanno i byte di <i>questa</i> versione.</summary>
    public AttachmentProvider Provider { get; set; }

    /// <summary>
    /// Come si chiama il file presso il deposito: l'id del file su Drive.
    /// <para>Di norma è <b>identico</b> fra una versione e la successiva, perché Drive sostituendo un file ne
    /// mantiene l'id; è diverso se hanno caricato un file nuovo invece di sostituire — o se un giorno cambia
    /// il deposito.</para>
    /// </summary>
    public string ExternalId { get; set; } = default!;

    /// <summary>Perché è stata caricata: «rifirmata dopo modifica CoP». È la metà utile della tracciatura.</summary>
    public string? Note { get; set; }

    public DateTime CreatedUtc { get; set; }
    public int CreatedByUserId { get; set; }
}
