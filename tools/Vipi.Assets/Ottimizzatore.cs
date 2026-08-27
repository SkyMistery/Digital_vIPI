using System.IO.Compression;
using System.Text;
using NUglify;
using NUglify.JavaScript;

namespace Vipi.Assets;

/// <summary>
/// Prepara la cartella <c>wwwroot</c> del pacchetto: toglie dai fogli di stile e dagli script quello che
/// serve a chi legge il codice e non a chi apre la pagina, e lascia accanto a ogni file di testo la sua
/// variante compressa, già pronta.
///
/// <para><b>Perché esiste.</b> Il 27 agosto 2026, contati: <b>il 44% dei byte di CSS e JavaScript spediti a
/// ogni visitatore erano commenti</b> — 218 905 su 500 367. Non è un difetto dei commenti, che in questo
/// codice sono la parte migliore: è che appartengono al <b>sorgente</b>, e finivano nel browser perché fra i
/// due non c'era nessun passaggio. Il solo <c>vipi-theme.css</c> era 293 KB di cui 134 di commento.</para>
///
/// <para><b>E perché anche la compressione.</b> Su net8 <c>UseStaticFiles</c> serve i file così come sono e
/// la compressione la fa il middleware, <b>a ogni richiesta</b>: il livello che ci si può permettere lì è la
/// qualità 4 di Brotli. La qualità 11 — che sullo stesso materiale toglie un altro 17% — costa troppo per
/// pagarla a ogni richiesta, ma qui si paga <b>una volta sola, a build</b>. Misura sui file del modulo:</para>
///
/// <code>
///   com'era: non minificato, compresso a richiesta (q4)   159 852 B
///   minificato + precompresso (q11)                        48 129 B      −70%
/// </code>
///
/// <para>⚠️ Sul JavaScript <b>non si rinominano le variabili locali</b>. Sarebbe la trasformazione che dà
/// più byte, ed è anche la sola che può cambiare il comportamento di un programma corretto (basta un
/// <c>eval</c>, un <c>with</c>, o del codice che si guarda il nome di una funzione). Misurata su questi
/// file vale <b>3 524 byte su 57 920</b>, il 2%: non è un prezzo che valga un guasto che si vede solo in
/// produzione, su una pagina sola, mesi dopo.</para>
/// </summary>
public static class Ottimizzatore
{
    /// <summary>I tipi che si comprimono. Tutto il resto (woff2, png, ico) è già compresso in sé: rifarlo
    /// produce file più grossi dell'originale e allunga soltanto il pacchetto.</summary>
    private static readonly string[] EstensioniDiTesto =
        { ".css", ".js", ".json", ".svg", ".html", ".xml", ".txt", ".webmanifest" };

    /// <summary>Il resoconto di una passata: serve al chiamante per stamparlo e ai test per verificarlo.</summary>
    public sealed record Esito(
        int FileMinificati, long ByteTolti, int FileCompressi, long ByteCompressi, long ByteOriginali,
        IReadOnlyList<string> Errori);

