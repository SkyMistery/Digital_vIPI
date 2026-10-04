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
    private readonly Auth.IEditAuthorizationService? _authz;

    /// <param name="lingua">In che lingua legge chi ha cercato. ⚠️ Nullo fuori da una richiesta (e nei test
    /// che non se ne curano): allora vale l'italiano, che è la lingua predefinita del sito.</param>
    /// <param name="authz">Chi ha cercato: i capitoli della Guida sull'editor si danno solo a chi può modificare.
    /// ⚠️ Nullo vale «lettore pubblico»: nel dubbio si mostra meno.</param>
    public SearchService(ISearchRepository repo, ReadingLanguageContext? lingua = null,
                         Auth.IEditAuthorizationService? authz = null)
    {
        _repo = repo;
        _lingua = lingua;
        _authz = authz;
    }

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, SearchScope scope = SearchScope.All, CancellationToken ct = default)
    {
        query = query?.Trim() ?? "";
        if (query.Length < 2) return System.Array.Empty<SearchHit>();

        var docs = await _repo.SearchAsync(query, scope, Limit, ct);

        // Le sezioni della Guida non sono documenti (nessuno scope doc): compaiono solo nel filtro "Tutti", e IN CODA.
        // ⚠️ Fino al 30 settembre 2026 stavano in cima («come si fa X»); il committente ha fissato l'ordine:
        // titoli dei documenti, titoli di sezione, di sotto-sezione, testo, e infine la Guida. Non contano nel tetto
        // dei documenti: con cinquanta risultati di testo sparirebbero proprio loro. Vedi GuideSearchCatalog.
        if (scope != SearchScope.All) return docs;

        // ⚠️ La Guida è scritta nelle due lingue, e il risultato di ricerca deve arrivare in quella di chi
        // ha cercato: un titolo italiano in mezzo a una pagina di risultati inglese è la solita schermata
        // mezza tradotta. Il testo si SCEGLIE, non si traduce (docs/design/regole-lingua.md R6-R7).
        var inglese = string.Equals(_lingua?.Corrente, "en", StringComparison.OrdinalIgnoreCase);
        var puoModificare = _authz?.IsEditor == true;
        var guide = GuideSearchCatalog.Match(query)
            .Where(e => GuideSearchCatalog.Visibile(e.Anchor, puoModificare))
            .Select(e => GuideSearchCatalog.ToHit(e, inglese)).ToList();
        if (guide.Count == 0) return docs;

        return docs.Concat(guide).ToList();
    }
}
