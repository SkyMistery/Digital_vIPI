using Vipi.Application.Content;

namespace Vipi.Application.Abstractions;

/// <summary>Ricerca full-text sulle versioni pubblicate correnti. Impl. EF in Infrastructure.</summary>
public interface ISearchRepository
{
    Task<IReadOnlyList<SearchHit>> SearchAsync(string query, SearchScope scope, int limit, CancellationToken ct = default);
}
