using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il tasto «Link» nei campi di testo (S122, 21 ottobre 2026, committente: «in qualunque posto si possa editare del
/// testo, dei link»): un indirizzo web o un allegato della biblioteca, e le parole selezionate diventano il testo.
/// </summary>
public class LinkNelTestoTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private sealed class Biblioteca : IAttachmentLibrary
    {
        public Task<IReadOnlyList<AttachmentRow>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AttachmentRow>>(new[]
            {
                new AttachmentRow(1, "loa-lirr-lfmm", "LoA Roma-Marseille", AttachmentKind.Loa, AttachmentScope.Division,
                    null, null, 1, 1, AttachmentProvider.Drive, "1A2b3C4d5E6f7G8h9I0jKlMnOpQrStUvW", DateTime.UnixEpoch, DateTime.UnixEpoch),
            });
        public Task<AttachmentRow?> BySlugAsync(string slug, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(AttachmentCreate Esito, AttachmentRow? Riga)> CreateAsync(AttachmentDraft draft, int userId, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<(AttachmentReplace Esito, AttachmentRow? Riga)> ReplaceAsync(string slug, string link, string? note, int userId, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<AttachmentDelete> DeleteAsync(string slug, int userId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    public LinkNelTestoTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddScoped<IAttachmentLibrary, Biblioteca>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<LinkPicker> Selettore(Action<string> scelto, string? testo = null) =>
        RenderComponent<LinkPicker>(p => p
            .Add(x => x.TestoIniziale, testo)
            .Add(x => x.OnScegli, EventCallback.Factory.Create<string>(this, scelto)));

    /// <summary>Un indirizzo che non è http/https completo non si inserisce, e il selettore dice perché.</summary>
    [Fact]
    public void Un_indirizzo_non_web_non_si_inserisce()
    {
        var cut = Selettore(_ => Assert.Fail("non doveva inserire"));

        cut.Find("input.link-pick-url").Input("javascript:alert(1)");

        Assert.True(cut.Find("button.link-pick-ok").HasAttribute("disabled"));
        Assert.Contains("Link_UrlInvalid", cut.Markup);
    }

    [Fact]
    public void Un_indirizzo_web_torna_come_link_con_le_parole_scritte()
    {
        string? scelto = null;
        var cut = Selettore(s => scelto = s, testo: "Eurocontrol");

        cut.Find("input.link-pick-url").Input("https://www.eurocontrol.int");
        cut.Find("button.link-pick-ok").Click();

        Assert.Equal("[Eurocontrol](https://www.eurocontrol.int/)", scelto);
    }

    /// <summary>Un allegato si sceglie dalla biblioteca, come nel blocco: le parole vuote prendono il titolo.</summary>
    [Theory]
    [InlineData(null, "[LoA Roma-Marseille](allegato:loa-lirr-lfmm)")]
    [InlineData("la lettera", "[la lettera](allegato:loa-lirr-lfmm)")]
    public void Un_allegato_si_sceglie_dalla_biblioteca(string? testo, string atteso)
    {
        string? scelto = null;
        var cut = Selettore(s => scelto = s, testo);

        cut.FindAll("button.sh-chip").ElementAt(1).Click();
        cut.WaitForElement("button.sidref-pick-row").Click();

        Assert.Equal(atteso, scelto);
    }

    /// <summary>
    /// Nella barra del testo c'è il tasto, e le parole selezionate nel campo arrivano nel selettore: diventano il testo
    /// del link, che poi prende il loro posto.
    /// </summary>
    [Fact]
    public void Il_tasto_della_barra_porta_le_parole_selezionate()
    {
        JSInterop.Setup<string>("vipiSidPrendi", _ => true).SetResult("g1");
        JSInterop.Setup<string>("vipiSidTesto", _ => true).SetResult("il sito");

        var cut = RenderComponent<RichTextArea>(p => p.Add(x => x.Value, "Vedi il sito."));
        cut.Find("button.rta-link").Click();

        Assert.Equal("il sito", cut.Find("input.link-pick-testo").GetAttribute("value"));
    }
}
