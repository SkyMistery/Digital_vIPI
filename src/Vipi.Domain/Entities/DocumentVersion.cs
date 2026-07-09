namespace Vipi.Domain.Entities;

/// <summary>Versione immutabile di un documento (audit + diff). SPEC §3.11.</summary>
public class DocumentVersion
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public Document? Document { get; set; }
    public int VersionNumber { get; set; }             // progressivo per documento
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
    public int CreatedByUserId { get; set; }              // autore (UserId IVAO)
    public DateTime CreatedUtc { get; set; }
    public string AiracCycle { get; set; } = default!;
    public string? Note { get; set; }                  // changelog

    public ICollection<DocumentSection> Sections { get; set; } = new List<DocumentSection>();
    public ICollection<ContentBlock> Blocks { get; set; } = new List<ContentBlock>();
}
