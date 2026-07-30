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
/// del documento (settore proprio → flusso → tabella) e nella vista Ridotta (risoluzione live del ricevente).
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

    public string Cop { get; set; } = default!;             // es. "VALMA", "—", "J1" (validazione soft)

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
