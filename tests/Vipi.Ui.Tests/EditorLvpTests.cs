using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Le note dei dati dello scalo non si scrivono più (S123, 21 ottobre 2026, committente: «devono sparire, dove voglio
/// scrivere testo seleziono paragrafo o quello che è e lo metto»): il testo libero sta nei blocchi della sezione.
/// Fino ad allora qui si provava il tetto della nota delle LVP (U-194).
/// </summary>
public class EditorLvpTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public EditorLvpTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
    }

    [Fact]
    public void Le_LVP_non_hanno_piu_un_campo_nota()
    {
        var cut = RenderComponent<AirportLvpEditor>(p => p
            .Add(x => x.Row, new LvpEdit())
            .Add(x => x.Editing, true));

        Assert.Empty(cut.FindAll("input[placeholder='Lvp_NotePlaceholder']"));
        Assert.DoesNotContain("Ape_Notes", cut.Markup);
    }

    /// <summary>Le regole piste nemmeno: né il campo, né la colonna «Note» nella lettura quando nessuna riga ne ha.</summary>
    [Fact]
    public void Le_regole_piste_non_hanno_piu_una_nota()
    {
        var editor = RenderComponent<AirportRunwayRulesEditor>(p => p
            .Add(x => x.Rows, new List<RuleEdit> { new() { Name = "Notte" } })
            .Add(x => x.RunwayIdents, new List<string> { "05", "23" })
            .Add(x => x.Editing, true));
        Assert.DoesNotContain("Ape_FreeText", editor.Markup);

        var lettura = RenderComponent<AirportRunwayRules>(p => p
            .Add(x => x.View, new AirportRulesView(new[] { new AirportRuleRowView(1, "sempre", "05", "05", "—") })));
        Assert.DoesNotContain("Ape_Notes", lettura.Markup);
        Assert.Equal(4, lettura.FindAll("thead th").Count);
    }

    /// <summary>Una release scattata prima la porta ancora nella sua fotografia: lì la colonna resta.</summary>
    [Fact]
    public void In_una_release_di_prima_la_nota_si_legge_ancora()
    {
        var lettura = RenderComponent<AirportRunwayRules>(p => p
            .Add(x => x.View, new AirportRulesView(new[] { new AirportRuleRowView(1, "sempre", "05", "05", "vento permettendo") })));

        Assert.Contains("vento permettendo", lettura.Markup);
        Assert.Equal(5, lettura.FindAll("thead th").Count);
    }
}
