using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Cattura l'output delle sezioni DERIVABILI in modalità <see cref="RenderMode.Frozen"/> di un documento, per
/// congelarlo nello snapshot della release (doc 10 §3b/§3c). Una implementazione per famiglia
/// (<see cref="ReleaseTargetType"/>); i motori la consultano via <see cref="IFrozenSectionRegistry"/> senza switch.
/// Le sezioni <see cref="RenderMode.Live"/> e quelle editoriali (già dentro <c>payload.Doc</c>) non sono catturate.
/// </summary>
public interface IFrozenSectionProvider
{
    ReleaseTargetType Type { get; }

    /// <summary>Per ogni sezione Frozen+derivabile trovata in <paramref name="doc"/>, deriva e serializza l'output
    /// (view-model già renderizzato). Chiave del dizionario = Id della sezione (== <see cref="RawSection.Id"/>).</summary>
    /// <remarks>⚠️ Il CICLO per cui si sta congelando non passa di qui: lo porta <c>ShapeReleaseContext</c>,
    /// che <c>ReleaseService</c> apre attorno alla cattura. Vale per le shape e — dalla stessa ragione — per
    /// le SID d'aeroporto, che compaiono dal ciclo successivo al prelievo.</remarks>
    Task<IReadOnlyDictionary<int, string>> CaptureFrozenAsync(string key, RawDocument doc, CancellationToken ct = default);
}

/// <summary>Registry dei provider di cattura, risolti per <see cref="ReleaseTargetType"/> (doc 10 §3b). I motori di
/// pubblicazione lo consultano; nessuno switch per-tipo.</summary>
public interface IFrozenSectionRegistry
{
    /// <summary>Cattura le sezioni Frozen del documento per il tipo dato; vuoto se nessun provider è registrato per il tipo.</summary>
    Task<IReadOnlyDictionary<int, string>> CaptureAsync(ReleaseTargetType type, string key, RawDocument doc, CancellationToken ct = default);
}

/// <inheritdoc cref="IFrozenSectionRegistry"/>
public sealed class FrozenSectionRegistry : IFrozenSectionRegistry
{
    private static readonly IReadOnlyDictionary<int, string> Empty = new Dictionary<int, string>();
    private readonly IReadOnlyDictionary<ReleaseTargetType, IFrozenSectionProvider> _byType;

    public FrozenSectionRegistry(IEnumerable<IFrozenSectionProvider> providers) =>
        _byType = providers.ToDictionary(p => p.Type);

    public Task<IReadOnlyDictionary<int, string>> CaptureAsync(ReleaseTargetType type, string key, RawDocument doc, CancellationToken ct = default) =>
        _byType.TryGetValue(type, out var p) ? p.CaptureFrozenAsync(key, doc, ct) : Task.FromResult(Empty);
}

/// <summary>Scansione condivisa dell'albero di un <see cref="RawDocument"/> per le sezioni da congelare: quelle in
/// <see cref="RenderMode.Frozen"/> la cui chiave è DERIVATA (<see cref="SectionCatalog.KindOf"/>). Le editoriali vivono
/// già nei blocchi statici del Doc; le Live si derivano al view.</summary>
public static class FrozenSectionScan
{
    public static IEnumerable<RawSection> FrozenDerived(RawDocument doc) =>
        Flatten(doc.Roots).Where(s => s.RenderMode == RenderMode.Frozen && SectionCatalog.KindOf(s.SectionKey) == SectionKind.Derived);

    private static IEnumerable<RawSection> Flatten(IEnumerable<RawSection> sections) =>
        sections.SelectMany(s => Prepend(s, Flatten(s.Children)));

    private static IEnumerable<RawSection> Prepend(RawSection head, IEnumerable<RawSection> tail)
    {
        yield return head;
        foreach (var s in tail) yield return s;
    }
}
