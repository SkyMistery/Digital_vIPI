using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Ottiene e mette in cache l'access token IVAO (flusso client_credentials, app-to-app). PIANO §7.3.
/// Token a scadenza breve, rinnovato con margine. MAI esposto al browser. Thread-safe.
/// </summary>
public sealed class IvaoTokenProvider
{
    public const string HttpClientName = "ivao";

    private readonly IHttpClientFactory _factory;
    private readonly IvaoOptions _opt;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _token;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public IvaoTokenProvider(IHttpClientFactory factory, IOptions<IvaoOptions> opt)
    {
        _factory = factory;
        _opt = opt.Value;
    }

    /// <summary>True se sono configurate credenziali (altrimenti l'endpoint tracker è pubblico).</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_opt.ClientId);

    /// <summary>Token valido (dalla cache o rinnovato). Null se non configurato.</summary>
    public async Task<string?> GetTokenAsync(CancellationToken ct = default)
    {
        if (!IsConfigured) return null;
        if (_token is not null && DateTimeOffset.UtcNow < _expiresAt) return _token;

        await _gate.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _expiresAt) return _token;

            using var req = new HttpRequestMessage(HttpMethod.Post, _opt.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = _opt.ClientId,
                    ["client_secret"] = _opt.ClientSecret,
                    ["scope"] = _opt.Scopes,
                }),
            };
            var http = _factory.CreateClient(HttpClientName);
            using var res = await http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                // EnsureSuccessStatusCode() butta via il body: IVAO ci mette il motivo (invalid_client,
                // scope non concesso, grant non abilitato…). Lo includo nel messaggio per diagnosticare
                // i 400 sul token app. Body troncato: evita di riversare risposte enormi nei log.
                var err = await res.Content.ReadAsStringAsync(ct);
                if (err.Length > 500) err = err[..500] + "…";
                throw new HttpRequestException(
                    $"Token IVAO fallito: HTTP {(int)res.StatusCode} {res.ReasonPhrase}. " +
                    $"Body: {(string.IsNullOrWhiteSpace(err) ? "(vuoto)" : err)}");
            }
            var body = await res.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct)
                       ?? throw new InvalidOperationException("Risposta token IVAO vuota.");

            _token = body.AccessToken;
            // Rinnova 120s prima della scadenza dichiarata: il margine assorbe lo skew d'orologio tra questo host
            // e IVAO (VM/NTP drift) evitando di presentare un token già scaduto lato server (→ 401 a cascata finché
            // la cache locale non scade). Clamp a metà durata per token a vita brevissima.
            var marginSec = Math.Min(120, Math.Max(30, body.ExpiresIn / 2));
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, body.ExpiresIn - marginSec));
            return _token;
        }
        finally
        {
            _gate.Release();
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
