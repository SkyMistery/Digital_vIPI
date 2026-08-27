using System.IO.Compression;
using System.Text;
using Vipi.Assets;

namespace Vipi.Assets.Tests;

/// <summary>
/// Le varianti già compresse: quelle che il publish lascia accanto ai file e che
/// <c>AssetPrecompressi</c> consegna al posto della compressione a richiesta.
///
/// <para>Lavorano su una cartella temporanea e non sui sorgenti, per una ragione sola: l'attrezzo riscrive
/// i file SUL POSTO, e puntarlo alla <c>wwwroot</c> del repository cancellerebbe proprio i commenti che
/// esiste per non spedire.</para>
/// </summary>
public sealed class PrecompressioneTests : IDisposable
{
    private readonly string _cartella = Path.Combine(Path.GetTempPath(), $"vipi-assets-{Guid.NewGuid():N}");

    public PrecompressioneTests() => Directory.CreateDirectory(_cartella);

    public void Dispose()
    {
        try { Directory.Delete(_cartella, recursive: true); } catch { /* pulizia best-effort */ }
    }

    /// <summary>
    /// Il giro completo su un file vero: si minifica, si affianca la variante, e la variante
    /// <b>si riapre identica al file servito</b>. È l'unica prova che conti — una variante che si
    /// decomprime in qualcos'altro sarebbe una pagina bianca, non un byte di troppo.
    /// </summary>
    [Fact]
    public void La_variante_compressa_si_riapre_identica_al_file()
    {
        var origine = Path.Combine(MinificazioneTests.Wwwroot(), "vipi-theme.css");
        var copia = Path.Combine(_cartella, "vipi-theme.css");
        File.Copy(origine, copia);

        var esito = Ottimizzatore.Esegui(_cartella);
        Assert.Empty(esito.Errori);

        var servito = File.ReadAllBytes(copia);
        Assert.Equal(servito, Decomprimi(copia + ".br", f => new BrotliStream(f, CompressionMode.Decompress)));
        Assert.Equal(servito, Decomprimi(copia + ".gz", f => new GZipStream(f, CompressionMode.Decompress)));
    }

    /// <summary>E deve valere la pena: sul foglio di stile del modulo la variante è una frazione del file.</summary>
    [Fact]
    public void Sul_foglio_di_stile_la_variante_vale_la_pena()
    {
        var copia = Path.Combine(_cartella, "vipi-theme.css");
        File.Copy(Path.Combine(MinificazioneTests.Wwwroot(), "vipi-theme.css"), copia);
        var grezzo = new FileInfo(copia).Length;

        Ottimizzatore.Esegui(_cartella);

        var minificato = new FileInfo(copia).Length;
        var compresso = new FileInfo(copia + ".br").Length;

        Assert.True(minificato < grezzo * 0.7, $"minificato {minificato} su {grezzo}: quasi nulla di tolto.");
        Assert.True(compresso < grezzo / 5, $"compresso {compresso} su {grezzo} grezzi: la precompressione non sta lavorando.");
    }

    /// <summary>
    /// Su un file minuscolo l'intestazione del formato costa più di quel che toglie. In quel caso la
    /// variante <b>non si scrive</b>: esisterebbe solo per far consegnare al browser più byte
    /// dell'originale, che è l'esatto contrario del punto.
    /// </summary>
    [Fact]
    public void Su_un_file_dove_comprimere_non_conviene_la_variante_non_si_scrive()
    {
        var minuscolo = Path.Combine(_cartella, "a.css");
        File.WriteAllText(minuscolo, "a{color:red}");

        Ottimizzatore.Esegui(_cartella);

        Assert.False(File.Exists(minuscolo + ".br"), "scritta una variante più grossa dell'originale.");
    }

    /// <summary>
    /// Quello che era già compresso in sé — i caratteri, le immagini — non si tocca: rifarlo produce file
    /// più grossi dell'originale e allunga soltanto il pacchetto.
    /// </summary>
    [Fact]
    public void I_font_e_le_immagini_non_si_ricomprimono()
    {
        var font = Path.Combine(_cartella, "c.woff2");
        File.WriteAllBytes(font, new byte[4096]);

        Ottimizzatore.Esegui(_cartella);

        Assert.False(File.Exists(font + ".br"));
        Assert.False(File.Exists(font + ".gz"));
    }

    /// <summary>
    /// L'attrezzo non passa sopra a un file rotto: lo riporta e <b>non lo riscrive</b>. È quel che fa
    /// fermare il publish invece di spedire una schermata che non risponde.
    /// </summary>
    [Fact]
    public void Un_file_rotto_ferma_la_passata_e_non_viene_riscritto()
    {
        var rotto = Path.Combine(_cartella, "rotto.js");
        const string sorgente = "function( { non e' javascript";
        File.WriteAllText(rotto, sorgente);

        var esito = Ottimizzatore.Esegui(_cartella);

        Assert.Single(esito.Errori);
        Assert.Contains("rotto.js", esito.Errori[0]);
        Assert.Equal(sorgente, File.ReadAllText(rotto));
    }

    /// <summary>Due passate sulla stessa cartella lasciano gli stessi byte: il publish dev'essere ripetibile.</summary>
    [Fact]
    public void Due_passate_lasciano_gli_stessi_byte()
    {
        var copia = Path.Combine(_cartella, "vipi-ui.js");
        File.Copy(Path.Combine(MinificazioneTests.Wwwroot(), "vipi-ui.js"), copia);

        Ottimizzatore.Esegui(_cartella);
        var dopoUna = (File.ReadAllBytes(copia), File.ReadAllBytes(copia + ".br"));

        Ottimizzatore.Esegui(_cartella);

        Assert.Equal(dopoUna.Item1, File.ReadAllBytes(copia));
        Assert.Equal(dopoUna.Item2, File.ReadAllBytes(copia + ".br"));
    }

    /// <summary>Le varianti non si minificano né si comprimono a loro volta.</summary>
    [Fact]
    public void Le_varianti_non_generano_varianti_di_se_stesse()
    {
        var copia = Path.Combine(_cartella, "vipi-ui.js");
        File.Copy(Path.Combine(MinificazioneTests.Wwwroot(), "vipi-ui.js"), copia);

        Ottimizzatore.Esegui(_cartella);
        Ottimizzatore.Esegui(_cartella);

        Assert.False(File.Exists(copia + ".br.br"));
        Assert.False(File.Exists(copia + ".br.gz"));
        Assert.False(File.Exists(copia + ".gz.br"));
    }

    private static byte[] Decomprimi(string percorso, Func<Stream, Stream> involucro)
    {
        using var sorgente = File.OpenRead(percorso);
        using var flusso = involucro(sorgente);
        using var destinazione = new MemoryStream();
        flusso.CopyTo(destinazione);
        return destinazione.ToArray();
    }
}
