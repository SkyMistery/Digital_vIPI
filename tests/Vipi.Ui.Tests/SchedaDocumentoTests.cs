using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Components.Doc;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// La scheda del documento negli editor e nella pagina Documenti (S133, carta
/// <c>docs/feature/2026-10-28-stato-dei-documenti.md</c>). Prende il posto del riquadro «Sezioni da scrivere», e ne
/// eredita le prove.
/// </summary>
public class SchedaDocumentoTests : TestContext
{
    private sealed class ChiaveComeValore : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private sealed class ServizioFinto : IStatoDelDocumentoService
    {
        public SchedaDelDocumento? Scheda { get; set; }
        public int Letture { get; private set; }

        public Task<SchedaDelDocumento?> SchedaAsync(ReleaseTargetType type, string key, CancellationToken ct = default)
        {
            Letture++;
            return Task.FromResult(Scheda);
        }
    }

    private readonly ServizioFinto _servizio = new();

    public SchedaDocumentoTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new ChiaveComeValore());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddSingleton<IStatoDelDocumentoService>(_servizio);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static StatoSezioni Milano() => new(7, new[]
    {
        new ContoSezioni("Settori di aerovia", 2, Array.Empty<SezioneVuota>()),
        new ContoSezioni("Milano APP", 4, new[]
        {
            new SezioneVuota(11, "vfr", "Gestione del traffico › VFR"),
            new SezioneVuota(12, "regulated", "Aree regolamentate"),
        }),
    });

    private static SchedaDelDocumento Scheda(StatoSezioni? sezioni = null, int traduzioni = 0, string? ciclo = "2611",
        IReadOnlyList<ReleaseDiffRow>? pubblico = null, ConfrontoProgrammata? programmata = null) =>
        new(7, sezioni, traduzioni, ciclo, pubblico ?? Array.Empty<ReleaseDiffRow>(), programmata,
            new DateTime(2026, 10, 28, 9, 30, 0, DateTimeKind.Utc));

    private IRenderedComponent<SchedaDocumento> Render(bool inModifica = false, bool compatto = false) =>
        RenderComponent<SchedaDocumento>(p => p
            .Add(x => x.Target, ReleaseTargetType.AccVipi).Add(x => x.Key, "LIMM|LIMM_CTR")
            .Add(x => x.InModifica, inModifica).Add(x => x.Compatto, compatto)
            .Add(x => x.EditorHref, "/services/vsop/limm/editor"));

    private static string Voce(IRenderedComponent<SchedaDocumento> cut, string voce) =>
        cut.Find($"[data-voce={voce}]").TextContent;

    [Fact]
    public void Le_sezioni_vuote_blocco_per_blocco_con_il_collegamento_alla_sezione()
    {
        _servizio.Scheda = Scheda(Milano());

        var cut = Render();

        Assert.Contains("amber", cut.Find("[data-voce=completare] .pill").ClassName);
        Assert.Equal(new[] { "Milano APP" }, cut.FindAll(".sd-part b").Select(b => b.TextContent));
        Assert.Equal(new[] { "#s-11", "#s-12" }, cut.FindAll(".sd-list a").Select(a => a.GetAttribute("href")));
    }

    /// <summary>Nella pagina Documenti la scheda non sta nell'editor: il collegamento ci porta.</summary>
    [Fact]
    public void Compatta_i_collegamenti_aprono_l_editor_alla_sezione()
    {
        _servizio.Scheda = Scheda(Milano());

        var cut = Render(compatto: true);

        Assert.Equal("/services/vsop/limm/editor#s-11", cut.FindAll(".sd-list a").First().GetAttribute("href"));
        Assert.Empty(cut.FindAll("#sec-stato"));   // l'ancora è dell'editor
    }

    [Fact]
    public void Tutto_scritto_e_tradotto_e_verde()
    {
        _servizio.Scheda = Scheda(new StatoSezioni(7, new[] { new ContoSezioni(null, 6, Array.Empty<SezioneVuota>()) }));

        var cut = Render();

        var pill = cut.Find("[data-voce=completare] .pill");
        Assert.Contains("green", pill.ClassName);
        Assert.Equal("Sd_CompleteOk", pill.TextContent);
        Assert.Empty(cut.FindAll(".sd-list"));
    }

    [Fact]
    public void Le_frasi_da_tradurre_sono_da_completare()
    {
        _servizio.Scheda = Scheda(traduzioni: 4);

        var cut = Render();

        Assert.Contains("TrEd_Wait_Missing", Voce(cut, "completare"));
        Assert.DoesNotContain("Sd_CompleteOk", Voce(cut, "completare"));
    }

    [Fact]
    public void Senza_documento_non_compare()
    {
        _servizio.Scheda = null;

        var cut = Render();

        Assert.Empty(cut.FindAll("[data-scheda-documento]"));
    }

    [Fact]
    public void Mai_pubblicato_lo_dice_e_non_mostra_differenze()
    {
        _servizio.Scheda = Scheda(ciclo: null);

        var cut = Render();

        Assert.Contains("Sd_NeverPublished", Voce(cut, "pubblico"));
        Assert.Empty(cut.FindAll("[data-voce=pubblico] table"));
    }

    [Fact]
    public void Rispetto_al_pubblico_le_sezioni_cambiate_col_loro_testo()
    {
        _servizio.Scheda = Scheda(pubblico: new[]
        {
            new ReleaseDiffRow("Regole piste", ReleaseChangeKind.Modified, 1, 1, new[]
            {
                new TextDiffLine(TextDiffKind.Removed, "5 kt"), new TextDiffLine(TextDiffKind.Added, "10 kt"),
            }),
        });

        var cut = Render();

        Assert.Contains("Sd_ChangedOne", Voce(cut, "pubblico"));
        Assert.Contains("Regole piste", cut.Find("[data-voce=pubblico] table").TextContent);
        Assert.Equal(2, cut.FindAll("[data-voce=pubblico] .diff-righe li").Count);
    }

    [Fact]
    public void Uguale_al_pubblico_e_verde()
    {
        _servizio.Scheda = Scheda();

        var cut = Render();

        Assert.Equal("Sd_Same", cut.Find("[data-voce=pubblico] .pill").TextContent);
    }

    [Fact]
    public void La_programmata_allineata_e_verde_e_quella_indietro_dice_cosa_manca()
    {
        var giorno = new DateTime(2026, 12, 3, 0, 0, 0, DateTimeKind.Utc);
        _servizio.Scheda = Scheda(programmata: new ConfrontoProgrammata("2613", giorno, Array.Empty<ReleaseDiffRow>(), false));
        var cut = Render();
        Assert.Equal("Sd_ScheduledAligned", cut.Find("[data-voce=programmata] .pill").TextContent);

        _servizio.Scheda = Scheda(programmata: new ConfrontoProgrammata("2613", giorno,
            new[] { new ReleaseDiffRow("Note", ReleaseChangeKind.Added, null, 1) }, DerivateCambiate: true));
        cut.Find(".sd-head button").Click();

        Assert.Equal("Sd_ScheduledBehind", cut.Find("[data-voce=programmata] .pill").TextContent);
        Assert.Contains("Note", cut.Find("[data-voce=programmata] table").TextContent);
        Assert.Contains("Sd_ScheduledDerived", Voce(cut, "programmata"));
        Assert.Contains("#sec-versioni", cut.Find("[data-voce=programmata] a").GetAttribute("href"));
    }

    [Fact]
    public void Nessuna_programmata_lo_dice()
    {
        _servizio.Scheda = Scheda();

        var cut = Render();

        Assert.Contains("Sd_NoScheduled", Voce(cut, "programmata"));
    }

    /// <summary>
    /// Si rilegge a «Fine modifica», non a ogni ridisegno del genitore: la scheda costa due snapshot, e durante la
    /// modifica cambierebbe a ogni salvataggio.
    /// </summary>
    [Fact]
    public void Si_rilegge_a_fine_modifica_e_non_a_ogni_ridisegno()
    {
        _servizio.Scheda = Scheda(Milano());
        var cut = Render(inModifica: false);
        Assert.Equal(1, _servizio.Letture);

        cut.SetParametersAndRender(p => p.Add(x => x.InModifica, true));
        cut.SetParametersAndRender(p => p.Add(x => x.InModifica, true));
        Assert.Equal(1, _servizio.Letture);

        _servizio.Scheda = Scheda(new StatoSezioni(7, new[] { new ContoSezioni(null, 8, Array.Empty<SezioneVuota>()) }));
        cut.SetParametersAndRender(p => p.Add(x => x.InModifica, false));

        Assert.Equal(2, _servizio.Letture);
        Assert.Contains("green", cut.Find("[data-voce=completare] .pill").ClassName);
    }
}
