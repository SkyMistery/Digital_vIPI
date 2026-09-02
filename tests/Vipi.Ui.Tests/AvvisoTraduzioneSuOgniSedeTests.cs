using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// <b>L'avviso «tradotta a macchina» deve esserci su tutte e cinque le famiglie, a schermo E sul foglio.</b>
///
/// <para>Il 2 settembre 2026 il riquadro pieno è diventato un gettone nella riga sotto il titolo, su
/// richiesta del committente: si mangiava un quarto della prima schermata, e un avviso che costringe a
/// scorrere per arrivare al documento è un avviso che si impara a saltare.</para>
///
/// <para>⚠️ <b>Lo spostamento porta con sé una trappola che non si vede leggendo la pagina.</b> Il gettone
/// sta dentro <c>.doc-head</c>, e <c>.doc-head</c> sul foglio è <b>nascosto</b>
/// (<c>.print-meta + .doc-head{display:none}</c> in <c>vipi-print.css</c>): la pagina stampata perderebbe
/// l'avviso senza che niente lo dica. È lo stesso inciampo già pagato con l'avviso di simulazione, ed è il
/// motivo per cui <c>PrintMeta</c> ha una riga sua. Il patto è quindi <b>doppio</b>, e queste due domande
/// vanno fatte insieme: chi mostra il gettone deve passare la copertura anche a <c>PrintMeta</c>.</para>
///
/// <para>⚠️ <b>E sulla carta il testo va per ESTESO</b>, non l'etichetta corta: davanti a un foglio non c'è
/// nessun «?» da aprire, né l'originale a portata di clic. Lo pretende <c>PrintMetaTests</c>.</para>
/// </summary>
public sealed class AvvisoTraduzioneSuOgniSedeTests
{
    /// <summary>
    /// Le cinque testate documentali, col percorso relativo a <c>src/Vipi.Ui</c>.
    /// <para>⚠️ La vLOA non è una pagina ma un COMPONENTE (<c>VloaDocumentView</c>): la sua testata sta lì,
    /// e la pagina che la ospita non ha una riga sotto il titolo a cui appendere il gettone — le passa la
    /// copertura e basta. Un elenco che guardasse solo <c>Pages/</c> la salterebbe in silenzio.</para>
    /// </summary>
    public static TheoryData<string> Testate() => new()
    {
        "Pages/AccVipiPage.razor",
        "Pages/AeroportoPage.razor",
        "Pages/AppnPage.razor",
        "Pages/MilDocumentPage.razor",
        "Components/VloaDocumentView.razor",
    };

    [Theory]
    [MemberData(nameof(Testate))]
    public void Ogni_testata_porta_il_gettone_E_lo_passa_alla_stampa(string relativo)
    {
        var sorgente = Leggi(relativo);

        Assert.True(
            sorgente.Contains("<TranslationNotice", StringComparison.Ordinal)
            && sorgente.Contains("Compatto=\"true\"", StringComparison.Ordinal),
            $"{relativo}: nessun gettone <TranslationNotice … Compatto=\"true\" /> nella riga sotto il titolo.");

        Assert.True(
            sorgente.Contains("<PrintMeta", StringComparison.Ordinal)
            && sorgente.Contains("Coverage=", StringComparison.Ordinal),
            $"{relativo}: il gettone c'è ma la copertura non arriva a PrintMeta. Sul foglio `.doc-head` è "
            + "nascosto, quindi questo documento si stamperebbe senza dire di essere tradotto a macchina.");
    }

    /// <summary>
    /// ⚠️ <b>L'avviso ha una riga PROPRIA, e non è una scelta di gusto.</b> Appeso in coda al sottotitolo
    /// mandava la riga a capo in mezzo alla frase dove il sottotitolo era lungo: sulla vIPI ACC si leggeva
    /// «…DO NOT USE FOR REAL LIFE / NAVIGATION», col gettone atterrato dopo quel troncone come se ci fosse
    /// finito per sbaglio. Segnalato dal committente il 2 settembre 2026, e la forma scelta è quella che il
    /// vSOP militare aveva già.
    ///
    /// <para>⚠️ Il gettone sta <b>nella stessa riga</b> dell'avviso di simulazione, non su una terza: due
    /// righe di cartelli sopra il documento sono di nuovo il problema che si stava togliendo.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Testate))]
    public void L_avviso_e_il_gettone_stanno_su_una_riga_LORO(string relativo)
    {
        var sorgente = Leggi(relativo);

        var riga = System.Text.RegularExpressions.Regex.Match(
            sorgente, @"<div class=""sim-line"">(?<c>.*?)</div>", System.Text.RegularExpressions.RegexOptions.Singleline);

        Assert.True(riga.Success, $"{relativo}: l'avviso non è su una riga propria (<div class=\"sim-line\">).");
        Assert.Contains("<SimDisclaimer", riga.Groups["c"].Value, StringComparison.Ordinal);
        Assert.Contains("<TranslationNotice", riga.Groups["c"].Value, StringComparison.Ordinal);
    }

