using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il clic sull'immagine che la riapre a dimensioni originali (8 settembre 2026).
///
/// <para>La finestra la costruisce il JS e qui non si vede: quel che si presidia è il <b>gancio</b> — che ci
/// sia dove si legge, che NON ci sia dove si scrive, e che porti con sé le etichette tradotte.</para>
///
/// <para>⚠️ La condizione è l'assenza dell'<c>Overlay</c>, cioè della maniglia di ridimensionamento. Nell'editor
/// il gesto del mouse su quell'immagine <b>è</b> il trascinamento della larghezza: se il gancio ci fosse anche
/// lì, a dito alzato chi ha appena stretto la foto se la vedrebbe spalancare in faccia.</para>
/// </summary>
public class ImmagineIngrandibileTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public ImmagineIngrandibileTests() =>
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());

    private static MediaRef Foto() => new("abc123", "Una carta", 1912, 1073, 60);

    [Fact]
    public void Nel_documento_l_immagine_si_apre_col_clic()
    {
        var c = RenderComponent<ImageFigure>(p => p.Add(x => x.Media, Foto()));

        var b = c.Find("figure.doc-img button.img-zoom");
        // Le etichette della finestra viaggiano come attributi `data-`: il JS non deve conoscere nessuna lingua.
        Assert.Equal("Common_Close", b.GetAttribute("data-lb-close"));
        Assert.Equal("Img_ZoomFit", b.GetAttribute("data-lb-fit"));
        Assert.Equal("Img_ZoomFull", b.GetAttribute("data-lb-full"));
        Assert.Equal("Img_ZoomOpen", b.GetAttribute("aria-label"));
        // L'immagine sta DENTRO il tasto, e resta quella di prima: sorgente, alt e misure native.
        var img = c.Find("figure.doc-img button.img-zoom img");
        Assert.Equal("/vsop/media/abc123", img.GetAttribute("src"));
        Assert.Equal("Una carta", img.GetAttribute("alt"));
        Assert.Equal("1912", img.GetAttribute("width"));
        Assert.Equal("lazy", img.GetAttribute("loading"));
    }

    [Fact]
    public void Dove_si_ridimensiona_il_clic_non_apre_niente()
    {
        var c = RenderComponent<ImageFigure>(p => p
            .Add(x => x.Media, Foto())
            .Add(x => x.Overlay, (RenderFragment)(b => b.AddMarkupContent(0, "<button class=\"img-size\"></button>"))));

        Assert.Empty(c.FindAll("button.img-zoom"));
        Assert.Single(c.FindAll("figure.doc-img > img"));   // l'immagine c'è, nuda
        Assert.Single(c.FindAll(".img-size"));              // e la maniglia è al suo posto
    }

    /// <summary>La larghezza scelta dall'editore non si perde per strada: è l'unico posto dove si applica.</summary>
    [Fact]
    public void Il_gancio_non_cambia_la_larghezza_della_figura()
    {
        var c = RenderComponent<ImageFigure>(p => p.Add(x => x.Media, Foto()));

        Assert.Contains("width:60%", c.Find("figure.doc-img").GetAttribute("style"));
    }

    [Fact]
    public void Senza_immagine_non_c_e_niente_da_aprire()
    {
        var c = RenderComponent<ImageFigure>(p => p.Add(x => x.Media, (MediaRef?)null));

        Assert.Empty(c.FindAll("button.img-zoom"));
        Assert.Single(c.FindAll("figure.img-ph"));
    }
}
