using Vipi.Domain;

namespace Vipi.Application.Content;

// ---- Modello "grezzo" caricato dal repository (struttura + contenuti, senza decisioni di resa) ----

/// <summary>Documento grezzo caricato dal DB: albero di sezioni con i blocchi non filtrati.</summary>
public sealed class RawDocument
{
    public required string Title { get; init; }
    public required string AiracCycle { get; init; }
    public required IReadOnlyList<RawSection> Roots { get; init; }

    /// <summary>
    /// La lingua in cui il documento è SCRITTO. Serve a sapere da dove si traduce.
    /// <para>⚠️ <b>Nullable, e non per pigrizia</b>: gli snapshot pubblicati prima del 28 agosto 2026 non la
    /// portano, e un default farebbe dire a una vLOA — che nasce in inglese — di essere italiana. Il viewer
    /// tradurrebbe testo inglese come se fosse italiano. null = non si sa, quindi non si traduce.</para>
    /// </summary>
    public Language? Language { get; init; }

    /// <summary>
    /// Le traduzioni <b>congelate</b> con questa release: lingua bersaglio → (impronta del testo sorgente →
    /// traduzione).
    ///
    /// <para>
    /// ⚠️ <b>Perché viaggiano nello snapshot e non si leggono dal vivo.</b> La memoria è indicizzata sulla
    /// FRASE, quindi una correzione fatta oggi sul documento di Roma cambierebbe anche l'inglese già
    /// pubblicato di Milano — sotto gli occhi di chi lo sta leggendo, e senza che il suo editor abbia
    /// pubblicato niente. Congelandole qui, il raggio d'azione di una correzione resta limitato: gli altri
    /// documenti la vedono alla LORO prossima ripubblicazione, quando il loro editor guarda il diff.
    /// </para>
    /// <para>
    /// null o vuoto = niente congelato: il viewer ricade sulla memoria viva. È il comportamento delle
    /// release pubblicate prima di questa funzione, ed è quello giusto per una bozza.
    /// </para>
    /// </summary>
    public Dictionary<string, Dictionary<string, string>>? Translations { get; init; }
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

    /// <summary>Sezione nascosta dal documento pubblicato (doc 11 §3c): viaggia nello snapshot, così la release
    /// congela anche la scelta di nascondere.</summary>
    public bool IsHidden { get; init; }

    /// <summary>Sotto-sezione resa prima del corpo del padre (doc 11 §3g). Viaggia nello snapshot con gli altri flag.</summary>
    public bool BeforeParentBody { get; init; }

    /// <summary>A chi si rivolge la sezione (carta vSOP militari §3). Viaggia nello snapshot con gli altri
    /// flag. <c>Both</c> = per tutti, ed è il default: nessun documento cambia finché nessuno marca.</summary>
    public SectionAudience Audience { get; init; }

    /// <summary>Prosa a CAPOFILA: una frase che introduce la tabella invece di una per clausola.</summary>
    public bool LeadSentence { get; init; }

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

    /// <inheritdoc cref="RawDocument.Language"/>
    public Language? Language { get; init; }

    /// <inheritdoc cref="RawDocument.Translations"/>
    public Dictionary<string, Dictionary<string, string>>? Translations { get; init; }
}

public sealed class SectionView
{
    public required string Id { get; init; }            // ancora per deep-link (es. "s-6")
    public required string Title { get; init; }
    public required int Depth { get; init; }
    public required string SectionKey { get; init; }

    /// <summary>Sezione nascosta dal documento pubblicato (doc 11 §3c). I viewer la omettono in pubblica/release e la
    /// marcano in anteprima bozza.</summary>
    public bool IsHidden { get; init; }

    /// <summary>Sotto-sezione resa prima del corpo del padre (doc 11 §3g).</summary>
    public bool BeforeParentBody { get; init; }

    /// <summary>A chi si rivolge la sezione (carta vSOP militari §3). Viaggia nello snapshot con gli altri
    /// flag. <c>Both</c> = per tutti, ed è il default: nessun documento cambia finché nessuno marca.</summary>
    public SectionAudience Audience { get; init; }

    /// <summary>Prosa a CAPOFILA: una frase che introduce la tabella invece di una per clausola.</summary>
    public bool LeadSentence { get; init; }

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