    /// <summary>
    /// ⚠️ <b>La riga dell'avviso è un <c>&lt;div&gt;</c>, mai un <c>&lt;p&gt;</c>, e non è pignoleria.</b>
    /// Il gettone porta dentro di sé il «?», che è un <c>&lt;details&gt;</c>, e <b>un <c>&lt;p&gt;</c> non
    /// può contenerlo</b>: il parser del browser chiude il <c>&lt;p&gt;</c> da solo e sposta il
    /// <c>&lt;details&gt;</c> <b>fuori</b>. A schermo il «?» atterrava su una riga sua, sotto e a sinistra
    /// del gettone, come un glifo perso — misurato il 2 settembre 2026, alla prima stesura di questa riga.
    ///
    /// <para>⚠️ È un difetto che <b>non si vede leggendo il Razor</b>: il markup scritto è giusto, è il DOM
    /// a essere un altro. Per questo la guardia sta sul sorgente e vale anche per la vista live e la mappa
    /// degli spazi aerei, che oggi il gettone non ce l'hanno — chi ce lo aggiungesse domani ricadrebbe
    /// dentro senza che niente lo avvisi.</para>
    /// </summary>
    [Theory]
    [InlineData("Pages/AccVipiPage.razor")]
    [InlineData("Pages/AeroportoPage.razor")]
    [InlineData("Pages/AppnPage.razor")]
    [InlineData("Pages/MilDocumentPage.razor")]
    [InlineData("Components/VloaDocumentView.razor")]
    [InlineData("Pages/LivePage.razor")]
    [InlineData("Pages/AirspacePage.razor")]
    public void La_riga_dell_avviso_non_e_MAI_un_paragrafo(string relativo)
    {
        Assert.DoesNotContain("<p class=\"sim-line\"", Leggi(relativo), StringComparison.Ordinal);
    }

    /// <summary>
    /// ⚠️ <b>Il gettone e il riquadro non devono convivere.</b> Finché la pagina mostrava tutt'e due si
    /// vedeva subito; ma basta lasciare indietro un <c>&lt;TranslationNotice Coverage="…" /&gt;</c> senza
    /// <c>Compatto</c> in fondo a una pagina — dove nessuno guarda — per riavere il riquadro che si voleva
    /// togliere, <b>oltre</b> al gettone.
    /// </summary>
    [Theory]
    [MemberData(nameof(Testate))]
    public void Nessuna_testata_tiene_ANCHE_il_riquadro_pieno(string relativo)
    {
        var sorgente = Leggi(relativo);

        var usi = System.Text.RegularExpressions.Regex.Matches(sorgente, @"<TranslationNotice\b[^>]*>");
        Assert.All(usi, u => Assert.Contains("Compatto=\"true\"", u.Value, StringComparison.Ordinal));
    }

    /// <summary>
    /// La regola di stampa che rende necessaria tutta questa faccenda. Se un giorno <c>.doc-head</c>
    /// tornasse visibile sul foglio, questo test cade — ed è il segnale per rileggere il patto qui sopra,
    /// non per cancellare l'asserzione.
    /// </summary>
    [Fact]
    public void Sul_foglio_la_riga_sotto_il_titolo_e_nascosta_ed_e_per_questo_che_serve_PrintMeta()
    {
        Assert.Contains(".print-meta + .doc-head { display: none !important; }",
            Leggi("wwwroot/vipi-print.css"), StringComparison.Ordinal);
        Assert.Contains(".pm-tr", Leggi("wwwroot/vipi-print.css"), StringComparison.Ordinal);
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
