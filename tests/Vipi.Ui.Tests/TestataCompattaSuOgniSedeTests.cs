using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// <b>La testata compatta delle cinque famiglie documentali</b> (3 settembre 2026).
///
/// <para>Richiesta del committente, nata guardando il vSOP militare: sopra il documento stavano una riga di
/// titolo, un blocco di tre bottoni («Everything · Pilot · ATC») e un riquadro pieno di tre righe per dire
/// che il documento è scritto in una lingua sola. Tre cartelli fra chi legge e ciò che è venuto a leggere.
/// Ora il filtro di lettura sta <b>in coda al sottotitolo</b> e la lingua è un <b>gettone</b> accanto
/// all'avviso di simulazione — la stessa cura già data al «tradotta a macchina» il 2 settembre.</para>
///
/// <para>⚠️ <b>Lo spostamento porta con sé la trappola di sempre.</b> I gettoni stanno dentro
/// <c>.doc-head</c>, e <c>.doc-head</c> sul foglio è <b>nascosto</b>
/// (<c>.print-meta + .doc-head{display:none}</c>): la pagina stampata perderebbe l'informazione senza che
/// niente lo dica. È lo stesso inciampo pagato con l'avviso di simulazione e col gettone di traduzione, ed
/// è il motivo per cui <c>PrintMeta</c> ha una riga per ciascuno. Il patto è <b>doppio</b>: chi mostra il
/// gettone deve passare la stessa lingua a <c>PrintMeta</c>.</para>
///
/// <para>⚠️ La chip pilota/ATC invece <b>non</b> ha bisogno del gemello di stampa: è un comando, non
/// contenuto, e in stampa era già nascosta apposta. Ciò che resta sul foglio è il <b>badge</b> sulle
/// singole sezioni, che dice a chi parla quella sezione.</para>
/// </summary>
public sealed class TestataCompattaSuOgniSedeTests
{
    /// <summary>
    /// Le cinque testate documentali, col percorso relativo a <c>src/Vipi.Ui</c>.
    /// <para>⚠️ La vLOA non è una pagina ma un COMPONENTE (<c>VloaDocumentView</c>): la testata sta lì, e la
    /// pagina che la ospita (<c>VloaListPage</c>) le passa i dati — lingua bloccata e filtro di lettura —
    /// perché è l'unica a sapere quali parametri della propria rotta vanno conservati.</para>
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
    public void Ogni_testata_porta_il_gettone_della_lingua_E_lo_passa_alla_stampa(string relativo)
    {
        var sorgente = Leggi(relativo);

        Assert.True(
            sorgente.Contains("<LinguaBloccataNotice", StringComparison.Ordinal)
            && sorgente.Contains("Compatto=\"true\"", StringComparison.Ordinal),
            $"{relativo}: nessun gettone <LinguaBloccataNotice … Compatto=\"true\" /> nella riga sotto il titolo.");

        Assert.True(
            sorgente.Contains("<PrintMeta", StringComparison.Ordinal)
            && sorgente.Contains("Bloccata=", StringComparison.Ordinal),
            $"{relativo}: il gettone c'è ma la lingua non arriva a PrintMeta. Sul foglio `.doc-head` è "
            + "nascosto, quindi questo documento si stamperebbe senza dire in che lingua è scritto.");
    }

    /// <summary>
    /// ⚠️ <b>Il gettone e il riquadro non devono convivere.</b> Basta lasciare indietro un
    /// <c>&lt;LinguaBloccataNotice Lingua="…" /&gt;</c> senza <c>Compatto</c> in fondo a una pagina — dove
    /// nessuno guarda — per riavere il riquadro che si voleva togliere, <b>oltre</b> al gettone.
    /// </summary>
    [Theory]
    [MemberData(nameof(Testate))]
    public void Nessuna_testata_tiene_ANCHE_il_riquadro_pieno_della_lingua(string relativo)
    {
        var usi = System.Text.RegularExpressions.Regex.Matches(Leggi(relativo), @"<LinguaBloccataNotice\b[^>]*>");
        Assert.All(usi, u => Assert.Contains("Compatto=\"true\"", u.Value, StringComparison.Ordinal));
    }

