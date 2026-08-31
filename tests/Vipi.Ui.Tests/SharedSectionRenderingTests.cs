using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// «Resa uguale, contenuto diverso» (doc 13 §3l): una sezione comune si disegna con UN componente, che riceve
/// dati diversi da ogni famiglia. Queste prove tengono il componente onesto sui casi che le copie inline
/// coprivano ciascuna a modo suo — ed è così che avevano finito per divergere.
/// </summary>
public class SharedSectionRenderingTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public SharedSectionRenderingTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
    }

    // ---- Configurazioni: il viewer della vIPI ACC aveva una copia riga per riga di questo componente ----

    private static AccConfigTableView Table(string key, string name, params AccConfigTableRow[] rows) =>
        new(key, name, rows);

    private IRenderedComponent<AppConfigurations> RenderConfigs(string mapScope, params AccConfigTableView[] tables) =>
        RenderComponent<AppConfigurations>(p => p
            .Add(x => x.Configs, Array.Empty<AccConfiguration>())
            .Add(x => x.Tables, tables)
            .Add(x => x.MapScope, mapScope));

    [Fact]
    public void Configurations_bind_each_table_to_the_map_of_its_own_document()
    {
        // data-cfgblock è il legame che il JS usa per aprire la configurazione scelta sulla mappa AoR. Nella
        // vIPI ACC è la chiave del BLOCCO (una mappa per blocco), nell'APP lo scope del documento: stesso
        // markup, ancoraggio diverso — è il «contenuto diverso» di questa sezione.
        var acc = RenderConfigs("grp:9f3a1c07", Table("cfg:a", "Nord"));
        var app = RenderConfigs("app-aor-LIBP_APP", Table("cfg:a", "Nord"));

        Assert.Contains("data-cfgblock=\"grp:9f3a1c07\"", acc.Markup);
        Assert.Contains("data-cfgblock=\"app-aor-LIBP_APP\"", app.Markup);
        Assert.Single(acc.FindAll("details.cfg-collapse"));
    }

    [Fact]
    public void Configurations_explain_the_link_with_the_map_only_when_there_is_something_to_open()
    {
        Assert.Contains("AppCfg_PickHint", RenderConfigs("b", Table("cfg:a", "Nord")).Markup);

        var vuoto = RenderConfigs("b");
        Assert.Contains("AppCfg_None", vuoto.Markup);
        Assert.DoesNotContain("AppCfg_PickHint", vuoto.Markup);
    }

    [Fact]
    public void A_configuration_with_no_open_sector_says_so_instead_of_an_empty_table()
    {
        // Lo diceva solo la copia della vIPI ACC: sull'APP la stessa configurazione mostrava una tabella muta.
        var cut = RenderConfigs("b", Table("cfg:a", "Nord"));

        Assert.Contains("AppCfg_NoOpenSector", cut.Markup);
        Assert.Empty(cut.FindAll("tbody tr.grp-start"));
    }

    [Fact]
    public void A_configuration_with_sectors_lists_them()
    {
        var cut = RenderConfigs("b", Table("cfg:a", "Nord",
            new AccConfigTableRow("LIBB_ES_CTR", new[] { "LIBB_FSS" }, "BRINDISI", "80")));

        var cells = cut.FindAll("tbody tr.grp-start td").Select(c => c.TextContent.Trim()).ToArray();
        Assert.Equal(new[] { "LIBB_ES_CTR", "LIBB_FSS", "BRINDISI", "80" }, cells);
        Assert.DoesNotContain("AppCfg_NoOpenSector", cut.Markup);
    }

    // ---- Frequenze: la vLOA aveva una tabella tutta sua, e infatti chiamava le colonne in un altro modo ----

    private static AppFreqRow Freq(string name, string callsign, string mhz, bool primary = false) =>
        new(null, name, callsign, mhz, "CTR", primary, false);

    [Fact]
    public void Frequencies_have_one_set_of_column_names_for_everybody()
    {
        var cut = RenderComponent<AppFrequencies>(p => p.Add(x => x.Rows, new[] { Freq("Brindisi Radar", "LIBB_ES_CTR", "128.300") }));

        var head = cut.FindAll("thead th").Select(t => t.TextContent.Trim()).ToArray();
        Assert.Equal(new[] { "AppFreq_Callsign", "AppFreq_Position", "Airport_Frequency" }, head);
    }

    [Fact]
    public void Frequencies_can_dim_a_row_and_carry_an_action_without_the_reorder_column()
    {
        // È ciò che serve alla vLOA in modifica: le frequenze escluse restano visibili, attenuate, con il loro
        // interruttore — ma senza il riordino, che sulla vLOA non esiste.
        var rows = new[] { Freq("Brindisi Radar", "LIBB_ES_CTR", "128.300"), Freq("Athinai Radar", "LGGG_CTR", "135.825") };

        var cut = RenderComponent<AppFrequencies>(p => p
            .Add(x => x.Rows, rows)
            .Add(x => x.IsDimmed, r => r.Callsign == "LGGG_CTR")
            .Add(x => x.RowActions, (RenderFragment<AppFreqRow>)(r => b => b.AddMarkupContent(0, $"<button>tog-{r.Callsign}</button>"))));

        // .ToList(): l'indicizzatore di RefreshableElementCollection esplode con questa coppia bUnit/AngleSharp.
        var righe = cut.FindAll("tbody tr").ToList();
        Assert.DoesNotContain("opacity:.45", righe[0].GetAttribute("style") ?? "");
        Assert.Contains("opacity:.45", righe[1].GetAttribute("style") ?? "");
        Assert.Contains("tog-LGGG_CTR", cut.Markup);
        Assert.DoesNotContain("Common_Order", cut.Markup);   // niente colonna di riordino
    }

    [Fact]
    public void Without_actions_the_table_stays_as_it_was()
    {
        var cut = RenderComponent<AppFrequencies>(p => p.Add(x => x.Rows, new[] { Freq("Brindisi Radar", "LIBB_ES_CTR", "128.300") }));

        Assert.Equal(3, cut.FindAll("thead th").Count);
        Assert.Equal(3, cut.FindAll("tbody tr td").Count);
    }
}
