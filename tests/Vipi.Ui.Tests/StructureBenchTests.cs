using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il banco di prova in Struttura: scelgo un ACC, apro e chiudo i settori, e la tabella dice chi tiene cosa. Qui si
/// prova il componente — che chieda lo scenario giusto e mostri quel che torna — non il motore, che sta in
/// <c>CoverageBenchTests</c> e <c>CoperturaUnicaTests</c>. Carta <c>docs/feature/2026-10-04-copertura-unica.md</c>.
/// </summary>
public class StructureBenchTests : TestContext
{
    private const string Ws2 = "LIMM_WS2_CTR", Es2 = "LIMM_ES2_CTR", Ws5 = "LIMM_WS5_CTR", Es5 = "LIMM_ES5_CTR";
    private const string Torino = "LIMF_WW0_APP", Genova = "LIMJ_WS0_APP";
    private const string RomaNe = "LIRR_NE_CTR", RomaNc = "LIRR_NC_CTR", Firenze = "LIRQ_NN0_APP", PadovaSw = "LIPP_SW1_CTR";

    /// <summary>Localizzatore che rende la CHIAVE: qui si prova il markup, non le traduzioni.</summary>
    private sealed class ChiaveComeValore : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + "(" + string.Join(",", arguments) + ")", resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    /// <summary>Il banco finto: registra ogni scenario chiesto e risponde con una tabella che lo ripete.</summary>
    private sealed class BancoFinto : ICoverageBenchService
    {
        public List<string[]> Scenari { get; } = new();
        public IReadOnlyList<ResolvedTransferFlow> Trasferimenti { get; set; } = Array.Empty<ResolvedTransferFlow>();

        /// <summary>Quel che il banco dice «non previsto» quando ES2 è aperto senza WS2 — come farebbe l'elenco vero.</summary>
        public bool ConElenco { get; set; }

        /// <summary>Aggiunge un ente con due avvicinamenti e una sua configurazione: un SECONDO gruppo.</summary>
        public bool ConAvvicinamenti { get; set; }

        /// <summary>Aggiunge gli enti di due altri centri: Roma (con una sua configurazione) e Padova.</summary>
        public bool ConAltriCentri { get; set; }

        /// <summary>Gli altri enti detti al servizio all'ultima domanda.</summary>
        public string[] Altri { get; private set; } = Array.Empty<string>();

        public Task<BenchScope?> ScopeAsync(string accCode, CancellationToken ct = default)
        {
            var settori = new List<BenchSector>
            {
                new(Ws2, "Milano WS2", Area: true), new(Es2, "Milano ES2", Area: true),
                new(Ws5, "Milano WS5", Area: true), new(Es5, "Milano ES5", Area: true),
            };
            var gruppi = new List<BenchPresetGroup>
            {
                new("LIMM", new[] { Ws2, Es2, Ws5, Es5 }, new[] { new BenchPreset("Fino a UNL", new[] { Ws2, Es2 }) }),
            };
            if (ConAvvicinamenti)
            {
                settori.Add(new(Torino, "Torino", Area: false));
                settori.Add(new(Genova, "Genova", Area: false));
                gruppi.Add(new("Milano West", new[] { Torino, Genova }, new[] { new BenchPreset("Solo Torino", new[] { Torino }) }));
            }
            var altri = !ConAltriCentri
                ? Array.Empty<BenchNeighbour>()
                : new[]
                {
                    new BenchNeighbour("LIPP", "Padova", new[] { new BenchSector(PadovaSw, "Padova SW1", Area: true) },
                        Array.Empty<BenchPresetGroup>()),
                    new BenchNeighbour("LIRR", "Roma",
                        new[]
                        {
                            new BenchSector(RomaNc, "Roma NC", Area: true), new BenchSector(RomaNe, "Roma NE", Area: true),
                            new BenchSector(Firenze, "Firenze", Area: false),
                        },
                        new[] { new BenchPresetGroup("LIRR", new[] { RomaNc, RomaNe }, new[] { new BenchPreset("Solo NC", new[] { RomaNc }) }) }),
                };
            return Task.FromResult<BenchScope?>(new BenchScope(accCode, settori, gruppi) { Neighbours = altri });
        }

