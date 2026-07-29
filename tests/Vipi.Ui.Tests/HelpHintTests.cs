using Bunit;
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
    [Fact]
    public void HelpHint_renders_details_popover_with_body_and_guide_link()
    {
        var cut = RenderComponent<HelpHint>(p => p
            .Add(x => x.Href, "/vsop/guida#editor-release")
            .AddChildContent("<b>Pubblicare</b> rende la bozza pubblica."));

        Assert.NotNull(cut.Find("details.help-hint"));
        Assert.Contains("data-icon=\"help-circle\"", cut.Markup);   // "?" = icona help-circle
        Assert.Contains("Pubblicare", cut.Markup);                   // testo breve reso
        var link = cut.Find("a.help-more");
        Assert.Equal("/vsop/guida#editor-release", link.GetAttribute("href")); // Href propagato, non letterale
    }

    [Fact]
    public void HelpHint_without_href_omits_guide_link()
    {
        var cut = RenderComponent<HelpHint>(p => p.AddChildContent("solo testo"));
        Assert.Empty(cut.FindAll("a.help-more"));
    }
}
