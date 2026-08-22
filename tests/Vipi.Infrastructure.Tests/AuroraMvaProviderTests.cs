using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vipi.Infrastructure.Sectorfile;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// <see cref="AuroraMvaProvider"/>: schema dei percorsi (le due famiglie di file MRVA), 404 come esito normale e
/// cache di processo per chiave — la carta di un ente viene chiesta a ogni apertura del documento.
/// </summary>
public class AuroraMvaProviderTests
{
    private const string Sample = """
        L;110;N044.13.15.000;E010.53.34.000;110;7;
        T;110;N044.19.36.000;E010.47.48.000;
        T;110;N044.13.47.000;E010.59.59.000;
        T;110;N044.19.36.000;E010.47.48.000;
        """;

    /// <summary>Handler che registra i percorsi richiesti e risponde 404 a quelli non previsti.</summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly IReadOnlyDictionary<string, string> _bodies;
        public List<string> Paths { get; } = new();

        public RecordingHandler(IReadOnlyDictionary<string, string> bodies) => _bodies = bodies;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath.TrimStart('/');
            lock (Paths) Paths.Add(path);
            return Task.FromResult(_bodies.TryGetValue(path, out var body)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private static AuroraMvaProvider Build(RecordingHandler handler, SectorfileCache cache, string baseUrl = "https://example.test/") =>
        new(new HttpClient(handler, disposeHandler: false),
            Options.Create(new SectorfileOptions { RawBaseUrl = baseUrl }),
            cache,
            NullLogger<AuroraMvaProvider>.Instance);

    [Fact]
    public async Task Acc_Chart_Comes_From_The_EnrmvaFolder()
    {
        var handler = new RecordingHandler(new Dictionary<string, string> { ["ENRMVA/lirr.mva"] = Sample });
        var chart = await Build(handler, new SectorfileCache()).GetAccChartAsync("LIRR");

        Assert.Equal("ENRMVA/lirr.mva", Assert.Single(handler.Paths));
        Assert.Single(chart.Shapes);
        Assert.Single(chart.Labels);
    }

    [Fact]
    public async Task Airport_Chart_Comes_From_The_Root_In_Lowercase()
    {
        // Il repo tiene i file per-aeroporto in minuscolo nella root e i raw di GitHub sono case-sensitive.
        var handler = new RecordingHandler(new Dictionary<string, string> { ["lirn.mva"] = Sample });
        var chart = await Build(handler, new SectorfileCache()).GetAirportChartAsync("LIRN");

        Assert.Equal("lirn.mva", Assert.Single(handler.Paths));
        Assert.False(chart.IsEmpty);
    }

    [Fact]
    public async Task Missing_File_Is_An_Empty_Chart_Not_A_Failure()
    {
        // 25 APP su 49 non hanno il file: è un caso normale del sectorfile, non un guasto.
        var handler = new RecordingHandler(new Dictionary<string, string>());
        var chart = await Build(handler, new SectorfileCache()).GetAirportChartAsync("LIRF");

        Assert.True(chart.IsEmpty);
    }

    [Fact]
    public async Task Chart_Is_Cached_Per_File()
    {
        var handler = new RecordingHandler(new Dictionary<string, string>
        {
            ["ENRMVA/lipp.mva"] = Sample,
            ["lipe.mva"] = Sample,
        });
        var cache = new SectorfileCache();
        var provider = Build(handler, cache);

        await provider.GetAccChartAsync("LIPP");
        await provider.GetAccChartAsync("LIPP");
        await provider.GetAirportChartAsync("LIPE");

        // Due file distinti → due GET; la seconda richiesta dello stesso file non tocca la rete.
        Assert.Equal(new[] { "ENRMVA/lipp.mva", "lipe.mva" }, handler.Paths);

        // Anche il 404 resta in cache: un APP senza file non deve ri-chiedere a ogni apertura del documento.
        await provider.GetAirportChartAsync("LIRF");
        await provider.GetAirportChartAsync("LIRF");
        Assert.Equal(3, handler.Paths.Count);
    }

    [Fact]
    public async Task Invalidate_Makes_The_Next_Call_Reload()
    {
        var handler = new RecordingHandler(new Dictionary<string, string> { ["ENRMVA/limm.mva"] = Sample });
        var cache = new SectorfileCache();
        var provider = Build(handler, cache);

        await provider.GetAccChartAsync("LIMM");
        cache.Invalidate();
        await provider.GetAccChartAsync("LIMM");

        Assert.Equal(2, handler.Paths.Count);
    }

    [Fact]
    public async Task Without_Base_Url_The_Source_Is_Off()
    {
        // RawBaseUrl vuota = sectorfile non configurato: nessuna chiamata, carta vuota (come l'import SID).
        var handler = new RecordingHandler(new Dictionary<string, string> { ["ENRMVA/lirr.mva"] = Sample });
        var chart = await Build(handler, new SectorfileCache(), baseUrl: "").GetAccChartAsync("LIRR");

        Assert.True(chart.IsEmpty);
        Assert.Empty(handler.Paths);
    }
}
