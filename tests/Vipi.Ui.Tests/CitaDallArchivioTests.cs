using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain.Entities;
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
public class CitaDallArchivioTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private sealed class ElencoFinto : IProcedureReferenceResolver
    {
        public List<string> Chiesti { get; } = new();

        public Task<NomiProcedura> PerVistaAsync(IEnumerable<SectionView> sezioni, bool pubblica,
            string? proprioIcao = null, AirportSidView? propriaTabella = null,
            AirportSidView? propriaTabellaStar = null, CancellationToken ct = default) =>
            Task.FromResult(NomiProcedura.Vuoto);

        public Task<NomiProcedura> PerTestiAsync(IEnumerable<string?> testi, CancellationToken ct = default) =>
            Task.FromResult(NomiProcedura.Vuoto);

        public Task<IReadOnlyList<ProceduraCitabile>> ElencoAsync(string icao, ProcedureKind kind = ProcedureKind.Sid, CancellationToken ct = default)
        {
            Chiesti.Add(icao);
            IReadOnlyList<ProceduraCitabile> elenco = icao == "LIBD"
                ? new[] { new ProceduraCitabile(ProcedureKind.Sid, "LIBD", "BANA8A", "BANAV 8A", "07"), new ProceduraCitabile(ProcedureKind.Sid, "LIBD", "TOPN9A", "TOPNO 9A", "07") }
                : Array.Empty<ProceduraCitabile>();
            return Task.FromResult(elenco);
        }
    }

    /// <summary>Gli enti col loro nominativo: due, uno dello scalo del documento e uno no.</summary>
    private sealed class EntiFinti : IFrequenzeDegliEnti
    {
        public Task<IReadOnlyList<LinkableFrequencyRow>> TutteAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<LinkableFrequencyRow>>(new[]
            {
                new LinkableFrequencyRow(1, "LIRF", "LIRF_TWR", "118.700", "Fiumicino Tower"),
                new LinkableFrequencyRow(2, "LIBD", "LIBD_TWR", "118.300", "Bari Tower"),
            });

        /// <summary>⚠️ LIBD_APP non ha frequenza: il suo NOMINATIVO si deve poter citare lo stesso.</summary>
        public Task<IReadOnlyList<EnteRow>> NominativiAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<EnteRow>>(new[]
            {
                new EnteRow(1, "LIRF", "LIRF_TWR", "Fiumicino Tower"),
                new EnteRow(2, "LIBD", "LIBD_TWR", "Bari Tower"),
                new EnteRow(3, "LIBD", "LIBD_APP", "Bari Approach"),
            });

        /// <summary>Gli scali chiesti: serve a dire che le piste si chiedono per lo SCALO del documento.</summary>
        public List<string> ScaliChiesti { get; } = new();

        public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> PisteAsync(
            IReadOnlyCollection<string> icaos, CancellationToken ct = default)
        {
            ScaliChiesti.AddRange(icaos);
            var d = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var i in icaos)
                if (string.Equals(i, "LIBD", StringComparison.OrdinalIgnoreCase)) d[i] = new[] { "07", "25" };
            return Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<string>>>(d);
        }

        public Task<IReadOnlySet<string>> PuntiAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlySet<string>>(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "BANAV", "TOPNO" });
    }

    private readonly ElencoFinto _elenco = new();
    private readonly EntiFinti _enti = new();

    public CitaDallArchivioTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddScoped<IProcedureReferenceResolver>(_ => _elenco);
        Services.AddScoped<IFrequenzeDegliEnti>(_ => _enti);
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

    /// <summary>
    /// Le chip del selettore: <b>tutte e sei</b> le famiglie che il testo sa citare. Un gesto solo per tutte
    /// — sei tasti in barra sarebbero sei decisioni prima ancora di aprire l'elenco.
    ///
    /// <para>🔴 Erano quattro fino al 20 settembre 2026: piste e punti si risolvevano e si segnalavano, ma
    /// non si potevano inserire. Una famiglia che il renderer riconosce e il selettore no si scrive a mano,
    /// e una chiave scritta a mano sbaglia.</para>
    /// </summary>
    [Fact]
    public void Il_selettore_ha_tutte_le_famiglie_citabili()
    {
        JSInterop.Setup<string>("vipiSidPrendi", _ => true).SetResult("g1");
        var c = CampoDiLIBD();
        c.Find("button.rta-sid").Click();
        c.WaitForAssertion(() => Assert.NotEmpty(c.FindAll(".sidref-pick-kind button")));

        Assert.Equal(new[] { "SID", "STAR", "FREQ", "ATC", "RWY", "FIX" },
            c.FindAll(".sidref-pick-kind button").Select(b => b.TextContent.Trim()).ToArray());
    }

    [Fact]
    public void Con_la_chip_FREQ_si_cita_una_frequenza()
    {
        JSInterop.Setup<string>("vipiSidPrendi", _ => true).SetResult("g1");
        JSInterop.Setup<bool>("vipiSidInserisci", _ => true).SetResult(true);
        var c = CampoDiLIBD();
        c.Find("button.rta-sid").Click();
        c.WaitForAssertion(() => Assert.NotEmpty(c.FindAll(".sidref-pick-kind button")));

        c.FindAll(".sidref-pick-kind button").First(b => b.TextContent.Trim() == "FREQ").Click();
        c.WaitForAssertion(() => Assert.Equal(2, c.FindAll(".sidref-pick-row").Count));

        // ⚠️ Lo scalo del documento PRIMA: chi scrive la vIPI di LIBD cita quasi sempre un ente di LIBD.
        Assert.Contains("LIBD_TWR", c.FindAll(".sidref-pick-row").First().TextContent);
        Assert.Contains("118.300", c.FindAll(".sidref-pick-row").First().TextContent);

        c.FindAll(".sidref-pick-row").First().Click();
        Assert.Equal("[[FREQ LIBD_TWR]]", Assert.Single(JSInterop.Invocations["vipiSidInserisci"]).Arguments[1]);
    }

    [Fact]
    public void Con_la_chip_ATC_si_cita_il_nominativo()
    {
        JSInterop.Setup<string>("vipiSidPrendi", _ => true).SetResult("g1");
        JSInterop.Setup<bool>("vipiSidInserisci", _ => true).SetResult(true);
        var c = CampoDiLIBD();
        c.Find("button.rta-sid").Click();
        c.WaitForAssertion(() => Assert.NotEmpty(c.FindAll(".sidref-pick-kind button")));

        c.FindAll(".sidref-pick-kind button").First(b => b.TextContent.Trim() == "ATC").Click();
        // 🔴 TRE, non due: l'elenco dei nominativi non è quello delle frequenze. `LIBD_APP` non ha una
        // frequenza dichiarata, quindi non è fra le linkabili — ma un nome alla radio ce l'ha, e fino al
        // 20 settembre 2026 non si poteva citare.
        c.WaitForAssertion(() => Assert.Equal(3, c.FindAll(".sidref-pick-row").Count));

        var righe = c.FindAll(".sidref-pick-row").Select(r => r.TextContent).ToList();
        // Lo scalo del documento per primo, e dentro lo scalo per callsign.
        Assert.Contains("Bari Approach", righe[0]);
        // Nell'elenco si legge il NOMINATIVO, col callsign accanto: è quello che si sta citando.
        Assert.Contains("Bari Tower", righe[1]);

        // ⚠️ `Skip(1).First()` e non l'indicizzatore: quello di bUnit chiama un membro di AngleSharp che a
        // runtime non si risolve (`MissingMethodException`), e il test cadrebbe per la ragione sbagliata.
        c.FindAll(".sidref-pick-row").Skip(1).First().Click();
        Assert.Equal("[[ATC LIBD_TWR]]", Assert.Single(JSInterop.Invocations["vipiSidInserisci"]).Arguments[1]);
    }

    /// <summary>
    /// 🔴 La chip RWY. Fino al 20 settembre 2026 piste e punti si risolvevano nel testo e si segnalavano
    /// quando sparivano, ma non si potevano <b>inserire</b>: restava scriverli a mano, che è il modo di
    /// sbagliare la chiave e farsi dire dalla testata che un dato che c'è «non si trova più».
    /// </summary>
    [Fact]
    public void Con_la_chip_RWY_si_cita_una_soglia_dello_scalo()
    {
        JSInterop.Setup<string>("vipiSidPrendi", _ => true).SetResult("g1");
        JSInterop.Setup<bool>("vipiSidInserisci", _ => true).SetResult(true);
        var c = CampoDiLIBD();
        c.Find("button.rta-sid").Click();
        c.WaitForAssertion(() => Assert.NotEmpty(c.FindAll(".sidref-pick-kind button")));

        c.FindAll(".sidref-pick-kind button").First(b => b.TextContent.Trim() == "RWY").Click();
        c.WaitForAssertion(() => Assert.Equal(2, c.FindAll(".sidref-pick-row").Count));

        // Le soglie si chiedono all'anagrafica dello SCALO DEL DOCUMENTO, ed escono come sono scritte.
        Assert.Contains("LIBD", _enti.ScaliChiesti);
        Assert.Contains("07", c.FindAll(".sidref-pick-row").First().TextContent);

        c.FindAll(".sidref-pick-row").First().Click();
        Assert.Equal("[[RWY LIBD 07]]", Assert.Single(JSInterop.Invocations["vipiSidInserisci"]).Arguments[1]);
    }

    /// <summary>La chip FIX: il catalogo dei punti è di tutta la divisione, quindi niente ICAO.</summary>
    [Fact]
    public void Con_la_chip_FIX_si_cita_un_punto_del_catalogo()
    {
        JSInterop.Setup<string>("vipiSidPrendi", _ => true).SetResult("g1");
        JSInterop.Setup<bool>("vipiSidInserisci", _ => true).SetResult(true);
        var c = CampoDiLIBD();
        c.Find("button.rta-sid").Click();
        c.WaitForAssertion(() => Assert.NotEmpty(c.FindAll(".sidref-pick-kind button")));

        c.FindAll(".sidref-pick-kind button").First(b => b.TextContent.Trim() == "FIX").Click();
        c.WaitForAssertion(() => Assert.Equal(2, c.FindAll(".sidref-pick-row").Count));

        // I punti non vivono dentro uno scalo: il campo ICAO non si mostra nemmeno.
        Assert.Empty(c.FindAll("input.icao"));

        c.FindAll(".sidref-pick-row").First().Click();
        Assert.Equal("[[FIX BANAV]]", Assert.Single(JSInterop.Invocations["vipiSidInserisci"]).Arguments[1]);
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
        var c = RenderComponent<TastoRiferimentoTabella>();

        c.Find("button").Click();

        Assert.Contains("Sid_Cell_NoFocus", c.Markup);
        Assert.Empty(c.FindAll(".sidref-pick"));
    }

    [Fact]
    public void Sotto_una_tabella_con_la_cella_selezionata_si_sceglie()
    {
        JSInterop.Setup<string>("vipiSidPrendiDa", _ => true).SetResult("g1");
        JSInterop.Setup<bool>("vipiSidInserisci", _ => true).SetResult(true);
        var c = RenderComponent<TastoRiferimentoTabella>(p => p.AddCascadingValue("IcaoDelDocumento", "LIBD"));

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
