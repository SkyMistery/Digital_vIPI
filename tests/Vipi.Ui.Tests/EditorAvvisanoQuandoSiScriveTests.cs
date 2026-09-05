using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// <b>Scrivere in una cella deve dire all'ospite che c'è da salvare.</b> Dal 4 settembre 2026 quell'avviso
/// È il salvataggio: non c'è più un «Salva tutto» da aggiornare né una guardia del browser da accendere —
/// l'ospite, ricevuto l'avviso, scrive (carta 2026-09-04-aeroporto-porta-sola). Il che rende queste prove
/// più importanti di prima, non meno: un campo che non avvisa è un campo che non si salva.
///
/// <para>🔴 <b>Era morto in tutti e quattro gli editor dello scalo.</b> L'aggancio stava su un
/// <c>@onchange</c> del <c>div</c> che avvolge la tabella, e non veniva <b>mai</b> chiamato. Misurato il
/// 4 settembre 2026 sul pacchetto pubblicato: scrivendo in una casella TORA il valore entrava nel modello —
/// quindi il <c>change</c> del DOM era partito e Blazor l'aveva gestito sull'input — ma «Salva tutto»
/// restava a <c>(0)</c> e nel registro del database non compariva <b>nessuna</b> scrittura. Chi scriveva in
/// quei campi e cambiava pagina perdeva tutto, e nessuno lo avvisava.</para>
///
/// <para>⚠️ È un difetto che un banco di prova avrebbe preso subito — questo test, con l'aggancio vecchio,
/// è rosso. Non era mai stato scritto perché sembrava marcatura e non comportamento.</para>
/// </summary>
public class EditorAvvisanoQuandoSiScriveTests : TestContext
{
    private sealed class ChiaveComeValore : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string n] => new(n, n, resourceNotFound: false);
        public LocalizedString this[string n, params object[] a] => new(n, n, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool p) => Enumerable.Empty<LocalizedString>();
    }

    public EditorAvvisanoQuandoSiScriveTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new ChiaveComeValore());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddLogging();
    }

    /// <summary>Quote di transizione: i tre campi della riga.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Le_quote_di_transizione_avvisano(int colonna)
    {
        var n = 0;
        var c = RenderComponent<AirportTransitionEditor>(p => p
            .Add(x => x.Rows, new List<TlEdit> { new() { From = 1013, To = 1030, Level = "FL70" } })
            .Add(x => x.Editing, true)
            .Add(x => x.RowsChanged, () => n++));

        c.FindAll("tbody tr td input").ToList()[colonna].Change("999");

        Assert.Equal(1, n);
    }

    /// <summary>E la Transition Altitude, che ha una porta sua: è un'altra colonna e un altro metodo di
    /// service, quindi non passa da <c>RowsChanged</c>. ⚠️ Il valore va passato INSIEME all'avviso — chi lo
    /// riceve salva subito, e senza il valore salverebbe quello di prima.</summary>
    [Fact]
    public void La_transition_altitude_avvisa_e_passa_il_valore()
    {
        int n = 0, ta = 0;
        var c = RenderComponent<AirportTransitionEditor>(p => p
            .Add(x => x.Rows, new List<TlEdit>())
            .Add(x => x.Editing, true)
            .Add(x => x.TransitionAltitudeFtChanged, (int? v) => { ta = v ?? 0; n++; }));

        c.Find("input[type=number]").Change("6000");

        Assert.Equal(6000, ta);
        Assert.Equal(1, n);
    }

    /// <summary>Regole piste: un campo editoriale avvisa.</summary>
    [Fact]
    public void Le_regole_piste_avvisano()
    {
        var n = 0;
        var c = RenderComponent<AirportRunwayRulesEditor>(p => p
            .Add(x => x.Rows, new List<RuleEdit> { new() { Name = "prova" } })
            .Add(x => x.RunwayIdents, new[] { "16L" })
            .Add(x => x.Editing, true)
            .Add(x => x.RowsChanged, () => n++));

        c.FindAll("input").ToList().First(i => i.GetAttribute("placeholder") == "Ape_RuleNamePh").Change("nuova");

        Assert.Equal(1, n);
    }

    /// <summary>
    /// ⚠️ E il pannello di PROVA delle regole non deve avvisare: direzione del vento, nodi e pista bagnata
    /// sono una simulazione locale, non contenuto. Marcarli «da salvare» accenderebbe la guardia del browser
    /// su un documento che nessuno ha toccato — e una guardia che suona a vuoto è una guardia che si ignora.
    /// </summary>
    [Fact]
    public void Il_pannello_di_prova_delle_regole_NON_avvisa()
    {
        var n = 0;
        var c = RenderComponent<AirportRunwayRulesEditor>(p => p
            .Add(x => x.Rows, new List<RuleEdit> { new() { Name = "prova" } })
            .Add(x => x.RunwayIdents, new[] { "16L" })
            .Add(x => x.Editing, true)
            .Add(x => x.RowsChanged, () => n++));

        c.FindAll("input").ToList().First(i => i.GetAttribute("placeholder") == "Ape_WindCalm").Change("270");

        Assert.Equal(0, n);
    }
}
