using Vipi.Domain;

namespace Vipi.Domain.Entities;

/// <summary>
/// Incarico editoriale assegnato a un editor: lavoro su un documento (vLOA/vIPI ACC/APP/Aeroporto via
/// <see cref="TargetType"/>+<see cref="TargetKey"/>, come le release) oppure LIBERO (target null). Traccia lo
/// stato di avanzamento e l'eventuale scadenza per ciclo AIRAC. Assegnato dagli admin o auto-assegnato dagli editor.
/// </summary>
public class EditorTask
{
    public int Id { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }

    public int AssigneeUserId { get; set; }                 // editor incaricato (UserId IVAO)
    public string? AssigneeName { get; set; }
    public int CreatedByUserId { get; set; }                // chi ha creato/assegnato

    public EditorTaskStatus Status { get; set; } = EditorTaskStatus.Todo;
    public EditorTaskPriority Priority { get; set; } = EditorTaskPriority.Normal;

    /// <summary>Ciclo AIRAC di scadenza ("YYNN"); null = senza scadenza. In ritardo se &lt; ciclo corrente e non Done.</summary>
    public string? DueAiracCycle { get; set; }

    /// <summary>Documento collegato (stesse chiavi delle release). null = incarico libero (non legato a un documento).</summary>
    public ReleaseTargetType? TargetType { get; set; }
    public string? TargetKey { get; set; }
    /// <summary>Etichetta leggibile del documento collegato (per l'elenco), es. "vLOA LIRR ↔ DAAA".</summary>
    public string? TargetLabel { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
}

/// <summary>
/// Concessione di editing: abilita un UserId a modificare TUTTI i documenti di una ACC (vIPI/aeroporto/vLOA,
/// topologia, trasferimenti). Gli admin (staff IT-AO*) non hanno bisogno di grant. PIANO sicurezza.
/// </summary>
public class EditGrant
{
    public int Id { get; set; }
    public int UserId { get; set; }                       // UserId IVAO abilitato
    public string? DisplayName { get; set; }           // nome opzionale per l'elenco admin
    public int AccId { get; set; }
    public Acc? Acc { get; set; }
    public int GrantedByUserId { get; set; }              // admin che ha concesso
    public DateTime GrantedAtUtc { get; set; }
}

/// <summary>
/// Staffista della divisione noto al sistema. Popolato al login di un membro con posizioni staff IT
/// e ri-verificato periodicamente via API IVAO (/v2/users/{UserId}). Alimenta il picker degli AOD/DIR
/// nella pagina permessi, evitando l'enumerazione dell'intera divisione (endpoint non disponibile
/// con token app). Chi non è più staff IT viene disattivato (IsActive=false).
/// </summary>
public class StaffMember
{
    public int UserId { get; set; }                       // PK = UserId IVAO (non auto-generato)
    public string? DisplayName { get; set; }           // nome dal login (claim) o publicNickname API
    public string? AtcRating { get; set; }             // es. "ACC" (dalla verifica API)
    public string StaffPositionsCsv { get; set; } = ""; // codici IT, es. "IT-AOA1,IT-T03"
    public bool IsActive { get; set; } = true;         // false = non più staffista IT
    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastLoginUtc { get; set; }
    public DateTime? LastVerifiedUtc { get; set; }     // ultima verifica via API
}

/// <summary>Tracciamento delle modifiche (chi, quando, cosa). SPEC_Modello_Dati §3.14.</summary>
public class AuditLog
{
    public long Id { get; set; }
    public int UserId { get; set; }                       // utente IVAO
    public AuditAction Action { get; set; }
    public string EntityType { get; set; } = default!; // es. "Document", "UnificationRule"
    public string EntityId { get; set; } = default!;
    public DateTime TimestampUtc { get; set; }
    public string? DetailsJson { get; set; }           // diff/contesto
}

/// <summary>
/// Flusso di traffico di un settore proprio nei coordinamenti (SPEC §7.4): es. «Roma NE · Traffico Dest LIRF».
/// Raggruppa una serie di punti di trasferimento (CoP/livello/ricevente). Reso nella sezione Coordinamenti
/// del documento (settore proprio → flusso → tabella) e nella vista live (risoluzione live del ricevente).
/// </summary>
public class TransferFlow
{
    public int Id { get; set; }
    public int AccId { get; set; }
    public Acc? Acc { get; set; }

    /// <summary>Settore proprio del documento a cui il flusso appartiene (es. LIRR_NE_CTR, un APP…).</summary>
    public int OwningSectorId { get; set; }
    public Sector? OwningSector { get; set; }

