namespace Vipi.Domain.Entities;

/// <summary>Un documento vIPI o vLOA. I contenuti vivono nelle versioni. SPEC_Modello_Dati §3.9.</summary>
public class Document
{
    public int Id { get; set; }
    public DocumentType Type { get; set; }
    public string Title { get; set; } = default!;
    public Language Language { get; set; }             // It (vIPI) | En (vLOA) — fisso
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
    public int? CurrentVersionId { get; set; }         // versione pubblicata corrente
    public DocumentVersion? CurrentVersion { get; set; }
    public DateTime LastUpdatedUtc { get; set; }
    public string LastUpdatedAiracCycle { get; set; } = default!; // calcolato da AiracService, es. "2606"
    public int? FeaturedRank { get; set; }             // ordine "in evidenza" (1..3) nella card vLOA della landing ACC; null = non in evidenza

    /// <summary>Nascosto dal pubblico (reversibile): il documento resta con la sua storia ma i loader pubblici lo escludono.</summary>
    public bool IsHidden { get; set; }

    public byte[]? RowVersion { get; set; }

    // Lock di editing esclusivo (PIANO sicurezza): impedisce a due editor di lavorare lo stesso documento.
    public int? LockedByUserId { get; set; }
    public string? LockedByName { get; set; }
    public DateTime? LockedAtUtc { get; set; }
    public DateTime? LockExpiresUtc { get; set; }

    public ICollection<DocumentParty> Parties { get; set; } = new List<DocumentParty>();
    public ICollection<DocumentVersion> Versions { get; set; } = new List<DocumentVersion>();

    /// <summary>Settori descritti da questo documento (uno-a-molti). Per le vIPI; uno è IsPrimary. SPEC §3.9.</summary>
    public ICollection<Sector> Sectors { get; set; } = new List<Sector>();
}
