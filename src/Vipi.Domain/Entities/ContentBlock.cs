namespace Vipi.Domain.Entities;

/// <summary>Unità minima di documentazione. Cuore del modello di visibilità (PIANO §20). SPEC §3.12 + §7.2.</summary>
public class ContentBlock
{
    public int Id { get; set; }
    public int DocumentVersionId { get; set; }
    public DocumentVersion? DocumentVersion { get; set; }
    public int SectionId { get; set; }                 // FK→DocumentSection (ex enum Section)
    public DocumentSection? Section { get; set; }
    public int Order { get; set; }
    public BlockTier Tier { get; set; }
    public BlockFormat Format { get; set; }
    public BlockVisibility Visibility { get; set; }
    public bool CollapsedByDefault { get; set; }       // collasso di presentazione in vista ridotta
    public CalloutKind? CalloutKind { get; set; }      // solo se Format=Callout

    public int? ScopeSectorId { get; set; }            // settore a cui il blocco si riferisce
    public Sector? ScopeSector { get; set; }
    public int? FromSectorId { get; set; }             // solo coordinamenti (Handoff relazionale)
    public Sector? FromSector { get; set; }
    public int? ToSectorId { get; set; }               // solo coordinamenti
    public Sector? ToSector { get; set; }

    public int? SharedBlockId { get; set; }            // se riusato per riferimento
    public SharedBlock? SharedBlock { get; set; }
    public string? Body { get; set; }                  // Markdown (prosa); null se usa SharedBlock
    public string? BodyJson { get; set; }              // struttura tabellare (Format=Table)
    public byte[]? RowVersion { get; set; }            // concorrenza ottimistica in editing
}