    public TransferFlowKind Kind { get; set; }              // Arrival/Departure/Overflight/Vfr/Other
    public string? AirportIcao { get; set; }                // dest (arrivi) / origine (partenze); null per OVF/VFR generici
    public string? AirportName { get; set; }                // nome per aeroporti fuori DB (nuovi/esteri); null se in DB (nome dal catalogo)
    public string? Description { get; set; }                // prosa "… trasferisce … riceve …"
    public int Order { get; set; }
    public byte[]? RowVersion { get; set; }

    public ICollection<TransferPoint> Points { get; set; } = new List<TransferPoint>();
}

/// <summary>
/// Riga della tabella di un <see cref="TransferFlow"/>: un Coordination Point con il suo vincolo di livello
/// e il settore ricevente (Next). Il livello è strutturato (valore + unità + vincolo) con escape «speciale»
/// (testo libero tipo «per aerovia»). Il ricevente live si risolve dal Next nominale risalendo la gerarchia
/// di copertura (ParentCallsign); se nessuno è online fino in cima → UNICOM.
/// </summary>
public class TransferPoint
{
    public int Id { get; set; }
    public int FlowId { get; set; }
    public TransferFlow? Flow { get; set; }

    /// <summary>Punto/rotta d'INGRESSO del traffico: es. "VALMA", "J1", una STAR, "ALL" (validazione soft).
    /// Dove passa il controllo lo dice <see cref="HandoffKind"/>: quando è <c>Unspecified</c> i due coincidono,
    /// che è il caso di un accordo ACC↔ACC.</summary>
    public string Cop { get; set; } = default!;

    // Livello AUTORIZZATO al punto d'ingresso («autorizza … a FL160 o superiore»). Su un accordo ACC↔ACC è anche
    // il livello al trasferimento, perché i due eventi coincidono; su un ACC→APP no — vedi HandoffLevel*.
    public int? LevelValue { get; set; }                    // 130 / 2500 (null = nessun livello / speciale)
    public LevelUnit LevelUnit { get; set; }                // Fl | Feet
    public LevelConstraint LevelConstraint { get; set; }    // AtOrAbove(↑) | AtOrBelow(↓) | Exact | Special
    public string? LevelSpecial { get; set; }               // testo se Constraint=Special (es. "per aerovia")
    public LevelParity Parity { get; set; }                 // Any | Even(pari) | Odd(dispari) — regola semicircolare

    // Stato verticale del traffico: parola «stabile/in discesa/in salita» nella frase. INDIPENDENTE dal vincolo di
    // livello (LevelConstraint): «a 130 o inferiore» è un bound, non implica una discesa. Unspecified = nessuna parola.
    public TransferVerticalState VerticalState { get; set; } // Unspecified | Level | Descending | Climbing

    public int? NextSectorId { get; set; }                  // ricevente nominale (settore reale); null = nessun ricevente → UNICOM
    public Sector? NextSector { get; set; }

    // Condizione operativa (livello variabile per pista/area/personalizzata). Tre dimensioni INDIPENDENTI e additive
    // (una riga può averle tutte): tutte null = riga sempre valida. Verità denormalizzata per il display (sopravvive a
    // rename/rimozione config e agli snapshot pubblicati). Più righe stessa CoP con condizioni diverse = varianti.
    public string? ConditionLabel { get; set; }             // PISTA/E in uso: può elencarne più ("16R / 16L")
    public int? ConditionRefId { get; set; }                // soft-ref opzionale pista singola: AirportRunwayRule.Id/RunwayRow.Id; nessun FK
    public string? ConditionAreaLabel { get; set; }         // AREA attiva (SpecialArea.Name)
    public string? ConditionCustomLabel { get; set; }       // condizione PERSONALIZZATA (testo libero)

    // ---- Faccetta TRASFERIMENTO (accordi ACC→APP) ----
    // Tutta opzionale: HandoffKind = Unspecified ⇒ il trasferimento coincide con l'ingresso e la riga si comporta
    // esattamente come prima (frase identica, colonne assenti). È l'invariante che rende sicure le righe storiche.

    public TransferHandoffKind HandoffKind { get; set; }    // Unspecified | Point | AorBoundary | Custom
    public string? HandoffLabel { get; set; }               // il fix, o il testo libero ("20 NM da AVN"); vuoto per AorBoundary

