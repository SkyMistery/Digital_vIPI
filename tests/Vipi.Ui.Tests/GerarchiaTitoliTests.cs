using System.Text.RegularExpressions;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Ogni pagina ha una testata, e la testata è un <c>&lt;h1&gt;</c>.
///
/// <para><b>Il difetto che presidia.</b> Fino al 23 agosto 2026 nessuna pagina ne aveva uno: la testata era
/// un <c>&lt;h2&gt;</c> e sulle vIPI lo erano anche i blocchi, quindi il titolo del documento e le sue
/// sezioni stavano allo stesso livello. Chi naviga per intestazioni — che è il modo in cui si legge un
/// documento operativo lungo con uno screen reader — non aveva una gerarchia da seguire, e chi arrivava su
/// una pagina non aveva modo di sentirne il titolo.</para>
///
/// <para><b>Perché si legge il SORGENTE e non il DOM.</b> Le pagine sono cinquanta, molte con rami
/// mutuamente esclusivi (documento trovato / non trovato / accesso negato) e dipendenze che renderle tutte
/// costerebbe più di quello che il test vale. Qui la domanda è strutturale — «quel file dichiara una
/// testata di livello 1?» — e sul testo si risponde in millisecondi e senza impalcature. ⚠️ Il limite va
/// detto: un <c>&lt;h1&gt;</c> dentro un ramo che non si rende mai passerebbe questo test. È il prezzo, ed è
/// piccolo rispetto al difetto che copre.</para>
/// </summary>
public class GerarchiaTitoliTests
{
    /// <summary>
    /// Pagine senza testata, con la ragione. Un elenco corto e motivato, non un interruttore: chi ne
    /// aggiunge una deve scrivere perché quella pagina non ha un titolo.
    /// </summary>
    private static readonly Dictionary<string, string> SenzaTestata = new()
    {
        // Reindirizza al viewer tipizzato: mostra una riga d'attesa e se ne va. Non è una pagina che si legge.
        ["ReleasePreviewPage.razor"] = "è un redirect, non una pagina",
    };

    public static TheoryData<string> Pagine()
    {
        var dati = new TheoryData<string>();
        foreach (var f in Directory.EnumerateFiles(CartellaPagine(), "*.razor")) dati.Add(Path.GetFileName(f));
        return dati;
    }

    [Theory]
    [MemberData(nameof(Pagine))]
    public void Ogni_pagina_dichiara_una_testata_di_livello_uno(string nomeFile)
    {
        var testo = File.ReadAllText(Path.Combine(CartellaPagine(), nomeFile));
        var h1 = Regex.Matches(testo, @"<h1\b", RegexOptions.IgnoreCase).Count;

        if (SenzaTestata.TryGetValue(nomeFile, out var motivo))
        {
            Assert.True(h1 == 0,
                $"{nomeFile} è nell'elenco delle pagine senza testata ({motivo}) ma ora ne ha una: " +
                "va tolta dall'elenco.");
            return;
        }

        Assert.True(h1 >= 1,
            $"{nomeFile} non dichiara nessun <h1>: la pagina non ha un titolo per chi la naviga per " +
            "intestazioni. La testata di pagina è `<h1 class=\"page-h1\">` — la classe tiene la misura di " +
            "prima (vedi vipi-theme.css), quindi la promozione non cambia il disegno.");
    }

    /// <summary>
    /// Il rovescio: la testata non deve tornare a essere un <c>&lt;h2&gt;</c>.
    ///
    /// <para>Si guarda <c>page-h1</c> e SOLO quella. Le classi che disegnavano le vecchie testate
    /// (<c>st-h2</c>, <c>xt-h2</c>) non servono: vivono benissimo su un titolo di SCHEDA, che è un'altra
    /// cosa — su <c>DiagnosticaPage</c> <c>.st-h2</c> sta su una scheda a 14px. Cercarle qui faceva
    /// scattare il test su un titolo che era giusto. <c>page-h1</c> invece dice una cosa sola: «questa è la
    /// testata della pagina», e su un <c>&lt;h2&gt;</c> non ci deve stare.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Pagine))]
    public void Nessuna_testata_di_pagina_e_rimasta_un_h2(string nomeFile)
    {
        var testo = File.ReadAllText(Path.Combine(CartellaPagine(), nomeFile));

        var sospetti = Regex.Matches(testo, @"<h2\b[^>]*class=""[^""]*\bpage-h1\b", RegexOptions.IgnoreCase)
            .Select(m => m.Value).ToList();

        Assert.True(sospetti.Count == 0,
            $"{nomeFile}: una testata di pagina è tornata <h2>.\n  " + string.Join("\n  ", sospetti));
    }

