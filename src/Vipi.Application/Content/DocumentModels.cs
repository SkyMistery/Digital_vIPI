using Vipi.Domain;

namespace Vipi.Application.Content;

// ---- Modello "grezzo" caricato dal repository (struttura + contenuti, senza decisioni di resa) ----

/// <summary>Documento grezzo caricato dal DB: albero di sezioni con i blocchi non filtrati.</summary>
public sealed class RawDocument
{
    public required string Title { get; init; }
    public required string AiracCycle { get; init; }
    public required IReadOnlyList<RawSection> Roots { get; init; }
}

public sealed class RawSection
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public required int Depth { get; init; }
    public required string SectionKey { get; init; }
    public required int Order { get; init; }

    /// <summary>Modalità di resa della sezione (doc 10 §3a): viaggia nello snapshot così cattura e viewer sanno se
    /// congelare/leggere-frozen (Frozen) o derivare live (Live). Default Frozen per retro-compat degli snapshot vecchi.</summary>
    public RenderMode RenderMode { get; init; } = RenderMode.Frozen;

    public List<RawBlock> Blocks { get; init; } = new();
    public List<RawSection> Children { get; init; } = new();
}

public sealed class RawBlock
{
    public required int Id { get; init; }
    public required int Order { get; init; }
    public required BlockFormat Format { get; init; }
    public required BlockVisibility Visibility { get; init; }
    public required BlockTier Tier { get; init; }
    public string? ScopeSectorKey { get; init; }
    public string? Body { get; init; }
    public string? BodyJson { get; init; }
    public CalloutKind? CalloutKind { get; init; }
}

// ---- View model reso (filtrato per Tier, con stato espanso/compresso) ----

/// <summary>Documento pronto per la UI: sezioni filtrate + blocchi con stato di resa.</summary>
public sealed class DocumentView
{
    public required string Title { get; init; }
    public required string AiracCycle { get; init; }
    public required IReadOnlyList<SectionView> Sections { get; init; }
}

public sealed class SectionView
{
    public required string Id { get; init; }            // ancora per deep-link (es. "s-6")
    public required string Title { get; init; }
    public required int Depth { get; init; }
    public required string SectionKey { get; init; }
    public required IReadOnlyList<BlockView> Blocks { get; init; }
    public required IReadOnlyList<SectionView> Children { get; init; }
}

public sealed class BlockView
{
    public required int Id { get; init; }
    public required BlockFormat Format { get; init; }
    public required RenderState State { get; init; }
    public string? CollapseLabel { get; init; }
    public string? Body { get; init; }
    public string? BodyJson { get; init; }
    public CalloutKind? CalloutKind { get; init; }
}
