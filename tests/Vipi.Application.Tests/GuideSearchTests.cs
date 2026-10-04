using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// La ricerca globale fa emergere le sezioni della Guida (intento "come si fa X"): solo nel filtro "Tutti",
/// in coda ai risultati documentali (committente, 30 settembre 2026). Vedi GuideSearchCatalog + SearchService.
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

    private sealed class AuthzFinto(VipiRole ruolo) : IEditAuthorizationService
    {
        public VipiRole Role => ruolo;
        public bool IsAdmin => ruolo >= VipiRole.Admin;
        public int? CurrentUserId => null;
        public string? CurrentName => null;
        public void EnsureAdmin() { }
    }

    private static readonly IEditAuthorizationService Editor = new AuthzFinto(VipiRole.Editor);

    private static SearchHit Doc(string title) => new()
    {
        DocTitle = title, DocType = DocumentType.Vipi, Where = title, Snippet = title, Url = "/services/vsop/x",
    };

    [Fact]
    public async Task Guide_section_comes_after_the_documents_in_all_scope()
    {
        var svc = new SearchService(new FakeRepo(Doc("vIPI Roma")), authz: Editor);

        var hits = await svc.SearchAsync("pubblicare", SearchScope.All);

        Assert.Equal("vIPI Roma", hits[0].Where);                    // prima i documenti
        var guida = hits.Skip(1).First();
        Assert.StartsWith("Guida ›", guida.Where);                   // poi la guida
        Assert.Equal("/services/vsop/guide#editor-release", guida.Url);       // ancora della sezione giusta
    }

    [Fact]
    public async Task La_guida_resta_anche_quando_i_documenti_riempiono_il_tetto()
    {
        var tanti = Enumerable.Range(1, 50).Select(i => Doc($"vIPI {i}")).ToArray();
        var svc = new SearchService(new FakeRepo(tanti), authz: Editor);

        var hits = await svc.SearchAsync("pubblicare", SearchScope.All);

        Assert.Equal("vIPI 1", hits[0].Where);
        Assert.Contains(hits.Skip(50), h => h.Url == "/services/vsop/guide#editor-release");
    }

    /// <summary>
    /// 🔴 Committente, 29 settembre 2026: la parte della Guida sull'editor non la vede un utente normale — e quindi
    /// nemmeno la ricerca gliela propone. Senza chi ha cercato (nullo) vale «lettore pubblico».
    /// </summary>
    [Theory]
    [InlineData(VipiRole.User)]
    [InlineData(VipiRole.DivisionStaff)]
    [InlineData(null)]
    public async Task Al_lettore_la_ricerca_non_propone_i_capitoli_sull_editor(VipiRole? ruolo)
    {
        var svc = new SearchService(new FakeRepo(Doc("vIPI Roma")),
            authz: ruolo is { } r ? new AuthzFinto(r) : null);

        var editor = await svc.SearchAsync("pubblicare", SearchScope.All);
        var pubblico = await svc.SearchAsync("ricerca", SearchScope.All);

        Assert.DoesNotContain(editor, h => h.Url.StartsWith("/services/vsop/guide#", StringComparison.Ordinal)
            && !GuideSearchCatalog.AncorePubbliche.Contains(h.Url["/services/vsop/guide#".Length..]));
        Assert.Contains(editor, h => h.Where == "vIPI Roma");
        Assert.Contains(pubblico, h => h.Url == "/services/vsop/guide#ricerca");
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

    /// <summary>
    /// Carta F1, slice 8: chi cerca come si incolla un'area dall'AIP arriva al convertitore, in tutte e due le
    /// lingue e con le parole che userebbe davvero.
    /// </summary>
    [Theory]
    [InlineData("arco")]
    [InlineData("aip")]
    [InlineData("raggio")]
    [InlineData("arc of circle")]
    [InlineData("radius")]
    public void L_Area_Dall_AIP_Porta_Al_Convertitore(string query) =>
        Assert.Contains(GuideSearchCatalog.Match(query), e => e.Anchor == "convertitore-coordinate");

    // ---- La Guida risponde nella lingua di chi ha cercato ---------------------------------------------

    [Fact]
    public async Task Chi_legge_in_inglese_trova_la_guida_in_inglese()
    {
        var lingua = new ReadingLanguageContext();
        using var _ = lingua.Rendering("en");
        var svc = new SearchService(new FakeRepo(Doc("vIPI Roma")), lingua, Editor);

        var hits = await svc.SearchAsync("publishing", SearchScope.All);

        var guida = hits.First(h => h.Url.StartsWith("/services/vsop/guide#", StringComparison.Ordinal));
        Assert.StartsWith("Guide ›", guida.Where);
        Assert.Equal("Publishing (AIRAC release)", guida.DocTitle);
    }

    [Fact]
    public async Task Chi_cerca_in_italiano_trova_anche_leggendo_in_inglese()
    {
        // ⚠️ Le parole chiave non si sdoppiano per lingua ED È VOLUTO: su un sito letto in inglese qualcuno
        // cerchera' «pubblicare», e deve trovare. Chi cerca vuole trovare, non essere coerente.
        var lingua = new ReadingLanguageContext();
        using var _ = lingua.Rendering("en");
        var svc = new SearchService(new FakeRepo(Doc("vIPI Roma")), lingua, Editor);

        var hits = await svc.SearchAsync("pubblicare", SearchScope.All);

        var guida = hits.First(h => h.Url == "/services/vsop/guide#editor-release");
        Assert.Equal("Publishing (AIRAC release)", guida.DocTitle);   // trovata in italiano, resa in inglese
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
