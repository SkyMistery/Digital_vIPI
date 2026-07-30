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
/// Le richieste concorrenti sullo stesso ICAO condividono una sola chiamata di rete.
/// </summary>
public sealed class NoaaWeatherClient : IWeatherProvider
{
    public const string HttpClientName = "weather";

    private readonly IHttpClientFactory _factory;
    private readonly WeatherOptions _opt;
    private readonly ConcurrentDictionary<string, (WeatherReport Report, DateTimeOffset Expiry)> _cache = new();

    // Fetch in volo per ICAO. Serve perché la lista aeroporti di una ACC chiede il meteo di tutti gli scali in
    // parallelo (Task.WhenAll) e ogni utente ripete: a cache fredda erano N chiamate identiche in volo su un'API
    // pubblica con rate limit. Lazy con ExecutionAndPublication garantisce che parta UN solo task, non che i
    // perdenti ne avvino uno da scartare (come farebbe la factory di GetOrAdd, che può girare più volte).
    private readonly ConcurrentDictionary<string, Lazy<Task<WeatherReport>>> _inFlight = new();

    public NoaaWeatherClient(IHttpClientFactory factory, IOptions<WeatherOptions> opt)
    {
        _factory = factory;
        _opt = opt.Value;
    }

    public Task<WeatherReport> GetAsync(string icao, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(icao)) return Task.FromResult(WeatherReport.Empty(icao ?? ""));
        icao = icao.Trim().ToUpperInvariant();

        if (_cache.TryGetValue(icao, out var hit) && DateTimeOffset.UtcNow < hit.Expiry)
            return Task.FromResult(hit.Report);

        var shared = _inFlight.GetOrAdd(icao, key => new Lazy<Task<WeatherReport>>(
            () => FetchAndStoreAsync(key), LazyThreadSafetyMode.ExecutionAndPublication)).Value;

        // WaitAsync: il chiamante che annulla smette di attendere senza abortire la fetch condivisa, che serve
        // anche agli altri. La durata resta comunque limitata dal timeout dell'HttpClient (10s).
        return shared.WaitAsync(ct);
    }

    private async Task<WeatherReport> FetchAndStoreAsync(string icao)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var http = _factory.CreateClient(HttpClientName);
            // METAR e TAF sono bollettini indipendenti: uno mancante (es. NOAA risponde 204 No Content sul
            // METAR ma ha il TAF) non deve scartare l'altro. Ogni fetch tollera errore/empty tornando null.
            var metar = await TryFetchAsync(() => FetchMetarAsync(http, icao));
            var taf = await TryFetchAsync(() => FetchTafAsync(http, icao));

            // Entrambi assenti: probabile servizio irraggiungibile → ricade sull'ultimo valore noto (anche scaduto).
            if (metar is null && taf is null && _cache.TryGetValue(icao, out var stale))
                return stale.Report;

            var report = new WeatherReport(icao, metar, taf, now);
            // TTL breve sull'esito vuoto: col TTL pieno un blip di pochi secondi di NOAA azzererebbe il meteo
            // dell'aeroporto per tutta la finestra normale, senza modo di riprovare prima.
            _cache[icao] = (report, now.Add(_opt.CacheTtlFor(report.HasData)));
            return report;
        }
        finally
        {
            // Il task si sfila da sé: non dipende da quale chiamante sopravvive all'attesa (se tutti annullassero,
            // una rimozione lato chiamante non avverrebbe mai e la voce completata resterebbe appesa).
            _inFlight.TryRemove(icao, out _);
        }
    }

    // Un singolo fetch: errore di trasporto o body vuoto (204 → JsonException) diventano "nessun dato" (null),
    // senza propagare l'eccezione all'altro bollettino.
    private static async Task<string?> TryFetchAsync(Func<Task<string?>> fetch)
    {
        try { return await fetch(); }
        catch { return null; }
    }

    private async Task<string?> FetchMetarAsync(HttpClient http, string icao)
    {
        var url = $"{_opt.BaseUrl.TrimEnd('/')}/api/data/metar?ids={icao}&format=json";
        var rows = await http.GetFromJsonAsync<List<MetarDto>>(url);
        return rows?.FirstOrDefault()?.RawOb;
    }

    private async Task<string?> FetchTafAsync(HttpClient http, string icao)
    {
        var url = $"{_opt.BaseUrl.TrimEnd('/')}/api/data/taf?ids={icao}&format=json";
        var rows = await http.GetFromJsonAsync<List<TafDto>>(url);
        return rows?.FirstOrDefault()?.RawTaf;
    }

    // DTO permissivi: ci interessa solo il testo grezzo.
    private sealed record MetarDto([property: JsonPropertyName("rawOb")] string? RawOb);
    private sealed record TafDto([property: JsonPropertyName("rawTAF")] string? RawTaf);
}
