using Vipi.Domain;

namespace Vipi.Domain.Entities;

/// <summary>
/// Un **accordo di coordinamento**: fra due parti, per un tipo di traffico, con le sue clausole — e fino a due
/// direzioni. Sostituisce la coppia <c>TransferFlow</c> + <c>TransferPoint</c>, che descriveva «un flusso di UN
/// settore verso UN aeroporto, con UN punto e UN ricevente per riga».
///
/// <para><b>Perché questa forma.</b> È quella dei documenti veri. Nel <i>Common Format LoA</i> EUROCONTROL
/// (Annex D.2) la tabella è <c>ATS-Route │ COP │ Level Allocation │ Special Conditions</c>, **una per
/// direzione**; nella LoA ACC Roma ↔ Marseille gli aeroporti stanno raccolti in un gruppo
/// (<c>LIRF-LIRA-LIRU-LIRE</c>) e i punti in un elenco; nell'IPI ENAV una frase introduce una tabella di
/// clausole. Il modello precedente costringeva a moltiplicare le righe per aeroporto, per punto, per settore
/// mittente, per direzione e per ACC — e la moltiplicazione per direzione era già degenerata: i sorvoli
/// LIBB↔LGGG in archivio elencano punti diversi nei due versi, e niente lo segnalava.</para>
///
/// <para><b>Cosa vedono i consumatori.</b> Niente di tutto questo: <c>AgreementExpansion</c> proietta l'accordo
/// nelle righe piatte di sempre (<c>TransferFlowRow</c>/<c>TransferPointRow</c>), che restano la forma letta da
/// derivazione, frasi, tabelle, vista live e matcher Aurora. Stesso schema dei settori: cataloghi = fonte
/// unica, proiezione a valle.</para>
///
/// <para>Carta e registro delle lacune: <c>docs/feature/2026-08-16-accordi-di-coordinamento.md</c>.</para>
/// </summary>
public class CoordinationAgreement
{
    public int Id { get; set; }

    /// <summary>
    /// ACC responsabile dell'accordo. Serve **solo all'autorizzazione** (<c>EnsureCanEditAccAsync</c>): la
    /// visibilità nei documenti non passa più di qui ma dalle PARTI, così un accordo di confine non può essere
    /// invisibile a uno dei suoi due capi — che è ciò che succedeva quando i flussi vivevano nel «secchio» di
    /// una ACC sola e un centro estero confinante con due ACC italiane andava riscritto due volte.
    /// </summary>
    public int OwnerAccId { get; set; }
    public Acc? OwnerAcc { get; set; }

    /// <summary>Che traffico riguarda: arrivi/partenze a un aeroporto, sorvoli, VFR, generico.</summary>
    public TransferFlowKind TrafficKind { get; set; }

    /// <summary>Prosa libera dell'accordo (il «Description» del flusso di prima).</summary>
    public string? Description { get; set; }

    public int Order { get; set; }
    // Nessun RowVersion: last-write-wins voluto sotto il lock di editing (14 agosto 2026). Vedi VipiDbContext.

    public ICollection<AgreementParty> Parties { get; set; } = new List<AgreementParty>();
    public ICollection<AgreementAirport> Airports { get; set; } = new List<AgreementAirport>();
    public ICollection<AgreementClause> Clauses { get; set; } = new List<AgreementClause>();
}

/// <summary>
/// Un ente a un capo dell'accordo. Più righe sullo stesso lato = l'accordo vale per tutti quei settori, ed è
/// come lo scrivono i documenti veri («TS EXE trasferisce a PS EXE / PN EXE»). Prima serviva un flusso per
/// settore mittente, e in pratica lo si scriveva su uno solo — lasciando mute le vIPI degli altri.
/// <para>Lato B senza nessuno = il traffico va rilasciato a UNICOM: non è un errore di modello, è un accordo
/// incompleto, e come tale va segnalato in editor (com'è oggi il badge «nessun ricevente»).</para>
/// </summary>
public class AgreementParty
{
    public int Id { get; set; }
    public int AgreementId { get; set; }
    public CoordinationAgreement? Agreement { get; set; }

    public AgreementSide Side { get; set; }

    public int SectorId { get; set; }
    public Sector? Sector { get; set; }

    public int Order { get; set; }
}

/// <summary>
/// Un aeroporto a cui l'accordo si applica. Zero righe = accordo senza aeroporto (sorvolo/VFR/altro); più righe
/// = lo stesso accordo per più scali, che è il caso reale («Dest LIEE–LIED», «LIRF-LIRA-LIRU-LIRE») e che prima
/// costava un flusso per aeroporto — quattro copie identiche in archivio per i soli arrivi via ASPIR.
/// </summary>
public class AgreementAirport
{
    public int Id { get; set; }
    public int AgreementId { get; set; }
    public CoordinationAgreement? Agreement { get; set; }

    public string Icao { get; set; } = default!;

    /// <summary>Nome per gli aeroporti fuori catalogo (nuovi/esteri); null se l'ICAO è in catalogo, da dove il
    /// nome arriva da sé.</summary>
    public string? Name { get; set; }

    public int Order { get; set; }
}

/// <summary>
/// Una clausola dell'accordo: i punti d'ingresso a cui si applica, il livello, la faccetta trasferimento, la
/// condizione. È l'ex <c>TransferPoint</c> con tre differenze e nient'altro — tutto il resto (livello, parità,
/// stato verticale, faccetta, velocità, condizione a tre dimensioni, outline delle varianti) è lo stesso campo
/// con lo stesso significato.
/// </summary>
public class AgreementClause
{
    public int Id { get; set; }
    public int AgreementId { get; set; }
    public CoordinationAgreement? Agreement { get; set; }

    /// <summary>
    /// In quale verso vale la clausola. È la differenza che rende il bilaterale **un accordo solo**: l'insieme
    /// dei punti di confine si scrive una volta e ogni direzione porta i propri livelli, come Annex D.2 (una
    /// tabella per verso) ed E.3 (una colonna di comunicazioni per verso).
    /// <para>«L'accordo è bilaterale» non è un flag e non si salva: è «ha clausole nei due versi», quindi non
    /// c'è niente da tenere d'accordo con nient'altro.</para>
    /// </summary>
    public AgreementDirection Direction { get; set; }

    /// <summary>
    /// I punti/rotte d'ingresso a cui la clausola si applica, **in elenco** (vedi <c>CopList</c> per il
    /// formato). Prima era un CoP solo, e sette punti sullo stesso livello erano sette righe: in archivio i
    /// sorvoli verso Atene sono esattamente questo. I token speciali restano interi — <c>ALL</c>,
    /// <c>ALL to X</c>, un intervallo di aerovie come <c>Y01-Y12</c>, una STAR come <c>TOPNO 3A</c>.
    /// </summary>
    public string Cops { get; set; } = default!;

    // ⚠️ Nessun ricevente qui: è il lato opposto dell'accordo. Prima ogni riga se lo ripeteva, e righe dello
    // stesso flusso potevano contraddirsi puntando a enti diversi — cosa che in archivio succede (gli arrivi
    // LIRN vanno per metà all'APP e per metà al CTR), e che sono due accordi, non uno.

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
    // Clausole dello stesso accordo che differiscono per condizione, organizzate a OUTLINE: le alternative di
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
