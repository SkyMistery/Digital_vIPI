using Vipi.Domain;

namespace Vipi.Application.Diagnostics;

/// <summary>Gravità di un'incongruenza rilevata (solo diagnosi: nessun dato viene modificato).</summary>
public enum ConsistencySeverity { Warning, Error }

/// <summary>
/// Di <b>chi</b> è il problema. Non è una sfumatura del testo: dice a chi legge se deve aprire un editor, il
/// pannello del server o il file di configurazione — e sono tre persone diverse in tre momenti diversi.
///
/// <para><b>Perché esiste.</b> Fino al 22 agosto 2026 la pagina si presentava come «incongruenze dei
/// riferimenti deboli (soft-ref)» e nella stessa tabella potevano comparire il drift di schema, le
/// impostazioni del server di database, il guasto di una manutenzione d'avvio e «nessuno può editare» — che
/// è il rilievo più grave che l'applicazione sappia produrre. Cinque famiglie presentate come una.</para>
///
/// <para>⚠️ Ogni produttore di rilievi la <b>dichiara</b>: è un parametro obbligatorio e non ha un default,
/// perché un default farebbe finire un controllo nuovo nell'area sbagliata senza che nessuno se ne accorga.</para>
/// </summary>
public enum ConsistencyArea
{
    /// <summary>Soft-ref e dati editoriali: si ripara aprendo un editor.</summary>
    Dati,
    /// <summary>Schema fisico contro modello EF: si ripara con una migrazione o un ALTER.</summary>
    Schema,
    /// <summary>Impostazioni del server di database che l'applicazione assume e non può imporre.</summary>
    Server,
    /// <summary>Una passata dell'avvio è fallita: l'istanza gira, ma non è partita intera.</summary>
    Avvio,
    /// <summary>Configurazione dell'applicazione (pattern admin, sezione Division): si ripara fuori dall'app.</summary>
    Configurazione,

    /// <summary>
    /// Il dato arriva così <b>dalla sorgente esterna</b>: da qui non si ripara, si sa e si tiene d'occhio.
    ///
    /// <para>Serve un'area sua perché la risposta è diversa da tutte le altre: non «apri l'editor», non
    /// «lancia una migrazione», ma «l'applicazione ci convive, ed ecco cosa ne consegue». Nasce il 25 agosto
    /// 2026 dopo <c>LIRR_TS_CTR</c>, la cui shape arriva da IVAO col contorno ripetuto due volte: senza una
    /// riga che lo dica, un settore che non attribuisce traffico resta invisibile finché non se ne accorge
    /// un occhio umano su una vista 3D.</para>
    /// </summary>
    Sorgente,

    /// <summary>
    /// Il <b>sectorfile Aurora</b> della divisione e i cataloghi IVAO non dicono la stessa cosa. Da qui non si
    /// ripara: si ripara nel sectorfile, e lo scrive l'IT-AOD.
    ///
    /// <para>Serve un'area sua per la ragione per cui l'enum esiste — <b>il destinatario è una terza
    /// persona</b>. Non chi apre un editor (<see cref="Dati"/>), non chi amministra la macchina
    /// (<see cref="Server"/>, <see cref="Schema"/>), non «ci conviviamo» (<see cref="Sorgente"/>): qui c'è
    /// qualcuno da avvisare, fuori da questa applicazione.</para>
    ///
    /// <para>⚠️ I rilievi di quest'area sono <b>esclusi dal conteggio dell'health check</b>: ce ne sono
    /// sempre alcuni — le due sorgenti hanno cadenze diverse — e un endpoint di salute perennemente
    /// «Degraded» è un endpoint spento. Vedi <c>VipiHealthCheck</c>.</para>
    /// </summary>
    Sectorfile,
}

