using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// La riga di «Da fare», resa una volta sola per tre posti — l'elenco, il banner in cima all'editor e la
/// sezione di «Da sistemare». Quel che si prova qui non lo provano i test del servizio: che il <b>tasto
/// promesso</b> sia quello giusto per la natura della riga, e che una riga senza collegamento non sparisca.
///
/// <para>Carta: <c>docs/feature/2026-08-26-da-fare-una-lista-sola.md</c> §3.</para>
/// </summary>
public class WorkItemRowTests : TestContext
{
    public WorkItemRowTests() =>
        Services.AddSingleton<IStringLocalizer<SharedResource>, ChiaviNude>();

    [Fact]
    public void Sulla_copia_indietro_non_c_e_il_flag_ma_ripubblica()
    {
        // ⚠️ La decisione D3, resa visibile: un ✓ qui sarebbe una promessa che il giro notturno smentisce
        // entro stanotte, e chi l'ha premuto penserebbe che il tasto sia rotto.
        var c = Rendi(Riga(WorkSeverity.DaRipubblicare, WorkAction.Ripubblica));

        Assert.DoesNotContain("Review_MarkReviewed", c.Markup);
        Assert.Contains("Work_Republish", c.Markup);
        Assert.Contains("/editor#sec-versioni", c.Find("a.btn").GetAttribute("href"));
    }

    [Fact]
    public void Su_cio_che_va_riletto_c_e_il_flag()
    {
        var c = Rendi(Riga(WorkSeverity.DaRileggere, WorkAction.SegnaFatto));

        Assert.Contains("Review_MarkReviewed", c.Markup);
        Assert.DoesNotContain("Work_Republish", c.Markup);
    }

    [Fact]
    public void Il_flag_chiama_indietro_con_la_riga_su_cui_si_e_premuto()
    {
        WorkItem? preso = null;
        var riga = Riga(WorkSeverity.DaRileggere, WorkAction.SegnaFatto);
        var c = RenderComponent<WorkItemRow>(p => p
            .Add(x => x.Item, riga)
            .Add(x => x.OnDone, (WorkItem w) => preso = w));

        c.Find("button.btn").Click();

        Assert.Equal(riga.Chiave, preso?.Chiave);
    }

    [Fact]
    public void Un_incarico_da_fare_offre_inizia_e_fatto()
    {
        var c = Rendi(Riga(WorkSeverity.Normale, WorkAction.CambiaStato) with
        {
            Origine = WorkOrigin.Persona,
            Stato = EditorTaskStatus.Todo,
        });

        Assert.Contains("Task_Start", c.Markup);
        Assert.Contains("Task_Done", c.Markup);
    }

    [Fact]
    public void Un_incarico_gia_in_corso_non_si_puo_ricominciare()
    {
        var c = Rendi(Riga(WorkSeverity.Normale, WorkAction.CambiaStato) with
        {
            Origine = WorkOrigin.Persona,
            Stato = EditorTaskStatus.InProgress,
        });

        Assert.DoesNotContain("Task_Start", c.Markup);
        Assert.Contains("Task_Done", c.Markup);
    }

    [Fact]
    public void Il_titolo_di_un_incarico_si_stampa_com_e_scritto()
    {
        // ⚠️ L'ha scritto una persona: NON è una chiave di localizzazione. Cercarlo fra le chiavi lo
        // renderebbe identico per caso, ma un titolo che somigliasse a una chiave verrebbe tradotto.
        var c = Rendi(Riga(WorkSeverity.Normale, WorkAction.CambiaStato) with
        {
            Origine = WorkOrigin.Persona,
            FraseKey = WorkPhrases.Raw,
            FraseArgs = new[] { "Rivedere le frequenze di LIRF" },
        });

        Assert.Contains("Rivedere le frequenze di LIRF", c.Markup);
        Assert.DoesNotContain(WorkPhrases.Raw, c.Markup);
    }

    [Fact]
    public void La_frase_di_sistema_si_compone_da_chiave_e_argomenti()
    {
        var c = Rendi(Riga(WorkSeverity.DaRileggere, WorkAction.SegnaFatto) with
        {
            FraseKey = "Impact_SectorGone",
            FraseArgs = new[] { "LIRR_TS_CTR" },
        });

        // Il localizzatore di prova rende «chiave arg»: prova che l'argomento è arrivato alla frase.
        Assert.Contains("Impact_SectorGone LIRR_TS_CTR", c.Markup);
    }

    [Fact]
    public void Una_riga_senza_collegamento_resta_visibile_e_lo_dice()
    {
        // Sparire dalla lista è il modo in cui un lavoro si dimentica: si mostra, senza link, col perché.
        var c = Rendi(Riga(WorkSeverity.Rotto, WorkAction.VaiASistemare) with { Url = null });

        Assert.Contains("vIPI Roma", c.Markup);
        Assert.Empty(c.FindAll("span.wi-doc a"));
        Assert.Contains("Work_NoLink", c.Markup);
        // Il tasto porta comunque da qualche parte: l'elenco dei documenti.
        Assert.Equal("/services/vsop/versions", c.Find("a.btn").GetAttribute("href"));
    }

    [Fact]
    public void Prendi_in_carico_si_offre_solo_sulle_righe_di_sistema_e_solo_a_chi_puo()
    {
        var sistema = Riga(WorkSeverity.DaRileggere, WorkAction.SegnaFatto);

        Assert.DoesNotContain("Work_Assign", Rendi(sistema).Markup);
        Assert.Contains("Work_Assign", RenderComponent<WorkItemRow>(p => p
            .Add(x => x.Item, sistema).Add(x => x.ShowAssign, true)).Markup);

        // Un incarico un assegnatario ce l'ha già: offrirglielo di nuovo non vuol dire niente.
        var incarico = sistema with { Origine = WorkOrigin.Persona, ImpactId = null, TaskId = 3 };
        Assert.DoesNotContain("Work_Assign", RenderComponent<WorkItemRow>(p => p
            .Add(x => x.Item, incarico).Add(x => x.ShowAssign, true)).Markup);
    }

    [Fact]
    public void L_urgenza_si_vede_anche_senza_distinguere_i_colori()
    {
        // ⚠️ La pastiglia da sola non basta: chi non distingue rosso e ambra deve vedere lo stesso ordine
        // di gravità. La classe sulla riga porta la barretta di sinistra.
        Assert.Contains("wi-giainpubblico", Rendi(Riga(WorkSeverity.GiaInPubblico, WorkAction.SegnaFatto)).Markup);
        Assert.Contains("wi-rotto", Rendi(Riga(WorkSeverity.Rotto, WorkAction.VaiASistemare)).Markup);
        Assert.Contains("wi-inritardo", Rendi(Riga(WorkSeverity.InRitardo, WorkAction.CambiaStato)).Markup);
    }

    private IRenderedComponent<WorkItemRow> Rendi(WorkItem item) =>
        RenderComponent<WorkItemRow>(p => p.Add(x => x.Item, item));

    private static WorkItem Riga(WorkSeverity severita, WorkAction azione) =>
        new(WorkOrigin.Sistema, "imp:1", 7, "vIPI Roma", "LIRR", "/services/vsop/lirr/editor",
            "Impact_SectorGone", Array.Empty<string>(), severita, azione,
            new DateTime(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc), ImpactId: 1);

    /// <summary>Le chiavi al posto delle frasi: il test guarda la struttura, non la traduzione.</summary>
    private sealed class ChiaviNude : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + string.Concat(arguments.Select(a => " " + a)), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            Enumerable.Empty<LocalizedString>();
    }
}