    /// <summary>
    /// I livelli non saltano un gradino: <c>h1</c> → <c>h3</c> lascia un buco a chi naviga per
    /// intestazioni, che è il modo in cui si legge un documento operativo lungo con uno screen reader.
    ///
    /// <para>Il difetto nasceva dal fatto che il tag veniva scelto per la MISURA, non per il posto nella
    /// gerarchia: un titolo di sezione era <c>&lt;h3&gt;</c> perché 28px è la misura giusta. Le due cose
    /// sono state separate — il tag dice la struttura, la misura la porta una classe
    /// (<c>.h-sect</c>, <c>.h-card</c>) o il selettore di contesto che già c'era.</para>
    ///
    /// <para>⚠️ Le intestazioni dentro un <c>&lt;aside&gt;</c> sono ESCLUSE, e non è una scorciatoia: la
    /// barra laterale (indice, scheda «Riepilogo», «Collegamenti») è una regione a sé, etichettata con
    /// <c>aria-label</c>, e le sue intestazioni non continuano lo schema del documento. Ritaggarle per far
    /// contento un contatore le farebbe sembrare sezioni del documento, che è il contrario del vero.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Pagine))]
    public void I_livelli_non_saltano_un_gradino(string nomeFile)
    {
        var livelli = IntestazioniDelDocumento(File.ReadAllText(Path.Combine(CartellaPagine(), nomeFile)));

        var salti = new List<string>();
        for (var i = 1; i < livelli.Count; i++)
            if (livelli[i].Liv - livelli[i - 1].Liv >= 2)
                salti.Add($"h{livelli[i - 1].Liv} \u2192 h{livelli[i].Liv}   \u00ab{livelli[i].Testo}\u00bb");

        Assert.True(salti.Count == 0,
            $"{nomeFile} salta un livello di intestazione:\n  " + string.Join("\n  ", salti) +
            "\n  Il tag dice la STRUTTURA; per tenere la misura di prima si usa .h-sect / .h-card " +
            "(vedi vipi-theme.css) o si allarga il selettore di contesto ai tag vicini.");
    }

    /// <summary>
    /// Le intestazioni del documento, in ordine, saltando i commenti Razor e il contenuto degli
    /// <c>&lt;aside&gt;</c>.
    ///
    /// <para>UNA scansione e non tre <c>Regex.Replace</c> in fila, e non e' pignoleria: la versione a
    /// rimpiazzi denunciava un salto che nel file non c'era — la stessa espressione, misurata fuori dal
    /// test, toglieva l'aside; dentro, no. Qui si scorre e si conta, che e' poi quello che fa uno screen
    /// reader, e il messaggio dice QUALE titolo invece di a che byte sta.</para>
    ///
    /// <para>Gli <c>&lt;aside&gt;</c> restano fuori dal conto: la barra laterale (indice, schede
    /// «Riepilogo» e «Collegamenti») e' una regione a se', etichettata con <c>aria-label</c>, e le sue
    /// intestazioni non continuano lo schema del documento. Ritaggarle per far contento un contatore le
    /// farebbe sembrare sezioni del documento, che e' il contrario del vero.</para>
    /// </summary>
    private static List<(int Liv, string Testo)> IntestazioniDelDocumento(string razor)
    {
        var fuori = new List<(int, string)>();
        var profonditaAside = 0;

        foreach (Match m in Regex.Matches(razor,
            @"@\*.*?\*@|<aside\b|</aside\s*>|<h(?<n>[1-6])\b[^>]*>(?<t>.*?)</h\k<n>\s*>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase))
        {
            var v = m.Value;
            if (v.StartsWith("@*", StringComparison.Ordinal)) continue;
            if (v.StartsWith("<aside", StringComparison.OrdinalIgnoreCase)) { profonditaAside++; continue; }
            if (v.StartsWith("</aside", StringComparison.OrdinalIgnoreCase)) { profonditaAside = Math.Max(0, profonditaAside - 1); continue; }
            if (profonditaAside > 0) continue;

            var testo = Regex.Replace(m.Groups["t"].Value, @"<[^>]*>|@\*.*?\*@", " ");
            testo = Regex.Replace(testo, @"\s+", " ").Trim();
            fuori.Add((int.Parse(m.Groups["n"].Value), testo.Length > 48 ? testo[..48] : testo));
        }
        return fuori;
    }

    /// <summary>
    /// Dal file di test alla cartella delle pagine. Si risale finché non si trova <c>src/Vipi.Ui/Pages</c>:
    /// la profondità della cartella di build cambia col TFM (net8/net10) e con la configurazione.
    /// </summary>
    private static string CartellaPagine()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidata = Path.Combine(dir.FullName, "src", "Vipi.Ui", "Pages");
            if (Directory.Exists(candidata)) return candidata;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("src/Vipi.Ui/Pages non trovata risalendo da " + AppContext.BaseDirectory);
    }
}
