using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il link «Segnala» accanto al titolo di una sezione pubblica (S56, richieste dal campo). Un link e non un'isola: il
/// documento resta statico. C'è solo dove la pagina lo chiede (la vista pubblica: in bozza e nelle anteprime no), sulle
/// radici, e porta famiglia, chiave e sezione al modulo.
/// </summary>
public class SegnalaNelleSezioniTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public SegnalaNelleSezioniTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
    }

    private static SectionView Sez(string id, string chiave, bool nascosta = false) => new()
    {
        Id = id, Title = "Titolo " + id, Depth = 0, SectionKey = chiave, IsHidden = nascosta,
        Blocks = Array.Empty<BlockView>(), Children = Array.Empty<SectionView>(),
    };

    private IRenderedComponent<DocumentSectionsView> Render(Func<SectionView, string?>? href) =>
        RenderComponent<DocumentSectionsView>(p => p
            .Add(x => x.Sections, new[] { Sez("s-1", "notes"), Sez("s-2", "extra", nascosta: true) })
            .Add(x => x.Profile, SectionProfile.Airport)
            .Add(x => x.IsDraft, false)
            .Add(x => x.ReportHrefOf, href));

    [Fact]
    public void In_vista_pubblica_ogni_radice_visibile_ha_il_suo_segnala()
    {
        var cut = Render(s => ReportLink.Per(ReleaseTargetType.Airport, "LIRF", s.SectionKey));

        var link = Assert.Single(cut.FindAll("a.req-link"));
        Assert.Equal("/services/vsop/requests?t=Airport&k=LIRF&s=notes", link.GetAttribute("href"));
        Assert.Contains("noprint", link.ClassList);   // in stampa non c'è
    }

    /// <summary>
    /// Una bandierina e non la parola «Segnala» (committente, 30 settembre 2026): che cos'è lo dicono il `title` al
    /// passaggio del mouse e l'`aria-label`, e c'è anche sulle SOTTO-sezioni.
    /// </summary>
    [Fact]
    public void E_una_bandierina_col_suo_titolo_anche_sulle_sotto_sezioni()
    {
        var figlia = new SectionView
        {
            Id = "s-1-1", Title = "Figlia", Depth = 1, SectionKey = "notes-local",
            Blocks = Array.Empty<BlockView>(), Children = Array.Empty<SectionView>(),
        };
        var radice = new SectionView
        {
            Id = "s-1", Title = "Radice", Depth = 0, SectionKey = "notes",
            Blocks = Array.Empty<BlockView>(), Children = new[] { figlia },
        };
        var cut = RenderComponent<DocumentSectionsView>(p => p
            .Add(x => x.Sections, new[] { radice })
            .Add(x => x.Profile, SectionProfile.Airport)
            .Add(x => x.IsDraft, false)
            .Add(x => x.ReportHrefOf, s => ReportLink.Per(ReleaseTargetType.Airport, "LIRF", s.SectionKey)));

        var link = cut.FindAll("a.req-link");
        Assert.Equal(new[] { "/services/vsop/requests?t=Airport&k=LIRF&s=notes",
                             "/services/vsop/requests?t=Airport&k=LIRF&s=notes-local" },
                     link.Select(a => a.GetAttribute("href")));
        Assert.All(link, a =>
        {
            Assert.Equal("Req_ReportTitle", a.GetAttribute("title"));
            Assert.Equal("Req_ReportTitle", a.GetAttribute("aria-label"));
            Assert.Equal("flag", a.QuerySelector("svg")?.GetAttribute("data-icon"));
            Assert.Equal("", a.TextContent.Trim());   // niente parola: la dice il title
        });
    }

    /// <summary>
    /// Errore o suggerimento si sceglie con uno SWITCH, la forma della scelta della lingua (committente, 30 settembre
    /// 2026), e non con due pallini. ⚠️ Presidio sul sorgente: la pagina ha il servizio delle richieste e l'identità.
    /// </summary>
    [Fact]
    public void Il_tipo_si_sceglie_con_uno_switch()
    {
        var pagina = File.ReadAllText(Path.Combine(RadiceUi(), "Pages", "RichiestePage.razor"));
        Assert.Contains("class=\"req-kind\"", pagina);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(pagina, "aria-pressed=\"@\\(_tipo ==").Count);
        Assert.DoesNotContain("type=\"radio\" name=\"req-kind\"", pagina);

        var foglio = File.ReadAllText(Path.Combine(RadiceUi(), "wwwroot", "vipi-theme.css"));
        Assert.Contains(".req-kind button[aria-pressed=\"true\"]", foglio);
    }

    private static string RadiceUi()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var c = Path.Combine(dir.FullName, "src", "Vipi.Ui");
            if (Directory.Exists(Path.Combine(c, "Pages"))) return c;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException($"src/Vipi.Ui non trovata risalendo da {AppContext.BaseDirectory}");
    }

    [Fact]
    public void Senza_indirizzo_non_c_e_nessun_link()
    {
        var cut = Render(null);

        Assert.Empty(cut.FindAll("a.req-link"));
    }
}
