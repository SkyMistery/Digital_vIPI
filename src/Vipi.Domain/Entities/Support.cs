namespace Vipi.Domain.Entities;

/// <summary>Tracciamento delle modifiche (chi, quando, cosa). SPEC_Modello_Dati §3.14.</summary>
public class AuditLog
{
    public long Id { get; set; }
    public int Vid { get; set; }                       // utente IVAO
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
