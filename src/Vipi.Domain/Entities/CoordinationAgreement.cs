using Vipi.Domain;

namespace Vipi.Domain.Entities;

/// <summary>
/// Un **accordo di coordinamento**: la RELAZIONE fra due enti, e basta. Uno solo per coppia, sempre
/// bidirezionale. Il traffico — arrivi, partenze, sorvoli — sta nelle <see cref="AgreementSection"/> che
/// contiene.
///
/// <para><b>Perché questa forma.</b> Fino al 18 agosto 2026 l'identità di un accordo era «due parti · un tipo ·
/// un gruppo di aeroporti», e la stessa coppia di enti compariva in tanti accordi quanti erano i tipi e i gruppi
/// di scali: sul <c>vipi.db</c> vero <b>40 accordi stavano in 17 coppie</b>, e la sola
/// <c>LGGG_W_CTR ⇄ LIBB_ES_CTR</c> ne teneva otto. Chi voleva vedere «cosa ho concordato con Atene» apriva otto
/// schede. Peggio: il <b>verso</b> si esprimeva ORIENTANDO l'accordo (60 clausole su 60 erano <c>AtoB</c>),
/// quindi i due sensi della stessa relazione finivano in due accordi diversi e nessuno vedeva che il reciproco
/// esisteva già.</para>
///
/// <para><b>Cosa vedono i consumatori.</b> Niente di tutto questo: <c>AgreementExpansion</c> proietta le sezioni
/// nelle righe piatte di sempre (<c>TransferFlowRow</c>/<c>TransferPointRow</c>), che restano la forma letta da
/// derivazione, frasi, tabelle, vista live e matcher Aurora. Stesso schema dei settori: cataloghi = fonte unica,
/// proiezione a valle.</para>
///
/// <para>Carte: <c>docs/feature/2026-08-18-accordi-a-sezioni.md</c> (questa forma),
/// <c>docs/feature/2026-08-16-accordi-di-coordinamento.md</c> (la precedente, storia valida nelle decisioni).</para>
/// </summary>
public class CoordinationAgreement
{
    public int Id { get; set; }

    /// <summary>
    /// ACC responsabile dell'accordo. Serve **solo all'autorizzazione** (<c>EnsureCanEditAccAsync</c>): la
    /// visibilità nei documenti non passa di qui ma dai due LATI, così un accordo di confine non può essere
    /// invisibile a uno dei suoi due capi — che è ciò che succedeva quando i flussi vivevano nel «secchio» di
    /// una ACC sola e un centro estero confinante con due ACC italiane andava riscritto due volte.
    /// </summary>
    public int OwnerAccId { get; set; }
    public Acc? OwnerAcc { get; set; }

    /// <summary>
    /// Un capo dell'accordo. **Un solo ente per lato**: sul <c>vipi.db</c> vero non è mai stato usato altro, e
    /// la collezione costava un prodotto cartesiano in proiezione, una tabella, un ordine e un picker multiplo.
    /// <para>⚠️ Il prezzo è dichiarato, non subito: la forma «TS EXE trasferisce a PS EXE / PN EXE» dei
    /// documenti reali si scrive come <b>due accordi</b>. È una scelta del committente del 18 agosto 2026.</para>
    /// <para>⚠️ I due lati stanno in forma <b>canonica</b> — <c>SideASectorId &lt; SideBSectorId</c> — perché
    /// l'unicità della coppia è un indice, e in SQL non esiste «insieme di due». Girare i lati è
    /// un'operazione <b>senza perdita</b> solo perché il verso vive sulla SEZIONE e si ribalta con loro: quando
    /// il verso stava sulla clausola, scambiare A e B capovolgeva il significato di tutto, ed era vietato.</para>
    /// </summary>
    public int SideASectorId { get; set; }
    public Sector? SideASector { get; set; }

    /// <inheritdoc cref="SideASectorId"/>
    public int SideBSectorId { get; set; }
    public Sector? SideBSector { get; set; }

