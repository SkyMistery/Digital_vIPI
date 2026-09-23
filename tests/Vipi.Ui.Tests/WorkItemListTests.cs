using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// La lista «Da fare» letta per cambiamento. Quel che i test puri di <c>WorkGrouping</c> non provano: che il gruppo
/// nasca <b>chiuso</b> e si apra col clic, che dentro non ripeta la frase della testata, che il ✓ di gruppo ci sia
/// solo quando ogni riga si spunta e chiami indietro con <b>tutte</b> le righe, e che la vista si scelga.
///
/// <para>Carta: <c>docs/feature/2026-09-23-da-fare-per-cambiamento.md</c> §2.</para>
/// </summary>
public class WorkItemListTests : TestContext
{
    public WorkItemListTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>, ChiaviNude>();
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static readonly DateTime Adesso = new(2026, 9, 23, 20, 0, 0, DateTimeKind.Utc);

    private static WorkItem Area(int id, int doc, string area = "area:7") =>
        new(WorkOrigin.Sistema, $"imp:{id}", doc, $"Documento {doc}", "LIRR", $"/doc/{doc}",
            "Impact_AreaChanged", new[] { "LI-R7" }, WorkSeverity.DaRileggere, WorkAction.SegnaFatto,
            Adesso.AddMinutes(-id), ImpactId: id, Tipo: ImpactKind.AreaChanged, Sorgente: area);

    private static WorkItem Deriva(int id, int doc) =>
        new(WorkOrigin.Sistema, $"imp:{id}", doc, $"Documento {doc}", "LIRR", $"/doc/{doc}",
            "Impact_ReleaseDrift", new[] { "Frequenze" }, WorkSeverity.DaRipubblicare, WorkAction.Ripubblica,
            Adesso.AddMinutes(-id), ImpactId: id, Tipo: ImpactKind.ReleaseDrift, Sorgente: $"K{doc}");

    private IRenderedComponent<WorkItemList> Rendi(IReadOnlyList<WorkItem> righe,
        Action<IReadOnlyList<WorkItem>>? tutte = null) =>
        RenderComponent<WorkItemList>(p =>
        {
            p.Add(x => x.Items, righe);
            if (tutte is not null) p.Add(x => x.OnDoneMany, tutte);
        });

    [Fact]
    public void Sei_documenti_con_la_stessa_area_sono_una_testata_chiusa()
    {
        var c = Rendi(Enumerable.Range(1, 6).Select(i => Area(i, i)).ToList());

        var testata = c.Find(".wi-grp-toggle");
        Assert.Equal("false", testata.GetAttribute("aria-expanded"));
        Assert.Contains("Work_GroupDocs 6", testata.TextContent);
        Assert.Empty(c.FindAll(".wi-sub"));
        Assert.DoesNotContain("Documento 3", c.Markup);   // i documenti si vedono aprendo
    }

    [Fact]
    public void Aprire_mostra_i_documenti_senza_ripetere_la_frase()
    {
        var c = Rendi(Enumerable.Range(1, 3).Select(i => Area(i, i)).ToList());

        c.Find(".wi-grp-toggle").Click();

        var sotto = c.Find(".wi-sub");
        Assert.Equal(3, sotto.QuerySelectorAll("li.wi").Length);
        Assert.Contains("Documento 2", sotto.TextContent);
        Assert.DoesNotContain("Impact_AreaChanged", sotto.TextContent);   // la frase la dice la testata
    }

    [Fact]
    public void Una_riga_sola_non_ha_testata()
    {
        var c = Rendi(new[] { Area(1, 1) });

        Assert.Empty(c.FindAll(".wi-grp"));
        Assert.Contains("Documento 1", c.Markup);
    }

    [Fact]
    public void Il_ok_di_gruppo_chiude_tutte_le_righe_dopo_la_conferma()
    {
        IReadOnlyList<WorkItem>? chiuse = null;
        var c = Rendi(Enumerable.Range(1, 4).Select(i => Area(i, i)).ToList(), r => chiuse = r);

        c.Find(".wi-grp-head .wi-act button").Click();   // apre la conferma in linea
        Assert.Null(chiuse);
        c.FindAll("button").First(b => b.TextContent.Contains("Review_MarkReviewed")).Click();

        Assert.Equal(4, chiuse?.Count);
    }

    [Fact]
    public void Sulle_derive_il_ok_di_gruppo_non_c_e()
    {
        var c = Rendi(new[] { Deriva(1, 1), Deriva(2, 2) }, _ => { });

        Assert.Single(c.FindAll(".wi-grp"));
        Assert.DoesNotContain("Work_MarkAll", c.Markup);
    }

    [Fact]
    public void Per_documento_la_testata_e_il_documento_e_dentro_c_e_la_frase()
    {
        var c = Rendi(new[] { Area(1, 1, "area:7"), Area(2, 1, "area:8"), Deriva(3, 1) });

        c.FindAll(".wi-views button").First(b => b.TextContent.Contains("Work_ViewDoc")).Click();

        var testata = c.Find(".wi-grp-toggle");
        Assert.Contains("Documento 1", testata.TextContent);
        Assert.Contains("Work_GroupItems 3", testata.TextContent);
        testata.Click();
        Assert.Contains("Impact_ReleaseDrift", c.Find(".wi-sub").TextContent);
    }

    [Fact]
    public void Elenco_e_la_lista_di_prima()
    {
        var c = Rendi(Enumerable.Range(1, 3).Select(i => Area(i, i)).ToList());

        c.FindAll(".wi-views button").First(b => b.TextContent.Contains("Work_ViewList")).Click();

        Assert.Empty(c.FindAll(".wi-grp"));
        Assert.Equal(3, c.FindAll("li.wi").Count);
    }

    [Fact]
    public void La_vista_scelta_si_ricorda_nel_browser()
    {
        var c = Rendi(new[] { Area(1, 1) });

        c.FindAll(".wi-views button").First(b => b.TextContent.Contains("Work_ViewDoc")).Click();

        var scritta = JSInterop.Invocations.Single(i => i.Identifier == "localStorage.setItem");
        Assert.Equal("Documento", scritta.Arguments[1]);
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
}
