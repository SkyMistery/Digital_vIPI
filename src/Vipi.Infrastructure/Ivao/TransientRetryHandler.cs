using System.Net;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Retry leggero per blip transitori verso l'API IVAO: ritenta su errori di rete e 5xx/429 con backoff
/// breve. Nessuna dipendenza esterna. Il polling tollera comunque i fallimenti (mantiene l'ultima cache),
/// questo riduce solo i buchi su singoli scatti.
/// </summary>
public sealed class TransientRetryHandler : DelegatingHandler
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
            catch (TaskCanceledException) when (attempt < MaxAttempts && !ct.IsCancellationRequested) { /* timeout: ritenta */ }

            response?.Dispose();
            await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), ct);
        }
    }

    private static bool IsTransient(HttpStatusCode code) =>
        (int)code >= 500 || code == HttpStatusCode.RequestTimeout || code == HttpStatusCode.TooManyRequests;
}
