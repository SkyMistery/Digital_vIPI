using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Vipi.AuroraBridge.Contracts;

namespace Vipi.AuroraBridge.Core;

/// <summary>Risposta del sito, con l'indicazione se arriva dalla rete o dalla cache locale.</summary>
public sealed record ResolveOutcome(TransferResolveResponse? Response, bool FromCache, string? Error)
{
    public bool Ok => Response is not null;
}

/// <summary>Parametri del client verso il sito.</summary>
public sealed record VipiApiOptions(
    string BaseAddress = "https://it.ivao.aero",
    string Path = "/vsop/api/v1/transfers/resolve",
    int TimeoutMs = 5000,
    string? CacheDirectory = null);

/// <summary>
/// Client dell'endpoint di risoluzione. Ogni risposta buona viene messa in cache su disco con la chiave del
/// contesto: se il sito non risponde in sessione, il tool continua a proporre l'ultima risposta valida per
/// quel contesto, dichiarandola vecchia. Meglio un dato datato e dichiarato che nessun dato.
/// </summary>
public sealed class VipiApiClient : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly VipiApiOptions _options;
    private readonly string _cacheDir;
    private readonly bool _ownsHttp;

    public VipiApiClient(VipiApiOptions? options = null, HttpClient? http = null)
    {
        _options = options ?? new VipiApiOptions();
        _ownsHttp = http is null;
        _http = http ?? new HttpClient();
        _http.BaseAddress ??= new Uri(_options.BaseAddress);
        _http.Timeout = TimeSpan.FromMilliseconds(_options.TimeoutMs);

        _cacheDir = _options.CacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VipiAuroraBridge", "cache");
    }

    public async Task<ResolveOutcome> ResolveAsync(TransferResolveRequest request, CancellationToken ct = default)
    {
        var key = CacheKey(request);
        try
        {
            using var response = await _http.PostAsJsonAsync(_options.Path, request, Json, ct).ConfigureAwait(false);

            if ((int)response.StatusCode == 429)
                return new ResolveOutcome(await ReadCacheAsync(key, ct).ConfigureAwait(false), true,
                    "Il sito sta limitando le richieste (429): rallenta il polling.");

            if (!response.IsSuccessStatusCode)
                return new ResolveOutcome(await ReadCacheAsync(key, ct).ConfigureAwait(false), true,
                    $"Il sito ha risposto {(int)response.StatusCode}.");

            var payload = await response.Content.ReadFromJsonAsync<TransferResolveResponse>(Json, ct).ConfigureAwait(false);
            if (payload is null)
                return new ResolveOutcome(await ReadCacheAsync(key, ct).ConfigureAwait(false), true, "Risposta illeggibile.");

            await WriteCacheAsync(key, payload, ct).ConfigureAwait(false);
            return new ResolveOutcome(payload, false, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            var cached = await ReadCacheAsync(key, ct).ConfigureAwait(false);
            return new ResolveOutcome(cached, true, cached is null
                ? $"Sito irraggiungibile e nessun dato in cache: {ex.Message}"
                : "Sito irraggiungibile: sto mostrando l'ultima risposta valida.");
        }
    }

    /// <summary>Chiave della cache: il contesto che DAVVERO cambia la risposta. La quota corrente e il rateo
    /// non ci entrano, altrimenti ogni giro di polling produrrebbe una voce nuova e la cache non servirebbe a nulla.</summary>
    private static string CacheKey(TransferResolveRequest r)
    {
        var seed = string.Join("|",
            r.OwnerCallsign, r.Departure, r.Arrival, r.CruiseLevel, r.NextStation,
            string.Join(",", r.RouteFixes.Select(f => f.Fix)));

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed.ToUpperInvariant()));
        return Convert.ToHexString(hash, 0, 12);
    }

    private async Task<TransferResolveResponse?> ReadCacheAsync(string key, CancellationToken ct)
    {
        try
        {
            var file = Path.Combine(_cacheDir, key + ".json");
            if (!File.Exists(file)) return null;

            await using var stream = File.OpenRead(file);
            return await JsonSerializer.DeserializeAsync<TransferResolveResponse>(stream, Json, ct).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return null;   // cache corrotta o illeggibile: vale come cache assente
        }
    }

    private async Task WriteCacheAsync(string key, TransferResolveResponse payload, CancellationToken ct)
    {
        try
        {
            Directory.CreateDirectory(_cacheDir);
            var file = Path.Combine(_cacheDir, key + ".json");
            await using var stream = File.Create(file);
            await JsonSerializer.SerializeAsync(stream, payload, Json, ct).ConfigureAwait(false);
        }
        catch (Exception) { /* niente cache è meglio che un crash in sessione */ }
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }
}
