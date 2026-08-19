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