/// <summary>
/// Una singola incongruenza rilevata dal report di consistenza.
///
/// <para><b>Due modi di leggere lo stesso rilievo, e servono entrambi.</b> <paramref name="Category"/> e
/// <paramref name="Detail"/> sono il testo **grezzo**, in italiano: lo leggono l'health check e i log, dove
/// una lingua d'interfaccia non esiste. <paramref name="CategoryKey"/> e <paramref name="DetailKey"/> sono
/// le chiavi con cui chi lo <b>mostra</b> lo traduce (<c>ConsistencyNarrator</c>, gemello di
/// <c>AuditNarrator</c>).</para>
///
/// <para>⚠️ Non si è scelto di localizzare al momento della scrittura: il finding nasce anche fuori da una
/// richiesta HTTP (le manutenzioni d'avvio) e viene consumato dove una cultura non c'è. E le chiavi non
/// sostituiscono il testo grezzo: se una chiave manca o è sbagliata, chi mostra ripiega sul testo — mai una
/// riga vuota al posto di un fatto.</para>
/// </summary>
/// <param name="Category">Famiglia del controllo (es. «Pista orfana», «Gerarchia dangling»).</param>
/// <param name="Severity">Gravità.</param>
/// <param name="Entity">Riferimento leggibile all'entità coinvolta (es. «Clausola #42 (LIRR, punti EKMUR)»).</param>
/// <param name="Detail">Spiegazione del disallineamento e come si è prodotto.</param>
/// <param name="Area">Di chi è il problema. Vedi <see cref="ConsistencyArea"/>.</param>
/// <param name="Where">
/// Dove si va a ripararlo: la rotta della pagina che lo tocca, o <c>null</c> se non c'è un posto da aprire
/// (impostazioni del server, configurazione, schema — si correggono fuori dall'applicazione).
///
/// <para><b>Perché sul finding e non una mappa nella pagina.</b> Chi produce il rilievo è l'unico che sa
/// dove si ripara: una mappa categoria→rotta lato UI sarebbe un secondo posto da tenere allineato, e un
/// controllo nuovo nascerebbe muto senza che il compilatore lo dica. Vale qui la regola del formattatore
/// unico.</para>
///
/// <para>⚠️ <c>null</c> è una risposta, non una dimenticanza: un link che non porta da nessuna parte è
/// peggio di nessun link.</para>
/// </param>
/// <param name="CategoryKey">
/// Chiave di traduzione della famiglia, per chi il rilievo lo <b>mostra</b>. <c>null</c> ⇒ si legge
/// <paramref name="Category"/> così com'è.
/// </param>
/// <param name="DetailKey">Chiave di traduzione della spiegazione; gli argomenti stanno in
/// <paramref name="DetailArgs"/>, nell'ordine in cui li usa il testo.</param>
/// <param name="DetailArgs">Argomenti di <paramref name="DetailKey"/>.</param>
/// <param name="EntityKey">
/// Chiave di traduzione del bersaglio, coi suoi <paramref name="EntityArgs"/>. ⚠️ Serve anche a lui: metà dei
/// bersagli non è un identificatore ma una <b>frase</b> — «Settore ACC LGGG_W_CTR», «Clausola #1 (LIBB,
/// punti Y01-Y12)» — e in pagina inglese restavano in italiano anche dopo aver tradotto categoria e
/// dettaglio. <c>null</c> per i bersagli che non sono prosa (<c>sql_mode</c>, <c>Documents.Title</c>).
/// </param>
/// <param name="EntityArgs">Argomenti di <paramref name="EntityKey"/>.</param>
public sealed record ConsistencyFinding(string Category, ConsistencySeverity Severity, string Entity,
    string Detail, ConsistencyArea Area, string? Where = null,
    string? CategoryKey = null, string? DetailKey = null, object[]? DetailArgs = null,
    string? EntityKey = null, object[]? EntityArgs = null);

/// <summary>Condizione di una clausola di accordo (soft-ref a pista/area denormalizzate).</summary>
/// <param name="Points">I punti della clausola, come si leggono: servono solo a dire QUALE clausola nel
/// messaggio del report.</param>
public sealed record TransferConditionRow(int ClauseId, string AccCode, string Points,
    int? ConditionRefId, string? ConditionLabel, string? ConditionAreaLabel);