    /// <summary>Nota libera sull'accordo. Era <c>Description</c>, e si chiamava come un campo che il documento
    /// rende: non lo è mai stato — la mostra solo il navigatore dell'editor. La prosa che introduce una tabella
    /// sta sulla sezione (<see cref="AgreementSection.Description"/>), dove i documenti veri la scrivono.</summary>
    public string? Note { get; set; }

    public int Order { get; set; }
    // Nessun RowVersion: last-write-wins voluto sotto il lock di editing (14 agosto 2026). Vedi VipiDbContext.

    public ICollection<AgreementSection> Sections { get; set; } = new List<AgreementSection>();
}

/// <summary>
/// Una **sezione** dell'accordo: un tipo di traffico, in un verso, per un gruppo di aeroporti — cioè una
/// tabella di clausole. «Arrivi verso LIBD·LIBR», «partenze da LIRF», «sorvoli A→B», «sorvoli B→A».
///
/// <para><b>Il verso sta qui, ed è un dato.</b> Una sezione «arrivi verso LIRF» ha un verso solo: cede chi non
/// ha LIRF, riceve chi ce l'ha. Non si ricalcola a ogni lettura (l'AoR cambia, l'accordo scritto no): si
/// propone quando la sezione nasce, e poi si salva. I <b>sorvoli</b> sono l'unico caso con due sezioni
/// speculari, ed è per questo che l'editor le mostra sempre in coppia — l'interruttore nascondeva ciò che
/// mancava, e per questo il reciproco non si scriveva mai.</para>
///
/// <para>⚠️ <b>Il nome.</b> In questo codice «sezione» è già la sezione di un DOCUMENTO
/// (<see cref="DocumentSection"/>, <c>SectionCatalog</c>, <c>SectionView</c>). Questa si chiama
/// <b>AgreementSection</b> sempre, per intero: è l'unico modo perché fra sei mesi «sezione» resti cercabile.</para>
/// </summary>
public class AgreementSection
{
    public int Id { get; set; }
    public int AgreementId { get; set; }
    public CoordinationAgreement? Agreement { get; set; }

    /// <summary>Che traffico riguarda: arrivi/partenze a un aeroporto, sorvoli, VFR, generico. Era il tipo
    /// dell'accordo.</summary>
    public TransferFlowKind Kind { get; set; }

    /// <summary>In quale verso vale la sezione. Era sulla clausola, e lì costringeva a tenere d'accordo il verso
    /// di righe che dicono la stessa cosa.</summary>
    public AgreementDirection Direction { get; set; }

    /// <summary>Prosa che introduce la tabella (l'IPI ENAV la scrive così). Era <c>Description</c> dell'accordo,
    /// dove valeva per tutti i tipi insieme.</summary>
    public string? Description { get; set; }

    public int Order { get; set; }

    public ICollection<AgreementAirport> Airports { get; set; } = new List<AgreementAirport>();
    public ICollection<AgreementClause> Clauses { get; set; } = new List<AgreementClause>();
}

/// <summary>
/// Un aeroporto a cui la sezione si applica. Zero righe = sezione senza aeroporto (sorvoli; VFR/Altro possono
/// averne, facoltativi); più righe = lo stesso traffico per più scali, che è il caso reale («Dest LIEE–LIED»,
/// «LIRF-LIRA-LIRU-LIRE»).
/// </summary>
public class AgreementAirport
{
    public int Id { get; set; }
    public int SectionId { get; set; }
    public AgreementSection? Section { get; set; }

    public string Icao { get; set; } = default!;

    /// <summary>Nome per gli aeroporti fuori catalogo (nuovi/esteri); null se l'ICAO è in catalogo, da dove il
    /// nome arriva da sé.</summary>
    public string? Name { get; set; }

    public int Order { get; set; }
}

/// <summary>
/// Una clausola della sezione: i punti d'ingresso a cui si applica, il livello, la faccetta trasferimento, la
/// condizione. Rispetto al modello di ferragosto cambia <b>due campi</b>: appende alla sezione invece che
/// all'accordo, e non porta più il verso — lo dice la sezione.
/// </summary>
public class AgreementClause
{
    public int Id { get; set; }
    public int SectionId { get; set; }
    public AgreementSection? Section { get; set; }

