using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Ui;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Rete sull'intestazione di sola stampa (PrintMeta): esiste solo per il foglio stampato, quindi una regressione
/// qui è invisibile a schermo. Morde se sparisce la classe .print-only (l'intestazione comparirebbe nella pagina),
/// se i parametri opzionali non vengono più omessi quando nulli, o se si perde lo span [data-print-time] su cui
/// vipi-ui.js scrive l'ora reale di stampa.
/// </summary>
public class PrintMetaTests : TestContext
{
    /// <summary>Localizer che rende la chiave stessa: le asserzioni restano stabili al variare delle traduzioni.</summary>
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public PrintMetaTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
    }

    [Fact]
    public void PrintMeta_renders_print_only_header_with_title_subtitle_airac_and_url()
    {
        var cut = RenderComponent<PrintMeta>(p => p
            .Add(x => x.Title, "LIRF — Roma Fiumicino")
            .Add(x => x.Subtitle, "Aeroporto · Roma")
            .Add(x => x.AiracCycle, "2608"));

        // .print-only è ciò che tiene l'intestazione fuori dallo schermo: senza, appare nella pagina.
        var root = cut.Find("div.print-only.print-meta");
        Assert.Contains("LIRF — Roma Fiumicino", cut.Find(".pm-title").TextContent);
        Assert.Contains("Aeroporto · Roma", cut.Find(".pm-sub").TextContent);
        Assert.Contains("2608", root.TextContent);
        // Ancoraggio per la riscrittura dell'ora al momento della stampa (vipi-ui.js, 'beforeprint').
        Assert.NotNull(cut.Find(".pm-line [data-print-time]"));
        // URL della pagina: sul foglio è l'unico modo di risalire alla fonte (il chrome è nascosto).
        Assert.Contains("http://localhost/", cut.Find(".pm-url").TextContent);
    }

    [Fact]
    public void PrintMeta_omits_subtitle_and_airac_when_not_provided()
    {
        var cut = RenderComponent<PrintMeta>(p => p.Add(x => x.Title, "vLOA LIRR ↔ LFMM"));

        Assert.Empty(cut.FindAll(".pm-sub"));
        Assert.DoesNotContain("Common_AiracCycle", cut.Markup);
        Assert.NotNull(cut.Find(".pm-line [data-print-time]"));
    }

    /// <summary>
    /// ⚠️ <b>L'avviso di traduzione automatica sul FOGLIO.</b> Dal 2 settembre 2026 a schermo è un gettone
    /// dentro <c>.doc-head</c>, e <c>.doc-head</c> in stampa è nascosto (<c>vipi-print.css</c>): senza
    /// questa riga un documento tradotto a macchina si stamperebbe <b>senza dirlo</b>. È l'avviso che pesa
    /// di più proprio su carta, dove non c'è nessun «?» da aprire né l'originale a portata di clic — per
    /// questo qui va il testo per esteso, non l'etichetta corta del gettone.
    /// </summary>
    [Fact]
    public void PrintMeta_stampa_l_avviso_di_traduzione_automatica_per_esteso()
    {
        var cut = RenderComponent<PrintMeta>(p => p
            .Add(x => x.Title, "LIRF — Roma Fiumicino")
            .Add(x => x.Coverage, new Vipi.Application.Translation.TranslationCoverage(
                Segmenti: 10, Tradotti: 6, Riletti: 0)));

        var righe = cut.FindAll(".pm-tr");
        Assert.Equal(2, righe.Count);                                   // «a macchina» e «ne mancano 4»
        Assert.Contains("Tr_NoticeMachine", cut.Markup);                // per esteso, non `Tr_ChipMachine`
        Assert.Contains("Tr_NoticePartial", cut.Markup);
        Assert.DoesNotContain("Tr_ChipMachine", cut.Markup);
    }

    [Fact]
    public void PrintMeta_senza_traduzione_non_stampa_nessun_avviso()
    {
        // Il default è «nessuna traduzione»: una pagina che non passa la copertura resta com'era.
        var cut = RenderComponent<PrintMeta>(p => p.Add(x => x.Title, "vIPI — Roma"));

        Assert.Empty(cut.FindAll(".pm-tr"));
    }

    [Fact]
    public void PrintMeta_su_un_documento_riletto_non_stampa_l_avviso()
    {
        var cut = RenderComponent<PrintMeta>(p => p
            .Add(x => x.Title, "vIPI — Roma")
            .Add(x => x.Coverage, new Vipi.Application.Translation.TranslationCoverage(10, 10, 10)));

        Assert.Empty(cut.FindAll(".pm-tr"));
    }
}