    /// <summary>
    /// Minifica e precomprime, sul posto, la cartella indicata.
    /// </summary>
    /// <remarks>
    /// ⚠️ Un errore di minificazione <b>non si ingoia</b>: finisce in <see cref="Esito.Errori"/> e il
    /// chiamante deve far fallire il publish. Un file saltato in silenzio significherebbe spedire un
    /// pacchetto in cui una schermata non funziona, scoperto da un utente invece che da una build.
    /// </remarks>
    public static Esito Esegui(string cartella)
    {
        var errori = new List<string>();
        int minificati = 0, compressi = 0;
        long byteTolti = 0, byteCompressi = 0, byteOriginali = 0;

        foreach (var file in Directory.EnumerateFiles(cartella, "*.*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        {
            var estensione = Path.GetExtension(file).ToLowerInvariant();

            if (DaMinificare(file, estensione))
            {
                var prima = File.ReadAllText(file);
                var (dopo, errore) = Minifica(prima, estensione);
                if (errore is not null) { errori.Add($"{Relativo(cartella, file)}: {errore}"); continue; }

                // Solo se ci si guadagna davvero. Un file già minificato che tornasse indietro di qualche
                // byte non è un guadagno, è un file riscritto per niente.
                if (dopo.Length < prima.Length)
                {
                    File.WriteAllText(file, dopo, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    minificati++;
                    byteTolti += prima.Length - dopo.Length;
                }
            }

            if (!EstensioniDiTesto.Contains(estensione)) continue;

            var contenuto = File.ReadAllBytes(file);
            byteOriginali += contenuto.Length;
            byteCompressi += Affianca(file + ".br", contenuto, Brotli);
            byteCompressi += Affianca(file + ".gz", contenuto, Gzip);
            compressi++;
        }

        return new Esito(minificati, byteTolti, compressi, byteCompressi, byteOriginali, errori);
    }

    /// <summary>
    /// Cosa si minifica e cosa no.
    ///
    /// <para><c>_framework</c> è la roba del runtime Blazor: arriva già minificata dall'SDK, e ripassarci
    /// sopra è rischio senza guadagno. <c>vendor</c> sono Leaflet e three.js, che non sono nostri e sono già
    /// minificati per conto loro. I <c>.min.</c> lo dichiarano nel nome.</para>
    /// </summary>
    public static bool DaMinificare(string percorso, string estensione)
    {
        if (estensione != ".css" && estensione != ".js") return false;

        var normalizzato = percorso.Replace('\\', '/');
        if (normalizzato.Contains("/_framework/", StringComparison.OrdinalIgnoreCase)) return false;
        if (normalizzato.Contains("/vendor/", StringComparison.OrdinalIgnoreCase)) return false;
        if (Path.GetFileName(percorso).Contains(".min.", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    /// <summary>Minifica un contenuto. Restituisce <c>(risultato, null)</c> o <c>(originale, motivo)</c>.</summary>
    public static (string Contenuto, string? Errore) Minifica(string sorgente, string estensione)
    {
        var esito = estensione == ".css"
            ? Uglify.Css(sorgente)
            : Uglify.Js(sorgente, ImpostazioniJs);

        if (esito.HasErrors)
            return (sorgente, string.Join(" | ", esito.Errors.Select(e => e.ToString())));

        return (esito.Code, null);
    }

    /// <summary>
    /// ⚠️ <c>LocalRenaming.KeepAll</c> è la riga che tiene questo attrezzo dalla parte sicura del confine.
    /// Vedi il perché in testa alla classe: vale il 2% dei byte e può cambiare il comportamento.
    /// </summary>
    private static CodeSettings ImpostazioniJs => new()
    {
        LocalRenaming = LocalRenaming.KeepAll,
    };

    /// <summary>Scrive la variante compressa, ma solo se è più piccola dell'originale.</summary>
    private static long Affianca(string percorso, byte[] contenuto, Func<byte[], byte[]> comprimi)
    {
        var compresso = comprimi(contenuto);
        if (compresso.Length >= contenuto.Length)
        {
            // Può succedere su file minuscoli, dove l'intestazione del formato costa più di quel che toglie.
            // Meglio nessun file che un file che il middleware sceglierebbe peggiorando la risposta.
            if (File.Exists(percorso)) File.Delete(percorso);
            return 0;
        }
        File.WriteAllBytes(percorso, compresso);
        return compresso.Length;
    }

    private static byte[] Brotli(byte[] dati) => Comprimi(dati,
        f => new BrotliStream(f, CompressionLevel.SmallestSize, leaveOpen: true));

    private static byte[] Gzip(byte[] dati) => Comprimi(dati,
        f => new GZipStream(f, CompressionLevel.SmallestSize, leaveOpen: true));

    private static byte[] Comprimi(byte[] dati, Func<Stream, Stream> involucro)
    {
        using var destinazione = new MemoryStream();
        using (var flusso = involucro(destinazione)) flusso.Write(dati, 0, dati.Length);
        return destinazione.ToArray();
    }

    private static string Relativo(string radice, string file) =>
        Path.GetRelativePath(radice, file).Replace('\\', '/');
}
