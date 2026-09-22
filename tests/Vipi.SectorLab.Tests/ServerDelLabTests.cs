using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Vipi.SectorLab.Ui.Server;

namespace Vipi.SectorLab.Tests;

/// <summary>
/// Il server locale del Lab, con un Kestrel VERO su 127.0.0.1 (carta F3 §3, slice 1): il cancello, e le trappole di
/// F0 che non si vedono quando mancano. La finestra e la WebView2 si provano a mano, con <c>--autoprova</c>.
/// </summary>
public sealed class ServerDelLabTests : IAsyncLifetime
{
    private const string Segreto = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";

    private WebApplication _app = null!;
    private Uri _base = null!;

    public async Task InitializeAsync()
    {
        _app = ServerDelLab.Crea(new SegretoDelLab(Segreto), typeof(ServerDelLabTests).Assembly.GetName().Name);
        await _app.StartAsync();
        _base = ServerDelLab.Indirizzo(_app);
    }

    public async Task DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    /// <summary>Un client che non segue i rimandi e non tiene i cookie: ogni richiesta dice solo quel che porta.</summary>
    private HttpClient Client() => new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { BaseAddress = _base };

    private static HttpRequestMessage ColCookie(HttpMethod metodo, string percorso, string valore = Segreto)
    {
        var r = new HttpRequestMessage(metodo, percorso);
        r.Headers.Add("Cookie", $"sectorlab={valore}");
        return r;
    }

    [Fact]
    public void AscoltaSoloSullInterfacciaDiLoopback()
    {
        Assert.Equal("127.0.0.1", _base.Host);
        Assert.NotEqual(0, _base.Port);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/_framework/blazor.web.js")]
    [InlineData("/_content/Vipi.SectorLab.Ui/sectorlab.js")]
    [InlineData("/pagina-che-non-esiste")]
    public async Task SenzaSegretoNonSiEntra(string percorso)
    {
        using var client = Client();
        var risposta = await client.GetAsync(percorso);
        Assert.Equal(HttpStatusCode.Forbidden, risposta.StatusCode);
    }

    [Fact]
    public async Task SenzaSegretoNonSiApreNemmenoIlCircuito()
    {
        // Il prototipo di F0 lasciava passare /_blazor senza chiedere niente.
        using var client = Client();
        var risposta = await client.PostAsync("/_blazor/negotiate?negotiateVersion=1", content: null);
        Assert.Equal(HttpStatusCode.Forbidden, risposta.StatusCode);
    }

    [Fact]
    public async Task UnSegretoSbagliatoNonApre()
    {
        using var client = Client();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/?k=" + Segreto[..^1] + "0")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(ColCookie(HttpMethod.Get, "/", "sbagliato"))).StatusCode);
    }

    [Fact]
    public async Task IlSegretoNellIndirizzoDiventaUnCookieETogliSeStesso()
    {
        using var client = Client();
        var risposta = await client.GetAsync("/?k=" + Segreto);

        Assert.Equal(HttpStatusCode.Redirect, risposta.StatusCode);
        Assert.Equal("/", risposta.Headers.Location!.OriginalString);

        string cookie = Assert.Single(risposta.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith($"sectorlab={Segreto}", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IlRimandoTieneGliAltriParametri()
    {
        using var client = Client();
        var risposta = await client.GetAsync("/cartella?a=1&k=" + Segreto + "&b=2");
        Assert.Equal("/cartella?a=1&b=2", risposta.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task ColCookieLaPaginaArriva()
    {
        // 🔴 F0, trappola 1: senza UseAntiforgery questa risposta è un 500 muto.
        using var client = Client();
        var risposta = await client.SendAsync(ColCookie(HttpMethod.Get, "/"));

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        string pagina = await risposta.Content.ReadAsStringAsync();
        Assert.Contains("Aurora Sector Lab", pagina, StringComparison.Ordinal);
        Assert.Contains("_framework/blazor.web", pagina, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/_framework/blazor.web.js")]
    [InlineData("/_content/Vipi.SectorLab.Ui/sectorlab.js")]
    [InlineData("/_content/Vipi.SectorLab.Ui/sectorlab.css")]
    public async Task ColCookieGliAssetArrivano(string percorso)
    {
        // Senza blazor.web.js la pagina arriva lo stesso, ma il circuito non parte mai e nessuno lo dice (slice 1).
        using var client = Client();
        var risposta = await client.SendAsync(ColCookie(HttpMethod.Get, percorso));
        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
    }

    [Fact]
    public async Task IngressoPortaIlSegretoAllaPrimaPagina()
    {
        using var client = Client();
        var ingresso = ServerDelLab.Ingresso(_app);
        Assert.Equal(_base.Authority, ingresso.Authority);

        var risposta = await client.GetAsync(ingresso.PathAndQuery);
        Assert.Equal(HttpStatusCode.Redirect, risposta.StatusCode);
    }

    [Fact]
    public void LaRadiceDeiContenutiELaCartellaDellEseguibile()
    {
        // 🔴 F0, trappola 2: con la cartella di lavoro come radice, lanciato da un collegamento tutti gli statici a 404.
        var ambiente = _app.Services.GetRequiredService<IHostEnvironment>();
        Assert.Equal(AppContext.BaseDirectory, ambiente.ContentRootPath);
    }

    [Fact]
    public void IlCircuitoRiceveMessaggiGrossi()
    {
        // 🔴 F0, trappola 3: di base 32 KB, e oltre il circuito muore in silenzio. Un pezzo d'AIP incollato li supera.
        var hub = _app.Services.GetRequiredService<IOptions<HubOptions>>().Value;
        Assert.True(hub.MaximumReceiveMessageSize >= 1024 * 1024, $"tetto a {hub.MaximumReceiveMessageSize} byte");
    }

    [Fact]
    public void OgniAvvioHaIlSuoSegreto()
    {
        var uno = new SegretoDelLab().Valore;
        var due = new SegretoDelLab().Valore;
        Assert.Equal(64, uno.Length);
        Assert.NotEqual(uno, due);
    }
}
