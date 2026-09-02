using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Ui;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// <b>«ONLY FOR SIMULATION: DO NOT USE FOR REAL LIFE NAVIGATION» sta ovunque.</b> È la richiesta del
/// committente del 1º settembre 2026, e la parola era <i>«in tutti, nessuno escluso»</i>: sotto il titolo di
/// ogni documento pubblico, della vista live e della mappa degli spazi aerei; e a piè di <b>ogni foglio
/// stampato di ogni pagina del sito</b>.
///
/// <para><b>Perché un test sul SORGENTE.</b> Stesso motivo di
/// <see cref="DueColonneSuOgniDocumentoTests"/>: i sette viewer si leggono uno per uno e da soli sembrano
/// sempre a posto — il difetto che si vuole prendere è <i>l'ottavo documento che nascerà senza</i>, non un
/// errore di render. La domanda giusta è «è stato scritto?», e si fa sul file.</para>
///
/// <para>⚠️ <b>La rete che conta davvero è quella sul testo unico.</b> L'avviso ha nove sedi: scritto a
/// mano in nove posti, al primo ritocco diventa nove testi diversi — e un cartello legale che cambia da
/// pagina a pagina non vale niente. Il letterale vive in <c>SimDisclaimer.razor</c> e in nessun altro
/// posto.</para>
/// </summary>
public sealed class AvvisoDiSimulazioneTests
{
    /// <summary>
    /// I SETTE posti in cui l'avviso si vede a schermo, col percorso relativo a <c>src/Vipi.Ui</c>: i cinque
    /// documenti pubblici, la vista live e la mappa degli spazi aerei.
    /// <para>⚠️ La vLOA non è una pagina ma un COMPONENTE (<c>VloaDocumentView</c>): il suo markup sta lì, e
    /// un elenco che guardasse solo <c>Pages/</c> la salterebbe in silenzio.</para>
    /// </summary>
    public static TheoryData<string> Schermate() => new()
    {
        "Pages/AccVipiPage.razor",
        "Pages/AeroportoPage.razor",
        "Pages/AppnPage.razor",
        "Pages/MilDocumentPage.razor",
        "Components/VloaDocumentView.razor",
        "Pages/LivePage.razor",
        "Pages/AirspacePage.razor",
    };

    [Theory]
    [MemberData(nameof(Schermate))]
    public void Ogni_schermata_pubblica_porta_l_avviso(string relativo)
    {
        var sorgente = Leggi(relativo);

        Assert.True(
            sorgente.Contains("<SimDisclaimer", StringComparison.Ordinal),
            $"{relativo}: nessun <SimDisclaimer /> sotto il titolo.");
    }

    /// <summary>
    /// Il piè di pagina sta nel LAYOUT, non nelle pagine: è l'unico modo perché valga anche per gli elenchi,
    /// per l'editor e per qualunque pagina nasca domani. ⚠️ E <c>position:fixed</c> non è stile ma
    /// meccanismo — è ciò che ripete la riga su ogni foglio: chi lo togliesse per «pulizia» lascerebbe
    /// l'avviso sulla sola ultima pagina.
    /// </summary>
    [Fact]
    public void Il_pie_di_stampa_sta_nel_layout_ed_e_fisso()
    {
        var layout = Leggi("Shared/SopLayout.razor");
        Assert.Contains("sim-foot", layout, StringComparison.Ordinal);
        Assert.Contains("print-only", layout, StringComparison.Ordinal);
        Assert.Contains("<SimDisclaimer", layout, StringComparison.Ordinal);

        var stampa = Leggi("wwwroot/vipi-print.css");
        // ⚠️ Si cerca l'apertura della REGOLA (`.sim-foot {`) e non il solo nome: il nome compare prima,
        // dentro il commento del margine di pagina, e la ricerca ingenua leggeva quel commento.
        var regola = stampa.IndexOf(".sim-foot {", StringComparison.Ordinal);
        Assert.True(regola > 0, "vipi-print.css: nessuna regola per .sim-foot");
        var corpo = stampa[regola..(stampa.IndexOf('}', regola) + 1)];
        Assert.Contains("position: fixed", corpo, StringComparison.Ordinal);
    }