    /// <summary>
    /// ⚠️ Il gettone sta <b>nella stessa riga</b> dell'avviso di simulazione, non su una terza: due righe di
    /// cartelli sopra il documento sono di nuovo il problema che si stava togliendo. E la riga è un
    /// <c>&lt;div&gt;</c>, mai un <c>&lt;p&gt;</c>, perché il «?» è un <c>&lt;details&gt;</c> — che un
    /// <c>&lt;p&gt;</c> non può contenere, e il parser sposterebbe fuori (lo pretende anche
    /// <c>AvvisoTraduzioneSuOgniSedeTests</c>).
    /// </summary>
    [Theory]
    [MemberData(nameof(Testate))]
    public void Il_gettone_della_lingua_sta_nella_riga_dell_avviso_di_simulazione(string relativo)
    {
        var riga = System.Text.RegularExpressions.Regex.Match(
            Leggi(relativo), @"<div class=""sim-line"">(?<c>.*?)</div>",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        Assert.True(riga.Success, $"{relativo}: l'avviso non è su una riga propria (<div class=\"sim-line\">).");
        Assert.Contains("<SimDisclaimer", riga.Groups["c"].Value, StringComparison.Ordinal);
        Assert.Contains("<LinguaBloccataNotice", riga.Groups["c"].Value, StringComparison.Ordinal);
    }

    /// <summary>
    /// La chip di lettura sta <b>in testata</b>, in coda al sottotitolo, e non più in un blocco suo sopra il
    /// documento. ⚠️ Una sola forma per tutte e cinque: una famiglia rimasta indietro si vedrebbe solo
    /// aprendo quella pagina, ed è esattamente il difetto che ha fatto nascere questa riga.
    /// </summary>
    [Theory]
    [MemberData(nameof(Testate))]
    public void La_chip_di_lettura_sta_in_coda_al_sottotitolo(string relativo)
    {
        var sorgente = Leggi(relativo);

        var usi = System.Text.RegularExpressions.Regex.Matches(sorgente, @"<AudienceChip\b[^>]*>");
        Assert.True(usi.Count > 0, $"{relativo}: la chip pilota/ATC non c'è più.");
        Assert.All(usi, u => Assert.Contains("Compatto=\"true\"", u.Value, StringComparison.Ordinal));

        var riga = System.Text.RegularExpressions.Regex.Match(
            sorgente, @"<div class=""sub-line"">(?<c>.*?)</div>",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(riga.Success, $"{relativo}: manca la riga del sottotitolo (<div class=\"sub-line\">).");
        Assert.Contains("<AudienceChip", riga.Groups["c"].Value, StringComparison.Ordinal);
    }

    /// <summary>
    /// La regola di stampa che rende necessario tutto questo, e la riga che la compensa. Se un giorno
    /// <c>.doc-head</c> tornasse visibile sul foglio questo test cade — ed è il segnale per rileggere il
    /// patto qui sopra, non per cancellare l'asserzione.
    /// </summary>
    [Fact]
    public void Sul_foglio_la_lingua_la_scrive_PrintMeta_perche_la_testata_e_nascosta()
    {
        var css = Leggi("wwwroot/vipi-print.css");
        Assert.Contains(".print-meta + .doc-head { display: none !important; }", css, StringComparison.Ordinal);
        Assert.Contains(".pm-lang", css, StringComparison.Ordinal);
        Assert.Contains("pm-lang", Leggi("Components/PrintMeta.razor"), StringComparison.Ordinal);
    }

    /// <summary>
    /// ⚠️ Sul foglio il testo va per <b>esteso</b>: davanti a una pagina stampata non c'è nessun «?» da
    /// aprire. La riga di stampa usa la chiave lunga, non l'etichetta corta del gettone.
    /// </summary>
    [Fact]
    public void Sul_foglio_la_lingua_si_scrive_per_esteso()
    {
        var printMeta = Leggi("Components/PrintMeta.razor");
        Assert.Contains("Lang_LockedBody", printMeta, StringComparison.Ordinal);
        Assert.DoesNotContain("Lang_LockedChip", printMeta, StringComparison.Ordinal);
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
