using System.Net;
using Vipi.Infrastructure.Ivao;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// T-069 (revisione del 13 settembre 2026): il retry dei blip transitori verso IVAO. Il tetto di
/// <c>HttpClient.Timeout</c> annulla il gettone che arriva all'handler, quindi un timeout NON si ritenta —
/// e il ramo che diceva di farlo non poteva scattare. Questi test fissano il comportamento vero.
/// </summary>
public class TransientRetryHandlerTests
{
    /// <summary>Handler finale finto: conta le chiamate e risponde con la sequenza data (o resta appeso).</summary>
    private sealed class Sorgente(params Func<CancellationToken, Task<HttpResponseMessage>>[] risposte) : HttpMessageHandler
    {
        public int Chiamate { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => risposte[Math.Min(Chiamate++, risposte.Length - 1)](ct);
    }

    private static HttpClient Client(Sorgente sorgente, TimeSpan timeout) =>
        new(new TransientRetryHandler { InnerHandler = sorgente }) { Timeout = timeout, BaseAddress = new Uri("http://ivao.test") };

    [Fact]
    public async Task Un_5xx_si_ritenta_e_il_secondo_tentativo_risponde()
    {
        var sorgente = new Sorgente(
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)),
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var client = Client(sorgente, TimeSpan.FromSeconds(10));

        using var risposta = await client.GetAsync("/v2/tracker/whazzup");

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
        Assert.Equal(2, sorgente.Chiamate);
    }

    [Fact]
    public async Task Un_errore_di_rete_si_ritenta_fino_al_terzo_tentativo()
    {
        var sorgente = new Sorgente(_ => throw new HttpRequestException("connessione rifiutata"));
        using var client = Client(sorgente, TimeSpan.FromSeconds(10));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("/v2/tracker/whazzup"));

        Assert.Equal(3, sorgente.Chiamate);
    }

    [Fact]
    public async Task Un_timeout_del_client_non_si_ritenta()
    {
        var sorgente = new Sorgente(async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var client = Client(sorgente, TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAsync<TaskCanceledException>(() => client.GetAsync("/v2/tracker/whazzup"));

        Assert.Equal(1, sorgente.Chiamate);
    }
}
