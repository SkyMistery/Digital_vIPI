using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Sectorfile;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// <see cref="AuroraProcedureProvider"/>: il verso chiesto decide il file — <c>.sid</c> per le partenze,
/// <c>.str</c> per gli arrivi — e un file che non c'è è un esito normale, non un guasto.
/// </summary>
public class AuroraProcedureProviderTests
{
    private sealed class Handler : HttpMessageHandler
    {
        private readonly IReadOnlyDictionary<string, string> _bodies;
        public List<string> Paths { get; } = new();

        public Handler(IReadOnlyDictionary<string, string> bodies) => _bodies = bodies;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath.TrimStart('/');
            lock (Paths) Paths.Add(path);
            return Task.FromResult(_bodies.TryGetValue(path, out var body)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private sealed class Punti : INavaidSource
    {
        public Task<NavaidCatalog> GetAsync(CancellationToken ct = default) =>
            Task.FromResult(new NavaidCatalog(new[]
            {
                new NavaidName("GILIO", NavaidKind.Fix),
                new NavaidName("ALAXI", NavaidKind.Fix),
            }));

        public Task<NavaidCatalog> RefreshAsync(CancellationToken ct = default) => GetAsync(ct);
    }

    private sealed class NessunAlias : ISidFixAliasRepository
    {
        public Task<IReadOnlyList<SidFixAliasRow>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SidFixAliasRow>>(Array.Empty<SidFixAliasRow>());

        public Task<IReadOnlyDictionary<string, string>> GetMapAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());

        public Task UpsertAsync(string prefix, string fixName, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static AuroraProcedureProvider Build(Handler handler) =>
        new(new HttpClient(handler, disposeHandler: false),
            Options.Create(new SectorfileOptions { RawBaseUrl = "https://example.test/" }),
            new NessunAlias(), new Punti(), NullLogger<AuroraProcedureProvider>.Instance);

    [Fact]
    public async Task Le_Partenze_Vengono_Dal_File_Sid()
    {
        var handler = new Handler(new Dictionary<string, string> { ["lirf.sid"] = "LIRF;16L;ALAX7G;;;;;1;" });

        var righe = await Build(handler).GetAsync("LIRF", ProcedureKind.Sid);

        Assert.Equal("lirf.sid", Assert.Single(handler.Paths));
        var r = Assert.Single(righe);
        Assert.Equal("ALAX7G", r.Name);
        Assert.Equal(ProcedureKind.Sid, r.Kind);
    }

    [Fact]
    public async Task Gli_Arrivi_Vengono_Dal_File_Str_E_Saltano_Le_Mappe()
    {
        var handler = new Handler(new Dictionary<string, string>
        {
            ["lirf.str"] = "LIRF;MAPS;LIRF CTR; ; ;1;\nLIRF;16L:16R;GILI3A;;;;;1;",
        });

        var righe = await Build(handler).GetAsync("LIRF", ProcedureKind.Star);

        Assert.Equal("lirf.str", Assert.Single(handler.Paths));
        Assert.Equal(2, righe.Count);                      // GILI3A sulle sue due piste, il CTR no
        Assert.All(righe, r => Assert.Equal(ProcedureKind.Star, r.Kind));
        Assert.All(righe, r => Assert.Equal("GILIO", r.Fix));
    }

    [Fact]
    public async Task Un_File_Che_Non_Ce_Non_E_Un_Guasto()
    {
        // 36 dei 90 `.str` della divisione non portano nemmeno una STAR, e ci sono scali senza `.str` affatto.
        var righe = await Build(new Handler(new Dictionary<string, string>())).GetAsync("LIRF", ProcedureKind.Star);

        Assert.Empty(righe);
    }

    [Fact]
    public async Task Senza_Sorgente_Configurata_Non_Si_Chiede_Niente()
    {
        var handler = new Handler(new Dictionary<string, string> { ["lirf.str"] = "LIRF;16L;GILI3A;;;;;1;" });
        var provider = new AuroraProcedureProvider(new HttpClient(handler, disposeHandler: false),
            Options.Create(new SectorfileOptions { RawBaseUrl = "" }),
            new NessunAlias(), new Punti(), NullLogger<AuroraProcedureProvider>.Instance);

        Assert.Empty(await provider.GetAsync("LIRF", ProcedureKind.Star));
        Assert.Empty(handler.Paths);
    }
}
