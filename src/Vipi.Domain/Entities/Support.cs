namespace Vipi.Domain.Entities;

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

    public int? NextSectorId { get; set; }                  // ricevente nominale (settore reale); null = nessun ricevente → UNICOM
    public Sector? NextSector { get; set; }

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
