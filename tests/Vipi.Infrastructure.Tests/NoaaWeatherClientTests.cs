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

    // ---- La sorgente di SCORTA per il METAR (8 settembre 2026) ---------------------------------------
    //
    // Chiesta dal committente: «ogni tanto NOAA fallisce e non dà il METAR».

    /// <summary>Una scorta che conta quante volte la si è chiamata: serve a provare che NON si chiama quando
    /// la principale ha risposto.</summary>
    private sealed class ScortaFinta : Vipi.Application.Abstractions.IMetarFallback
    {
        private readonly string? _metar;
        public int Chiamate { get; private set; }
        public ScortaFinta(string? metar) => _metar = metar;
        public string NomeFinto { get; init; } = "VATSIM";
        public string Nome => NomeFinto;
        public Task<string?> GetMetarAsync(string icao, CancellationToken ct = default)
        {
            Chiamate++;
            return Task.FromResult(_metar);
        }
    }

    /// <summary>Una scorta che NON risponde finché non la si annulla: è il solo modo di provare che il
    /// budget d'attesa è SUO e non della catena.</summary>
    private sealed class ScortaCheSiPianta : Vipi.Application.Abstractions.IMetarFallback
    {
        public int Chiamate { get; private set; }
        public string Nome => "PIANTATA";
        public async Task<string?> GetMetarAsync(string icao, CancellationToken ct = default)
        {
            Chiamate++;
            await Task.Delay(System.Threading.Timeout.Infinite, ct);
            return null;
        }
    }

    /// <summary>Una scorta che esplode: sono servizi di terzi, ognuno col suo modo di rompersi.</summary>
    private sealed class ScortaCheEsplode : Vipi.Application.Abstractions.IMetarFallback
    {
        public int Chiamate { get; private set; }
        public string Nome => "ESPLOSA";
        public Task<string?> GetMetarAsync(string icao, CancellationToken ct = default)
        {
            Chiamate++;
            throw new HttpRequestException("la scorta e' rotta");
        }
    }

    // ---- I due bollettini si chiedono INSIEME (8 settembre 2026) --------------------------------------
    //
    // 🔴 Fino a quel giorno erano due `await` in fila. Con NOAA che NON risponde — un host che ingoia i
    // pacchetti, non un DNS morto, che fallisce in 4 ms e non prova niente — diventavano DUE attese da
    // dieci secondi, e aprire un aeroporto costava VENTI SECONDI. Misurato a schermo, e segnalato dal
    // committente come «e' il meccanismo nuovo del METAR che rallenta»: non lo era.

    /// <summary>Handler che TIENE la richiesta finché non lo si lascia andare, e dice quante ne ha in mano
    /// nello stesso momento. È il solo modo di provare che due fetch viaggiano insieme e non in fila.</summary>
    private sealed class HandlerCheTrattiene : HttpMessageHandler
    {
        private int _dentro, _massimoInsieme;
        public TaskCompletionSource Apri { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int MassimoInsieme => Volatile.Read(ref _massimoInsieme);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            // ⚠️ Il `finally` NON è pulizia: senza, una richiesta ANNULLATA non scala il contatore e resta
            // dentro per sempre. Il banco allora vede «due insieme» anche quando le due sono in FILA — la
            // prima scaduta e mai uscita, la seconda entrata dopo — e passa qualunque cosa faccia il codice.
            // Preso provando il banco a rovescio: in sequenza passava lo stesso, cioè non provava niente.
            var ora = Interlocked.Increment(ref _dentro);
            int visto;
            do { visto = Volatile.Read(ref _massimoInsieme); }
            while (ora > visto && Interlocked.CompareExchange(ref _massimoInsieme, ora, visto) != visto);

            try
            {
                await Apri.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            finally { Interlocked.Decrement(ref _dentro); }
        }
    }

    [Fact]
    public async Task Il_Metar_E_Il_Taf_Si_Chiedono_INSIEME()
    {
        var handler = new HandlerCheTrattiene();
        var client = Build(handler);

        var lettura = client.GetAsync("LIRF");
        // Si aspetta che tutt'e due siano partite: se fossero in fila, la seconda non partirebbe mai finché
        // la prima non torna — e questo test scadrebbe invece di passare.
        var scadenza = DateTime.UtcNow.AddSeconds(10);
        while (handler.MassimoInsieme < 2 && DateTime.UtcNow < scadenza)
            await Task.Delay(20);

        handler.Apri.SetResult();
        await lettura;

        Assert.Equal(2, handler.MassimoInsieme);
    }

    /// <summary>⚠️ I due tetti d'attesa sono espliciti, e quello delle SCORTE è più corto: una scorta serve a
    /// coprire un buco in fretta, e una lenta quanto ciò che sostituisce fa aspettare due volte.</summary>
    [Fact]
    public void La_Scorta_Aspetta_Meno_Della_Principale()
    {
        var o = new WeatherOptions();

        Assert.True(o.FallbackTimeout < o.Timeout);
        Assert.True(o.Timeout < TimeSpan.FromSeconds(10));   // il tetto della HttpClient resta l'ultimo
        Assert.Equal(TimeSpan.FromSeconds(1), new WeatherOptions { TimeoutSeconds = 0, FallbackTimeoutSeconds = -3 }.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(1), new WeatherOptions { TimeoutSeconds = 0, FallbackTimeoutSeconds = -3 }.FallbackTimeout);
    }

    /// <summary>⚠️ Le scorte sono una CATENA ordinata, non una sola: <c>params</c> perché i banchi ne provano
    /// zero, una e due — e l'ordine è parte di quel che si presidia.</summary>
    private static NoaaWeatherClient Build(HttpMessageHandler handler,
        params Vipi.Application.Abstractions.IMetarFallback[] scorte) =>
        new(new StubFactory(handler), Options.Create(new WeatherOptions { BaseUrl = "https://example.test" }), scorte);

    /// <summary>Come <see cref="Build(HttpMessageHandler, Vipi.Application.Abstractions.IMetarFallback[])"/>,
    /// ma col tetto d'attesa delle scorte al minimo (1 s): i banchi che provano un'attesa devono costare
    /// un secondo, non tre.</summary>
    private static NoaaWeatherClient BuildScorteImpazienti(HttpMessageHandler handler,
        params Vipi.Application.Abstractions.IMetarFallback[] scorte) =>
        new(new StubFactory(handler),
            Options.Create(new WeatherOptions { BaseUrl = "https://example.test", FallbackTimeoutSeconds = 1 }),
            scorte);

    [Fact]
    public async Task Se_NOAA_Non_Da_Il_Metar_Lo_Chiede_Alla_Scorta()
    {
        var scorta = new ScortaFinta("LIRF 081720Z 28004KT 9999 SCT015 27/24 Q1014 NOSIG");
        var client = Build(new CountingHandler(null, TafJson), scorta);   // METAR 204, TAF c'è

        var report = await client.GetAsync("LIRF");

        Assert.Equal("LIRF 081720Z 28004KT 9999 SCT015 27/24 Q1014 NOSIG", report.Metar);
        Assert.Equal(1, scorta.Chiamate);
        // ⚠️ La provenienza si scrive: senza, una scorta morta e una scorta mai servita si vedono uguali.
        Assert.Equal("VATSIM", report.MetarSource);
        // Il TAF resta quello di NOAA: sono due bollettini indipendenti.
        Assert.Contains("TAF LIRF", report.Taf);
    }

    /// <summary>⚠️ La scorta NON si chiede quando la principale ha risposto: è una sorgente di terzi, e
    /// interrogarla a ogni giro sarebbe traffico su un servizio che non è nostro.</summary>
    [Fact]
    public async Task Se_NOAA_Da_Il_Metar_La_Scorta_Non_Si_Disturba()
    {
        var scorta = new ScortaFinta("NON DEVE ARRIVARE");
        var client = Build(new CountingHandler(MetarJson, TafJson), scorta);

        var report = await client.GetAsync("LIRF");

        Assert.Equal(0, scorta.Chiamate);
        Assert.Contains("LIRF 121250Z", report.Metar);
        // Sorgente principale ⇒ nessuna etichetta: una che c'è sempre non si legge più.
        Assert.Null(report.MetarSource);
    }

    /// <summary>Anche la scorta può non avere il bollettino (un ICAO che nessuno serve): resta un report senza
    /// METAR, non un guasto.</summary>
    [Fact]
    public async Task Se_Nemmeno_La_Scorta_Ce_L_Ha_Non_Succede_Niente_Di_Brutto()
    {
        var scorta = new ScortaFinta(null);
        var client = Build(new CountingHandler(null, null), scorta);

        var report = await client.GetAsync("ZZZZ");

        Assert.False(report.HasData);
        Assert.Null(report.MetarSource);
        Assert.Equal(1, scorta.Chiamate);
    }

    /// <summary>⚠️ Senza scorta registrata il comportamento è ESATTAMENTE quello di prima: la ricaduta è
    /// un'aggiunta, non una riscrittura.</summary>
    [Fact]
    public async Task Senza_Scorta_Tutto_Come_Prima()
    {
        var client = Build(new CountingHandler(null, TafJson));

        var report = await client.GetAsync("LIRF");

        Assert.Null(report.Metar);
        Assert.Null(report.MetarSource);
        Assert.Contains("TAF LIRF", report.Taf);
    }

    /// <summary>
    /// ⚠️ <b>L'ORDINE della catena è una decisione, non un dettaglio di registrazione</b>: prima IVAO —
    /// è la rete che questi documenti servono — poi VATSIM. Se la prima risponde, la seconda non si
    /// disturba: sono servizi di altri.
    /// </summary>
    [Fact]
    public async Task La_Catena_Si_Ferma_Alla_Prima_Che_Risponde()
    {
        var ivao = new ScortaFinta("LIRF 081720Z 28004KT CAVOK 27/24 Q1014") { NomeFinto = "IVAO" };
        var vatsim = new ScortaFinta("NON DEVE ARRIVARE") { NomeFinto = "VATSIM" };
        var client = Build(new CountingHandler(null, TafJson), ivao, vatsim);

        var report = await client.GetAsync("LIRF");

        Assert.Equal("IVAO", report.MetarSource);
        Assert.Equal(1, ivao.Chiamate);
        Assert.Equal(0, vatsim.Chiamate);
    }

    /// <summary>
    /// 🔴 Il budget d'attesa è di OGNI scorta, non della catena. Fino al 12 settembre 2026 era uno solo per
    /// tutte: la prima che si piantava se lo portava via intero e la seconda — che il METAR ce l'aveva — non
    /// veniva nemmeno chiamata. Visto guidando l'app con NOAA e IVAO su una porta morta: la pagina diceva
    /// «METAR non disponibile» mentre la terza sorgente rispondeva benissimo.
    /// </summary>
    [Fact]
    public async Task Una_Scorta_Piantata_Non_Si_Porta_Via_Le_Altre()
    {
        var piantata = new ScortaCheSiPianta();
        var vatsim = new ScortaFinta("LIRF 081720Z 28004KT CAVOK 27/24 Q1014") { NomeFinto = "VATSIM" };
        var client = BuildScorteImpazienti(new CountingHandler(null, TafJson), piantata, vatsim);

        var report = await client.GetAsync("LIRF");

        Assert.Equal("VATSIM", report.MetarSource);
        Assert.Equal(1, piantata.Chiamate);
        Assert.Equal(1, vatsim.Chiamate);
    }

    /// <summary>⚠️ E nemmeno una che esplode: da qui l'eccezione uscirebbe fino alla pagina, che perderebbe
    /// anche il TAF — già arrivato, e che con la scorta non c'entra niente.</summary>
    [Fact]
    public async Task Una_Scorta_Che_Esplode_Non_Si_Porta_Via_Le_Altre()
    {
        var esplosa = new ScortaCheEsplode();
        var vatsim = new ScortaFinta("LIRF 081720Z 28004KT CAVOK 27/24 Q1014") { NomeFinto = "VATSIM" };
        var client = Build(new CountingHandler(null, TafJson), esplosa, vatsim);

        var report = await client.GetAsync("LIRF");

        Assert.Equal("VATSIM", report.MetarSource);
        Assert.NotNull(report.Taf);
        Assert.Equal(1, esplosa.Chiamate);
        Assert.Equal(1, vatsim.Chiamate);
    }

    /// <summary>E se la prima non ce l'ha, si passa alla seconda: è tutto il senso della terza gamba.</summary>
    [Fact]
    public async Task Se_La_Prima_Scorta_Tace_Risponde_La_Seconda()
    {
        var ivao = new ScortaFinta(null) { NomeFinto = "IVAO" };
        var vatsim = new ScortaFinta("LIRF 081720Z 28004KT CAVOK 27/24 Q1014") { NomeFinto = "VATSIM" };
        var client = Build(new CountingHandler(null, TafJson), ivao, vatsim);

        var report = await client.GetAsync("LIRF");

        Assert.Equal("VATSIM", report.MetarSource);
        Assert.Equal(1, ivao.Chiamate);
        Assert.Equal(1, vatsim.Chiamate);
    }

    /// <summary>
    /// ⚠️ La scorta sta DENTRO la fetch, quindi eredita la cache: due letture ravvicinate la chiamano una
    /// volta sola. Se stesse in un decoratore attorno a <c>GetAsync</c> la chiamerebbe a ogni render di ogni
    /// pagina, su un servizio che non è nostro.
    /// </summary>
    [Fact]
    public async Task La_Scorta_Passa_Dalla_Cache_Come_Tutto_Il_Resto()
    {
        var scorta = new ScortaFinta("LIRF 081720Z 28004KT CAVOK 27/24 Q1014");
        var client = Build(new CountingHandler(null, TafJson), scorta);

        await client.GetAsync("LIRF");
        await client.GetAsync("LIRF");

        Assert.Equal(1, scorta.Chiamate);
    }
}
