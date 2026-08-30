using System.Net;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// Quel che il browser riceve perché «Attempting to reconnect to the server…» smetta di essere un vicolo
/// cieco: l'avvio di Blazor con i nostri tempi, il riquadro tradotto al posto del suo, e il colpetto che
/// tiene sveglio il processo.
///
/// <para>⚠️ Questi test guardano l'HTML servito e la risposta di un endpoint, che è metà della verifica.
/// L'altra metà — che staccando davvero il server la pagina si ricarichi da sola — la fa un browser, e sta
/// nella nota di verifica del 31 agosto 2026 (<c>docs/history/</c>): nessun test qui dentro apre una
/// pagina.</para>
/// </summary>
public sealed class RiconnessioneTests : IClassFixture<SmokeTests.VipiAppFactory>
{
    private readonly SmokeTests.VipiAppFactory _factory;
    public RiconnessioneTests(SmokeTests.VipiAppFactory factory) => _factory = factory;

    /// <summary>
    /// ⚠️ <b>Il test che vale tutti gli altri di questo file.</b> Da quando i tempi di riconnessione si
    /// scrivono, <c>blazor.web.js</c> non parte più da solo e ad avviarlo è <c>vipi-riconnessione.js</c>:
    /// le due righe sono una cosa sola. Se un giorno restasse l'<c>autostart="false"</c> senza il file che
    /// chiama <c>Blazor.start</c>, il sito si vedrebbe intero e non risponderebbe a NIENTE — nessun errore
    /// in pagina, nessuna riga nei log, solo tasti che non fanno niente.
    /// </summary>
    [Theory]
    [InlineData("/services/vsop")]
    [InlineData("/services")]
    public async Task Chi_spegne_lavvio_automatico_deve_riaccenderlo(string percorso)
    {
        var html = await _factory.CreateClient().GetStringAsync(percorso);

        var blazor = html.IndexOf("_framework/blazor.web.js", StringComparison.Ordinal);
        var nostro = html.IndexOf("vipi-riconnessione.js", StringComparison.Ordinal);

        Assert.True(blazor >= 0, "blazor.web.js non è nel markup");
        Assert.Contains("autostart=\"false\"", html, StringComparison.Ordinal);
        Assert.True(nostro > blazor,
            "vipi-riconnessione.js deve venire DOPO blazor.web.js: è quel file a definire `Blazor`, e con "
            + "autostart=\"false\" nessuno avvierebbe il circuito. Ordine trovato: blazor.web.js a "
            + $"{blazor}, vipi-riconnessione.js a {nostro}.");
    }

    /// <summary>
    /// Il riquadro è nostro, ma i tre id li cerca Blazor per nome: senza, torna a disegnare il suo — in
    /// inglese, fuori dal tema, e con un tasto che riprova invece di ricaricare. Non lo direbbe nessun
    /// errore.
    /// </summary>
    [Fact]
    public async Task Il_riquadro_porta_gli_id_che_Blazor_cerca()
    {
        var html = await _factory.CreateClient().GetStringAsync("/services/vsop");

        foreach (var id in new[]
                 {
                     "components-reconnect-modal",
                     "components-reconnect-current-attempt",
                     "components-reconnect-max-retries",
                 })
            Assert.Contains($"id=\"{id}\"", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// E le frasi sono tradotte: il riquadro esiste per non lasciare un messaggio inglese davanti a chi sta
    /// leggendo una vIPI in italiano.
    /// </summary>
    [Fact]
    public async Task Le_frasi_del_riquadro_sono_tradotte()
    {
        var html = await _factory.CreateClient().GetStringAsync("/services/vsop?culture=it");

        Assert.Contains("Collegamento interrotto", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Reconnect_Title", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Il colpetto: 204 e basta. ⚠️ La cosa da non perdere è che non guardi il database — lo chiama ogni
    /// scheda aperta ogni due minuti e mezzo, e una sonda vera lì sarebbe un carico continuo comprato per
    /// risolvere un problema di inattività.
    /// </summary>
    [Fact]
    public async Task Il_colpetto_risponde_vuoto_e_non_si_fa_mettere_in_cache()
    {
        var risposta = await _factory.CreateClient().GetAsync("/vsop/ping");

        Assert.Equal(HttpStatusCode.NoContent, risposta.StatusCode);
        Assert.Contains("no-store", risposta.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    /// E risponde anche in <c>HEAD</c>. ⚠️ Non è pignoleria: i servizi di sorveglianza esterni — il modo
    /// previsto di tenere caldo il processo quando non c'è nessuno — bussano in HEAD per default, e un 405
    /// lì somiglia a un guasto nostro. Trovato dal vivo il 31 agosto 2026, provando l'endpoint appena
    /// scritto con <c>curl -I</c>.
    /// </summary>
    [Fact]
    public async Task Il_colpetto_risponde_anche_a_HEAD()
    {
        using var richiesta = new HttpRequestMessage(HttpMethod.Head, "/vsop/ping");

        var risposta = await _factory.CreateClient().SendAsync(richiesta);

        Assert.Equal(HttpStatusCode.NoContent, risposta.StatusCode);
    }

    /// <summary>
    /// ⚠️ E il colpetto NON è una pagina: la tabella degli indirizzi di ieri non deve mai redirigerlo, o
    /// ogni due minuti e mezzo, per ogni scheda aperta, si pagherebbero due richieste invece di una.
    /// </summary>
    [Fact]
    public async Task Il_colpetto_non_viene_rediretto_come_un_indirizzo_vecchio()
    {
        using var cliente = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var risposta = await cliente.GetAsync("/vsop/ping");

        Assert.Equal(HttpStatusCode.NoContent, risposta.StatusCode);
    }
}