        public Task<BenchOutcome> SimulateAsync(string accCode, IReadOnlyCollection<string> open,
            IReadOnlyCollection<string> neighbours, CancellationToken ct = default)
        {
            var aperti = open.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            Scenari.Add(aperti);
            Altri = neighbours.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            var gruppi = aperti.Select(a => new BenchGroup(a, Outside: false, Array.Empty<BenchItem>())).ToList();
            if (!open.Contains(Es5)) gruppi.Add(new BenchGroup(null, Outside: false, new[] { new BenchItem(Es5, "FL325–UNL") }));
            var fuori = ConElenco && open.Contains(Es2) && !open.Contains(Ws2)
                ? new[]
                {
                    new GruppoFuoriElenco(
                        new ElencoDiConfigurazioni(ConfigurationGroupKind.AccArea, "LIMM", Array.Empty<AccConfiguration>()),
                        aperti),
                }
                : Array.Empty<GruppoFuoriElenco>();
            return Task.FromResult(new BenchOutcome(gruppi, Trasferimenti, fuori));
        }
    }

    private readonly BancoFinto _banco = new();

    public StructureBenchTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new ChiaveComeValore());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddSingleton<ICoverageBenchService>(_banco);
    }

    private IRenderedComponent<StructureBench> Monta() => RenderComponent<StructureBench>(p => p
        .Add(x => x.Accs, new[] { new AccRow(1, "LIMM", "Milano", "LI", 4) })
        .Add(x => x.InFila, op => op()));

    private static async Task ScegliMilano(IRenderedComponent<StructureBench> cut) =>
        await cut.InvokeAsync(() => cut.Find("#bench-acc").Change("LIMM"));

    /// <summary>
    /// Uno scenario che non è una configurazione possibile dichiarata si DICE — e la tabella esce lo stesso: il
    /// banco serve anche a vedere che cosa succede quando qualcuno lo fa davvero (carta
    /// 2026-10-08-configurazioni-possibili §4).
    /// </summary>
    [Fact]
    public async Task Uno_scenario_non_previsto_si_dice_e_la_tabella_esce_lo_stesso()
    {
        _banco.ConElenco = true;
        var cut = Monta();
        await ScegliMilano(cut);
        Assert.Empty(cut.FindAll(".bench-notforeseen"));   // tutti aperti: previsto

        await cut.InvokeAsync(() => cut.FindAll(".sector-pick .sp-item").First(b => b.TextContent.Trim() == Ws2).Click());

        var avviso = Assert.Single(cut.FindAll(".bench-notforeseen"));
        Assert.Contains("LIMM", avviso.TextContent);
        Assert.Contains(Es2, avviso.TextContent);
        Assert.NotEmpty(cut.FindAll(".cfg-table tbody tr"));
    }

    [Fact]
    public void Senza_un_ACC_scelto_non_chiede_niente()
    {
        var cut = Monta();

        Assert.Empty(_banco.Scenari);
        Assert.Empty(cut.FindAll(".sector-pick .sp-item"));
    }

    [Fact]
    public async Task Scelto_l_ACC_parte_da_tutti_aperti()
    {
        var cut = Monta();
        await ScegliMilano(cut);

        Assert.Equal(new[] { Es2, Es5, Ws2, Ws5 }, Assert.Single(_banco.Scenari));
        // Quattro settori accesi; la configurazione pubblicata è un tasto in più, spento perché non coincide.
        Assert.Equal(4, cut.FindAll(".sector-pick .sp-item.on").Count);
        Assert.Equal(5, cut.FindAll(".sector-pick .sp-item").Count);
    }

    [Fact]
    public void Chi_arriva_dai_Trasferimenti_trova_il_banco_gia_sul_suo_ACC()
    {
        var cut = RenderComponent<StructureBench>(p => p
            .Add(x => x.Accs, new[] { new AccRow(1, "LIMM", "Milano", "LI", 4) })
            .Add(x => x.InFila, op => op())
            .Add(x => x.AccIniziale, "limm"));

        Assert.Single(_banco.Scenari);
        Assert.Equal(4, cut.FindAll(".sector-pick .sp-item.on").Count);
    }

    [Fact]
    public void Un_ACC_iniziale_che_non_esiste_non_apre_niente()
    {
        var cut = RenderComponent<StructureBench>(p => p
            .Add(x => x.Accs, new[] { new AccRow(1, "LIMM", "Milano", "LI", 4) })
            .Add(x => x.InFila, op => op())
            .Add(x => x.AccIniziale, "XXXX"));

        Assert.Empty(_banco.Scenari);
        Assert.Empty(cut.FindAll(".sector-pick .sp-item"));
    }

    [Fact]
    public async Task Chiudere_un_settore_richiede_lo_scenario_senza_di_lui_e_mostra_chi_resta_scoperto()
    {
        var cut = Monta();
        await ScegliMilano(cut);

        await cut.InvokeAsync(() => cut.FindAll(".sector-pick .sp-item").First(b => b.TextContent.Trim() == Es5).Click());

        Assert.Equal(new[] { Es2, Ws2, Ws5 }, _banco.Scenari.Last());
        Assert.Contains("Bench_Nobody", cut.Markup);
        Assert.Contains($"{Es5} (FL325–UNL)", cut.Markup);
    }

    [Fact]
    public async Task Una_configurazione_pubblicata_apre_i_suoi_settori_e_si_accende()
    {
        var cut = Monta();
        await ScegliMilano(cut);

        await cut.InvokeAsync(() => cut.FindAll(".sector-pick .sp-item").First(b => b.TextContent.Trim() == "Fino a UNL").Click());

        Assert.Equal(new[] { Es2, Ws2 }, _banco.Scenari.Last());
        Assert.Contains(cut.FindAll(".sector-pick .sp-item.on"), b => b.TextContent.Trim() == "Fino a UNL");
    }

    /// <summary>
    /// Il difetto del 12 ottobre 2026: «Conf 2» dell'area apriva i suoi due settori e chiudeva tutto il resto
    /// dell'ACC, avvicinamenti compresi. Una configurazione parla dei soli settori del suo gruppo.
    /// </summary>
    [Fact]
    public async Task Una_configurazione_di_un_gruppo_non_chiude_i_settori_degli_altri()
    {
        _banco.ConAvvicinamenti = true;
        var cut = Monta();
        await ScegliMilano(cut);

        await cut.InvokeAsync(() => cut.FindAll(".sector-pick .sp-item").First(b => b.TextContent.Trim() == "Fino a UNL").Click());
        Assert.Equal(new[] { Es2, Torino, Genova, Ws2 }.OrderBy(x => x, StringComparer.OrdinalIgnoreCase), _banco.Scenari.Last());

        // E il verso opposto: la configurazione dell'ente chiude Genova, non l'area — e tutte e due restano accese.
        await cut.InvokeAsync(() => cut.FindAll(".sector-pick .sp-item").First(b => b.TextContent.Trim() == "Solo Torino").Click());
        Assert.Equal(new[] { Es2, Torino, Ws2 }.OrderBy(x => x, StringComparer.OrdinalIgnoreCase), _banco.Scenari.Last());
        var accese = cut.FindAll(".bench-grp .sp-item.on").Select(b => b.TextContent.Trim()).ToArray();
        Assert.Equal(new[] { "Fino a UNL", "Solo Torino" }, accese);
    }

    // ---- gli altri enti degli accordi (12 ottobre 2026: «mancano i trasferimenti tra ACC») ----

    private static string[] Ordinati(params string[] nomi) => nomi.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();

    private static Task Premi(IRenderedComponent<StructureBench> cut, string selettore, string testo) =>
        cut.InvokeAsync(() => cut.FindAll(selettore).First(b => b.TextContent.Trim() == testo).Click());

    [Fact]
    public async Task Gli_enti_degli_altri_centri_sono_sul_tavolo_aperti_e_si_dicono_al_servizio()
    {
        _banco.ConAltriCentri = true;
        var cut = Monta();
        await ScegliMilano(cut);

        Assert.Equal(new[] { "LIPP", "LIRR" }, cut.FindAll(".bench-nb").Select(x => x.GetAttribute("data-bench-acc")));
        Assert.Equal(4, cut.FindAll(".bench-nb > .sector-pick .sp-item.on").Count);
        Assert.Equal(Ordinati(PadovaSw, RomaNc, RomaNe, Firenze), _banco.Altri);
        Assert.Equal(Ordinati(Ws2, Es2, Ws5, Es5, PadovaSw, RomaNc, RomaNe, Firenze), _banco.Scenari.Last());
    }

    /// <summary>
    /// Il difetto: fuori dall'ACC era tutto aperto per costruzione, e un trasferimento verso Roma non si poteva
    /// provare con Roma chiusa. Ora l'altro capo si chiude come un settore di casa.
    /// </summary>
    [Fact]
    public async Task Un_ente_di_un_altro_centro_si_chiude_e_il_resto_dello_scenario_non_cambia()
    {
        _banco.ConAltriCentri = true;
        var cut = Monta();
        await ScegliMilano(cut);

        await Premi(cut, ".bench-nb .sp-item", RomaNe);

        Assert.Equal(Ordinati(Ws2, Es2, Ws5, Es5, PadovaSw, RomaNc, Firenze), _banco.Scenari.Last());
        Assert.Equal(Ordinati(PadovaSw, RomaNc, RomaNe, Firenze), _banco.Altri);   // resta nello scenario: è CHIUSO, non assente
    }

    [Fact]
    public async Task Chiudere_tutto_un_centro_non_tocca_gli_altri_e_si_riapre_dallo_stesso_tasto()
    {
        _banco.ConAltriCentri = true;
        var cut = Monta();
        await ScegliMilano(cut);

        await cut.InvokeAsync(() => cut.Find(".bench-nb[data-bench-acc=LIRR] .bench-nb-all").Click());
        Assert.Equal(Ordinati(Ws2, Es2, Ws5, Es5, PadovaSw), _banco.Scenari.Last());
        Assert.Contains("Bench_OpenAll", cut.Find(".bench-nb[data-bench-acc=LIRR] .bench-nb-all").TextContent);

        await cut.InvokeAsync(() => cut.Find(".bench-nb[data-bench-acc=LIRR] .bench-nb-all").Click());
        Assert.Equal(Ordinati(Ws2, Es2, Ws5, Es5, PadovaSw, RomaNc, RomaNe, Firenze), _banco.Scenari.Last());
    }

    [Fact]
    public async Task La_configurazione_di_un_altro_centro_cambia_i_soli_settori_del_suo_gruppo()
    {
        _banco.ConAltriCentri = true;
        var cut = Monta();
        await ScegliMilano(cut);

        await Premi(cut, ".bench-nb .bench-grp .sp-item", "Solo NC");

        // NE si chiude; Firenze, che è di Roma ma non del gruppo, e Padova restano come sono.
        Assert.Equal(Ordinati(Ws2, Es2, Ws5, Es5, PadovaSw, RomaNc, Firenze), _banco.Scenari.Last());
    }

    [Fact]
    public async Task Tutti_chiusi_chiude_l_ACC_e_lascia_gli_altri_centri_Tutti_aperti_riapre_tutto()
    {
        _banco.ConAltriCentri = true;
        var cut = Monta();
        await ScegliMilano(cut);
        await Premi(cut, ".bench-nb .sp-item", RomaNe);

        await Premi(cut, "button.btn", "Bench_AllClosed");
        Assert.Equal(Ordinati(PadovaSw, RomaNc, Firenze), _banco.Scenari.Last());

        await Premi(cut, "button.btn", "Bench_AllOpen");
        Assert.Equal(Ordinati(Ws2, Es2, Ws5, Es5, PadovaSw, RomaNc, RomaNe, Firenze), _banco.Scenari.Last());
    }

    [Fact]
    public async Task Senza_altri_enti_il_riquadro_non_c_e()
    {
        var cut = Monta();
        await ScegliMilano(cut);

        Assert.Empty(cut.FindAll(".bench-nb"));
        Assert.DoesNotContain("Bench_Neighbours", cut.Markup);
        Assert.Empty(_banco.Altri);
    }

    /// <summary>
    /// Il difetto del 12 ottobre 2026: di suo si vedevano i soli punti che cambiavano mano, e un trasferimento che
    /// funziona come è scritto non c'era. Ora ci sono tutti; il filtro restringe.
    /// </summary>
    [Fact]
    public async Task Dei_trasferimenti_si_vedono_di_suo_tutti_e_il_filtro_li_restringe()
    {
        _banco.Trasferimenti = new[]
        {
            Flusso(Es5, Ws5, Punto("CAMBIA", "LIPP_CTR", "LIPP_CTR")),      // il cedente non è quello scritto
            Flusso(Ws2, Ws2, Punto("RESTA", "LIPP_CTR", "LIPP_CTR")),       // tutto come scritto
        };
        var cut = Monta();
        await ScegliMilano(cut);

        Assert.Contains("CAMBIA", cut.Markup);
        Assert.Contains("RESTA", cut.Markup);
        Assert.Contains("Bench_TransfersCount(2,1,1,0,0)", cut.Markup);
        Assert.Single(cut.FindAll(".bench-diff"));
        Assert.NotNull(cut.Find("details[open]"));

        await cut.InvokeAsync(() => cut.FindAll(".bench-filter .sp-item").First(b => b.TextContent.Contains("Bench_FilterChanged")).Click());
        Assert.Contains("CAMBIA", cut.Markup);
        Assert.DoesNotContain("RESTA", cut.Markup);
    }

    /// <summary>
    /// WS2 che raccoglie il suo ricevente chiuso non passa niente a nessuno: è il caso che la vista live toglie.
    /// Nel banco resta fra «tutte», attenuato, e non conta fra i cambi di mano.
    /// </summary>
    [Fact]
    public async Task Chi_passa_a_se_stesso_e_attenuato_e_non_conta_fra_i_cambi()
    {
        _banco.Trasferimenti = new[] { Flusso(Ws2, Ws2, Punto("EGHIN 1J", Genova, Ws2)) };
        var cut = Monta();
        await ScegliMilano(cut);

        Assert.Single(cut.FindAll("tr.bench-self"));
        Assert.Empty(cut.FindAll(".bench-diff"));
        Assert.Contains("Bench_StateSelf", cut.Markup);
        Assert.Contains("Bench_TransfersCount(1,0,0,1,0)", cut.Markup);

        await cut.InvokeAsync(() => cut.FindAll(".bench-filter .sp-item").First(b => b.TextContent.Contains("Bench_FilterChanged")).Click());
        Assert.Empty(cut.FindAll("tr.bench-self"));
        Assert.Contains("Bench_NoTransfers", cut.Markup);
    }

    [Fact]
    public async Task Una_clausola_che_non_riceve_nessuno_si_vede_e_resta_col_filtro_delle_non_coperte()
    {
        _banco.Trasferimenti = new[]
        {
            Flusso(Ws2, Ws2, Punto("SCOPERTO", Genova, TransferOnlineResolver.Unicom, online: false)),
            Flusso(Ws2, Ws2, Punto("RESTA", "LIPP_CTR", "LIPP_CTR")),
        };
        var cut = Monta();
        await ScegliMilano(cut);

        await cut.InvokeAsync(() => cut.FindAll(".bench-filter .sp-item").First(b => b.TextContent.Contains("Bench_FilterUncovered")).Click());

        Assert.Contains("SCOPERTO", cut.Markup);
        Assert.DoesNotContain("RESTA", cut.Markup);
        Assert.Contains("Bench_Nobody", cut.Find("td.bench-warn").TextContent);
    }

    private static ResolvedTransferFlow Flusso(string scritto, string cede, params ResolvedTransferPoint[] punti) => new()
    {
        Flow = new TransferFlowRow
        {
            Id = 1, AccCode = "LIMM", OwningSectorId = 1, OwningSectorCallsign = scritto,
            Kind = TransferFlowKind.Overflight, Order = 0, Points = Array.Empty<TransferPointRow>(),
        },
        ResolvedOwnerCallsign = cede,
        OwnerOnline = true,
        Points = punti,
    };

    private static ResolvedTransferPoint Punto(string cop, string scritto, string riceve, bool online = true) => new()
    {
        Point = new TransferPointRow
        {
            Id = 1, Cop = cop, LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.AtOrBelow,
            LevelText = "FL350", NextSectorCallsign = scritto, Order = 0,
        },
        ResolvedHandler = riceve,
        IsOnline = online,
    };
}
