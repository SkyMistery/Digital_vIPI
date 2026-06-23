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

    public Task<IReadOnlyList<SearchHit>> SearchAsync(string query, SearchScope scope = SearchScope.All, CancellationToken ct = default)
    {
        query = query?.Trim() ?? "";
        if (query.Length < 2) return Task.FromResult<IReadOnlyList<SearchHit>>(System.Array.Empty<SearchHit>());
        return _repo.SearchAsync(query, scope, Limit, ct);
    }
}