    /// <summary>
    /// I punti/rotte d'ingresso a cui la clausola si applica, **in elenco** (vedi <c>CopList</c> per il
    /// formato). I token speciali restano interi — <c>ALL</c>, <c>ALL to X</c>, un intervallo di aerovie come
    /// <c>Y01-Y12</c>, una STAR come <c>TOPNO 3A</c>.
    /// </summary>
    public string Cops { get; set; } = default!;

    // ⚠️ Nessun ricevente qui: è il lato opposto dell'accordo, e il verso lo sceglie la sezione. Prima ogni riga
    // se lo ripeteva, e righe dello stesso flusso potevano contraddirsi puntando a enti diversi.

    // Livello AUTORIZZATO al punto d'ingresso. Su un accordo ACC↔ACC è anche il livello al trasferimento,
    // perché i due eventi coincidono; su un ACC→APP no — vedi la faccetta più sotto.
    public int? LevelValue { get; set; }
    public LevelUnit LevelUnit { get; set; }
    public LevelConstraint LevelConstraint { get; set; }
    public string? LevelSpecial { get; set; }
    public LevelParity Parity { get; set; }

    /// <summary>Stato verticale del traffico: la parola «stabile/in discesa/in salita» della frase. INDIPENDENTE
    /// dal vincolo di livello — «a 130 o inferiore» è un bound, non una discesa.</summary>
    public TransferVerticalState VerticalState { get; set; }

    // Condizione operativa: tre dimensioni INDIPENDENTI e additive (una clausola può averle tutte); tutte null =
    // sempre valida. Verità denormalizzata per il display: sopravvive a rename/rimozione della config e agli
    // snapshot pubblicati.
    public string? ConditionLabel { get; set; }             // pista/e in uso ("16R / 16L")
    public int? ConditionRefId { get; set; }                // soft-ref pista singola; nessun FK
    public string? ConditionAreaLabel { get; set; }         // area attiva
    public string? ConditionCustomLabel { get; set; }       // condizione personalizzata

    // ---- Faccetta TRASFERIMENTO ----
    // Unspecified ⇒ il trasferimento coincide con l'ingresso e la clausola si comporta come un accordo ACC↔ACC.
    public TransferHandoffKind HandoffKind { get; set; }
    public string? HandoffLabel { get; set; }
    public int? HandoffLevelValue { get; set; }
    public LevelUnit HandoffLevelUnit { get; set; }
    public LevelConstraint HandoffLevelConstraint { get; set; }

    // Trasferimento delle COMUNICAZIONI, quando avviene altrove rispetto al controllo.
    public TransferHandoffKind CommsHandoffKind { get; set; }
    public string? CommsHandoffLabel { get; set; }

    // Restrizione di VELOCITÀ al trasferimento (nodi IAS).
    public int? SpeedValue { get; set; }
    public SpeedConstraint SpeedConstraint { get; set; }

    // ---- VARIANTI ----
    // Clausole della stessa sezione che differiscono per condizione, organizzate a OUTLINE: le alternative di
    // primo livello sono pari-grado (pista 07 · pista 25, nessuna è lo standard dell'altra), le eccezioni si
    // annidano a profondità libera, e una clausola può scavalcarle tutte. L'ordine È la struttura.
    /// <summary>null = clausola singola; progressivo per accordo.</summary>
    public int? VariantGroup { get; set; }
    /// <summary>0 = alternativa di primo livello, 1 = sua eccezione, 2 = eccezione dell'eccezione, …
    /// Una clausola di profondità N appartiene all'ultima di profondità N-1 che la precede.</summary>
    public int VariantDepth { get; set; }
    /// <summary>La clausola scavalca le alternative: vale per tutto il gruppo, e si rende in fondo.</summary>
    public bool IsGroupWide { get; set; }

    public int Order { get; set; }
}
