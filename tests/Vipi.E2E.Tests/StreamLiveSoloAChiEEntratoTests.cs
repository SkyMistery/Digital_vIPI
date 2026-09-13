using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Vipi.Application.Abstractions;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// Lo stream SSE <c>/vsop/live/atc</c> lo apre solo chi è entrato.
///
/// <para>🔴 <b>Perché (T-021, revisione del 13 settembre 2026).</b> L'endpoint era pubblico con un tetto solo
/// <b>globale</b> di 300 connessioni: uno script che ne apriva 300 e le teneva vive col ping ogni 25 secondi
/// lasciava tutti i gettoni «live» della divisione a 503. Un tetto per IP non basta dietro Cloudflare, dove
/// molti controllori arrivano dallo stesso indirizzo (T-020). Ma dal §CZ (1.25.1) un anonimo lo stream non lo
/// apre più: il gettone in barra per lui è spento. Quindi la porta si chiude a chi non è entrato, e a un
/// anonimo non costa niente.</para>
/// </summary>
public sealed class StreamLiveSoloAChiEEntratoTests : IClassFixture<SmokeTests.VipiAppFactory>
{
    private readonly SmokeTests.VipiAppFactory _factory;
    public StreamLiveSoloAChiEEntratoTests(SmokeTests.VipiAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Un_anonimo_non_apre_lo_stream()
    {
        using var anonimo = _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddScoped<ICurrentUserProvider, Nessuno>()));
        using var client = anonimo.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        using var resp = await client.GetAsync("/vsop/live/atc", HttpCompletionOption.ResponseHeadersRead, cts.Token);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Chi_e_entrato_lo_apre()
    {
        using var client = _factory.CreateClient();   // identità di sviluppo
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        using var resp = await client.GetAsync("/vsop/live/atc", HttpCompletionOption.ResponseHeadersRead, cts.Token);

        Assert.Equal(System.Net.HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("text/event-stream", resp.Content.Headers.ContentType?.MediaType);
    }

    private sealed class Nessuno : ICurrentUserProvider
    {
        public CurrentUser? Get() => null;
    }
}
