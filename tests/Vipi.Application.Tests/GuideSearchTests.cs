using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// La ricerca globale fa emergere le sezioni della Guida (intento "come si fa X"): solo nel filtro "Tutti",
/// in cima ai risultati documentali. Vedi GuideSearchCatalog + SearchService.
/// </summary>
public class GuideSearchTests
{
    // Repo fake: restituisce sempre gli stessi hit documentali, indipendentemente dalla query.
    private sealed class FakeRepo : ISearchRepository
    {
        private readonly IReadOnlyList<SearchHit> _hits;
        public FakeRepo(params SearchHit[] hits) => _hits = hits;
        public Task<IReadOnlyList<SearchHit>> SearchAsync(string q, SearchScope s, int limit, CancellationToken ct = default)
            => Task.FromResult(_hits);
    }

    private static SearchHit Doc(string title) => new()
    {
        DocTitle = title, DocType = DocumentType.Vipi, Where = title, Snippet = title, Url = "/vsop/x",
    };

    [Fact]
    public async Task Guide_section_surfaces_first_in_all_scope()
    {
        var svc = new SearchService(new FakeRepo(Doc("vIPI Roma")));

        var hits = await svc.SearchAsync("pubblicare", SearchScope.All);

        Assert.NotEmpty(hits);
        Assert.StartsWith("Guida ›", hits[0].Where);                 // la guida viene prima dei documenti
        Assert.Equal("/vsop/guida#editor-release", hits[0].Url);     // ancora della sezione giusta
        Assert.Contains(hits, h => h.Where == "vIPI Roma");          // i documenti restano presenti
    }

    [Fact]
    public async Task Guide_hidden_outside_all_scope()
    {
        var svc = new SearchService(new FakeRepo(Doc("vIPI Roma")));

        var hits = await svc.SearchAsync("pubblicare", SearchScope.Vipi);

        Assert.DoesNotContain(hits, h => h.Where.StartsWith("Guida ›"));
    }

    [Fact]
    public async Task No_guide_match_returns_only_documents()
    {
        var svc = new SearchService(new FakeRepo(Doc("vIPI Roma")));

        var hits = await svc.SearchAsync("zzzznomatch", SearchScope.All);

        Assert.All(hits, h => Assert.False(h.Where.StartsWith("Guida ›")));
    }

    [Fact]
    public void Catalog_anchors_are_unique()
    {
        var anchors = GuideSearchCatalog.Entries.Select(e => e.Anchor).ToList();
        Assert.Equal(anchors.Count, anchors.Distinct().Count());
    }
}
