using System.Text.RegularExpressions;
using Vipi.Assets;

namespace Vipi.Assets.Tests;

/// <summary>
/// La minificazione lavora sui file VERI del modulo, non su esempi inventati.
///
/// <para>È la scelta che dà a questi test il loro valore: JavaScript e CSS non li compila nessuno, quindi
/// una riga con un errore di sintassi dentro <c>vipi-ui.js</c> attraversa tutta la build senza incontrare
/// niente che la guardi — e il primo a incontrarla è chi apre la pagina. Qui il minificatore fa da
/// parser, e lo fa a ogni giro di test invece che al publish.</para>
/// </summary>
public sealed class MinificazioneTests
{
    public static TheoryData<string> FileDelModulo()
    {
        var dati = new TheoryData<string>();
        foreach (var f in Directory.EnumerateFiles(Wwwroot(), "*.*", SearchOption.AllDirectories))
        {
            var est = Path.GetExtension(f).ToLowerInvariant();
            if (Ottimizzatore.DaMinificare(f, est)) dati.Add(f);
        }
        return dati;
    }

    /// <summary>Nessuno dei nostri file deve mettere in difficoltà il minificatore.</summary>
    [Theory]
    [MemberData(nameof(FileDelModulo))]
    public void Ogni_file_del_modulo_si_minifica_senza_errori(string percorso)
    {
        var (_, errore) = Ottimizzatore.Minifica(File.ReadAllText(percorso), Path.GetExtension(percorso).ToLowerInvariant());

        Assert.True(errore is null,
            $"{Path.GetFileName(percorso)} non è minificabile: {errore}\n" +
            "Quasi sempre vuol dire che c'è un errore di sintassi in quel file. Il publish si fermerebbe qui.");
    }

    /// <summary>
    /// <b>L'invariante che conta di più.</b> Tutto ciò che il resto dell'applicazione chiama dal di fuori —
    /// l'interop di Blazor, i gestori nel markup — passa da <c>window.qualcosa = …</c>. Se la minificazione
    /// ne perdesse anche uno solo, la pagina si aprirebbe e un pezzo smetterebbe di rispondere: il guasto
    /// più difficile da riconoscere, perché non somiglia a un errore.
    ///
    /// <para>⚠️ È anche la ragione per cui l'attrezzo non rinomina le variabili locali. Questo test
    /// resterebbe verde anche con la rinomina attiva (i membri di <c>window</c> non si rinominano mai): non
    /// prova che la rinomina sia sicura, prova che <b>questo</b> passaggio non perde nomi pubblici.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(FileDelModulo))]
    public void La_minificazione_non_perde_nessuna_funzione_globale(string percorso)
    {
        if (Path.GetExtension(percorso) != ".js") return;

        var prima = Globali(File.ReadAllText(percorso));
        var (dopo, errore) = Ottimizzatore.Minifica(File.ReadAllText(percorso), ".js");
        Assert.Null(errore);

        var perse = prima.Except(Globali(dopo)).ToList();
        Assert.True(perse.Count == 0,
            $"{Path.GetFileName(percorso)}: la minificazione ha perso {string.Join(", ", perse)}. " +
            "Sono i nomi che l'interop Blazor e il markup chiamano dal di fuori.");
    }

    /// <summary>
    /// Sul CSS l'equivalente sono i nomi delle variabili: il tema è costruito su quelle (296 nell'ultimo
    /// conteggio), e perderne una non rompe la pagina — la scolora, che è peggio, perché nessuno se ne
    /// accorge subito.
    /// </summary>
    [Fact]
    public void La_minificazione_del_tema_non_perde_nessuna_variabile()
    {
        var percorso = Path.Combine(Wwwroot(), "vipi-theme.css");
        var sorgente = File.ReadAllText(percorso);

        var (minificato, errore) = Ottimizzatore.Minifica(sorgente, ".css");
        Assert.Null(errore);

        var prima = VariabiliCss(sorgente);
        Assert.NotEmpty(prima);

        var perse = prima.Except(VariabiliCss(minificato)).ToList();
        Assert.True(perse.Count == 0, $"variabili perse: {string.Join(", ", perse.Take(20))}");
    }

