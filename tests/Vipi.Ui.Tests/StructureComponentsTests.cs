using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Ui;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Componenti dichiarativi estratti da StrutturaPage (C4): sostituiscono l'HTML costruito a mano in
/// RenderTreeBuilder. Blazor encoda i valori dinamici ⇒ chiude alla radice il rischio XSS (C1).
/// </summary>
public class StructureComponentsTests : TestContext
{

    /// <summary>Localizzatore che rende la CHIAVE: qui si prova il markup, non le traduzioni.</summary>
    private sealed class ChiaveComeValore : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public StructureComponentsTests() =>
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new ChiaveComeValore());
    [Fact]
    public void FallbackChain_root_shows_help_when_no_ancestors()
    {
        var cut = RenderComponent<StructureFallbackChain>(p =>
            p.Add(x => x.Rows, Array.Empty<FallbackChainRow>()));
        Assert.Contains("Struct_RootNoParent", cut.Markup);
    }

    [Fact]
    public void FallbackChain_renders_ancestors_in_order()
    {
        var rows = new[]
        {
            new FallbackChainRow("LIRR_APP", "APP", "app", "LIRR"),
            new FallbackChainRow("LIRR_CTR", "ACC", "acc", "LIRR"),
        };
        var cut = RenderComponent<StructureFallbackChain>(p => p.Add(x => x.Rows, rows));
        Assert.Contains("LIRR_APP", cut.Markup);
        Assert.Contains("LIRR_CTR", cut.Markup);
    }

    [Fact]
    public void Coverage_leaf_shows_leaf_message()
    {
        var cut = RenderComponent<StructureCoverage>(p => p
            .Add(x => x.IsLeaf, true)
            .Add(x => x.Rows, Array.Empty<CoverageChildRow>()));
        Assert.Contains("Struct_LeafAirport", cut.Markup);
    }

    [Fact]
    public void Coverage_lists_children_and_airport_summary()
    {
        var rows = new[] { new CoverageChildRow("LIRF_TWR", "TWR", "twr", 2) };
        var cut = RenderComponent<StructureCoverage>(p => p
            .Add(x => x.IsLeaf, false)
            .Add(x => x.Rows, rows)
            .Add(x => x.AirportsCovered, 1));
        Assert.Contains("LIRF_TWR", cut.Markup);
        Assert.Contains("+2", cut.Markup);
        Assert.Contains("1 aeroporto coperto", cut.Markup);   // singolare
    }

    // Garanzia di regressione per C1: un callsign malevolo esce ESCAPED, non eseguibile.
    [Fact]
    public void FallbackChain_html_encodes_dynamic_values()
    {
        var rows = new[] { new FallbackChainRow("<script>alert(1)</script>", "ACC", "acc", "LI<b>") };
        var cut = RenderComponent<StructureFallbackChain>(p => p.Add(x => x.Rows, rows));
        Assert.DoesNotContain("<script>", cut.Markup);
        Assert.Contains("&lt;script&gt;", cut.Markup);
    }

    [Fact]
    public void Coverage_html_encodes_dynamic_values()
    {
        var rows = new[] { new CoverageChildRow("<img src=x onerror=1>", "TWR", "twr", 0) };
        var cut = RenderComponent<StructureCoverage>(p => p
            .Add(x => x.IsLeaf, false)
            .Add(x => x.Rows, rows)
            .Add(x => x.AirportsCovered, 0));
        Assert.DoesNotContain("<img", cut.Markup);
        Assert.Contains("&lt;img", cut.Markup);
    }
}
