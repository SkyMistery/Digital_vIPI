using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Infrastructure.Ivao;

namespace Vipi.Infrastructure.Tests;

/// <summary>Cache ATC + adapter HTTP IVAO (filtro prefisso, parsing, evento). F3.</summary>
public class IvaoPollingTests
{
    [Fact]
    public void Cache_pubblica_snapshot_e_notifica()
    {
        var cache = new OnlineAtcCache();
        var notified = 0;
        cache.Changed += () => notified++;

        Assert.Empty(cache.GetCurrent().Callsigns); // Empty iniziale

        cache.Set(new OnlineAtcSnapshot
        {
            Callsigns = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "LIRR_NE_CTR" },
            Details = new[] { new OnlineAtc("LIRR_NE_CTR", 123, "VID 123", 4) },
            AsOf = DateTimeOffset.UtcNow,
        });

        Assert.Equal(1, notified);
        Assert.Contains("LIRR_NE_CTR", cache.GetCurrent().Callsigns);
    }

    [Fact]
    public async Task ApiClient_filtra_prefisso_e_normalizza()
    {
        const string json = """
        [
          { "callsign": "LIRR_NE_CTR", "userId": 111, "rating": 4 },
          { "callsign": "LIMM_WS_CTR", "userId": 222, "rating": 5 },
          { "callsign": "LFFF_CTR",    "userId": 333, "rating": 3 },
          { "callsign": "EDGG_CTR",    "userId": 444, "rating": 2 }
        ]
        """;

        var client = BuildClient(json, prefix: "LI");
        var atcs = await client.GetOnlineAtcAsync();

        // Tiene solo gli ATC con prefisso "LI" (Italia).
        Assert.Equal(2, atcs.Count);
        Assert.All(atcs, a => Assert.StartsWith("LI", a.Callsign));
        var ne = Assert.Single(atcs, a => a.Callsign == "LIRR_NE_CTR");
        Assert.Equal(111, ne.Vid);
        Assert.Equal(4, ne.Rating);
    }

    private static IvaoApiClient BuildClient(string responseJson, string prefix)
    {
        var opt = Options.Create(new IvaoOptions { ClientId = "" /* pubblico, no token */ });
        var div = Options.Create(new Vipi.Application.DivisionOptions { IcaoPrefixes = new() { prefix } });
        var http = new HttpClient(new StubHandler(responseJson));
        var token = new IvaoTokenProvider(new NullHttpClientFactory(), opt);
        return new IvaoApiClient(http, token, opt, div);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _body;
        public StubHandler(string body) => _body = body;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            });
    }

    private sealed class NullHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
