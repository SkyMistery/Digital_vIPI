namespace Vipi.Domain.Entities;

/// <summary>Sezione ad albero (annidamento max 3 livelli). Genera la TOC dinamica. SPEC §7.1.</summary>
public class DocumentSection
{
    public int Id { get; set; }
    public int DocumentVersionId { get; set; }
    public DocumentVersion? DocumentVersion { get; set; }
    public int? ParentSectionId { get; set; }          // null = sezione radice
    public DocumentSection? ParentSection { get; set; }
    public string Title { get; set; } = default!;
    public int Order { get; set; }                     // ordine tra fratelli
    public int Depth { get; set; }                     // 0 = radice … max 3 (vincolo applicativo)
    public string SectionKey { get; set; } = "custom"; // chiave SectionCatalog (ex enum BlockSection); "custom" = editoriale generica

    /// <summary>Solo per le sezioni DERIVABILI (doc 10 §3a): Frozen = output congelato nello snapshot; Live = derivata
    /// sempre corrente al view. Le sezioni statiche restano Frozen. Default Frozen (l'editor imposta Live dove serve).</summary>
    public RenderMode RenderMode { get; set; } = RenderMode.Frozen;

    public byte[]? RowVersion { get; set; }                // concorrenza ottimistica in editing

    public ICollection<DocumentSection> Children { get; set; } = new List<DocumentSection>();
    public ICollection<ContentBlock> Blocks { get; set; } = new List<ContentBlock>();

    /// <summary>Profondità massima consentita per l'albero delle sezioni (SPEC §7.1).</summary>
    public const int MaxDepth = 3;
}
