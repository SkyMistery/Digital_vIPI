using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Weather;

/// <summary>
/// Adapter METAR/TAF reale verso NOAA aviationweather.gov (API pubblica, senza chiave). Singleton con cache
/// per ICAO a TTL: il METAR aggiorna ~oraria, quindi non interroga a ogni render. In errore ritorna l'ultimo
/// valore in cache (anche scaduto) o un report vuoto: la UI mostra l'empty-state "non disponibile".
/// </summary>
public sealed class NoaaWeatherClient : IWeatherProvider
{
    public const string HttpClientName = "weather";

    private readonly IHttpClientFactory _factory;
    private readonly WeatherOptions _opt;
    private readonly ConcurrentDictionary<string, (WeatherReport Report, DateTimeOffset Expiry)> _cache = new();

    public NoaaWeatherClient(IHttpClientFactory factory, IOptions<WeatherOptions> opt)
    {
        _factory = factory;
        _opt = opt.Value;
    }

    public async Task<WeatherReport> GetAsync(string icao, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(icao)) return WeatherReport.Empty(icao ?? "");
        icao = icao.Trim().ToUpperInvariant();

        var now = DateTimeOffset.UtcNow;
        if (_cache.TryGetValue(icao, out var hit) && now < hit.Expiry)
            return hit.Report;

        var http = _factory.CreateClient(HttpClientName);
        // METAR e TAF sono bollettini indipendenti: uno mancante (es. NOAA risponde 204 No Content sul
        // METAR ma ha il TAF) non deve scartare l'altro. Ogni fetch tollera errore/empty tornando null.
        var metar = await TryFetchAsync(() => FetchMetarAsync(http, icao, ct));
        var taf = await TryFetchAsync(() => FetchTafAsync(http, icao, ct));

        // Entrambi assenti: probabile servizio irraggiungibile → ricade sull'ultimo valore noto (anche scaduto).
        if (metar is null && taf is null && _cache.TryGetValue(icao, out var stale))
            return stale.Report;

        var report = new WeatherReport(icao, metar, taf, now);
        _cache[icao] = (report, now.AddMinutes(Math.Max(1, _opt.TtlMinutes)));
        return report;
    }

    // Un singolo fetch: errore di trasporto o body vuoto (204 → JsonException) diventano "nessun dato" (null),
    // senza propagare l'eccezione all'altro bollettino.
    private static async Task<string?> TryFetchAsync(Func<Task<string?>> fetch)
    {
        try { return await fetch(); }
        catch { return null; }
    }

    private async Task<string?> FetchMetarAsync(HttpClient http, string icao, CancellationToken ct)
    {
        var url = $"{_opt.BaseUrl.TrimEnd('/')}/api/data/metar?ids={icao}&format=json";
        var rows = await http.GetFromJsonAsync<List<MetarDto>>(url, ct);
        return rows?.FirstOrDefault()?.RawOb;
    }

    private async Task<string?> FetchTafAsync(HttpClient http, string icao, CancellationToken ct)
    {
        var url = $"{_opt.BaseUrl.TrimEnd('/')}/api/data/taf?ids={icao}&format=json";
        var rows = await http.GetFromJsonAsync<List<TafDto>>(url, ct);
        return rows?.FirstOrDefault()?.RawTaf;
    }

    // DTO permissivi: ci interessa solo il testo grezzo.
    private sealed record MetarDto([property: JsonPropertyName("rawOb")] string? RawOb);
    private sealed record TafDto([property: JsonPropertyName("rawTAF")] string? RawTaf);
}
