using System.Net;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Infrastructure.Ivao;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Gli import che perdevano dati senza dirlo (lotto L3 della revisione del 13 settembre 2026).
///
/// <para><b>T-005</b>: una pagina fallita delle aree regolamentate consegnava al prune un elenco parziale, e le
/// aree delle altre pagine perdevano il legame — e venivano cancellate. <b>T-007</b>: un dettaglio di subcenter
/// non letto (429 anche dopo i ritentativi) azzerava la frequenza in catalogo.</para>
/// </summary>
public class ImportCheNonPerdonoDatiTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfAccAdminRepository _repo = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _repo = new EfAccAdminRepository(_db);
        await _repo.ImportAsync(new[] { new SourceCenter("LIRR_CTR", "LIRR", "Roma Control", false, "124.000") });
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    // ---- T-005: il client ----

    [Fact]
    public async Task Una_pagina_successiva_che_non_risponde_fa_fallire_la_lettura_invece_di_troncarla()
    {
        var client = Client(new()
        {
            ["/v2/centers/LIRR/specialAreas?page=1"] = (HttpStatusCode.OK, """{"pages":3,"items":[{"id":"1","name":"A"}]}"""),
            ["/v2/centers/LIRR/specialAreas?page=2"] = (HttpStatusCode.TooManyRequests, ""),
        });

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetSpecialAreasAsync("LIRR", new HashSet<string>(), CancellationToken.None));
    }

    [Fact]
    public async Task Un_200_senza_elenco_non_e_nessuna_area()
    {
        var client = Client(new()
        {
            ["/v2/centers/LIRR/specialAreas?page=1"] = (HttpStatusCode.OK, """{"message":"maintenance"}"""),
        });

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            client.GetSpecialAreasAsync("LIRR", new HashSet<string>(), CancellationToken.None));
    }

    // ---- T-005: la guardia di massa nel use case ----

    [Fact]
    public async Task Un_elenco_che_omette_troppe_aree_non_pota_niente()
    {
        var aree = Enumerable.Range(1, 8).Select(i => Area(i.ToString())).ToList();
        await _repo.ImportSpecialAreasAsync(aree);

        // La sorgente, questa notte, ne restituisce una sola (pagina persa a monte, o risposta monca).
        var r = await new SpecialAreaImportUseCase(_repo, new Aree(aree.Take(1).ToList()), new EfImportPolicyStore(_db)).RunAsync();

        Assert.Equal("LIRR", Assert.Single(r.Failures).AccCode);
        Assert.Equal(8, await _db.SpecialAreas.CountAsync());
    }

    [Fact]
    public async Task Poche_aree_sparite_davvero_si_potano_come_prima()
    {
        var aree = Enumerable.Range(1, 8).Select(i => Area(i.ToString())).ToList();
        await _repo.ImportSpecialAreasAsync(aree);

        var r = await new SpecialAreaImportUseCase(_repo, new Aree(aree.Take(7).ToList()), new EfImportPolicyStore(_db)).RunAsync();

        Assert.Empty(r.Failures);
        Assert.Equal(7, await _db.SpecialAreas.CountAsync());
    }

    // ---- T-007 ----

    [Fact]
    public async Task Un_dettaglio_non_letto_non_azzera_la_frequenza()
    {
        await _repo.ImportSubcentersAsync(new[] { new SourceSubcenter("LIRR_NE_CTR", "LIRR", "CTR", "NE", "125.500", null, IvaoId: 1174) });

        // Il giro dopo, il dettaglio risponde 429: la frequenza non arriva.
        await _repo.ImportSubcentersAsync(new[] { new SourceSubcenter("LIRR_NE_CTR", "LIRR", "CTR", "NE", null, null, IvaoId: 1174) });

        Assert.Equal("125.500", (await _db.AccSectors.AsNoTracking().SingleAsync(s => s.ComposePosition == "LIRR_NE_CTR")).Frequency);
    }

    private static SourceSpecialArea Area(string id) =>
        new(id, "R", "LI R" + id, null, null, 0, 5000, false, "LIRR", "[[41,12]]");

    private static IvaoAccClient Client(Dictionary<string, (HttpStatusCode, string)> risposte)
    {
        var opt = Options.Create(new IvaoOptions { ClientId = "prova", ClientSecret = "x" });
        var http = new IvaoHttp(new HttpClient(new Centralino(risposte)), new IvaoTokenProvider(new FabbricaConToken(), opt), opt);
        return new IvaoAccClient(http, opt);
    }

    private sealed class Aree(IReadOnlyList<SourceSpecialArea> aree) : IAccDirectory
    {
        public Task<IReadOnlyList<SourceSpecialArea>> GetSpecialAreasAsync(string accIcao, IReadOnlySet<string> skipDetailIds, CancellationToken ct = default) =>
            Task.FromResult(aree);
        public Task<IReadOnlyList<SourceCenter>> GetCentersAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SourceCenter>> GetCentersByCountryAsync(string countryId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SourceSubcenter>> GetSubcentersAsync(string accIcao, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class Centralino(Dictionary<string, (HttpStatusCode Status, string Body)> r) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            var (status, body) = r.TryGetValue(req.RequestUri!.PathAndQuery, out var hit) ? hit : (HttpStatusCode.NotFound, "");
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class FabbricaConToken : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new Stampella());

        private sealed class Stampella : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"access_token":"finto","expires_in":3600}""", Encoding.UTF8, "application/json"),
                });
        }
    }
}
