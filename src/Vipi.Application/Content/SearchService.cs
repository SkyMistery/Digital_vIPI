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
    private readonly ReadingLanguageContext? _lingua;

    /// <param name="lingua">In che lingua legge chi ha cercato. ⚠️ Nullo fuori da una richiesta (e nei test
    /// che non se ne curano): allora vale l'italiano, che è la lingua predefinita del sito.</param>
    public SearchService(ISearchRepository repo, ReadingLanguageContext? lingua = null)
    {
        _repo = repo;
        _lingua = lingua;
    }

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, SearchScope scope = SearchScope.All, CancellationToken ct = default)
    {
        query = query?.Trim() ?? "";
        if (query.Length < 2) return System.Array.Empty<SearchHit>();

        var docs = await _repo.SearchAsync(query, scope, Limit, ct);

        // Le sezioni della Guida non sono documenti (nessuno scope doc): compaiono solo nel filtro "Tutti", in cima,
        // perché rispondono all'intento "come si fa X". Vedi GuideSearchCatalog.
        if (scope != SearchScope.All) return docs;

        // ⚠️ La Guida è scritta nelle due lingue, e il risultato di ricerca deve arrivare in quella di chi
        // ha cercato: un titolo italiano in mezzo a una pagina di risultati inglese è la solita schermata
        // mezza tradotta. Il testo si SCEGLIE, non si traduce (docs/design/regole-lingua.md R6-R7).
        var inglese = string.Equals(_lingua?.Corrente, "en", StringComparison.OrdinalIgnoreCase);
        var guide = GuideSearchCatalog.Match(query).Select(e => GuideSearchCatalog.ToHit(e, inglese)).ToList();
        if (guide.Count == 0) return docs;

        return guide.Concat(docs).Take(Limit).ToList();
    }
}
