using System.Text.Json;
using Vipi.Application.Abstractions;
using Vipi.Application.Routing;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>La struttura che serve ai documenti collegati, senza i documenti: quelli si leggono ogni volta, perché
/// «pubblico adesso» non può aspettare una cache.</summary>
public sealed record DocLinkStructure(
    IReadOnlyDictionary<string, string?> ParentOf,
    IReadOnlyDictionary<string, string> CenterOf,
    IReadOnlyList<DocLinkAirport> Airports,
    IReadOnlyDictionary<string, DocLinkApp> Apps);

/// <summary>Chi legge la struttura dal database. Una implementazione, in Infrastructure.</summary>
public interface IDocLinkStructureSource
{
    Task<DocLinkStructure> LoadAsync(CancellationToken ct = default);
}

/// <summary>I documenti collegati (§A109): che cosa si congela pubblicando, e che cosa si disegna leggendo.</summary>
public interface IDocLinkService
{
    /// <summary>I posti da congelare nella release. Struttura letta ADESSO, senza cache: si sta pubblicando.</summary>
    Task<DocLinkSnapshot> CaptureAsync(ReleaseTargetType type, string key, CancellationToken ct = default);

    /// <summary>
    /// I link da disegnare nella pagina del documento, già risolti: solo documenti pubblici adesso.
    /// </summary>
    /// <param name="releaseId">Anteprima di UNA release (<c>as=rel:N</c>): i suoi posti. Null = la release in vigore.</param>
    /// <param name="bozza">Anteprima bozza: i posti si calcolano dalla struttura di adesso.</param>
    Task<IReadOnlyList<ResolvedDocLink>> ForPageAsync(ReleaseTargetType type, string key, int? releaseId, bool bozza,
        CancellationToken ct = default);
}

/// <inheritdoc cref="IDocLinkService"/>
public sealed class DocLinkService : IDocLinkService
{
    private readonly IDocLinkStructureSource _struttura;
    private readonly IDocumentAdminRepository _documenti;
    private readonly IReleaseRepository _release;
    private readonly IDocRoutesRegistry _rotte;

    public DocLinkService(IDocLinkStructureSource struttura, IDocumentAdminRepository documenti,
        IReleaseRepository release, IDocRoutesRegistry rotte)
    {
        _struttura = struttura;
        _documenti = documenti;
        _release = release;
        _rotte = rotte;
    }

    // ⚠️ La struttura in memoria per DUE MINUTI, statica come la cache dei confinanti di EfHierarchyEditingService:
    // serve le release di prima di §A109 e le anteprime bozza, cioè OGNI apertura di pagina finché i documenti non
    // vengono ripubblicati. Cambia solo con un import o un cambio di padre; due minuti di ritardo sui link di una
    // bozza non li vede nessuno. La pubblicazione NON passa di qui.
    private static readonly object CacheLock = new();
    private static (DocLinkStructure Struttura, DateTime Letta)? _cache;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

    public async Task<DocLinkSnapshot> CaptureAsync(ReleaseTargetType type, string key, CancellationToken ct = default)
    {
        var struttura = await _struttura.LoadAsync(ct).ConfigureAwait(false);
        var documenti = await _documenti.ListAsync(ct).ConfigureAwait(false);
        return DocumentiCollegati.Capture(Grafo(struttura, documenti), type, key);
    }

    public async Task<IReadOnlyList<ResolvedDocLink>> ForPageAsync(ReleaseTargetType type, string key, int? releaseId,
        bool bozza, CancellationToken ct = default)
    {
        var documenti = await _documenti.ListAsync(ct).ConfigureAwait(false);
        var snap = (bozza ? null : await CongelatiAsync(type, key, releaseId, ct).ConfigureAwait(false))
                   ?? DocumentiCollegati.Capture(Grafo(await StrutturaInCacheAsync(ct).ConfigureAwait(false), documenti), type, key);

        // Pubblico adesso = la regola degli elenchi pubblici (doc 10 §3f): release in vigore e non nascosto.
        var pubblici = documenti.Where(d => d.HasEffectiveRelease && !d.IsHidden && d.DocumentId is not null)
            .Select(d => d.DocumentId!.Value).ToHashSet();

        return DocumentiCollegati.Resolve(snap, t => pubblici.Contains(t.DocumentId))
            .Select(x => new ResolvedDocLink(x.Group, x.Target, Indirizzo(x.Target)))
            .Where(x => x.Href.Length > 0)
            .ToList();
    }

    /// <summary>I posti congelati nella release (quella chiesta o quella in vigore). Null = da calcolare: release
    /// senza il campo (di prima di §A109), o nessuna release.</summary>
    private async Task<DocLinkSnapshot?> CongelatiAsync(ReleaseTargetType type, string key, int? releaseId, CancellationToken ct)
    {
        var rel = releaseId is int id
            ? await _release.GetByIdAsync(id, ct).ConfigureAwait(false)
            : await _release.GetEffectiveAsync(type, key, DateTime.UtcNow, ct).ConfigureAwait(false);
        // La release chiesta deve essere DI QUESTO documento, come in ReleaseService.GetPreviewAsync.
        if (rel is null || rel.TargetType != type
            || !string.Equals(rel.TargetKey, key, StringComparison.OrdinalIgnoreCase)) return null;

        try { return JsonSerializer.Deserialize<SoloCollegamenti>(rel.PayloadJson)?.Collegamenti; }
        catch (JsonException) { return null; }
    }

    /// <summary>Solo il campo che serve: il resto del payload (fino a 221 KB) si salta senza costruire niente.</summary>
    private sealed class SoloCollegamenti
    {
        public DocLinkSnapshot? Collegamenti { get; set; }
    }

    private async Task<DocLinkStructure> StrutturaInCacheAsync(CancellationToken ct)
    {
        lock (CacheLock)
            if (_cache is { } c && DateTime.UtcNow - c.Letta < CacheTtl) return c.Struttura;

        var s = await _struttura.LoadAsync(ct).ConfigureAwait(false);
        lock (CacheLock) _cache = (s, DateTime.UtcNow);
        return s;
    }

    private static DocLinkGraph Grafo(DocLinkStructure s, IReadOnlyList<ManagedDoc> documenti) =>
        new(s.ParentOf, s.CenterOf, s.Airports, s.Apps, documenti);

    private string Indirizzo(DocLinkTarget t)
    {
        var url = _rotte.For(t.Type).PublicUrl(t.AccCode.ToLowerInvariant(), t.Key, null);
        if (string.IsNullOrEmpty(url)) return "";
        return t.Anchor is { Length: > 0 } a ? $"{url}#{a}" : url;
    }
}
