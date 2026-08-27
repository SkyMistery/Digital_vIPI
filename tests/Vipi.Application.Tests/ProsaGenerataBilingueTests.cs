using System.Globalization;
using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Application.Tests;

/// <summary>
/// La prosa <b>generata</b> nella lingua di chi legge (carta <c>2026-08-27-documenti-bilingue.md</c> §4).
///
/// <para>
/// ⚠️ <b>La prosa generata si SCEGLIE, non si traduce.</b> Le frasi di coordinamento le scrive il nostro
/// codice, e di entrambe le versioni possediamo l'originale: mandarle a un motore automatico vorrebbe dire
/// pagare per tradurre una cosa che sappiamo già dire, e accettarne la fraseologia invece della nostra.
/// </para>
///
/// <para>
/// ⚠️ <b>Il difetto che questa slice chiude.</b> Fino al 28 agosto 2026 il template lo sceglieva la
/// <b>famiglia</b> del documento, non chi legge: la vLOA prendeva sempre l'inglese, l'ACC sempre l'italiano.
/// Un lettore italiano apriva una vLOA tradotta e trovava i coordinamenti ancora in inglese — cioè
/// esattamente la schermata mezza tradotta che tutta questa funzione esiste per evitare.
/// </para>
/// </summary>
public class ProsaGenerataBilingueTests
{
    // ---- La scelta del template ----------------------------------------------------------------------

    [Fact]
    public void Chi_legge_in_inglese_prende_il_template_inglese()
    {
        var italiano = new CoordinationSentenceTemplate();
        Assert.Same(CoordinationSentenceTemplate.English, CoordinationSentenceTemplate.For("en", italiano));
        Assert.Same(CoordinationSentenceTemplate.English, CoordinationSentenceTemplate.For("EN", italiano));
    }

    [Fact]
    public void Chi_legge_in_italiano_prende_il_template_DEL_FILE_non_una_costante()
    {
        // ⚠️ Il template italiano si puo' sovrascrivere da «content/coordination-sentence.json», e la
        // divisione lo fa. Prenderlo da una costante farebbe sparire quelle personalizzazioni proprio nel
        // momento in cui si comincia a scegliere per lingua — un difetto che nessuno collegherebbe alla
        // funzione bilingue.
        var personalizzato = new CoordinationSentenceTemplate { Template = "PERSONALIZZATO {owner}" };
        Assert.Same(personalizzato, CoordinationSentenceTemplate.For("it", personalizzato));
        Assert.Same(personalizzato, CoordinationSentenceTemplate.For(null, personalizzato));
        Assert.Same(personalizzato, CoordinationSentenceTemplate.For("fr", personalizzato));
    }

    [Fact]
    public void Le_due_frasi_dicono_la_stessa_cosa_in_due_lingue()
    {
        // L'invariante del committente vale anche per la prosa generata: stessi slot, stesso significato.
        var it = new CoordinationSentenceTemplate();
        var en = CoordinationSentenceTemplate.English;

        foreach (var slot in new[] { "{owner}", "{target}", "{airport}", "{stato}", "{fl}", "{point}" })
        {
            Assert.Contains(slot, it.Template);
            Assert.Contains(slot, en.Template);
        }
    }

    // ---- Il contesto di lettura ----------------------------------------------------------------------

