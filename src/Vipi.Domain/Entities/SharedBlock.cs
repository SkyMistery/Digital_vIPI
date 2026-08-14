namespace Vipi.Domain.Entities;

/// <summary>Contenuto condiviso per riferimento (modifica una volta, aggiorna ovunque). SPEC §3.13.</summary>
public class SharedBlock
{
    public int Id { get; set; }
    public string Key { get; set; } = default!;        // univoco, es. "minime-separazione-generali"
    public string Title { get; set; } = default!;
    public BlockFormat Format { get; set; }
    public string? Body { get; set; }
    public string? BodyJson { get; set; }
    // Nessun RowVersion: last-write-wins voluto (14 ago 2026). Vedi VipiDbContext, commento su SharedBlock.
}
