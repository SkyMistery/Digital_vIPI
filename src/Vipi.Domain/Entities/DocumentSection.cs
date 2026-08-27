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
    public string SectionKey { get; set; } = "custom"; // chiave SectionCatalog (ex enum BlockSection); "custom:{guid8}" = libera

    /// <summary>Solo per le sezioni DERIVABILI (doc 10 §3a): Frozen = output congelato nello snapshot; Live = derivata
    /// sempre corrente al view. Le sezioni statiche restano Frozen. Default Frozen (l'editor imposta Live dove serve).</summary>
    public RenderMode RenderMode { get; set; } = RenderMode.Frozen;

    /// <summary>Sezione nascosta dal documento pubblicato (doc 11 §3c): l'editor la mostra sempre, la vista pubblica e
    /// le anteprime release la omettono, l'anteprima bozza la marca. Gemello di <see cref="RenderMode"/>: sta sulla
    /// SEZIONE, quindi è versionato e finisce nello snapshot di release — prima viveva in tre storage diversi
    /// (blockmeta ACC versionato, <c>DocumentProfile</c> di APP e vLOA non versionato) e usciva in pubblico senza
    /// pubblicare.</summary>
    public bool IsHidden { get; set; }

    /// <summary>Sotto-sezione resa PRIMA del corpo della sezione padre (doc 11 §3g): blocchi per una sezione
    /// editoriale, resa derivata per una strutturata. Default false = dopo, il comportamento storico. Terzo flag
    /// per-sezione con <see cref="RenderMode"/> e <see cref="IsHidden"/>: versionato e catturato nello snapshot.</summary>
    public bool BeforeParentBody { get; set; }

    /// <summary>
    /// Come si legge la PROSA di una sezione derivata a tabelle (i coordinamenti): <c>false</c> = distesa, una
    /// frase per clausola sopra la tabella — il comportamento storico; <c>true</c> = capofila, UNA frase che
    /// introduce la tabella, che è la forma dei documenti veri (LoA EUROCONTROL, IPI ENAV).
    /// <para>Quarto flag per-sezione con <see cref="RenderMode"/>, <see cref="IsHidden"/> e
    /// <see cref="BeforeParentBody"/>, e per la stessa ragione: è una scelta editoriale, quindi è versionata e
    /// finisce nello snapshot di release. Default false, così nessuna sezione già scritta cambia da sola.</para>
    /// </summary>
    public bool LeadSentence { get; set; }

    /// <summary>
    /// A chi si rivolge la sezione (carta <c>2026-08-27-vsop-militari.md</c> §3). <b>Quinto flag</b> con
    /// <see cref="RenderMode"/>, <see cref="IsHidden"/>, <see cref="BeforeParentBody"/> e
    /// <see cref="LeadSentence"/>, e per la stessa ragione: è una scelta editoriale, quindi è versionata e
    /// finisce nello snapshot di release senza una riga di codice in più.
    ///
    /// <para>Default <see cref="SectionAudience.Both"/> = per tutti: nessuna sezione già scritta cambia da
    /// sé, e la chip di filtro compare solo dove qualcuno ha marcato qualcosa.</para>
    /// <para>⚠️ Non è controllo d'accesso: il documento è pubblico e la vista ATC la apre chiunque cambi
    /// l'indirizzo. È un filtro di lettura.</para>
    /// </summary>
    public SectionAudience Audience { get; set; }

    public byte[]? RowVersion { get; set; }                // concorrenza ottimistica in editing

    public ICollection<DocumentSection> Children { get; set; } = new List<DocumentSection>();
    public ICollection<ContentBlock> Blocks { get; set; } = new List<ContentBlock>();

    /// <summary>Profondità massima consentita per l'albero delle sezioni (SPEC §7.1).</summary>
    public const int MaxDepth = 3;
}
