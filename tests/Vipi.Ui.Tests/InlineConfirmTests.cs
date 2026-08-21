using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Rete su <c>InlineConfirm</c>, e in particolare sul gancio <c>CanOpenAsync</c> aggiunto il 21 agosto 2026.
///
/// <para><b>Perché serve.</b> Una pagina che elenca è una fotografia: il presupposto dell'azione può essere
/// cambiato dopo il caricamento. In /vsop/versioni un documento passa in modifica a qualcun altro mentre
/// l'elenco sta aperto — il servizio rifiuta comunque, ma senza questo gancio la domanda «eliminare
/// definitivamente?» veniva posta lo stesso, e l'occupato si scopriva solo <b>dopo</b> aver confermato.</para>
/// </summary>
public class InlineConfirmTests : TestContext
{
    /// <summary>Localizer che rende la chiave stessa (stesso stratagemma di CoordinationCollapseTests).</summary>
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public InlineConfirmTests() =>
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());

    private IRenderedComponent<InlineConfirm> Render(Func<Task<bool>>? canOpen, Action? onConfirm = null) =>
        RenderComponent<InlineConfirm>(p =>
        {
            p.Add(x => x.Prompt, "Eliminare?");
            p.Add(x => x.Trigger, (RenderFragment)(b => b.AddContent(0, "🗑")));
            if (canOpen is not null) p.Add(x => x.CanOpenAsync, canOpen);
            if (onConfirm is not null) p.Add(x => x.OnConfirm, onConfirm);
        });

    [Fact]
    public void Senza_gancio_la_conferma_si_apre_come_sempre()
    {
        var cut = Render(canOpen: null);
        cut.Find("button").Click();
        Assert.Contains("Eliminare?", cut.Markup);
    }

    [Fact]
    public void Il_gancio_che_dice_di_si_lascia_aprire()
    {
        var chiamate = 0;
        var cut = Render(() => { chiamate++; return Task.FromResult(true); });

        cut.Find("button").Click();

        Assert.Equal(1, chiamate);
        Assert.Contains("Eliminare?", cut.Markup);
    }

    /// <summary>Il caso che il gancio esiste per coprire: il presupposto è caduto, la domanda non si fa.</summary>
    [Fact]
    public void Il_gancio_che_dice_di_no_NON_apre_la_conferma()
    {
        var cut = Render(() => Task.FromResult(false));

        cut.Find("button").Click();

        Assert.DoesNotContain("Eliminare?", cut.Markup);
        Assert.DoesNotContain("ic-prompt", cut.Markup);
    }

    /// <summary>
    /// Il gancio è consultato a <b>ogni</b> apertura, non solo alla prima: fra un tentativo e l'altro il lock
    /// può essere stato preso, e una risposta memorizzata sarebbe una fotografia dentro la fotografia.
    /// </summary>
    [Fact]
    public void Il_gancio_e_consultato_a_ogni_tentativo()
    {
        var chiamate = 0;
        var cut = Render(() => { chiamate++; return Task.FromResult(false); });

        cut.Find("button").Click();
        cut.Find("button").Click();
        cut.Find("button").Click();

        Assert.Equal(3, chiamate);
    }

    /// <summary>Un gancio che nega non deve nemmeno sfiorare l'azione: niente conferma, niente OnConfirm.</summary>
    [Fact]
    public void Il_gancio_che_nega_non_esegue_l_azione()
    {
        var eseguita = false;
        var cut = Render(() => Task.FromResult(false), onConfirm: () => eseguita = true);

        cut.Find("button").Click();

        Assert.False(eseguita);
    }
}