/// <summary>
/// Un campo che ha in archivio un documento che la sua <b>categoria non ammette</b> (carta
/// <c>2026-09-11-categorie-aeroporto.md</c>): una vIPI civile su un campo solo militare, o un vSOP militare su un
/// campo civile o civile con presenza militare.
///
/// <para>⚠️ Non è di per sé un difetto: le guardie bloccano la <b>nascita</b> di un documento fuori categoria,
/// non l'<b>apertura</b> di uno che c'era già — e la via d'uscita (nascondere, o eliminare) è un gesto editoriale
/// che nessuno deve fare al posto di chi decide. Diventa un rilievo solo quando quel documento è ancora
/// <b>visibile</b>, o quando resta <b>unito</b> all'altra edizione dello stesso campo.</para>
/// </summary>
/// <param name="Edizione">Quale dei due documenti è fuori categoria.</param>
/// <param name="Visibile">Quel documento si vede dal web: ha una release in vigore, non è nascosto, e nemmeno
/// l'aeroporto lo è. Sono le tre condizioni che il caricatore pubblico chiede, tutte e tre.</param>
/// <param name="UnitoAllAltra">Quel documento e l'ALTRA edizione dello stesso campo stanno nella <b>stessa</b>
/// unione.
/// <para>⚠️ Conta anche a documento nascosto: la pubblicazione accoppiata <b>non guarda</b> <c>IsHidden</c>,
/// quindi ogni pubblicazione dell'altra edizione crea una release anche per un documento che nessuno vede.</para></param>
public sealed record DocumentoFuoriCategoriaRow(string Icao, string AccCode, DocumentEdition Edizione,
    bool Visibile, bool UnitoAllAltra);

/// <summary>Nodo dei cataloghi che dichiara un padre di copertura per callsign (soft-ref cross-catalogo, no FK).</summary>
/// <param name="Kind">Che cosa è il nodo, in chiaro: «Settore ACC», «Settore APT», «Aeroporto».</param>
/// <param name="KindKey">La stessa cosa come chiave di traduzione, per chi il rilievo lo mostra.</param>
public sealed record ParentRefRow(string Kind, string Reference, string ParentCallsign, string? KindKey = null);

/// <summary>
/// Sezione <c>regulated</c> di un documento con la sua selezione di aree, come JSON grezzo: il parse sta
/// nell'analisi (funzione pura) e non nel repository. <paramref name="Reference"/> è il nome leggibile del
/// documento, <paramref name="Kind"/> ne dice la famiglia (vIPI ACC / vIPI APP).
/// </summary>
public sealed record RegulatedRefRow(string Kind, string Reference, string? Json);

/// <summary>
/// Fotografia di sola lettura dei dati soggetti a soft-ref, caricata dalla persistenza e analizzata dal
/// <see cref="ConsistencyReportService"/>. Separa i dati (repo) dalla logica di rilevazione (pura, testabile).
/// </summary>
public sealed class ConsistencyDataset
{
    /// <summary>Condizioni pista/area dei punti di trasferimento.</summary>
    public IReadOnlyList<TransferConditionRow> TransferConditions { get; init; } = Array.Empty<TransferConditionRow>();

    /// <summary>Piste esistenti: Id → Ident corrente (per rilevare ref orfani e label divergenti).</summary>
    public IReadOnlyDictionary<int, string> RunwayIdents { get; init; } = new Dictionary<int, string>();

