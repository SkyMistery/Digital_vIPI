using Vipi.Application.Abstractions;

namespace Vipi.Application.Content;

/// <summary>Use-case di ricerca full-text (lettura pubblica). Sottile sopra <see cref="ISearchRepository"/>.</summary>
public interface ISearchService
{
    Task<IReadOnlyList<SearchHit>> SearchAsync(string query, SearchScope scope = SearchScope.All, CancellationToken ct = default);
}

/// <inheritdoc cref="ISearchService"/>
public sealed class SearchService : ISearchService
{
    private const int Limit = 50;
    private readonly ISearchRepository _repo;

    public SearchService(ISearchRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, SearchScope scope = SearchScope.All, CancellationToken ct = default)
    {
        query = query?.Trim() ?? "";
        if (query.Length < 2) return System.Array.Empty<SearchHit>();

        var docs = await _repo.SearchAsync(query, scope, Limit, ct);

        // Le sezioni della Guida non sono documenti (nessuno scope doc): compaiono solo nel filtro "Tutti", in cima,
        // perché rispondono all'intento "come si fa X". Vedi GuideSearchCatalog.
        if (scope != SearchScope.All) return docs;

        var guide = GuideSearchCatalog.Match(query).Select(GuideSearchCatalog.ToHit).ToList();
        if (guide.Count == 0) return docs;

        return guide.Concat(docs).Take(Limit).ToList();
    }
}
