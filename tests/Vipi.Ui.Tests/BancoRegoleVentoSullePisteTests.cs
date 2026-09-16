using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il banco di prova delle regole piste: il riquadro dell'esito dice con che vento lavorano le piste IN USO
/// (richiesta del committente, 16 settembre 2026). Prima i numeri stavano solo nelle schede per regola, dove
/// la pista che vince si mescola con quelle delle regole scartate.
///
/// <para>Il vento di prova parte da 200°/10 kt (lo stato iniziale del banco): i test lo usano così com'è.</para>
/// </summary>
public class BancoRegoleVentoSullePisteTests : TestContext
{
    private sealed class ChiaveComeValore : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public BancoRegoleVentoSullePisteTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new ChiaveComeValore());
        Services.AddSingleton<StringheDelSito>();
        Services.AddLogging();
    }

    private IRenderedComponent<AirportRunwayRulesEditor> ProvaCon(List<RuleEdit> regole, params string[] piste)
    {
        var c = RenderComponent<AirportRunwayRulesEditor>(p => p
            .Add(x => x.Rows, regole)
            .Add(x => x.RunwayIdents, piste));
        c.FindAll("button").First(b => b.TextContent.Trim().StartsWith("▷", StringComparison.Ordinal)).Click();
        return c;
    }

    private static List<(string Ident, string Tail, string Cross)> Righe(AngleSharp.Dom.IElement riquadro) =>
        riquadro.QuerySelectorAll(".rule-prova-inuso > span")
            .Select(s => (s.QuerySelector("b")!.TextContent.Trim(),
                          s.QuerySelectorAll("b")[1].TextContent.Trim(),
                          s.QuerySelectorAll("b")[2].TextContent.Trim()))
            .ToList();

    /// <summary>
    /// Una regola vince con DEP e ARR diverse: il riquadro verde porta UNA riga per pista, e i numeri sono gli
    /// stessi della scheda di quella regola. 200°/10 su 160°: 40° di scarto → headwind 8, traverso 6, tailwind 0.
    /// </summary>
    [Fact]
    public void La_regola_che_vince_dice_il_vento_su_ognuna_delle_sue_piste()
    {
        var regola = new RuleEdit { MaxTail = 10 };
        regola.Dep.Add("16L");
        regola.Arr.Add("16R");

        var c = ProvaCon(new List<RuleEdit> { regola }, "16L", "16R", "34L", "34R");

        var riquadro = c.Find(".callout.success");
        Assert.Equal(new[] { ("16L", "0 kt", "6 kt"), ("16R", "0 kt", "6 kt") }, Righe(riquadro));
        Assert.Contains("Ape_TestColTail", riquadro.TextContent);
        Assert.Contains("Ape_TestColCross", riquadro.TextContent);
        // Il ruolo accanto all'ident, come nella scheda.
        Assert.Equal(new[] { "DEP", "ARR" },
                     riquadro.QuerySelectorAll(".rule-prova-inuso .muted").Select(m => m.TextContent.Trim()));

        // ⚠️ Gli stessi numeri della scheda della regola: se il riquadro li ricalcolasse per conto suo potrebbero
        // divergere, e il banco mentirebbe proprio su quello che serve a capire.
        var scheda = c.Find(".rule-prova-regola.vince").QuerySelectorAll("tbody tr")
            .Select(tr => tr.QuerySelectorAll("td").Select(td => td.TextContent.Trim()).ToArray())
            .Select(t => (t[0].Split(' ')[0], t[1], t[2]))
            .ToList();
        Assert.Equal(scheda, Righe(riquadro));
    }

    /// <summary>
    /// Nessuna regola vince: il riquadro del ripiego dice le piste scelte e il vento su di esse, dalle risorse.
    /// 34 ha tailwind 8 con massimo 0 → la regola cade; il ripiego sceglie 16 (headwind 8, traverso 6).
    /// </summary>
    [Fact]
    public void Senza_regola_il_ripiego_dice_il_vento_sulla_pista_scelta()
    {
        var regola = new RuleEdit { MaxTail = 0 };
        regola.Dep.Add("34");
        regola.Arr.Add("34");

        var c = ProvaCon(new List<RuleEdit> { regola }, "16", "34");

        var riquadro = c.Find(".callout.warning");
        Assert.Equal(new[] { ("16", "0 kt", "6 kt") }, Righe(riquadro));
        Assert.Equal("DEP · ARR", riquadro.QuerySelector(".rule-prova-inuso .muted")!.TextContent.Trim());

        // ⚠️ La frase del ripiego è italiano cablato («Headwind 8 kt su 16, vento traverso 6 kt») e usciva anche
        // nell'interfaccia inglese: con una pista scelta non si mostra più.
        Assert.DoesNotContain(" su 16", riquadro.TextContent, StringComparison.Ordinal);
    }

    /// <summary>Vento calmo (direzione vuota): le componenti sono zero, e il riquadro lo dice invece di tacere.</summary>
    [Fact]
    public void Col_vento_calmo_le_componenti_sono_zero()
    {
        var regola = new RuleEdit { MaxTail = 0 };
        regola.Dep.Add("34");
        regola.Arr.Add("34");

        var c = RenderComponent<AirportRunwayRulesEditor>(p => p
            .Add(x => x.Rows, new List<RuleEdit> { regola })
            .Add(x => x.RunwayIdents, new[] { "16", "34" }));
        c.FindAll("input").First(i => i.GetAttribute("placeholder") == "Ape_WindCalm").Change("");
        c.FindAll("button").First(b => b.TextContent.Trim().StartsWith("▷", StringComparison.Ordinal)).Click();

        // Calmo = una regola senza vincoli di vento vince (tailwind 0 ≤ 0): il riquadro è verde, coi numeri a zero.
        // ⚠️ Uguaglianza esatta e non `Assert.All`: su un elenco vuoto `All` passa sempre, e il test non
        // distinguerebbe un riquadro coi numeri a zero da un riquadro senza numeri.
        var riquadro = c.Find(".callout.success");
        Assert.Equal(new[] { ("34", "0 kt", "0 kt") }, Righe(riquadro));
    }
}