    /// <summary>Nomi delle aree speciali esistenti (case-insensitive) per validare <c>ConditionAreaLabel</c>.</summary>
    public IReadOnlySet<string> AreaNames { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Nodi che dichiarano un padre di copertura.</summary>
    public IReadOnlyList<ParentRefRow> ParentRefs { get; init; } = Array.Empty<ParentRefRow>();

    /// <summary>
    /// callsign → padre <b>EFFETTIVO</b> (scritto se c'è, altrimenti derivato dalla scaletta d'aeroporto).
    /// È l'albero che leggono davvero proiezione, ricaduta e pagina Struttura: è l'unico su cui abbia senso
    /// cercare un anello. ⚠️ Non confonderlo con <see cref="ParentRefs"/>, che porta i soli padri SCRITTI ed
    /// esiste per un controllo diverso (il padre che non risolve a nessun nodo).
    /// </summary>
    public IReadOnlyDictionary<string, string?> EffectiveParents { get; init; } = new Dictionary<string, string?>();

    /// <summary>Callsign validi come padre (union delle chiavi naturali dei cataloghi ACC/aeroporto).</summary>
    public IReadOnlySet<string> ValidCallsigns { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Sezioni <c>regulated</c> con la selezione di aree salvata (JSON grezzo).</summary>
    public IReadOnlyList<RegulatedRefRow> RegulatedRefs { get; init; } = Array.Empty<RegulatedRefRow>();

    /// <summary>IvaoId delle aree speciali esistenti, per validare gli id salvati nelle selezioni.</summary>
    public IReadOnlySet<string> SpecialAreaIds { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Le shape dei settori come stanno in archivio, per i controlli sulla geometria di sorgente.</summary>
    public IReadOnlyList<SectorShapeRow> SectorShapes { get; init; } = Array.Empty<SectorShapeRow>();

    /// <summary>
    /// I callsign presenti in <b>tutti e due</b> i cataloghi: l'albero effettivo ne tiene uno solo.
    /// <para>Non è un errore d'ingresso — ogni riga è legale nella sua tabella, e le tabelle sono due, quindi
    /// nessun indice può impedirlo. È qui che si racconta, invece di sovrascrivere in silenzio.</para>
    /// </summary>
    public IReadOnlyList<Domain.Services.HierarchyDuplicate> HierarchyDuplicates { get; init; }
        = Array.Empty<Domain.Services.HierarchyDuplicate>();

    /// <summary>La banda verticale dichiarata di ogni settore visibile: serve a chiedersi se la sua ricaduta
    /// quel cielo lo copre davvero.</summary>
    public IReadOnlyList<SectorBandRow> SectorBands { get; init; } = Array.Empty<SectorBandRow>();

    /// <summary>Le righe di ripiego dichiarate, per settore e in ordine: sono la prima parte della catena.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<Content.FallbackRow>> Fallbacks { get; init; }
        = new Dictionary<string, IReadOnlyList<Content.FallbackRow>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// callsign → padre nell'albero <b>PROIETTATO</b> (<c>Sector.ParentSectorId</c>).
    ///
    /// <para>⚠️ Non è un doppione di <see cref="EffectiveParents"/>: quello è l'albero dei <b>cataloghi</b>,
    /// questo è quello che leggono la geometria (<c>EfSectorVolumeCatalog</c>) e la topologia. Devono
    /// coincidere — la proiezione nasce dai cataloghi — ma sono <b>due letture</b>, e quando divergono la
    /// ricaduta e la copertura rispondono due cose diverse alla stessa domanda.</para>
    /// </summary>
    public IReadOnlyDictionary<string, string?> ProjectedParents { get; init; }
        = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Tutti i punti di trasferimento, uno per riga: la base della scala di risalita.</summary>
    public IReadOnlyList<TransferLadderRow> TransferLadders { get; init; } = Array.Empty<TransferLadderRow>();

    /// <summary>I documenti che la categoria del loro campo non ammette. Vuoto = niente da dire.</summary>
    public IReadOnlyList<DocumentoFuoriCategoriaRow> DocumentiFuoriCategoria { get; init; } = Array.Empty<DocumentoFuoriCategoriaRow>();
}

/// <summary>La banda verticale di un settore, coi limiti <b>grezzi</b> del catalogo (l'unità non è tracciata
/// a schema: la deduce <c>AorFlBand</c>).</summary>
public sealed record SectorBandRow(string Callsign, int? LowerLimit, int? UpperLimit);

/// <summary>
/// Un <b>punto</b> di trasferimento come serve a percorrerne la scala di risalita: chi cede, chi riceve, a
/// che quota, e dove.
///
/// <para>⚠️ Una riga per <b>punto</b>, non per clausola: una clausola porta più CoP e ognuno ha la sua
/// scala. E <b>tutte</b> le clausole, non solo quelle con una condizione — che è il filtro di
/// <see cref="TransferConditionRow"/>, giusto per quel controllo e sbagliato per questo.</para>
/// </summary>
/// <param name="LevelFeet">La quota <b>al trasferimento</b>, già in piedi.</param>
public sealed record TransferLadderRow(
    string AccCode, int ClauseId, string Cop, string? NextSectorCallsign,
    string OwningSectorCallsign, int? LevelFeet);

/// <summary>
/// La shape di un settore come arriva dalla sorgente, <b>grezza</b>.
///
/// <para>⚠️ Grezza e non già interpretata, ed è il punto: <c>PolygonGeometry.ParsePoints</c> ripara al volo
/// il contorno ripetuto, quindi chi guarda i punti già letti <b>non vede più l'anomalia</b>. Per raccontarla
/// bisogna leggere la stringa com'è.</para>
/// </summary>
/// <param name="Kind">«Settore ACC» o «Postazione d'aeroporto», per la riga a video.</param>
/// <param name="Position">Suffisso (CTR/FSS/TWR/APP/GND/DEL/ATIS): dice se una shape è attesa o no.</param>
public sealed record SectorShapeRow(string Kind, string Callsign, string? Position, string? RawPolygon, bool IsSynthetic);
