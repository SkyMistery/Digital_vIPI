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
public sealed record ConsistencyFinding(string Category, ConsistencySeverity Severity, string Entity,
    string Detail, ConsistencyArea Area, string? Where = null,
    string? CategoryKey = null, string? DetailKey = null, object[]? DetailArgs = null);

/// <summary>Condizione di una clausola di accordo (soft-ref a pista/area denormalizzate).</summary>
/// <param name="Points">I punti della clausola, come si leggono: servono solo a dire QUALE clausola nel
/// messaggio del report.</param>
public sealed record TransferConditionRow(int ClauseId, string AccCode, string Points,
    int? ConditionRefId, string? ConditionLabel, string? ConditionAreaLabel);

/// <summary>Nodo dei cataloghi che dichiara un padre di copertura per callsign (soft-ref cross-catalogo, no FK).</summary>
public sealed record ParentRefRow(string Kind, string Reference, string ParentCallsign);

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

    /// <summary>Callsign validi come padre (union delle chiavi naturali dei cataloghi ACC/aeroporto).</summary>
    public IReadOnlySet<string> ValidCallsigns { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Sezioni <c>regulated</c> con la selezione di aree salvata (JSON grezzo).</summary>
    public IReadOnlyList<RegulatedRefRow> RegulatedRefs { get; init; } = Array.Empty<RegulatedRefRow>();

    /// <summary>IvaoId delle aree speciali esistenti, per validare gli id salvati nelle selezioni.</summary>
    public IReadOnlySet<string> SpecialAreaIds { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
