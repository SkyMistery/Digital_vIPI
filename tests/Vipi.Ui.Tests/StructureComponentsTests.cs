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

    public StructureComponentsTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new ChiaveComeValore());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
    }
    private static IReadOnlyList<IReadOnlyList<FallbackChainRow>> Passi(params IReadOnlyList<FallbackChainRow>[] p) => p;

    [Fact]
    public void FallbackChain_senza_passi_dice_che_si_finisce_su_UNICOM()
    {
        var cut = RenderComponent<StructureFallbackChain>(p => p
            .Add(x => x.SelfCallsign, "LIMM_WS2_CTR")
            .Add(x => x.Steps, Passi()));

        Assert.Contains("Struct_Fallback_Seq_None", cut.Markup);
        Assert.Contains("LIMM_WS2_CTR", cut.Markup);
    }

    /// <summary>
    /// Il punto del componente: le voci di UNO STESSO passo stanno nello stesso gruppo, perche' sono i
    /// settori che a quel punto si dividono il traffico per fascia — non tentativi in fila.
    /// </summary>
    [Fact]
    public void FallbackChain_mette_sullo_stesso_passo_chi_si_divide_il_traffico()
    {
        var cut = RenderComponent<StructureFallbackChain>(p => p
            .Add(x => x.SelfCallsign, "LIMM_WS5_CTR")
            .Add(x => x.Steps, Passi(new[]
            {
                new FallbackChainRow("LIMM_ES5_CTR", "ACC", "acc", "LIMM", Banda: "FL325-UNL"),
                new FallbackChainRow("LIMM_WS2_CTR", "ACC", "acc", "LIMM", DalPadre: true),
            })));

        // Un solo passo, e dentro due voci.
        Assert.Single(cut.FindAll(".fb-seq-step"));
        Assert.Equal(2, cut.FindAll(".fb-seq-step .fb-seq-alt").Count);
        Assert.Contains("FL325-UNL", cut.Markup);
        Assert.Contains("Struct_Fallback_Seq_FromParent", cut.Markup);   // il padre e' etichettato
        Assert.Contains("Struct_Fallback_Seq_AnyLevel", cut.Markup);     // e vale a ogni quota
    }

    [Fact]
    public void FallbackChain_numera_i_passi()
    {
        var cut = RenderComponent<StructureFallbackChain>(p => p
            .Add(x => x.SelfCallsign, "LIMM_ES5_CTR")
            .Add(x => x.Steps, Passi(
                new[] { new FallbackChainRow("LIMM_WS5_CTR", "ACC", "acc", "LIMM", DalPadre: true) },
                new[] { new FallbackChainRow("LIMM_WS2_CTR", "ACC", "acc", "LIMM", DalPadre: true) })));

        var numeri = cut.FindAll(".fb-seq-n").Select(n => n.TextContent.Trim()).ToList();
        Assert.Equal(new[] { "1", "2" }, numeri);
    }

    /// <summary>Chi e' in frequenza adesso si distingue: e' la voce che il traffico prenderebbe davvero.</summary>
    [Fact]
    public void FallbackChain_segna_chi_e_online()
    {
        var cut = RenderComponent<StructureFallbackChain>(p => p
            .Add(x => x.SelfCallsign, "LIMM_WS5_CTR")
            .Add(x => x.Steps, Passi(new[]
            {
                new FallbackChainRow("LIMM_ES5_CTR", "ACC", "acc", "LIMM", Banda: "FL325-UNL", Online: true),
                new FallbackChainRow("LIMM_WS2_CTR", "ACC", "acc", "LIMM", DalPadre: true),
            })));

        Assert.Single(cut.FindAll(".fb-seq-alt.live"));
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
        Assert.Contains("Struct_Coverage_Airport1", cut.Markup);   // singolare: chiave sua, non una desinenza
    }

    // Garanzia di regressione per C1: un callsign malevolo esce ESCAPED, non eseguibile.
    [Fact]
    public void FallbackChain_html_encodes_dynamic_values()
    {
        var cut = RenderComponent<StructureFallbackChain>(p => p
            .Add(x => x.SelfCallsign, "LIRR_CTR")
            .Add(x => x.Steps, Passi(new[]
            {
                new FallbackChainRow("<script>alert(1)</script>", "ACC", "acc", "LI<b>", Banda: "<i>FL325</i>"),
            })));

        Assert.DoesNotContain("<script>", cut.Markup);
        Assert.Contains("&lt;script&gt;", cut.Markup);
        Assert.DoesNotContain("<i>FL325</i>", cut.Markup);
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