    // Livello AL TRASFERIMENTO, distinto da quello autorizzato: «autorizza a FL160 … trasferisce passando FL110».
    // Lo stato verticale di quel momento («in discesa») è già VerticalState e non si duplica qui.
    public int? HandoffLevelValue { get; set; }
    public LevelUnit HandoffLevelUnit { get; set; }
    // Nessun inizializzatore: il valore di riposo è lo zero dell'enum, come per gli altri campi della faccetta.
    // La forma di riferimento «passando FL110» (Exact) la propone l'EDITOR, non il modello — un default di
    // proprietà diverso dallo zero dell'enum si scontrerebbe con HasDefaultValue, che su valore CLR di default
    // omette la colonna in INSERT: una riga salvata AtOrAbove tornerebbe indietro come Exact.
    public LevelConstraint HandoffLevelConstraint { get; set; }

    // Trasferimento delle COMUNICAZIONI, quando avviene altrove rispetto al controllo. Vuoto = dove passa il controllo.
    public TransferHandoffKind CommsHandoffKind { get; set; }
    public string? CommsHandoffLabel { get; set; }

    // Restrizione di VELOCITÀ al trasferimento (nodi IAS, unità implicita). Unspecified = nessuna restrizione.
    public int? SpeedValue { get; set; }
    public SpeedConstraint SpeedConstraint { get; set; }

    // ---- VARIANTI ----
    // Righe dello stesso accordo che differiscono per condizione. Il gruppo è una chiave sulla riga, non una
    // tabella figlia: i consumatori a valle (matcher, bridge, vista live) continuano a vedere righe piatte, che
    // per loro è la lettura giusta — due varianti SONO due candidati distinti. Le righe di un gruppo condividono
    // flusso, Cop e NextSectorId.
    //
    // Un gruppo è un OUTLINE, non una capofila con subordinate: le alternative di primo livello sono PARI-GRADO
    // fra loro (pista 07 e pista 25 — nessuna è lo standard dell'altra) e ognuna può avere le proprie eccezioni,
    // che a loro volta possono averne. L'ordine (Order) È la struttura: nessun puntatore al padre.
    public int? VariantGroup { get; set; }                  // null = riga singola; progressivo per flusso

    /// <summary>Rientro della riga nel gruppo: 0 = alternativa di primo livello, 1 = sua eccezione, 2 =
    /// eccezione dell'eccezione, e così via senza limite. Una riga di profondità N appartiene all'ultima riga
    /// di profondità N-1 che la precede — come una lista puntata.</summary>
    public int VariantDepth { get; set; }

    /// <summary>La riga SCAVALCA le alternative: vale per tutto il gruppo, non per una capofila («di notte,
    /// qualunque pista»). Non partiziona come un'alternativa e non appartiene a nessuna: si rende in fondo al
    /// gruppo. Ha senso solo a profondità 0 — una riga che scavalca le alternative non può stare dentro una.</summary>
    public bool IsGroupWide { get; set; }

    public int Order { get; set; }
}

/// <summary>Dataset di riferimento per la validazione semantica dei riferimenti nav, legato all'AIRAC. SPEC §3.15.</summary>
public class NavReference
{
    public int Id { get; set; }
    public NavRefType Type { get; set; }
    public string Ident { get; set; } = default!;      // es. "BAVOM"
    public string AiracCycle { get; set; } = default!;
}

/// <summary>Whitelist per la validazione dei CoP: fix reali (nav-data) + convenzionali (es. J1). SPEC §7.6.</summary>
public class CoordinationPoint
{
    public int Id { get; set; }
    public string Ident { get; set; } = default!;      // es. "BAVOM", "J1"
    public CopKind Kind { get; set; }
    public string? AiracCycle { get; set; }            // per i Fix
}

/// <summary>Insieme di minime di vettoramento importate dal sectorfile GitHub. IMPLEMENTAZIONE FUTURE. SPEC §7.5.</summary>
public class VectoringMinimaSet
{
    public int Id { get; set; }
    public int? ScopeSectorId { get; set; }
    public Sector? ScopeSector { get; set; }
    public string Source { get; set; } = "SectorfileGitHub";
    public string SourceAiracCycle { get; set; } = default!;
    public string? SourceCommit { get; set; }
    public DateTime? ImportedAtUtc { get; set; }

    public ICollection<VectoringMinimaRow> Rows { get; set; } = new List<VectoringMinimaRow>();
}

/// <summary>Riga di una tabella di minime di vettoramento. IMPLEMENTAZIONE FUTURE. SPEC §7.5.</summary>
public class VectoringMinimaRow
{
    public int Id { get; set; }
    public int SetId { get; set; }
    public VectoringMinimaSet? Set { get; set; }
    public string AreaName { get; set; } = default!;
    public int MinimaFt { get; set; }
    public string? Note { get; set; }
}
