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

    // Le sorgenti di scorta per il solo METAR, NELL'ORDINE in cui si provano: IVAO, poi VATSIM. Vuota =
    // nessuna scorta, e il comportamento torna quello di prima.
    // ⚠️ L'ordine è quello di registrazione nel contenitore, ed è una decisione: IVAO prima perché è la rete
    // che questi documenti servono — se le due sorgenti divergessero, quella giusta per noi è la sua.
    private readonly IReadOnlyList<IMetarFallback> _scorte;

    public NoaaWeatherClient(IHttpClientFactory factory, IOptions<WeatherOptions> opt,
        IEnumerable<IMetarFallback>? scorte = null)
    {
        _factory = factory;
        _opt = opt.Value;
        _scorte = scorte?.ToList() ?? (IReadOnlyList<IMetarFallback>)Array.Empty<IMetarFallback>();
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
            //
            // 🔴 E si chiedono INSIEME, non uno dopo l'altro. Fino all'8 settembre 2026 erano due `await` in
            // fila: con NOAA che non risponde diventavano DUE attese da dieci secondi, e aprire un aeroporto
            // costava VENTI SECONDI — misurato, non stimato, con un host che ingoia i pacchetti (un DNS
            // morto non prova niente: fallisce in 4 ms). Sono indipendenti per definizione, e aspettarli in
            // fila non serviva a nessuno.
            // ⚠️ Il segnale e' arrivato come «e' il meccanismo nuovo del METAR che rallenta»: non lo era —
            // la ricaduta costa qualche decimo. Era questa riga, e c'era da prima.
            using var attesa = new CancellationTokenSource(_opt.Timeout);
            var metarTask = TryFetchAsync(() => FetchMetarAsync(http, icao, attesa.Token));
            var tafTask = TryFetchAsync(() => FetchTafAsync(http, icao, attesa.Token));
            await Task.WhenAll(metarTask, tafTask).ConfigureAwait(false);
            var metar = metarTask.Result;
            var taf = tafTask.Result;

            // ⚠️ La SCORTA si chiede solo per il METAR mancante, e solo dopo che la principale ha risposto
            // (o ha taciuto). Segnalato dal committente l'8 settembre 2026: «ogni tanto NOAA fallisce e non
            // dà il METAR». Sta QUI dentro, e non in un decoratore attorno a `GetAsync`, perché così eredita
            // la cache per ICAO, la deduplica delle richieste in volo e la ricaduta sull'ultimo valore noto:
            // un decoratore fuori le vedrebbe tutte già risolte e finirebbe per chiedere la scorta a ogni
            // render di ogni pagina.
            // ⚠️ Il TAF non ha scorta, e non è una dimenticanza: un TAF gratuito e indipendente da NOAA non
            // esiste (provato). Meglio un METAR senza il suo TAF che nessuno dei due.
            // ⚠️ Si prova UNA scorta per volta e ci si ferma alla prima che risponde: sono servizi di altri, e
            // interrogarli tutti quando il primo ha già dato il METAR sarebbe traffico regalato.
            // ⚠️ Le scorte hanno un budget LORO, piu' corto, e vale per TUTTA la catena messa insieme: una
            // scorta serve a coprire un buco in fretta, e una scorta lenta quanto cio' che sostituisce fa
            // aspettare la pagina due volte invece di salvarla.
            string? sorgente = null;
            if (metar is null && _scorte.Count > 0)
            {
                using var budget = new CancellationTokenSource(_opt.FallbackTimeout);
                foreach (var scorta in _scorte)
                {
                    if (metar is not null || budget.IsCancellationRequested) break;
                    metar = await scorta.GetMetarAsync(icao, budget.Token).ConfigureAwait(false);
                    if (metar is not null) sorgente = scorta.Nome;
                }
            }

            // Entrambi assenti: probabile servizio irraggiungibile → ricade sull'ultimo valore noto (anche scaduto).
            if (metar is null && taf is null && _cache.TryGetValue(icao, out var stale))
                return stale.Report;

            var report = new WeatherReport(icao, metar, taf, now, sorgente);
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