    [Fact]
    public void Fuori_da_una_cattura_la_lingua_e_quella_dell_interfaccia()
    {
        // ⚠️ La prosa generata deve seguire LA STESSA chip della barra che decide tutto il resto della
        // pagina: due sorgenti di verita' sulla lingua darebbero una schermata mezza tradotta.
        var prima = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en-GB");
            Assert.Equal("en", new ReadingLanguageContext().Corrente);

            CultureInfo.CurrentUICulture = new CultureInfo("it-IT");
            Assert.Equal("it", new ReadingLanguageContext().Corrente);
        }
        finally { CultureInfo.CurrentUICulture = prima; }
    }

    [Fact]
    public void Durante_una_cattura_la_lingua_la_decide_chi_pubblica()
    {
        // Nel congelamento non c'e' nessun lettore: la lingua e' quella SORGENTE del documento. Senza questa
        // forzatura il congelato prenderebbe la cultura del circuito di chi ha premuto Pubblica -- cioe' la
        // stessa release direbbe cose diverse a seconda di chi l'ha fatta.
        var prima = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("it-IT");
            var ctx = new ReadingLanguageContext();
            Assert.Equal("it", ctx.Corrente);

            using (ctx.Rendering("en"))
                Assert.Equal("en", ctx.Corrente);

            Assert.Equal("it", ctx.Corrente);   // il contesto si richiude
        }
        finally { CultureInfo.CurrentUICulture = prima; }
    }

    // ---- La prosa congelata guarda la lingua, il resto no --------------------------------------------

    private static FrozenSections Lotto(Language? lingua, string chiave, string json)
    {
        var doc = new RawDocument
        {
            Title = "T", AiracCycle = "2609", Language = lingua,
            Roots = new[]
            {
                new RawSection
                {
                    Id = 7, Title = "Coordination", Depth = 0, SectionKey = chiave, Order = 1,
                    RenderMode = RenderMode.Frozen,
                },
            },
        };
        return FrozenSections.FromSnapshot(new Dictionary<int, string> { [7] = json }, doc);
    }

    private sealed record Prosa(string Testo);

    [Fact]
    public void La_prosa_congelata_in_un_ALTRA_lingua_non_si_usa()
    {
        // Snapshot inglese, lettore italiano: null = «deriva live», e la derivazione live compone in
        // italiano. Senza questo, il lettore italiano si troverebbe i coordinamenti in inglese dentro una
        // pagina per il resto tradotta.
        var lotto = Lotto(Language.En, "coordination", """{"Testo":"transfers to"}""");
        Assert.Null(lotto.GetProsa<Prosa>("coordination", "it"));
        Assert.NotNull(lotto.GetProsa<Prosa>("coordination", "en"));
    }

    [Fact]
    public void Uno_snapshot_senza_lingua_usa_il_congelato_com_e()
    {
        // ⚠️ Le release pubblicate prima del 28 agosto 2026 non portano la lingua. Scartarle farebbe
        // ricomparire LIVE delle release chiuse — cioe' il pubblico vedrebbe dati di oggi su un documento
        // che dichiara un ciclo AIRAC passato.
        var lotto = Lotto(null, "coordination", """{"Testo":"trasferisce a"}""");
        Assert.NotNull(lotto.GetProsa<Prosa>("coordination", "it"));
        Assert.NotNull(lotto.GetProsa<Prosa>("coordination", "en"));
    }

    [Fact]
    public void Senza_lingua_di_lettura_il_congelato_vale_comunque()
    {
        var lotto = Lotto(Language.En, "coordination", """{"Testo":"transfers to"}""");
        Assert.NotNull(lotto.GetProsa<Prosa>("coordination", null));
        Assert.NotNull(lotto.GetProsa<Prosa>("coordination", ""));
    }

    [Fact]
    public void Cio_che_NON_e_prosa_resta_congelato_anche_in_un_altra_lingua()
    {
        // ⚠️ AoR, frequenze e minime sono numeri, geometrie e callsign: una lingua non ce l'hanno. Scartare
        // l'intero lotto per un lettore in un'altra lingua gli mostrerebbe l'AoR DI OGGI invece di quella
        // dell'AIRAC pubblicato — cioe' romperebbe la promessa della release per risolvere un problema che
        // quelle sezioni non hanno.
        var lotto = Lotto(Language.En, "aor", """{"Testo":"geometria"}""");
        Assert.NotNull(lotto.Get<Prosa>("aor"));
        Assert.Equal(Language.En, lotto.Language);
    }
}
