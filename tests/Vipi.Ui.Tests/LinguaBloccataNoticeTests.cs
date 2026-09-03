using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Ui;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// L'avviso «documento in una lingua sola» (carta <c>2026-08-31-lingua-bloccata.md</c> §5).
///
/// <para>⚠️ <b>Non è l'avviso di traduzione automatica: è il suo opposto.</b> Là si dice «questa pagina
/// l'ha tradotta una macchina», qui «questa pagina NON è tradotta, ed è voluto». I due non compaiono mai
/// insieme — su un documento bloccato la copertura è zero segmenti — ma condividono la riga sotto il
/// titolo, e dal 3 settembre 2026 anche la forma: un gettone col testo lungo dietro il «?».</para>
///
/// <para>⚠️ <b>La nota segue chi GUARDA, non il documento.</b> La legge un italiano che si è trovato davanti
/// una pagina inglese e vuole sapere perché; detta in inglese non servirebbe proprio a lui. Per questo
/// chiede a <c>StringheDelSito</c> e non a <c>L</c> — e per questo si porta un <c>lang</c> suo, dentro una
/// pagina marcata con la lingua del documento.</para>
/// </summary>
public class LinguaBloccataNoticeTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public LinguaBloccataNoticeTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
    }

    private IRenderedComponent<LinguaBloccataNotice> Riquadro(string? lingua) =>
        RenderComponent<LinguaBloccataNotice>(p => p.Add(x => x.Lingua, lingua));

    private IRenderedComponent<LinguaBloccataNotice> Gettone(string? lingua) =>
        RenderComponent<LinguaBloccataNotice>(p => p.Add(x => x.Lingua, lingua).Add(x => x.Compatto, true));

    [Fact]
    public void Su_un_documento_NON_bloccato_non_compare_niente()
    {
        // La stragrande maggioranza dei documenti non è bloccata: un avviso qui sarebbe rumore ovunque.
        Assert.Empty(Riquadro(null).Markup.Trim());
        Assert.Empty(Riquadro("").Markup.Trim());
    }

    [Fact]
    public void Il_gettone_sparisce_esattamente_quando_spariva_il_riquadro()
    {
        // La condizione di comparsa è UNA, condivisa dalle due forme: se si sdoppiasse, lo stesso documento
        // avrebbe l'avviso in una pagina e non nell'altra.
        Assert.Empty(Gettone(null).Markup.Trim());
        Assert.Empty(Gettone("").Markup.Trim());
    }

    [Fact]
    public void Il_riquadro_pieno_resta_quello_di_prima()
    {
        var cut = Riquadro("en");

        Assert.Single(cut.FindAll(".callout"));
        Assert.Contains("tr-notice", cut.Markup);
        Assert.Contains(RisorseCondivise.Testo("Lang_LockedTitle", System.Globalization.CultureInfo.CurrentUICulture),
            cut.Markup);
    }

    /// <summary>
    /// ⚠️ <b>Che cosa il gettone NON perde.</b> Che l'avviso esista e <b>in quale lingua</b> sia scritto il
    /// documento resta in chiaro; a scomparire dietro il «?» è la frase lunga, che spiega la regola. Un
    /// gettone che dicesse solo «lingua bloccata» avrebbe risparmiato spazio buttando via il contenuto.
    /// </summary>
    [Fact]
    public void Il_gettone_dice_QUALE_lingua_e_tiene_la_frase_lunga_dietro_il_punto_interrogativo()
    {
        var cut = Gettone("en");

        Assert.Empty(cut.FindAll(".callout"));           // niente riquadro
        Assert.Contains("lang-chip", cut.Markup);
        Assert.Contains("tr-notice", cut.Markup);        // la rete che pretende «l'avviso c'è» non si accorge della forma

        var cultura = System.Globalization.CultureInfo.CurrentUICulture;
        var inglese = RisorseCondivise.Testo("Lang_NameEn", cultura);
        Assert.Contains(inglese, cut.Find(".lang-chip>span").TextContent);

        // La frase lunga c'è, ma dentro il popover del «?».
        Assert.Contains(inglese, cut.Find(".help-pop").TextContent);
        Assert.Contains(
            string.Format(RisorseCondivise.Testo("Lang_LockedBody", cultura), inglese),
            cut.Find(".help-pop").TextContent);
    }

    /// <summary>
    /// Le due lingue si dicono con due CHIAVI, non con <c>CultureInfo.DisplayName</c>: quello dipende dalla
    /// lingua di <b>Windows</b> della macchina che serve la pagina — su questo host direbbe «Italian» dentro
    /// una frase italiana.
    /// </summary>
    [Theory]
    [InlineData("it", "Lang_NameIt")]
    [InlineData("en", "Lang_NameEn")]
    [InlineData("EN", "Lang_NameEn")]
    public void Il_nome_della_lingua_viene_dalle_risorse(string codice, string chiave)
    {
        var atteso = RisorseCondivise.Testo(chiave, System.Globalization.CultureInfo.CurrentUICulture);
        Assert.Contains(atteso, Gettone(codice).Find(".lang-chip>span").TextContent);
    }

    /// <summary>
    /// ⚠️ Il gettone porta un <c>lang</c> <b>suo</b>: in forma compatta sta dentro <c>.doc-head</c>, e sulla
    /// vLOA tutta la testata è avvolta da un <c>&lt;div lang="…"&gt;</c> con la lingua del <b>documento</b>.
    /// Senza, uno screen reader leggerebbe la spiegazione italiana con la pronuncia inglese — cioè proprio
    /// alla persona a cui questa nota è rivolta.
    /// </summary>
    [Fact]
    public void Le_due_forme_dichiarano_la_lingua_di_CHI_GUARDA()
    {
        var attesa = Vipi.Application.Content.LinguaDiLettura.DelLettore();
        Assert.Equal(attesa, Gettone("en").Find(".lang-chip").GetAttribute("lang"));
        Assert.Equal(attesa, Riquadro("en").Find(".callout").GetAttribute("lang"));
    }
}
