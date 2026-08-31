using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Ui.Components;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// L'albero degli accordi: <b>ACC della controparte ▸ accordo</b>, e l'accordo è una foglia.
///
/// <para>Due livelli e non tre. Il livello in mezzo — la «relazione» — esisteva perché una coppia di enti poteva
/// avere più accordi; dal 18 agosto 2026 non può, e il livello è sparito con la ragione che lo teneva in piedi.
/// Le regole che questa rete prova vivono solo nel markup — un livello che non si apre, un conteggio che diventa
/// un totale — e sono esattamente quelle che, sbagliate, rimettono in cima un asse che il modello ha tolto senza
/// rompere nessun altro test.</para>
/// </summary>
public class XferNavigatorTests : TestContext
{
    /// <summary>Localizer che rende la chiave stessa: le asserzioni parlano di chiavi, non di traduzioni.</summary>
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public XferNavigatorTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
    }

    private static XferNavAgreement Agreement(int id, string near, string far, int sections = 1, int clauses = 3,
        int missingReverse = 0, int toReview = 0, string? note = null) =>
        new(id, near, far, note, sections, clauses, missingReverse, toReview);

    private static XferNavCounterpart Group(string acc, string? note, int order, params XferNavAgreement[] agreements) =>
        new($"acc:{acc}", acc, note, order, agreements);

    private IRenderedComponent<XferNavigator> Render(params XferNavCounterpart[] groups) =>
        RenderComponent<XferNavigator>(p => p
            .Add(x => x.Counterparts, groups)
            .Add(x => x.ForceOpen, true));

    [Fact]
    public void Il_primo_livello_e_la_ACC_della_controparte()
    {
        var cut = Render(
            Group("LIRR", null, 0, Agreement(1, "LIBB_ES_CTR", "LIRR_TS_CTR")),
            Group("LDZO", null, 0, Agreement(2, "LIBB_ES_CTR", "LDZO_CTR")));

        var rami = cut.FindAll(".xt-nav-sec .xt-nav-name").Select(x => x.TextContent).ToList();
        Assert.Equal(new[] { "LIRR", "LDZO" }, rami);
    }

    [Fact]
    public void Sotto_una_ACC_le_foglie_sono_gli_accordi_e_nessun_livello_in_mezzo()
    {
        // Sotto Roma stanno più relazioni: distinguerle serve, ma sono già foglie — non c'è più un livello da
        // aprire per arrivarci.
        var cut = Render(Group("LIRR", null, 0,
            Agreement(1, "LIBB_ES_CTR", "LIRR_TS_CTR"),
            Agreement(2, "LIBB_ES_CTR", "LIRN_US0_APP")));

        var lontani = cut.FindAll(".xt-nav-relfar").Select(x => x.TextContent.Trim()).ToList();
        Assert.Equal(new[] { "LIRR_TS_CTR", "LIRN_US0_APP" }, lontani);
        // Un solo nodo apribile: l'ACC.
        Assert.Single(cut.FindAll(".xt-nav-sec"));
        Assert.Equal(2, cut.FindAll(".xt-nav-flow").Count);
    }

    [Fact]
    public void La_foglia_porta_ENTRAMBI_i_capi()
    {
        // Sugli accordi interni il solo ente lontano è un NOSTRO settore, e compariva come se fosse la
        // controparte. Una coppia si legge come una relazione; un callsign da solo, no.
        var cut = Render(Group("LIBB", "Xfer_NavInternal", 1,
            Agreement(1, "LIBD_CS0_APP", "LIBB_ES_CTR")));

        Assert.Equal("LIBD_CS0_APP", cut.Find(".xt-nav-relnear").TextContent.Trim());
        Assert.Equal("LIBB_ES_CTR", cut.Find(".xt-nav-relfar").TextContent.Trim());
        // Il titolo porta la coppia piena, perché il nostro capo può essere tagliato dal CSS.
        Assert.Equal("LIBD_CS0_APP ⇄ LIBB_ES_CTR", cut.Find(".xt-nav-flow").GetAttribute("title"));
    }

    [Fact]
    public void La_foglia_mostra_SEZIONI_e_clausole_non_il_solo_totale()
    {
        // Un accordo con sei sezioni e due clausole è scritto a metà, e il solo «2» non lo direbbe.
        var cut = Render(Group("LGGG", null, 0,
            Agreement(1, "LIBB_ES_CTR", "LGGG_W_CTR", sections: 6, clauses: 2)));

        var conteggio = cut.Find(".xt-nav-flow .xt-nav-count");
        Assert.Contains("6", conteggio.TextContent);
        Assert.Contains("2", conteggio.TextContent);
    }

    [Fact]
    public void La_qualifica_del_ramo_compare_solo_dove_serve()
    {
        var cut = Render(
            Group("LIRR", null, 0, Agreement(1, "LIBB_ES_CTR", "LIRR_TS_CTR")),
            Group("LIBB", "Xfer_NavInternal", 1, Agreement(2, "LIBB_ES_CTR", "LIBD_CS0_APP")));

        var note = cut.FindAll(".xt-nav-note").Select(x => x.TextContent).ToList();
        Assert.Equal(new[] { "Xfer_NavInternal" }, note);
    }

    [Fact]
    public void Un_avviso_risale_fino_alla_ACC()
    {
        // Un avviso che si vede solo dopo aver aperto il ramo giusto non è un avviso.
        var conProblema = Agreement(1, "LIBB_ES_CTR", "LIRR_TS_CTR", missingReverse: 1);
        var cut = Render(Group("LIRR", null, 0, conProblema));

        Assert.Equal(2, cut.FindAll(".xt-nav-warn").Count);   // sul ramo e sulla foglia
        Assert.Contains("Xfer_NavMissingReciprocal", cut.Find(".xt-nav-flow .xt-nav-warn").GetAttribute("title"));
    }

    [Fact]
    public void L_elenco_vuoto_dice_se_e_il_filtro_o_l_archivio()
    {
        var conFiltro = RenderComponent<XferNavigator>(p => p
            .Add(x => x.Counterparts, Array.Empty<XferNavCounterpart>())
            .Add(x => x.Filtered, true));
        Assert.Contains("Xfer_NoFilterMatch", conFiltro.Markup);

        var senzaFiltro = RenderComponent<XferNavigator>(p => p
            .Add(x => x.Counterparts, Array.Empty<XferNavCounterpart>()));
        Assert.Contains("Xfer_NoFlows", senzaFiltro.Markup);
    }

    [Fact]
    public void Un_ramo_chiuso_non_rende_le_sue_foglie()
    {
        var cut = RenderComponent<XferNavigator>(p => p
            .Add(x => x.Counterparts, new[]
            {
                Group("LIRR", null, 0, Agreement(1, "LIBB_ES_CTR", "LIRR_TS_CTR")),
            })
            .Add(x => x.Collapsed, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "acc:LIRR" }));

        Assert.Empty(cut.FindAll(".xt-nav-flow"));
    }
}
