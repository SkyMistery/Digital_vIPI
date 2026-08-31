using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Translation;
using Vipi.Ui;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// L'avviso su una pagina tradotta a macchina (carta <c>2026-08-27-documenti-bilingue.md</c> §5).
///
/// <para>
/// ⚠️ <b>Non è una formalità legale, ed è il motivo per cui ha dei test.</b> Misurato contro il servizio
/// vero: «riporta sottovento» torna «bring it back downwind» — grammatica giusta, identificatori intatti,
/// e <b>non è fraseologia</b>. Plausibile e sbagliato è peggio di assente, perché nessuno se ne accorge
/// leggendo. Se questo avviso sparisse per un difetto, sparirebbe in silenzio.
/// </para>
/// </summary>
public class TranslationNoticeTests : TestContext
{
    /// <summary>Localizer che rende la chiave: qui si prova la LOGICA di quando comparire, non il testo.</summary>
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + ":" + string.Join(",", arguments), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public TranslationNoticeTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
    }

    private IRenderedComponent<TranslationNotice> Rendi(TranslationCoverage c) =>
        RenderComponent<TranslationNotice>(p => p.Add(x => x.Coverage, c));

    [Fact]
    public void Su_un_documento_non_tradotto_non_compare_niente()
    {
        // Nessun segmento = pagina nella sua lingua. Un avviso qui sarebbe rumore su ogni pagina italiana.
        Assert.DoesNotContain("tr-notice", Rendi(TranslationCoverage.Nessuna).Markup);
    }

    [Fact]
    public void Se_tutto_e_tradotto_E_riletto_l_avviso_sparisce()
    {
        // È il traguardo: una persona ha riletto tutto, la pagina non ha più niente da dichiarare.
        var cut = Rendi(new TranslationCoverage(Segmenti: 10, Tradotti: 10, Riletti: 10));
        Assert.DoesNotContain("tr-notice", cut.Markup);
    }

    [Fact]
    public void Se_e_tradotto_ma_non_riletto_l_avviso_c_e()
    {
        var cut = Rendi(new TranslationCoverage(Segmenti: 10, Tradotti: 10, Riletti: 0));
        Assert.Contains("tr-notice", cut.Markup);
        Assert.Contains("Tr_NoticeMachine", cut.Markup);
        Assert.DoesNotContain("Tr_NoticePartial", cut.Markup);   // non manca niente
    }

    [Fact]
    public void Se_manca_qualcosa_lo_dice_col_numero()
    {
        // Un documento a chiazze si legge male ma si legge. Meglio dire QUANTE frasi sono rimaste
        // nell'originale che lasciarle scoprire una per una.
        var cut = Rendi(new TranslationCoverage(Segmenti: 10, Tradotti: 4, Riletti: 4));
        Assert.Contains("tr-notice", cut.Markup);
        Assert.Contains("Tr_NoticePartial:6,10", cut.Markup);
        Assert.DoesNotContain("Tr_NoticeMachine", cut.Markup);   // le 4 tradotte sono tutte riviste
    }

    [Fact]
    public void Parzialmente_tradotto_E_non_riletto_dice_tutte_e_due_le_cose()
    {
        var cut = Rendi(new TranslationCoverage(Segmenti: 10, Tradotti: 6, Riletti: 2));
        Assert.Contains("Tr_NoticeMachine", cut.Markup);
        Assert.Contains("Tr_NoticePartial:4,10", cut.Markup);
    }

    [Fact]
    public void La_copertura_calcola_quel_che_promette()
    {
        var c = new TranslationCoverage(Segmenti: 10, Tradotti: 7, Riletti: 3);
        Assert.Equal(3, c.Mancanti);
        Assert.False(c.Completa);
        Assert.True(c.DaRileggere);

        var piena = new TranslationCoverage(10, 10, 10);
        Assert.Equal(0, piena.Mancanti);
        Assert.True(piena.Completa);
        Assert.False(piena.DaRileggere);
    }
}
