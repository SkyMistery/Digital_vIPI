using System.Net;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Retry leggero per blip transitori verso l'API IVAO: ritenta su errori di rete e 5xx/429 con backoff
/// breve. Nessuna dipendenza esterna. Il polling tollera comunque i fallimenti (mantiene l'ultima cache),
/// questo riduce solo i buchi su singoli scatti.
///
/// <para>⚠️ Un <b>timeout non si ritenta</b>, e non per scelta: <c>HttpClient.Timeout</c> annulla il gettone che
/// arriva qui, e tutti i tentativi dividono quello stesso tetto. Il ramo «timeout: ritenta» che c'era non poteva
/// scattare (T-069, revisione del 13 settembre 2026); il giro successivo del poller fa da ritentativo.</para>
/// </summary>
internal sealed class TransientRetryHandler : DelegatingHandler
{
    private const int MaxAttempts = 3;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        HttpResponseMessage? response = null;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                response = await base.SendAsync(request, ct);
                if (attempt >= MaxAttempts || !IsTransient(response.StatusCode)) return response;
            }
            catch (HttpRequestException) when (attempt < MaxAttempts) { /* ritenta */ }

            response?.Dispose();
            await Task.Delay(BackoffFor(attempt), ct);
        }
    }

    /// <summary>Backoff esponenziale (250ms·2^(n-1): 250/500/1000…) con jitter ±50% per evitare retry sincronizzati
    /// (thundering herd) quando più chiamate incappano nello stesso 429/5xx.</summary>
    private static TimeSpan BackoffFor(int attempt)
    {
        var baseMs = 250d * Math.Pow(2, attempt - 1);
        var jitter = Random.Shared.NextDouble();           // [0,1) → fattore in [0.5, 1.5)
        return TimeSpan.FromMilliseconds(baseMs * (0.5 + jitter));
    }

    private static bool IsTransient(HttpStatusCode code) =>
        (int)code >= 500 || code == HttpStatusCode.RequestTimeout || code == HttpStatusCode.TooManyRequests;
}
