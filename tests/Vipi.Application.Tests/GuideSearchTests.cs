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
        DocTitle = title, DocType = DocumentType.Vipi, Where = title, Snippet = title, Url = "/services/vsop/x",
    };

    [Fact]
    public async Task Guide_section_surfaces_first_in_all_scope()
    {
        var svc = new SearchService(new FakeRepo(Doc("vIPI Roma")));

        var hits = await svc.SearchAsync("pubblicare", SearchScope.All);

        Assert.NotEmpty(hits);
        Assert.StartsWith("Guida ›", hits[0].Where);                 // la guida viene prima dei documenti
        Assert.Equal("/services/vsop/guide#editor-release", hits[0].Url);     // ancora della sezione giusta
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

    // ---- La Guida risponde nella lingua di chi ha cercato ---------------------------------------------

    [Fact]
    public async Task Chi_legge_in_inglese_trova_la_guida_in_inglese()
    {
        var lingua = new ReadingLanguageContext();
        using var _ = lingua.Rendering("en");
        var svc = new SearchService(new FakeRepo(Doc("vIPI Roma")), lingua);

        var hits = await svc.SearchAsync("publishing", SearchScope.All);

        Assert.StartsWith("Guide ›", hits[0].Where);
        Assert.Equal("Publishing (AIRAC release)", hits[0].DocTitle);
    }

    [Fact]
    public async Task Chi_cerca_in_italiano_trova_anche_leggendo_in_inglese()
    {
        // ⚠️ Le parole chiave non si sdoppiano per lingua ED È VOLUTO: su un sito letto in inglese qualcuno
        // cerchera' «pubblicare», e deve trovare. Chi cerca vuole trovare, non essere coerente.
        var lingua = new ReadingLanguageContext();
        using var _ = lingua.Rendering("en");
        var svc = new SearchService(new FakeRepo(Doc("vIPI Roma")), lingua);

        var hits = await svc.SearchAsync("pubblicare", SearchScope.All);

        Assert.Equal("/services/vsop/guide#editor-release", hits[0].Url);
        Assert.Equal("Publishing (AIRAC release)", hits[0].DocTitle);   // trovata in italiano, resa in inglese
    }

    [Fact]
    public void Ogni_voce_ha_tutte_e_due_le_lingue()
    {
        // Una voce a meta' non fa rumore: si vede solo cercando quella parola in quella lingua.
        Assert.All(GuideSearchCatalog.Entries, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.TitleIt), $"{e.Anchor}: titolo italiano mancante");
            Assert.False(string.IsNullOrWhiteSpace(e.TitleEn), $"{e.Anchor}: titolo inglese mancante");
            Assert.False(string.IsNullOrWhiteSpace(e.SnippetIt), $"{e.Anchor}: estratto italiano mancante");
            Assert.False(string.IsNullOrWhiteSpace(e.SnippetEn), $"{e.Anchor}: estratto inglese mancante");
        });
    }

    [Fact]
    public void Gli_unici_titoli_uguali_nelle_due_lingue_sono_NOMI_PROPRI()
    {
        // ⚠️ Un titolo identico nelle due lingue di solito vuol dire «non tradotto», e non si vede: si
        // scopre cercando quella parola in inglese. Le eccezioni vere sono i NOMI, e vanno elencate qui —
        // così l'elenco è la domanda «è davvero un nome?» posta a chi ne aggiunge uno.
        var nomiPropri = new[] { "profile-swapper" };   // «Aurora Profile Swapper» è il nome dello strumento

        var uguali = GuideSearchCatalog.Entries
            .Where(e => string.Equals(e.TitleIt, e.TitleEn, StringComparison.Ordinal))
            .Select(e => e.Anchor)
            .ToArray();

        Assert.Equal(nomiPropri, uguali);
    }
}