    /// <summary>
    /// I <c>.woff2</c> sono referenziati da DENTRO <c>vipi-fonts.css</c> e non passano dall'impronta di
    /// <c>AssetVersion</c>: se la minificazione toccasse anche un solo <c>url(...)</c>, il testo del sito
    /// tornerebbe al carattere di ripiego senza che nulla dia errore.
    /// </summary>
    [Fact]
    public void La_minificazione_dei_font_non_tocca_gli_indirizzi_dei_file()
    {
        var sorgente = File.ReadAllText(Path.Combine(Wwwroot(), "vipi-fonts.css"));
        var (minificato, errore) = Ottimizzatore.Minifica(sorgente, ".css");
        Assert.Null(errore);

        var prima = Url(sorgente);
        Assert.NotEmpty(prima);
        Assert.Equal(prima.OrderBy(x => x, StringComparer.Ordinal), Url(minificato).OrderBy(x => x, StringComparer.Ordinal));
    }

    /// <summary>Minificare due volte dev'essere come minificare una volta: il publish dev'essere ripetibile.</summary>
    [Fact]
    public void Minificare_due_volte_da_lo_stesso_risultato()
    {
        foreach (var nome in new[] { "vipi-ui.js", "vipi-theme.css" })
        {
            var estensione = Path.GetExtension(nome);
            var (una, e1) = Ottimizzatore.Minifica(File.ReadAllText(Path.Combine(Wwwroot(), nome)), estensione);
            Assert.Null(e1);
            var (due, e2) = Ottimizzatore.Minifica(una, estensione);
            Assert.Null(e2);
            Assert.Equal(una, due);
        }
    }

    /// <summary>
    /// Non si tocca quel che non è nostro. <c>_framework</c> arriva già minificato dall'SDK, <c>vendor</c>
    /// sono Leaflet e three.js — ripassarci sopra è rischio senza guadagno, e su three.js sarebbe mezzo
    /// megabyte di rischio.
    /// </summary>
    [Theory]
    [InlineData("wwwroot/_framework/blazor.web.js", ".js", false)]
    [InlineData("wwwroot/_content/Vipi.Ui/vendor/three.min.js", ".js", false)]
    [InlineData("wwwroot/_content/Vipi.Ui/vendor/leaflet/leaflet.js", ".js", false)]
    [InlineData("wwwroot/qualcosa.min.js", ".js", false)]
    [InlineData("wwwroot/_content/Vipi.Ui/vipi-ui.js", ".js", true)]
    [InlineData("wwwroot/_content/Vipi.Ui/vipi-theme.css", ".css", true)]
    [InlineData("wwwroot/fonts/pxiByp8kv8JHgFVrLCz7Z1JlFc-K.woff2", ".woff2", false)]
    public void Si_minifica_solo_quello_che_e_nostro(string percorso, string estensione, bool atteso)
        => Assert.Equal(atteso, Ottimizzatore.DaMinificare(percorso, estensione));

    /// <summary>
    /// Un file rotto non passa in silenzio: torna indietro l'originale <b>e</b> un motivo, che il
    /// chiamante trasforma in un publish fallito. L'alternativa — saltarlo e proseguire — spedirebbe un
    /// pacchetto in cui una schermata non funziona.
    /// </summary>
    [Fact]
    public void Un_file_con_un_errore_di_sintassi_non_passa_in_silenzio()
    {
        var (contenuto, errore) = Ottimizzatore.Minifica("function( { non e' javascript", ".js");

        Assert.NotNull(errore);
        Assert.Equal("function( { non e' javascript", contenuto);
    }

    private static IReadOnlyCollection<string> Globali(string js) =>
        Regex.Matches(js, @"window\s*\.\s*([A-Za-z_$][\w$]*)\s*=(?!=)")
            .Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

    private static IReadOnlyCollection<string> VariabiliCss(string css) =>
        Regex.Matches(css, @"(--[A-Za-z0-9_-]+)\s*:").Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

    private static IReadOnlyCollection<string> Url(string css) =>
        Regex.Matches(css, @"url\(\s*['""]?([^'""()]+)['""]?\s*\)").Select(m => m.Groups[1].Value.Trim()).ToList();

    /// <summary>La wwwroot dei sorgenti, risalendo dall'output dei test (come fanno gli altri test del repo).</summary>
    internal static string Wwwroot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var c = Path.Combine(dir.FullName, "src", "Vipi.Ui", "wwwroot");
            if (Directory.Exists(c)) return c;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException($"wwwroot non trovata risalendo da {AppContext.BaseDirectory}");
    }
}
