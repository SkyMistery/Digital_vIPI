using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il tasto «chiedi alla sorgente adesso» dentro la finestra di eliminazione. Tre cose si vedono solo qui,
/// e nessuna la provano i test del servizio: che il tasto compaia <b>soltanto</b> quando a trattenere è la
/// sorgente, che il verdetto si legga a schermo, e che confermando si passi l'<b>ordine di richiedere</b>
/// invece del verdetto già preso — che è ciò che tiene onesta la promessa «l'esecuzione ricalcola».
///
/// <para>Carta: <c>docs/feature/2026-08-26-chiedere-alla-sorgente.md</c> §5.</para>
/// </summary>
public class DeleteDialogSourceTests : TestContext
{
    private static readonly DeletionTarget Bersaglio = DeletionTarget.Sector(1);

    [Fact]
    public void Il_tasto_compare_solo_se_a_trattenere_e_la_sorgente()
    {
        var finto = Predisponi(Piano(new DeletionBlocker("la sorgente la manda ancora", null, DallaSorgente: true)));
        var c = Apri();

        Assert.Single(c.FindAll("button.btn.ghost"), b => b.TextContent.Contains("Del_AskSource"));
        Assert.True(finto.Anteprime > 0);
    }

    [Fact]
    public void Con_un_altro_blocco_non_c_e_niente_da_chiedere()
    {
        // Chiedere a IVAO non scioglie un accordo di coordinamento: il tasto sarebbe una chiamata di rete
        // che promette qualcosa che non può mantenere.
        Predisponi(Piano(new DeletionBlocker("elimina prima l'accordo «LIRR ↔ LIMM»", "/x")));
        var c = Apri();

        Assert.DoesNotContain("Del_AskSource", c.Markup);
    }

    [Fact]
    public void Senza_blocchi_non_c_e_il_tasto()
    {
        Predisponi(Piano());
        var c = Apri();

        Assert.DoesNotContain("Del_AskSource", c.Markup);
        Assert.False(c.Find("button.btn.danger").HasAttribute("disabled"));
    }

    [Fact]
    public void Il_verdetto_si_legge_a_schermo_e_sblocca_il_tasto_elimina()
    {
        var finto = Predisponi(
            Piano(new DeletionBlocker("la sorgente la manda ancora", null, DallaSorgente: true)),
            SourceProbeResult.Assente("LIRR ne elenca 7 e questo non c'è"),
            dopoLaProva: Piano());
        var c = Apri();

        Assert.True(c.Find("button.btn.danger").HasAttribute("disabled"));

        c.FindAll("button.btn.ghost").First(b => b.TextContent.Contains("Del_AskSource")).Click();

        Assert.Contains("Del_SourceGone", c.Markup);
        Assert.Contains("LIRR ne elenca 7", c.Markup);
        Assert.Contains("callout success", c.Markup);
        Assert.False(c.Find("button.btn.danger").HasAttribute("disabled"));
        Assert.Equal(1, finto.Verifiche);
    }

    [Fact]
    public void Un_non_si_sa_si_vede_e_non_sblocca_niente()
    {
        var bloccato = Piano(new DeletionBlocker("la sorgente la manda ancora", null, DallaSorgente: true));
        Predisponi(bloccato, SourceProbeResult.NonSiSa("la sorgente ha risposto 502"), dopoLaProva: bloccato);
        var c = Apri();

        c.FindAll("button.btn.ghost").First(b => b.TextContent.Contains("Del_AskSource")).Click();

        Assert.Contains("Del_SourceUnknown", c.Markup);
        Assert.Contains("callout warning", c.Markup);
        Assert.True(c.Find("button.btn.danger").HasAttribute("disabled"));
    }

    [Fact]
    public void Confermando_si_passa_l_ordine_di_richiedere_non_il_verdetto()
    {
        // ⚠️ Il cuore: il servizio non deve fidarsi di una prova presa dieci minuti fa in un'altra chiamata.
        // La finestra chiede di RIFARE la domanda, e il verdetto che conta è quello dell'istante del DELETE.
        var finto = Predisponi(
            Piano(new DeletionBlocker("la sorgente la manda ancora", null, DallaSorgente: true)),
            SourceProbeResult.Assente("non c'è"),
            dopoLaProva: Piano());
        var c = Apri();

        c.FindAll("button.btn.ghost").First(b => b.TextContent.Contains("Del_AskSource")).Click();
        c.Find("button.btn.danger").Click();

        Assert.True(finto.ChiestaLaVerificaAllEliminazione);
    }

    [Fact]
    public void Senza_prova_l_eliminazione_ordinaria_non_chiede_niente()
    {
        var finto = Predisponi(Piano());
        var c = Apri();

        c.Find("button.btn.danger").Click();

        Assert.False(finto.ChiestaLaVerificaAllEliminazione);
    }

    // ── Impalcatura ──────────────────────────────────────────────────────────────────────────────────

    private static DeletionPlan Piano(params DeletionBlocker[] blocca) =>
        new(Bersaglio, "LIRR_W_CTR",
            new[] { "il settore LIRR_W_CTR" }, Array.Empty<string>(), Array.Empty<string>(),
            blocca, DeletionActions.Nessuna);

    private IRenderedComponent<DeleteDialog> Apri()
    {
        var c = RenderComponent<DeleteDialog>(p => p
            .Add(x => x.Target, Bersaglio)
            .Add(x => x.Nome, "LIRR_W_CTR"));
        c.Find("span.inline-confirm button").Click();
        return c;
    }

    private DeletionFinta Predisponi(DeletionPlan piano, SourceProbeResult? prova = null,
        DeletionPlan? dopoLaProva = null)
    {
        var finto = new DeletionFinta(piano, prova, dopoLaProva);
        Services.AddSingleton<IDeletionService>(finto);
        Services.AddSingleton<IStringLocalizer<SharedResource>, ChiaviNude>();
        return finto;
    }

    /// <summary>Le chiavi al posto delle frasi: il test guarda la struttura, non la traduzione.</summary>
    private sealed class ChiaviNude : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + string.Concat(arguments.Select(a => " " + a)), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            Enumerable.Empty<LocalizedString>();
    }

    private sealed class DeletionFinta : IDeletionService
    {
        private readonly DeletionPlan _piano;
        private readonly SourceProbeResult? _prova;
        private readonly DeletionPlan? _dopo;

        public int Anteprime { get; private set; }
        public int Verifiche { get; private set; }
        public bool ChiestaLaVerificaAllEliminazione { get; private set; }

        public DeletionFinta(DeletionPlan piano, SourceProbeResult? prova, DeletionPlan? dopo)
        {
            _piano = piano;
            _prova = prova;
            _dopo = dopo;
        }

        public Task<DeletionPlan> AnteprimaAsync(DeletionTarget bersaglio, CancellationToken ct = default)
        {
            Anteprime++;
            return Task.FromResult(_piano);
        }

        public Task<DeletionProbeOutcome> VerificaAllaSorgenteAsync(DeletionTarget bersaglio,
            CancellationToken ct = default)
        {
            Verifiche++;
            return Task.FromResult(new DeletionProbeOutcome(
                _prova ?? SourceProbeResult.NonSiSa("niente"), _dopo ?? _piano));
        }

        public Task<DeletionPlan> EliminaAsync(DeletionTarget bersaglio, bool conVerificaAllaSorgente = false,
            CancellationToken ct = default)
        {
            ChiestaLaVerificaAllEliminazione = conVerificaAllaSorgente;
            return Task.FromResult(_dopo ?? _piano);
        }
    }
}
