using System.Net;
using Microsoft.Extensions.Options;
using Vipi.Infrastructure.Weather;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// <see cref="NoaaWeatherClient"/>: coalescenza delle richieste concorrenti sullo stesso ICAO (la lista aeroporti
/// di una ACC chiede il meteo di tutti gli scali in parallelo) e politica di TTL, dove un esito vuoto non deve
/// restare in cache per la finestra piena.
/// </summary>
public class NoaaWeatherClientTests
{
    /// <summary>Handler che conta le richieste e risponde con un corpo fisso per endpoint.</summary>
    private sealed class CountingHandler : HttpMessageHandler
    {
        private readonly string? _metarBody;
        private readonly string? _tafBody;
        private int _calls;
        public int Calls => Volatile.Read(ref _calls);
        public TaskCompletionSource Gate { get; } = new();
        public bool UseGate { get; init; }

        public CountingHandler(string? metarBody, string? tafBody)
        {
            _metarBody = metarBody;
            _tafBody = tafBody;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref _calls);
            if (UseGate) await Gate.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

            var isMetar = request.RequestUri!.AbsolutePath.Contains("metar", StringComparison.OrdinalIgnoreCase);
            var body = isMetar ? _metarBody : _tafBody;
            if (body is null) return new HttpResponseMessage(HttpStatusCode.NoContent);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class StubFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public StubFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) =>
            new(_handler, disposeHandler: false) { BaseAddress = new Uri("https://example.test") };
    }

    private static NoaaWeatherClient Build(HttpMessageHandler handler, WeatherOptions? opt = null) =>
        new(new StubFactory(handler), Options.Create(opt ?? new WeatherOptions { BaseUrl = "https://example.test" }));

    private const string MetarJson = """[{"rawOb":"LIRF 121250Z 22008KT CAVOK 24/12 Q1018"}]""";
    private const string TafJson = """[{"rawTAF":"TAF LIRF 121100Z 1212/1312 22010KT CAVOK"}]""";

    [Fact]
    public async Task Richieste_Concorrenti_Sullo_Stesso_Icao_Fanno_Una_Sola_Fetch()
    {
        var handler = new CountingHandler(MetarJson, TafJson) { UseGate = true };
        var client = Build(handler);

        var callers = Enumerable.Range(0, 16).Select(_ => client.GetAsync("LIRF")).ToArray();
        handler.Gate.SetResult();
        var results = await Task.WhenAll(callers);

        // 2 chiamate HTTP in tutto (un METAR + un TAF), non 2 per chiamante.
        Assert.Equal(2, handler.Calls);
        Assert.All(results, r => Assert.Contains("22008KT", r.Metar));
    }

    [Fact]
    public async Task Il_Secondo_Accesso_Usa_La_Cache()
    {
        var handler = new CountingHandler(MetarJson, TafJson);
        var client = Build(handler);

        await client.GetAsync("LIRF");
        var callsAfterFirst = handler.Calls;
        await client.GetAsync("LIRF");
        await client.GetAsync("lirf");   // normalizzazione: stesso ICAO

        Assert.Equal(2, callsAfterFirst);
        Assert.Equal(callsAfterFirst, handler.Calls);
    }

    [Fact]
    public async Task Icao_Diversi_Non_Vengono_Coalescenti_Insieme()
    {
        var handler = new CountingHandler(MetarJson, TafJson);
        var client = Build(handler);

        await Task.WhenAll(client.GetAsync("LIRF"), client.GetAsync("LIMC"));

        Assert.Equal(4, handler.Calls);   // 2 endpoint x 2 aeroporti
    }

    [Fact]
    public async Task Icao_Vuoto_Non_Chiama_La_Rete()
    {
        var handler = new CountingHandler(MetarJson, TafJson);
        var client = Build(handler);

        var report = await client.GetAsync("   ");

        Assert.Equal(0, handler.Calls);
        Assert.False(report.HasData);
    }

    [Fact]
    public async Task Nessun_Bollettino_Produce_Un_Report_Vuoto_Senza_Eccezioni()
    {
        var handler = new CountingHandler(null, null);   // NOAA risponde 204 su entrambi
        var client = Build(handler);

        var report = await client.GetAsync("LIRF");

        Assert.False(report.HasData);
        Assert.Equal("LIRF", report.Icao);
    }

    [Theory]
    [InlineData(true, 10)]
    [InlineData(false, 1)]
    public void Il_Ttl_Dell_Esito_Vuoto_E_Molto_Piu_Corto(bool hasData, int expectedMinutes)
    {
        var opt = new WeatherOptions { TtlMinutes = 10, EmptyTtlMinutes = 1 };

        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), opt.CacheTtlFor(hasData));
    }

    [Fact]
    public void Il_Ttl_Non_Scende_Sotto_Il_Minuto()
    {
        var opt = new WeatherOptions { TtlMinutes = 0, EmptyTtlMinutes = -5 };

        Assert.Equal(TimeSpan.FromMinutes(1), opt.CacheTtlFor(hasData: true));
        Assert.Equal(TimeSpan.FromMinutes(1), opt.CacheTtlFor(hasData: false));
    }
}