    /// <summary>
    /// ⚠️ <b>La regola è una sola: <c>bottom</c> non può essere negativo.</b> Un elemento fisso si posiziona
    /// sull'area di pagina, e la parte che finisce <b>sotto</b> il suo bordo inferiore Chrome non la taglia —
    /// la <b>ridisegna in cima al foglio successivo</b>. Per chi legge, l'avviso appare tagliato per il lungo
    /// fra due fogli, e il fondo bianco della metà di sopra cancella la prima riga di quel foglio.
    ///
    /// <para>⚠️ E <b>non si ripara calibrando i millimetri</b>: è la lezione pagata due volte. Una sporgenza
    /// di 2mm sembrava innocua su A4 coi margini di questo foglio, ma bastano la scala al 90% — il «adatta
    /// alla pagina» del dialogo di stampa — o i margini scelti a mano e il difetto torna. Misurato sulla vIPI
    /// di Brindisi, su sei modi di stampare: −4mm/8mm sbaglia su 20 fogli di 21, −2mm/5mm su 11 alla scala
    /// 90%, <c>bottom:0</c> su nessuno in nessun modo.</para>
    ///
    /// <para>⚠️ Il prezzo di <c>bottom:0</c>, dichiarato: la scatola sta dentro l'area di pagina, dove scorre
    /// il testo, e il suo fondo bianco morde le descendenti dell'ultima riga di un foglio pieno. Con
    /// <c>position:fixed</c> non è eliminabile; si limita tenendo la riga <b>bassa</b>. Per questo l'altezza
    /// ha un tetto.</para>
    /// </summary>
    [Theory]
    [InlineData(0.0, 3.5, true)]     // quel che c'è oggi
    [InlineData(-4.0, 8.0, false)]   // la prima consegna: avviso spezzato su 20 fogli di 21
    [InlineData(-2.0, 5.0, false)]   // il primo rimedio: regge su A4 al 100%, cade alla scala 90%
    [InlineData(0.0, 8.0, false)]    // non sporge, ma con 8mm il morso sull'ultima riga è una riga intera
    public void La_regola_del_pie_di_pagina(double bottom, double height, bool valida)
    {
        Assert.Equal(valida, bottom >= 0 && height <= 4.0);
    }

    /// <summary>E i numeri che stanno davvero nel foglio devono passare quella regola.</summary>
    [Fact]
    public void Il_pie_di_pagina_del_foglio_rispetta_la_regola()
    {
        var stampa = Leggi("wwwroot/vipi-print.css");
        var regola = stampa.IndexOf(".sim-foot {", StringComparison.Ordinal);
        Assert.True(regola > 0, "vipi-print.css: nessuna regola per .sim-foot");
        var corpo = stampa[regola..(stampa.IndexOf('}', regola) + 1)];

        Assert.Contains("margin: 14mm 12mm 18mm", stampa, StringComparison.Ordinal);

        var bottom = Mm(corpo, "bottom:");
        var height = Mm(corpo, "height:");

        Assert.True(bottom >= 0,
            $"il piè sporge {-bottom}mm sotto l'area di pagina: Chrome ridisegna la sporgenza in cima al foglio dopo, "
            + "e l'avviso esce tagliato fra due fogli");
        Assert.True(height <= 4.0,
            $"la riga del piè è alta {height}mm: sta dentro l'area di testo, e più è alta più mangia l'ultima riga");
    }

