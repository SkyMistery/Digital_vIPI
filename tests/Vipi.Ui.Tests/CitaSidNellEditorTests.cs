using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il tasto «SID» dell'editor (§A73, slice 3): dalla barra di un campo di prosa e sotto una tabella si apre il
/// selettore, si sceglie una SID e nel campo va il RIFERIMENTO — non il nome, che si decide al disegno.
///
/// <para>⚠️ Il gesto nel campo (segnare il cursore, scrivere, il <c>change</c> sintetico) vive in
/// <c>vipi-editor.js</c> e qui non gira: si prova il contratto con le funzioni JS — chi si chiama, con che cosa.
/// Il resto lo prova la verifica dal vivo.</para>
/// </summary>
public class CitaSidNellEditorTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private sealed class ElencoFinto : ISidReferenceResolver
    {
        public List<string> Chiesti { get; } = new();

        public Task<NomiSid> PerVistaAsync(IEnumerable<SectionView> sezioni, bool pubblica,
            string? proprioIcao = null, AirportSidView? propriaTabella = null, CancellationToken ct = default) =>
            Task.FromResult(NomiSid.Vuoto);

        public Task<NomiSid> PerTestiAsync(IEnumerable<string?> testi, CancellationToken ct = default) =>
            Task.FromResult(NomiSid.Vuoto);

        public Task<IReadOnlyList<SidCitabile>> ElencoAsync(string icao, CancellationToken ct = default)
        {
            Chiesti.Add(icao);
            IReadOnlyList<SidCitabile> elenco = icao == "LIBD"
                ? new[] { new SidCitabile("LIBD", "BANA8A", "BANAV 8A", "07"), new SidCitabile("LIBD", "TOPN9A", "TOPNO 9A", "07") }
                : Array.Empty<SidCitabile>();
            return Task.FromResult(elenco);
        }
    }

    private readonly ElencoFinto _elenco = new();

    public CitaSidNellEditorTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddScoped<ISidReferenceResolver>(_ => _elenco);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<RichTextArea> CampoDiLIBD() =>
        RenderComponent<RichTextArea>(p => p.AddCascadingValue("IcaoDelDocumento", "LIBD"));

    [Fact]
    public void Dalla_barra_si_sceglie_una_SID_e_nel_campo_va_il_riferimento()
    {
        JSInterop.Setup<string>("vipiSidPrendi", _ => true).SetResult("g1");
        JSInterop.Setup<bool>("vipiSidInserisci", _ => true).SetResult(true);
        var c = CampoDiLIBD();

        c.Find("button.rta-sid").Click();

        // Lo scalo arriva dall'editor: l'elenco è già quello di LIBD, col nome completo.
        c.WaitForAssertion(() => Assert.Equal(2, c.FindAll(".sidref-pick-row").Count));
        Assert.Contains("BANAV 8A", c.Markup);
        Assert.Equal("LIBD", Assert.Single(_elenco.Chiesti));

        c.FindAll(".sidref-pick-row").First().Click();

        var scritto = Assert.Single(JSInterop.Invocations["vipiSidInserisci"]);
        Assert.Equal("[[SID LIBD BANA8A]]", scritto.Arguments[1]);
        Assert.Empty(c.FindAll(".sidref-pick"));   // scelto, il selettore si chiude
    }

    [Fact]
    public void La_ricerca_filtra_e_Invio_sceglie_la_prima()
    {
        JSInterop.Setup<string>("vipiSidPrendi", _ => true).SetResult("g1");
        JSInterop.Setup<bool>("vipiSidInserisci", _ => true).SetResult(true);
        var c = CampoDiLIBD();
        c.Find("button.rta-sid").Click();
        c.WaitForAssertion(() => Assert.Equal(2, c.FindAll(".sidref-pick-row").Count));

        var cerca = c.Find(".sidref-pick input.cerca");
        cerca.Input("topno");
        Assert.Single(c.FindAll(".sidref-pick-row"));
        cerca.KeyDown("Enter");

        Assert.Equal("[[SID LIBD TOPN9A]]", Assert.Single(JSInterop.Invocations["vipiSidInserisci"]).Arguments[1]);
    }

    /// <summary>Il tasto segna il campo PRIMA di aprire: aperto il selettore, il fuoco va nella sua ricerca.</summary>
    [Fact]
    public void Il_campo_si_segna_al_clic_sul_tasto()
    {
        JSInterop.Setup<string>("vipiSidPrendi", _ => true).SetResult("g1");
        var c = CampoDiLIBD();

        c.Find("button.rta-sid").Click();

        Assert.Single(JSInterop.Invocations["vipiSidPrendi"]);
    }

    /// <summary>Sotto una tabella, senza una cella col fuoco il selettore non si apre: lo dice il tasto.</summary>
    [Fact]
    public void Sotto_una_tabella_senza_cella_selezionata_lo_dice()
    {
        JSInterop.Setup<string>("vipiSidPrendiDa", _ => true).SetResult("");
        var c = RenderComponent<TastoSidTabella>();

        c.Find("button").Click();

        Assert.Contains("Sid_Cell_NoFocus", c.Markup);
        Assert.Empty(c.FindAll(".sidref-pick"));
    }

    [Fact]
    public void Sotto_una_tabella_con_la_cella_selezionata_si_sceglie()
    {
        JSInterop.Setup<string>("vipiSidPrendiDa", _ => true).SetResult("g1");
        JSInterop.Setup<bool>("vipiSidInserisci", _ => true).SetResult(true);
        var c = RenderComponent<TastoSidTabella>(p => p.AddCascadingValue("IcaoDelDocumento", "LIBD"));

        c.Find("button").Click();
        c.WaitForAssertion(() => Assert.Equal(2, c.FindAll(".sidref-pick-row").Count));
        c.FindAll(".sidref-pick-row").ElementAt(1).Click();

        Assert.Equal("[[SID LIBD TOPN9A]]", Assert.Single(JSInterop.Invocations["vipiSidInserisci"]).Arguments[1]);
    }

    /// <summary>🔴 Revisione del 18 settembre 2026: l'inserimento porta il GETTONE di quell'apertura. Con un segno
    /// solo per la pagina, due selettori aperti si scambiavano il campo — la scelta fatta in A finiva in B.</summary>
    [Fact]
    public void L_inserimento_porta_il_gettone_di_chi_ha_aperto()
    {
        JSInterop.Setup<string>("vipiSidPrendi", _ => true).SetResult("g7");
        JSInterop.Setup<bool>("vipiSidInserisci", _ => true).SetResult(true);
        var c = CampoDiLIBD();

        c.Find("button.rta-sid").Click();
        c.WaitForAssertion(() => Assert.Equal(2, c.FindAll(".sidref-pick-row").Count));
        c.FindAll(".sidref-pick-row").First().Click();

        Assert.Equal("g7", Assert.Single(JSInterop.Invocations["vipiSidInserisci"]).Arguments[0]);
    }

    /// <summary>Nessun campo segnato (il JS torna un gettone vuoto): il selettore non si apre.</summary>
    [Fact]
    public void Senza_un_campo_segnato_il_selettore_non_si_apre()
    {
        JSInterop.Setup<string>("vipiSidPrendi", _ => true).SetResult("");
        var c = CampoDiLIBD();

        c.Find("button.rta-sid").Click();

        Assert.Empty(c.FindAll(".sidref-pick"));
    }

    /// <summary>Dove il testo non passa da un disegno che risolve i nomi (l'intro VFR dell'APP), il tasto non c'è:
    /// prometterebbe un nome che si aggiorna e non si aggiornerebbe.</summary>
    [Fact]
    public void Dove_il_nome_non_si_aggiornerebbe_il_tasto_non_c_e()
    {
        var c = RenderComponent<RichTextArea>(p => p.Add(x => x.CitaSid, false));
        Assert.Empty(c.FindAll("button.rta-sid"));
    }

    /// <summary>🔴 Le funzioni che la UI chiama esistono nel JS con QUEL nome: un nome sbagliato qui è un tasto
    /// che non fa niente, e con <c>JSRuntimeMode.Loose</c> nessun test bUnit se ne accorgerebbe.</summary>
    [Fact]
    public void Le_funzioni_chiamate_esistono_in_vipi_editor_js()
    {
        var js = File.ReadAllText(TrovaFile("src/Vipi.Ui/wwwroot/vipi-editor.js"));
        foreach (var f in new[] { "vipiSidPrendi", "vipiSidPrendiDa", "vipiSidInserisci" })
            Assert.Contains($"window.{f} = function", js);
    }

    private static string TrovaFile(string relativo)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, relativo))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new FileNotFoundException(relativo), relativo);
    }
}
