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
/// Le configurazioni possibili in Struttura: si scelgono per ACC, si leggono senza lock, si scrivono con
/// «Applica» — e sotto ogni elenco c'è quello che ne segue, <b>prima</b> di applicare. Carta
/// <c>docs/feature/2026-10-08-configurazioni-possibili.md</c> §4.
/// </summary>
public class StructureConfigurationsTests : TestContext
{
    private const string Ws2 = "LIMM_WS2_CTR", Es2 = "LIMM_ES2_CTR", Mil = "LIMM_MIL_CTR";
    private const string Ww0 = "LIMF_WW0_APP", Wn0 = "LIMF_WN0_APP", Ws0 = "LIMJ_WS0_APP";

    private sealed class ChiaveComeValore : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + ":" + string.Join("|", arguments), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private static AccConfiguration Cfg(string nome, params string[] aperti) => new()
    {
        Key = "cfg:" + nome,
        Name = nome,
        Open = aperti.Select(a => new AccConfigOpen { Callsign = a }).ToList(),
    };

    /// <summary>Il servizio finto: tiene gli elenchi in memoria e registra ogni scrittura.</summary>
    private sealed class ServizioFinto : ISectorConfigurationService
    {
        public Dictionary<string, List<AccConfiguration>> Elenchi { get; } = new(StringComparer.OrdinalIgnoreCase)
        {
            ["LIMM"] = new() { Cfg("Conf 1", Ws2), Cfg("Conf 2", Es2, Ws2) },
            // Come sta oggi nel documento: mancano {WN0} e {WS0}.
            [Ww0] = new() { Cfg("Conf 1", Ww0), Cfg("Conf 2", Ww0, Ws0), Cfg("Conf 3", Wn0, Ws0) },
        };
        public List<(string Codice, string[] Nomi)> Scritture { get; } = new();
        public HashSet<string> Completi { get; } = new(StringComparer.OrdinalIgnoreCase) { "LIMM" };
        public int Letture { get; private set; }
        public string? Rifiuta { get; set; }

        private static AccSectorPick P(string cs) => new(cs, cs);

        public Task<IReadOnlyList<GruppoDiSettori>> GruppiAsync(string accCode, CancellationToken ct = default)
        {
            Letture++;
            IReadOnlyList<GruppoDiSettori> gruppi = new[]
            {
                new GruppoDiSettori(ConfigurationGroupKind.AccArea, "LIMM", "LIMM", new[] { P(Es2), P(Mil), P(Ws2) }, Copia("LIMM"), Completi.Contains("LIMM")),
                new GruppoDiSettori(ConfigurationGroupKind.AtcUnit, Ww0, "Torino - Genova", new[] { P(Ww0), P(Wn0), P(Ws0) }, Copia(Ww0), Completi.Contains(Ww0)),
            };
            return Task.FromResult(gruppi);
        }

        private List<AccConfiguration> Copia(string codice) => ConfigurazioniJson.Leggi(ConfigurazioniJson.Scrivi(Elenchi[codice]));

        public Task<IReadOnlyList<AccConfiguration>> ListAsync(ConfigurationGroupKind genere, string codice, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AccConfiguration>>(Copia(codice));

        public Task ReplaceAsync(ConfigurationGroupKind genere, string codice, IReadOnlyList<AccConfiguration> configurazioni,
            bool completo, CancellationToken ct = default)
        {
            if (Rifiuta is not null) throw new Vipi.Application.Aor.ValidationException(Rifiuta);
            if (completo) Completi.Add(codice); else Completi.Remove(codice);
            Scritture.Add((codice, configurazioni.Select(c => c.Name + "=" + string.Join("+", c.OpenCallsigns)).ToArray()));
            Elenchi[codice] = ConfigurazioniJson.Leggi(ConfigurazioniJson.Scrivi(configurazioni.ToList()));
            return Task.CompletedTask;
        }

        public Task<ConfigurazioniPossibili> TutteAsync(CancellationToken ct = default) =>
            Task.FromResult(ConfigurazioniPossibili.Nessuna);
    }

    private readonly ServizioFinto _servizio = new();

