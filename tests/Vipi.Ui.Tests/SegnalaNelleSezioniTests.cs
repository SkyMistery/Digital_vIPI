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

    [Fact]
    public void Senza_indirizzo_non_c_e_nessun_link()
    {
        var cut = Render(null);

        Assert.Empty(cut.FindAll("a.req-link"));
    }
}
