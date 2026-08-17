using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Domain;
using Vipi.Ui.Components;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// L'albero degli accordi: <b>ACC della controparte ▸ relazione (noi ⇄ loro) ▸ accordo</b>.
///
/// <para>Prima rete di questa famiglia di componenti. Le regole che prova vivono solo nel markup — un livello
/// che non si apre, un conteggio che diventa un totale — e sono esattamente quelle che, sbagliate, rimettono in
/// cima l'asse che il modello nuovo aveva tolto senza rompere nessun test.</para>
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

    public XferNavigatorTests() =>
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());

    private static XferNavAgreement Agreement(int id, int outbound, int inbound,
        TransferFlowKind kind = TransferFlowKind.Overflight, string airports = "") =>
        new(id, kind, airports, null, outbound, inbound, NoReceiver: false, ToReview: 0);

    private static XferNavCounterpart Group(string acc, string? note, int order, params XferNavRelation[] relations) =>
        new($"acc:{acc}", acc, note, order, relations);

    private static XferNavRelation Rel(string near, string far, params XferNavAgreement[] agreements) =>
        new(near, far, agreements);

    private IRenderedComponent<XferNavigator> Render(params XferNavCounterpart[] groups) =>
        RenderComponent<XferNavigator>(p => p
            .Add(x => x.Counterparts, groups)
            .Add(x => x.ForceOpen, true));

    [Fact]
    public void Il_primo_livello_e_la_ACC_della_controparte()
    {
        var cut = Render(
            Group("LIRR", null, 0, Rel("LIBB_ES_CTR", "LIRR_TS_CTR", Agreement(1, 2, 0))),
            Group("LDZO", null, 0, Rel("LIBB_ES_CTR", "LDZO_CTR", Agreement(2, 1, 0))));

        var rami = cut.FindAll(".xt-nav-sec .xt-nav-name").Select(x => x.TextContent).ToList();
        Assert.Equal(new[] { "LIRR", "LDZO" }, rami);
    }

    [Fact]
    public void La_relazione_e_un_intestazione_non_un_nodo_da_aprire()
    {
        // Sotto Roma stanno più relazioni: distinguerle serve, ma aprirle una per una costerebbe due gesti per
        // arrivare a una foglia che ne chiede uno.
        var cut = Render(Group("LIRR", null, 0,
            Rel("LIBB_ES_CTR", "LIRR_TS_CTR", Agreement(1, 1, 0)),
            Rel("LIBB_ES_CTR", "LIRN_US0_APP", Agreement(2, 1, 0))));

        var lontani = cut.FindAll(".xt-nav-relfar").Select(x => x.TextContent.Trim()).ToList();
        Assert.Equal(new[] { "LIRR_TS_CTR", "LIRN_US0_APP" }, lontani);
        // Un solo nodo apribile: l'ACC.
        Assert.Single(cut.FindAll(".xt-nav-sec"));
        Assert.Equal(2, cut.FindAll(".xt-nav-flow").Count);
    }

    [Fact]
    public void L_intestazione_porta_ENTRAMBI_i_capi()
    {
        // Sugli accordi interni il solo ente lontano è un NOSTRO settore, e compariva come se fosse la
        // controparte. Una coppia si legge come una relazione; un callsign da solo, no.
        var cut = Render(Group("LIBB", "Xfer_NavInternal", 1,
            Rel("LIBD_CS0_APP", "LIBB_ES_CTR", Agreement(1, 0, 0, TransferFlowKind.Departure, "LIBD · LIBR"))));

        Assert.Equal("LIBD_CS0_APP", cut.Find(".xt-nav-relnear").TextContent.Trim());
        Assert.Equal("LIBB_ES_CTR", cut.Find(".xt-nav-relfar").TextContent.Trim());
        // Il titolo porta la coppia piena, perché il nostro capo può essere tagliato dal CSS.
        Assert.Equal("LIBD_CS0_APP ⇄ LIBB_ES_CTR", cut.Find(".xt-nav-rel").GetAttribute("title"));
    }

    [Fact]
    public void Due_foglie_identiche_sotto_la_stessa_coppia_si_vedono()
    {
        // È la «relazione spezzata» del cruscotto (#26/#27 in archivio): sotto una coppia le foglie si
        // distinguono per traffico e aeroporti, quindi due righe uguali di fila sono il difetto, a occhio.
        var cut = Render(Group("LIBB", "Xfer_NavInternal", 1,
            Rel("LIBB_ES_CTR", "LIBD_CS0_APP",
                Agreement(1, 3, 0, TransferFlowKind.Arrival, "LIBD"),
                Agreement(2, 3, 0, TransferFlowKind.Arrival, "LIBD"))));

        var apts = cut.FindAll(".xt-nav-flow .xt-nav-apts").Select(x => x.TextContent.Trim()).ToList();
        Assert.Equal(new[] { "LIBD", "LIBD" }, apts);
        Assert.Single(cut.FindAll(".xt-nav-rel"));
    }

    [Fact]
    public void La_foglia_mostra_i_DUE_conteggi_e_non_il_totale()
    {
        // «3 ⇄ 0» dice che il reciproco non è scritto; «3» non lo diceva, ed è il motivo per cui in archivio i
        // reciproci scritti sono zero senza che nessuno se ne fosse accorto.
        var cut = Render(Group("LGGG", null, 0, Rel("LIBB_ES_CTR", "LGGG_W_CTR", Agreement(1, 3, 0))));

        var conteggio = cut.Find(".xt-nav-flow .xt-nav-count");
        Assert.Contains("3", conteggio.TextContent);
        Assert.Contains("0", conteggio.TextContent);
        // Non bilaterale: nessuna evidenza, e il titolo dice che manca il reciproco.
        Assert.DoesNotContain("xt-nav-bi", conteggio.ClassName);
        Assert.Equal("Xfer_NavMissingReciprocal", conteggio.GetAttribute("title"));
    }

    [Fact]
    public void Un_accordo_con_clausole_nei_due_versi_si_marca_bilaterale()
    {
        var cut = Render(Group("LGGG", null, 0, Rel("LIBB_ES_CTR", "LGGG_W_CTR", Agreement(1, 3, 4))));

        var conteggio = cut.Find(".xt-nav-flow .xt-nav-count");
        Assert.Contains("xt-nav-bi", conteggio.ClassName);
        Assert.Equal("Xfer_BidirectionalTitle", conteggio.GetAttribute("title"));
    }

    [Fact]
    public void La_qualifica_del_ramo_compare_solo_dove_serve()
    {
        var cut = Render(
            Group("LIRR", null, 0, Rel("LIBB_ES_CTR", "LIRR_TS_CTR", Agreement(1, 1, 0))),
            Group("LIBB", "Xfer_NavInternal", 1, Rel("LIBB_ES_CTR", "LIBD_CS0_APP", Agreement(2, 1, 0))));

        var note = cut.FindAll(".xt-nav-note").Select(x => x.TextContent).ToList();
        Assert.Equal(new[] { "Xfer_NavInternal" }, note);
    }

    [Fact]
    public void Un_avviso_risale_fino_alla_ACC()
    {
        // Un avviso che si vede solo dopo aver aperto il ramo giusto non è un avviso.
        var conProblema = new XferNavAgreement(1, TransferFlowKind.Arrival, "LIBD", null, 1, 0,
            NoReceiver: true, ToReview: 0);
        var cut = Render(Group("LIRR", null, 0, Rel("LIBB_ES_CTR", "LIRR_TS_CTR", conProblema)));

        Assert.Equal(2, cut.FindAll(".xt-nav-warn").Count);   // sul ramo e sulla foglia
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
                Group("LIRR", null, 0, Rel("LIBB_ES_CTR", "LIRR_TS_CTR", Agreement(1, 1, 0))),
            })
            .Add(x => x.Collapsed, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "acc:LIRR" }));

        Assert.Empty(cut.FindAll(".xt-nav-flow"));
        Assert.Empty(cut.FindAll(".xt-nav-rel"));
    }
}
