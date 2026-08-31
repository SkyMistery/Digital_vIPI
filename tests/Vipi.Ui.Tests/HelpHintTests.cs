using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Ui;
using Microsoft.AspNetCore.Components;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Rete sul "?" contestuale (HelpHint): popover nativo &lt;details&gt; con testo breve + link opzionale alla Guida.
/// Morde se qualcuno rompe il markup del popover o la propagazione dell'Href (attributo scritto senza @).
/// </summary>
public class HelpHintTests : TestContext
{

    /// <summary>Localizzatore che rende la CHIAVE: qui si prova il markup, non le traduzioni.</summary>
    private sealed class ChiaveComeValore : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public HelpHintTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new ChiaveComeValore());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
    }
    [Fact]
    public void HelpHint_renders_details_popover_with_body_and_guide_link()
    {
        var cut = RenderComponent<HelpHint>(p => p
            .Add(x => x.Href, "/services/vsop/guide#editor-release")
            .AddChildContent("<b>Pubblicare</b> rende la bozza pubblica."));

        Assert.NotNull(cut.Find("details.help-hint"));
        Assert.Contains("data-icon=\"help-circle\"", cut.Markup);   // "?" = icona help-circle
        Assert.Contains("Pubblicare", cut.Markup);                   // testo breve reso
        var link = cut.Find("a.help-more");
        Assert.Equal("/services/vsop/guide#editor-release", link.GetAttribute("href")); // Href propagato, non letterale
    }

    [Fact]
    public void HelpHint_without_href_omits_guide_link()
    {
        var cut = RenderComponent<HelpHint>(p => p.AddChildContent("solo testo"));
        Assert.Empty(cut.FindAll("a.help-more"));
    }
}