    public StructureConfigurationsTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new ChiaveComeValore());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddSingleton<ISectorConfigurationService>(_servizio);
    }

    private IRenderedComponent<StructureConfigurations> Monta(bool canEdit) => RenderComponent<StructureConfigurations>(p => p
        .Add(x => x.Accs, new[] { new AccRow(1, "LIMM", "Milano", "LI", 4) })
        .Add(x => x.InFila, op => op())
        .Add(x => x.CanEdit, canEdit));

    private static async Task ScegliMilano(IRenderedComponent<StructureConfigurations> cut) =>
        await cut.InvokeAsync(() => cut.Find("#cfgs-acc").Change("LIMM"));

    private static AngleSharp.Dom.IElement Gruppo(IRenderedComponent<StructureConfigurations> cut, string codice) =>
        cut.FindAll(".cfgs-group").First(g => g.GetAttribute("data-cfgs-group") == codice);

    private static string[] Riga(AngleSharp.Dom.IElement gruppo, string settore) =>
        gruppo.QuerySelectorAll(".cfgs-cons tbody tr").First(r => r.Children[0].TextContent.Trim() == settore)
            .Children.Select(c => c.TextContent.Trim()).ToArray();

    [Fact]
    public void Senza_un_ACC_scelto_non_legge_niente()
    {
        var cut = Monta(canEdit: false);

        Assert.Equal(0, _servizio.Letture);
        Assert.Empty(cut.FindAll(".cfgs-group"));
    }

    [Fact]
    public async Task Senza_lock_si_leggono_gli_elenchi_e_quel_che_ne_segue_ma_non_c_e_niente_da_premere()
    {
        var cut = Monta(canEdit: false);
        await ScegliMilano(cut);

        Assert.Equal(2, cut.FindAll(".cfgs-group").Count);
        Assert.Empty(cut.FindAll(".cfgs-group button"));
        Assert.Empty(cut.FindAll(".cfgs-group input"));

        // Milano: ES2 apre solo con WS2 — ricavato, non scritto.
        Assert.Equal(new[] { Es2, Ws2, "—", "Cfgs_No" }, Riga(Gruppo(cut, "LIMM"), Es2));
        Assert.Equal(new[] { Ws2, "—", "—", "Cfgs_Yes" }, Riga(Gruppo(cut, "LIMM"), Ws2));
        // MIL è del gruppo ma nessuna configurazione lo nomina: si dice che resta libero.
        Assert.Contains(Mil, Gruppo(cut, "LIMM").TextContent);
        Assert.Contains("Cfgs_Unnamed", Gruppo(cut, "LIMM").TextContent);
    }

    /// <summary>
    /// 🔴 Il buco di Torino si LEGGE: con le tre configurazioni del documento, WN0 risulta «solo con WS0» e WS0
    /// «non da solo» — tutt'e due falsi (committente, 8 ottobre 2026), ed è da qui che ci si accorge che mancano.
    /// </summary>
    [Fact]
    public async Task L_elenco_incompleto_di_Torino_mostra_le_conseguenze_false()
    {
        var cut = Monta(canEdit: false);
        await ScegliMilano(cut);

        Assert.Equal(new[] { Wn0, Ws0, Ww0, "Cfgs_No" }, Riga(Gruppo(cut, Ww0), Wn0));
        Assert.Equal(new[] { Ws0, "—", "—", "Cfgs_No" }, Riga(Gruppo(cut, Ww0), Ws0));
    }

    [Fact]
    public async Task Col_lock_un_gesto_cambia_le_conseguenze_subito_e_scrive_solo_con_Applica()
    {
        var cut = Monta(canEdit: true);
        await ScegliMilano(cut);
        Assert.Empty(cut.FindAll(".pill.amber"));

        // Si aggiunge una configurazione al gruppo di Torino e ci si apre WN0 da solo.
        await cut.InvokeAsync(() => Gruppo(cut, Ww0).QuerySelectorAll("button").First(b => b.TextContent.Trim() == "AppCfg_Add").Click());
        await cut.InvokeAsync(() => Gruppo(cut, Ww0).QuerySelectorAll(".block").Last()
            .QuerySelectorAll(".sp-item").First(b => b.TextContent.Trim() == Wn0).Click());

        // Le conseguenze seguono la copia di lavoro: WN0 non è più «solo con WS0», e può stare da solo.
        Assert.Equal(new[] { Wn0, "—", Ww0, "Cfgs_Yes" }, Riga(Gruppo(cut, Ww0), Wn0));
        Assert.Single(cut.FindAll(".pill.amber"));
        Assert.True(cut.Instance.Sporco);
        Assert.Empty(_servizio.Scritture);                      // niente è stato scritto

        await cut.InvokeAsync(() => Gruppo(cut, Ww0).QuerySelectorAll(".fb-acts .btn.primary").First().Click());

        var (codice, nomi) = Assert.Single(_servizio.Scritture);
        Assert.Equal(Ww0, codice);
        Assert.Equal(4, nomi.Length);
        Assert.EndsWith("=" + Wn0, nomi[3]);
        Assert.False(cut.Instance.Sporco);
        Assert.Empty(cut.FindAll(".pill.amber"));
    }

    /// <summary>
    /// 🔴 La casella «l'elenco è completo» è quella che fa vincolare. Spenta, le conseguenze si dicono al
    /// condizionale; accenderla è una modifica come le altre — si applica, e arriva al servizio.
    /// </summary>
    [Fact]
    public async Task La_casella_completo_si_applica_e_cambia_come_si_dicono_le_conseguenze()
    {
        var cut = Monta(canEdit: true);
        await ScegliMilano(cut);

        // Milano è completo, Torino è un elenco di esempi (come dopo il travaso).
        Assert.Contains("Cfgs_StateExhaustive", Gruppo(cut, "LIMM").TextContent);
        Assert.Contains("Cfgs_Consequences", Gruppo(cut, "LIMM").QuerySelectorAll(".app-lbl").Select(p => p.TextContent.Trim()));
        Assert.Contains("Cfgs_StateExamples", Gruppo(cut, Ww0).TextContent);
        Assert.Contains("Cfgs_ConsequencesIf", Gruppo(cut, Ww0).QuerySelectorAll(".app-lbl").Select(p => p.TextContent.Trim()));
        Assert.False(cut.Instance.Sporco);

        await cut.InvokeAsync(() => Gruppo(cut, Ww0).QuerySelector(".cfgs-exhaustive input")!.Change(true));

        Assert.True(cut.Instance.Sporco);
        Assert.DoesNotContain(Ww0, _servizio.Completi);          // non ancora scritto
        Assert.Contains("Cfgs_StateExhaustive", Gruppo(cut, Ww0).TextContent);

        await cut.InvokeAsync(() => Gruppo(cut, Ww0).QuerySelectorAll(".fb-acts .btn.primary").First().Click());

        Assert.Contains(Ww0, _servizio.Completi);
        Assert.False(cut.Instance.Sporco);

        // «Annulla» riporta anche la casella.
        await cut.InvokeAsync(() => Gruppo(cut, "LIMM").QuerySelector(".cfgs-exhaustive input")!.Change(false));
        Assert.True(cut.Instance.Sporco);
        await cut.InvokeAsync(() => Gruppo(cut, "LIMM").QuerySelectorAll(".fb-acts .btn.ghost").First().Click());
        Assert.False(cut.Instance.Sporco);
        Assert.Contains("LIMM", _servizio.Completi);
    }

    [Fact]
    public async Task Annulla_riporta_l_elenco_a_quello_scritto()
    {
        var cut = Monta(canEdit: true);
        await ScegliMilano(cut);

        await cut.InvokeAsync(() => Gruppo(cut, "LIMM").QuerySelectorAll(".block").First()
            .QuerySelectorAll(".sp-item").First(b => b.TextContent.Trim() == Es2).Click());
        Assert.True(cut.Instance.Sporco);

        await cut.InvokeAsync(() => Gruppo(cut, "LIMM").QuerySelectorAll(".fb-acts .btn.ghost").First().Click());

        Assert.False(cut.Instance.Sporco);
        Assert.Empty(_servizio.Scritture);
        Assert.Equal(new[] { Es2, Ws2, "—", "Cfgs_No" }, Riga(Gruppo(cut, "LIMM"), Es2));
    }

    [Fact]
    public async Task Un_rifiuto_del_servizio_diventa_una_frase_e_l_elenco_resta_da_applicare()
    {
        _servizio.Rifiuta = "«LIRR_NE_CTR» non è un settore del gruppo.";
        var cut = Monta(canEdit: true);
        await ScegliMilano(cut);
        await cut.InvokeAsync(() => Gruppo(cut, "LIMM").QuerySelectorAll(".block").First()
            .QuerySelectorAll(".sp-item").First(b => b.TextContent.Trim() == Es2).Click());

        await cut.InvokeAsync(() => Gruppo(cut, "LIMM").QuerySelectorAll(".fb-acts .btn.primary").First().Click());

        Assert.Contains("LIRR_NE_CTR", cut.Find("[role=alert]").TextContent);
        Assert.True(cut.Instance.Sporco);
    }

    /// <summary>
    /// «Inizia modifica» rilegge PRIMA di poter scrivere (U-011): nel frattempo il lock l'ha tenuto un altro, e
    /// gli elenchi si riscrivono per intero — la prima «Applica» riporterebbe indietro il suo lavoro.
    /// </summary>
    [Fact]
    public async Task Alla_presa_del_lock_gli_elenchi_si_rileggono()
    {
        var cut = Monta(canEdit: false);
        await ScegliMilano(cut);
        Assert.Equal(1, _servizio.Letture);

        _servizio.Elenchi["LIMM"] = new() { Cfg("Di un altro", Ws2) };
        cut.SetParametersAndRender(p => p.Add(x => x.CanEdit, true));
        cut.WaitForAssertion(() => Assert.Equal(2, _servizio.Letture));

        Assert.Equal("Di un altro", Gruppo(cut, "LIMM").QuerySelector("input.app-in")!.GetAttribute("value"));
    }
}
