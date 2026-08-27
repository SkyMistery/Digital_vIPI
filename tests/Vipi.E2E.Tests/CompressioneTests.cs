using System.Net.Http.Headers;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// La compressione delle risposte, misurata invece che dichiarata.
///
/// <para><b>Il difetto che questi test chiudono</b>, trovato il 27 agosto 2026. I due provider erano
/// registrati e funzionanti, l'header <c>Content-Encoding: br</c> arrivava, nessun errore da nessuna parte —
/// e ciononostante <b>attivare Brotli faceva scaricare più byte</b> che non averlo. Il default di ASP.NET per
/// il livello è <c>CompressionLevel.Fastest</c>, che per Brotli è la qualità 1; e siccome Brotli è registrato
/// per primo vince la negoziazione con ogni browser moderno. Misura di allora:</para>
///
/// <code>
///   vipi-theme.css   grezzo 295 571   br 120 601   gzip 101 217
///   HTML vIPI ACC    grezzo 294 776   br  62 161   gzip  50 018
/// </code>
///
/// <para>⚠️ Il confronto è <b>br contro gzip</b>, non «br sotto una soglia di byte». Una soglia fissa
/// invecchia al primo foglio di stile che cresce e va aggiornata a mano; l'invariante vera è che il formato
/// che scegliamo per primo non deve essere il peggiore dei due — e quella non invecchia mai.</para>
/// </summary>
public sealed class CompressioneTests : IClassFixture<SmokeTests.VipiAppFactory>
{
    private readonly SmokeTests.VipiAppFactory _factory;
    public CompressioneTests(SmokeTests.VipiAppFactory factory) => _factory = factory;

    /// <summary>
    /// Il foglio di stile del modulo (il singolo asset più pesante), un JS denso e una pagina resa.
    ///
    /// <para><b>Perché una tolleranza del 2% e non «minore o uguale».</b> Il livello sensato da pagare a
    /// ogni richiesta è <c>Optimal</c>, che per Brotli è la qualità 4. Su testo ripetitivo — HTML, CSS —
    /// stravince; su JavaScript già denso può restare un capello sopra gzip-6 (misurato: 18 522 contro
    /// 18 288 byte su <c>vipi-ui.js</c>, lo 1,3%). Non è un difetto della configurazione: è il punto in cui
    /// la qualità 4 finisce. La qualità 11, che vincerebbe ovunque e di molto, costa centinaia di
    /// millisecondi di CPU e non si può pagare a richiesta — si paga <b>a build</b>, e infatti è quello che
    /// fa la precompressione degli asset statici.</para>
    ///
    /// <para>⚠️ La tolleranza copre quel capello, non un ritorno a <c>Fastest</c>: con la qualità 1 lo
    /// scarto misurato era del <b>19÷24%</b>, cioè dieci volte la soglia. Il 2% è il peggior scarto
    /// misurato (1,3% su <c>vipi-ui.js</c>) più un margine per il prossimo file altrettanto denso. Se
    /// questo test torna rosso, il livello è stato perso: non è il rumore del formato.</para>
    /// </summary>
    [Theory]
    [InlineData("/_content/Vipi.Ui/vipi-theme.css")]
    [InlineData("/_content/Vipi.Ui/vipi-ui.js")]
    [InlineData("/services/vsop")]
    public async Task Brotli_non_puo_essere_peggio_di_gzip(string percorso)
    {
        var (brByte, brEnc) = await ScaricaAsync(percorso, "br");
        var (gzByte, gzEnc) = await ScaricaAsync(percorso, "gzip");

        Assert.Equal("br", brEnc);
        Assert.Equal("gzip", gzEnc);
        Assert.True(brByte <= gzByte * 1.02,
            $"{percorso}: Brotli ({brByte} B) supera gzip ({gzByte} B) di più del 2%. " +
            "È il difetto del 27 agosto 2026: il livello del provider è tornato a Fastest (qualità 1). " +
            "Vedi VipiStartup, la Configure<BrotliCompressionProviderOptions>.");
    }

    /// <summary>
    /// E comprimere deve comunque servire a qualcosa: la variante compressa dev'essere nettamente più
    /// piccola dell'originale. Metà è una soglia larga di proposito — qui interessa il caso in cui la
    /// compressione smette del tutto di funzionare, non l'ultimo punto percentuale.
    /// </summary>
    [Theory]
    [InlineData("/_content/Vipi.Ui/vipi-theme.css")]
    [InlineData("/services/vsop")]
    public async Task La_variante_compressa_e_molto_piu_piccola_dell_originale(string percorso)
    {
        var (compressi, _) = await ScaricaAsync(percorso, "br");
        var (grezzi, encGrezzo) = await ScaricaAsync(percorso, "identity");

        Assert.Null(encGrezzo);
        Assert.True(compressi * 2 < grezzi,
            $"{percorso}: compresso {compressi} B contro {grezzi} B grezzi — la compressione non sta lavorando.");
    }

    /// <summary>
    /// Lo streaming SSE dell'ATC live non passa dalla compressione: <c>text/event-stream</c> è fuori dalla
    /// lista dei tipi apposta, perché quella rotta disattiva il buffering e dev'essere consegnata subito.
    /// Se qualcuno lo aggiungesse alla lista, il live comincerebbe ad arrivare a blocchi — un guasto che si
    /// vede solo a schermo, con un browser aperto, e che nessun altro test qui dentro coglierebbe.
    /// </summary>
    [Fact]
    public async Task Lo_stream_dell_atc_live_non_viene_compresso()
    {
        using var client = _factory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, "/vsop/live/atc");
        req.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));
        req.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

        // ResponseHeadersRead: la rotta è uno stream che non finisce mai — si leggono le intestazioni e basta.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var res = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        Assert.Empty(res.Content.Headers.ContentEncoding);
    }

    private async Task<(int Byte, string? Encoding)> ScaricaAsync(string percorso, string codifica)
    {
        using var client = _factory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, percorso);
        req.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue(codifica));

        using var res = await client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var byteRicevuti = (await res.Content.ReadAsByteArrayAsync()).Length;
        return (byteRicevuti, res.Content.Headers.ContentEncoding.FirstOrDefault());
    }
}
