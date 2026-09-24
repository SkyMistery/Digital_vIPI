using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Pages;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// 🔴 T-014 (revisione del 13 settembre 2026): la Ricerca pubblica lanciava una ricerca a ogni tasto sul
/// DbContext del circuito. Due ricerche sovrapposte abbattevano il circuito («A second operation was started»),
/// e una risposta vecchia arrivata tardi sovrascriveva quella nuova.
/// </summary>
public class RicercaUnaAllaVoltaTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    /// <summary>Conta quante ricerche sono dentro insieme; la ricerca più CORTA risponde più TARDI, così una
    /// risposta superata arriva dopo quella buona se nessuno la scarta.</summary>
    private sealed class RicercaCheConta : ISearchService
    {
        private int _dentro;
        public int MassimoInsieme;
        public int Chiamate;

        public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, SearchScope scope = SearchScope.All, CancellationToken ct = default)
        {
            Interlocked.Increment(ref Chiamate);
            var ora = Interlocked.Increment(ref _dentro);
            if (ora > MassimoInsieme) MassimoInsieme = ora;
            try { await Task.Delay(Math.Max(10, 120 - query.Length * 25)); }
            finally { Interlocked.Decrement(ref _dentro); }
            return new[] { new SearchHit { DocTitle = "d", DocType = DocumentType.Vipi, Where = $"[{query}]", Snippet = "s", Url = "/x" } };
        }
    }

    private (IRenderedComponent<SearchPage> Pagina, RicercaCheConta Ricerca) Apri()
    {
        var ricerca = new RicercaCheConta();
        Services.AddSingleton<ISearchService>(ricerca);
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<StringheDelSito>();
        Services.AddSingleton(new EnglishStrings());
        JSInterop.Mode = JSRuntimeMode.Loose;
        return (RenderComponent<SearchPage>(), ricerca);
    }

    /// <summary>Si scrive «LIRF» di seguito: nessuna sovrapposizione, e sullo schermo il risultato dell'ULTIMA.</summary>
    [Fact]
    public async Task Scrivere_di_seguito_non_sovrappone_ricerche_e_vince_l_ultima()
    {
        var (pagina, ricerca) = Apri();

        // ⚠️ Il campo si ritrova a ogni gesto: un ridisegno fra un gesto e l'altro rinnova i gestori, e un
        // elemento trovato una volta sola dà «There is no event handler with ID» (visto nella suite intera).
        // E Find e gesto stanno DENTRO lo stesso InvokeAsync: fuori dal dispatcher il ridisegno di una ricerca che
        // finisce può cadere proprio fra i due (visto il 13-set, solo net8, nella suite intera).
        // Dal 24 settembre 2026 (S7) la ricerca parte dall'input stesso, non dal keyup: ogni lettera è UN gesto.
        var gesti = new List<Task>();
        foreach (var testo in new[] { "LI", "LIR", "LIRF" })
            gesti.Add(pagina.InvokeAsync(() => pagina.Find("input").InputAsync(new() { Value = testo })));
        await Task.WhenAll(gesti);
        pagina.WaitForAssertion(() => Assert.Contains("[LIRF]", pagina.Markup), TimeSpan.FromSeconds(3));

        Assert.Equal(1, ricerca.MassimoInsieme);
        Assert.DoesNotContain("[LI]", pagina.Markup);
        Assert.DoesNotContain("[LIR]", pagina.Markup);
        var caduta = await Task.WhenAny(Renderer.UnhandledException, Task.Delay(300));
        if (caduta == Renderer.UnhandledException) Assert.Fail("Eccezione non gestita: " + await Renderer.UnhandledException);
    }

    /// <summary>
    /// 🔴 S7 (24 settembre 2026): la ricerca partiva solo al <c>keyup</c>, mentre il testo mostrato seguiva
    /// l'<c>input</c>. Tutto ciò che scrive nel campo senza tasti — incolla col mouse, completamento automatico del
    /// browser, testo trascinato, la prova di consegna fatta via script — lasciava a schermo la parola NUOVA col
    /// conteggio VECCHIO: «0 results for Brindisi» in produzione, con 16 risultati veri. Misurato sul sito: solo
    /// `input` → 0, più un `keyup` → 16.
    /// </summary>
    [Fact]
    public async Task Il_testo_arrivato_senza_tasti_cerca_lo_stesso()
    {
        var (pagina, ricerca) = Apri();

        await pagina.InvokeAsync(() => pagina.Find("input").InputAsync(new() { Value = "Brindisi" }));

        pagina.WaitForAssertion(() => Assert.Contains("[Brindisi]", pagina.Markup), TimeSpan.FromSeconds(3));
        Assert.True(ricerca.Chiamate >= 1);
    }

    private sealed class SoloGuida : ISearchService
    {
        public Task<IReadOnlyList<SearchHit>> SearchAsync(string query, SearchScope scope = SearchScope.All, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SearchHit>>(new[]
            {
                new SearchHit { DocTitle = "Releases", DocType = DocumentType.Vipi, Where = "Guide › Releases", Snippet = "s", Url = "/services/vsop/guide#release" },
                new SearchHit { DocTitle = "vIPI Roma", DocType = DocumentType.Vipi, Where = "vIPI Roma", Snippet = "s", Url = "/services/vsop/lirr/vipi" },
            });
    }

    /// <summary>
    /// S8 (24 settembre 2026): le voci della Guida si riconoscevano dal testo «Guida ›», e in inglese la Guida scrive
    /// «Guide ›»: perdevano il libro e sembravano documenti — e la verifica di consegna, che conta i documenti, le
    /// avrebbe contate. Ora si riconoscono dall'indirizzo.
    /// </summary>
    [Fact]
    public async Task Una_voce_della_Guida_in_inglese_resta_una_voce_della_Guida()
    {
        Services.AddSingleton<ISearchService>(new SoloGuida());
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<StringheDelSito>();
        Services.AddSingleton(new EnglishStrings());
        JSInterop.Mode = JSRuntimeMode.Loose;
        var pagina = RenderComponent<SearchPage>();

        await pagina.InvokeAsync(() => pagina.Find("input").InputAsync(new() { Value = "release" }));

        pagina.WaitForAssertion(() => Assert.Equal(2, pagina.FindAll("a.res-row").Count), TimeSpan.FromSeconds(3));
        Assert.Contains("guide", pagina.FindAll("a.res-row").First(a => a.GetAttribute("href")!.Contains("guide")).ClassName);
        Assert.DoesNotContain("guide", pagina.FindAll("a.res-row").First(a => a.GetAttribute("href")!.Contains("lirr")).ClassName ?? "");
    }

    /// <summary>La pagina tiene la ricerca su uno scope suo: senza, il DbContext è quello del circuito.</summary>
    [Fact]
    public void La_ricerca_ha_uno_scope_suo() =>
        Assert.True(typeof(ScopeProprioCheAspetta).IsAssignableFrom(typeof(SearchPage)));
}