    /// <summary>Il primo valore in mm della proprietà dentro il corpo di una regola.</summary>
    private static double Mm(string corpo, string proprieta)
    {
        var i = corpo.IndexOf(proprieta, StringComparison.Ordinal);
        Assert.True(i > 0, $"proprietà assente: {proprieta}");
        var m = System.Text.RegularExpressions.Regex.Match(corpo[i..], @"(-?\d+(?:\.\d+)?)mm");
        Assert.True(m.Success, $"{proprieta} non è in mm");
        return double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Sul foglio <c>.doc-head</c> è nascosto (<c>.print-meta + .doc-head</c>): senza la riga dentro
    /// <c>PrintMeta</c>, la prima pagina di un documento stampato perderebbe l'avviso accanto al titolo.
    /// Qui si rende davvero il componente, perché la domanda è cosa esce, non cosa è scritto.
    /// </summary>
    [Fact]
    public void L_intestazione_di_stampa_porta_l_avviso()
    {
        using var ctx = new TestContext();
        ctx.Services.AddSingleton<IStringLocalizer<SharedResource>>(new ChiaveComeTesto());

        var cut = ctx.RenderComponent<PrintMeta>(p => p
            .Add(x => x.Title, "LIRF — Roma Fiumicino")
            .Add(x => x.Subtitle, "Aeroporto · Roma"));

        Assert.Equal(SimDisclaimer.Testo, cut.Find(".pm-sim .sim-disc").TextContent.Trim());
    }

    /// <summary>Il componente rende il testo alla lettera, con la classe che gli dà rosso e grassetto.</summary>
    [Fact]
    public void Il_componente_rende_il_testo_alla_lettera()
    {
        using var ctx = new TestContext();
        var cut = ctx.RenderComponent<SimDisclaimer>();

        Assert.Equal(SimDisclaimer.Testo, cut.Find("span.sim-disc").TextContent.Trim());
        Assert.Equal("ONLY FOR SIMULATION: DO NOT USE FOR REAL LIFE NAVIGATION", SimDisclaimer.Testo);
    }

    /// <summary>
    /// ⚠️ <b>Il letterale vive in un posto solo.</b> Nove sedi e nove copie sarebbero nove testi diversi
    /// al primo ritocco. Questo test è la ragione per cui esiste un componente invece di uno <c>span</c>
    /// scritto a mano dove serve.
    /// </summary>
    [Fact]
    public void Il_testo_e_scritto_una_volta_sola()
    {
        var radice = Radice();
        var sep = Path.DirectorySeparatorChar;
        var copie = Directory
            .EnumerateFiles(radice, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.Contains($"{sep}obj{sep}", StringComparison.Ordinal)
                     && !f.Contains($"{sep}bin{sep}", StringComparison.Ordinal))
            .Where(f => File.ReadAllText(f).Contains(SimDisclaimer.Testo, StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(radice, f))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { Path.Combine("Components", "SimDisclaimer.razor") }, copie);
    }

    /// <summary>
    /// ⚠️ Il rosso viene dal TOKEN e non da un letterale: è la regola del tema (regole-brand), ed è ciò che
    /// tiene la riga leggibile anche a tema scuro.
    /// </summary>
    [Fact]
    public void Il_rosso_viene_dal_token_del_tema()
    {
        var tema = Leggi("wwwroot/vipi-theme.css");
        var regola = tema.IndexOf(".sim-disc{", StringComparison.Ordinal);
        Assert.True(regola > 0, "vipi-theme.css: nessuna regola per .sim-disc");

        var corpo = tema[regola..(tema.IndexOf('}', regola) + 1)];
        Assert.Contains("var(--danger-ink)", corpo, StringComparison.Ordinal);
        Assert.Contains("font-weight:800", corpo, StringComparison.Ordinal);
    }

    /// <summary>Localizer che rende la chiave stessa: le asserzioni non dipendono dalle traduzioni.</summary>
    private sealed class ChiaveComeTesto : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private static string Leggi(string relativo) =>
        File.ReadAllText(Path.Combine(Radice(), relativo.Replace('/', Path.DirectorySeparatorChar)));

    private static string Radice()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var c = Path.Combine(dir.FullName, "src", "Vipi.Ui");
            if (Directory.Exists(Path.Combine(c, "Pages"))) return c;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException($"src/Vipi.Ui non trovata risalendo da {AppContext.BaseDirectory}");
    }
}
