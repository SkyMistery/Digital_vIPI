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

    /// <summary>
    /// La segnalazione di sistema (<c>DocumentImpact</c>) da cui questo incarico è stato «preso in carico»;
    /// null = l'ha scritto una persona da zero. Carta
    /// <c>docs/feature/2026-08-26-da-fare-una-lista-sola.md</c> §2/D5.
    ///
    /// <para>⚠️ <b>Riferimento debole, senza chiave esterna</b>, ed è voluto: gli impatti si potano
    /// (<c>PruneClearedBeforeAsync</c> toglie i chiusi dopo due cicli AIRAC) e una FK farebbe sparire
    /// l'incarico insieme alla segnalazione che l'ha originato — cioè cancellerebbe l'impegno di una persona
    /// perché il sistema ha fatto pulizia. Serve solo a non mostrare due volte lo stesso lavoro: un Id che
    /// non risolve più significa «la segnalazione non c'è più», e la lista lo tratta già così.</para>
    /// </summary>
    public int? FromImpactId { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
}

/// <summary>
/// Promozione (o declassamento) a mano di una persona, per VID. Carta
/// <c>docs/feature/2026-08-28-autorizzazioni-a-livelli.md</c> §5.
///
/// <para><b>Non è il livello di quella persona: è solo la metà scritta a mano.</b> Il livello effettivo è
/// <c>max(quello garantito dalle posizioni staff IVAO, questo)</c> — e quel <c>max</c> è tutto il
/// meccanismo del pavimento: «nessuno si declassa sotto ciò che la sua posizione staff gli garantisce» non
/// è un controllo da scrivere, è ciò che <c>max</c> fa già. Una riga con un livello sotto il pavimento non
/// è vietata: è <b>inerte</b>.</para>
///
/// <para><b>Una riga per persona</b> (la chiave è il VID, non un id di comodo): declassare non aggiunge una
/// riga contraria, riscrive quella che c'è. Togliere del tutto una promozione è cancellare la riga.</para>
///
/// <para>⚠️ <b>La tabella si legge INTERA e si tiene in memoria</b> (<c>IRoleOverrides</c>): poche decine di
/// righe, e la domanda «che livello ha questa persona?» arriva a <b>ogni</b> richiesta. Una <c>SELECT</c>
/// per richiesta rimetterebbe nel layout la query che questa funzione toglie — cioè la causa prima delle
/// corse sul <c>DbContext</c> di circuito.</para>
/// </summary>
public class RoleOverride
{
    /// <summary>VID IVAO della persona. È la chiave: una riga per persona, non un id di comodo.</summary>
    public int UserId { get; set; }

    /// <summary>Il livello assegnato a mano. Vale solo se è <b>sopra</b> quello garantito dallo staff.</summary>
    public VipiRole Level { get; set; }

    /// <summary>Chi ha firmato la promozione (VID dell'admin).</summary>
    public int GrantedByUserId { get; set; }

    public DateTime GrantedAtUtc { get; set; }

    /// <summary>Perché, in una riga. Facoltativa, ma è l'unica cosa che spiega la riga fra sei mesi.</summary>
    public string? Note { get; set; }

    /// <summary>Nome per l'elenco, come per gli altri: il VID da solo non dice chi è.</summary>
    public string? DisplayName { get; set; }
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
