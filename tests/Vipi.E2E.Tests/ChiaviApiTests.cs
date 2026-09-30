using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// 🔴 <b>Le API non sono mai anonime</b> (committente, 13 settembre 2026; carta
/// <c>docs/feature/2026-09-13-chiavi-api.md</c>, T-017). Le risposte della porta, sul server vero: 401 senza
/// chiave o con una chiave sbagliata o revocata, 403 con una chiave buona per un'altra API, 200 con la chiave
/// giusta in uno dei due header.
/// </summary>
public sealed class ChiaviApiTests : IClassFixture<ChiaviApiTests.ApiChiusaFactory>
{
    private const string Archivio = "/vsop/api/v1/atc/sessions";
    private const string Bridge = "/vsop/api/v1/transfers/resolve";
    private const string Aeroporti = "/vsop/api/v1/airports";

    private readonly ApiChiusaFactory _factory;
    public ChiaviApiTests(ApiChiusaFactory factory) => _factory = factory;

    /// <summary>Emette una chiave scrivendo dal deposito, senza passare dal cancello di chi emette: qui si
    /// prova la porta, non la pagina.</summary>
    public static async Task<string> EmettiAsync(IServiceProvider services, params string[] endpoint)
    {
        using var scope = services.CreateScope();
        var chiave = ChiaveApi.Genera();
        await scope.ServiceProvider.GetRequiredService<IApiClientStore>()
            .AddAsync("prova", ChiaveApi.Prefisso(chiave), ChiaveApi.Impronta(chiave), endpoint, 1);
        return chiave;
    }

    private HttpClient ConBearer(string chiave)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", chiave);
        return c;
    }

    [Fact]
    public async Task Archivio_senza_chiave_quando_e_richiesta_401()
    {
        var res = await _factory.CreateClient().GetAsync(Archivio);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.Contains("Bearer", res.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task Archivio_con_la_chiave_giusta_in_Bearer_200()
    {
        var chiave = await EmettiAsync(_factory.Services, "archivio");
        var res = await ConBearer(chiave).GetAsync(Archivio);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Archivio_con_la_chiave_giusta_in_X_Api_Key_200()
    {
        var chiave = await EmettiAsync(_factory.Services, "archivio");
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Add("X-Api-Key", chiave);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync(Archivio)).StatusCode);
    }

    [Fact]
    public async Task Archivio_con_una_chiave_mai_emessa_o_malformata_401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await ConBearer(ChiaveApi.Genera()).GetAsync(Archivio)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await ConBearer("vipi_indovinata").GetAsync(Archivio)).StatusCode);
    }

    [Fact]
    public async Task Archivio_con_una_chiave_revocata_401_e_l_ultimo_uso_si_vede()
    {
        var chiave = await EmettiAsync(_factory.Services, "archivio");
        Assert.Equal(HttpStatusCode.OK, (await ConBearer(chiave).GetAsync(Archivio)).StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VipiDbContext>();
            var riga = await db.ApiClients.AsNoTracking().SingleAsync(c => c.ImprontaSha256 == ChiaveApi.Impronta(chiave));
            Assert.NotNull(riga.UltimoUsoUtc);   // il passo 3 del §7: si sa chi la usa senza chiederlo
            await scope.ServiceProvider.GetRequiredService<IApiClientStore>().RevocaAsync(riga.Id, 1);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await ConBearer(chiave).GetAsync(Archivio)).StatusCode);
    }

    [Fact]
    public async Task Una_chiave_del_bridge_non_legge_l_archivio_403()
    {
        var chiave = await EmettiAsync(_factory.Services, "bridge");
        Assert.Equal(HttpStatusCode.Forbidden, (await ConBearer(chiave).GetAsync(Archivio)).StatusCode);
    }

    [Fact]
    public async Task Bridge_senza_chiave_401_con_chiave_d_archivio_403_con_la_sua_200()
    {
        var corpo = new { ownerCallsign = "ZZZZ_CTR" };

        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().PostAsJsonAsync(Bridge, corpo)).StatusCode);

        var archivio = await EmettiAsync(_factory.Services, "archivio");
        Assert.Equal(HttpStatusCode.Forbidden, (await ConBearer(archivio).PostAsJsonAsync(Bridge, corpo)).StatusCode);

        var bridge = await EmettiAsync(_factory.Services, "bridge");
        Assert.Equal(HttpStatusCode.OK, (await ConBearer(bridge).PostAsJsonAsync(Bridge, corpo)).StatusCode);
    }

    /// <summary>
    /// Le API degli aeroporti (carta 2026-09-30-api-aeroporti.md) vogliono la chiave SEMPRE, anche dove l'archivio
    /// è ancora aperto: nascono dopo la regola. E solo la loro: una chiave dell'archivio non le apre.
    /// </summary>
    [Fact]
    public async Task Aeroporti_senza_chiave_401_con_chiave_d_archivio_403_con_la_sua_200()
    {
        using var aperta = new ApiChiusaFactory(richiediChiave: false);

        foreach (var percorso in new[] { Aeroporti, Aeroporti + "/LIRF", Aeroporti + "/LIRF/sids", Aeroporti + "/LIRF/stars" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await aperta.CreateClient().GetAsync(percorso)).StatusCode);

        var archivio = await EmettiAsync(aperta.Services, "archivio");
        var c = aperta.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", archivio);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync(Aeroporti)).StatusCode);

        var chiave = await EmettiAsync(aperta.Services, "aeroporti");
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", chiave);
        var res = await c.GetAsync(Aeroporti);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var json = System.Text.Json.JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.Equal(json.RootElement.GetProperty("airports").GetArrayLength(), json.RootElement.GetProperty("count").GetInt32());
    }

    /// <summary>Uno scalo senza documento pubblicato non esiste per l'API, qualunque cosa abbia in archivio: le
    /// SID che lo staff non ha dato al pubblico non escono da una porta laterale.</summary>
    [Fact]
    public async Task Aeroporti_uno_scalo_senza_documento_pubblicato_404()
    {
        var chiave = await EmettiAsync(_factory.Services, "aeroporti");
        foreach (var percorso in new[] { Aeroporti + "/ZZZZ", Aeroporti + "/ZZZZ/sids", Aeroporti + "/ZZZZ/stars", Aeroporti + "/LIR/sids" })
            Assert.Equal(HttpStatusCode.NotFound, (await ConBearer(chiave).GetAsync(percorso)).StatusCode);
    }

    /// <summary>
    /// Il periodo di passaggio (§7): con <c>Api:RichiediChiave</c> spento l'archivio risponde a chi non porta
    /// una chiave, come prima — ma una chiave presentata si verifica comunque, e se è sbagliata è un 401. Il
    /// bridge invece la chiede sempre.
    /// </summary>
    [Fact]
    public async Task Nel_passaggio_l_archivio_resta_aperto_ma_una_chiave_sbagliata_resta_sbagliata()
    {
        using var aperta = new ApiChiusaFactory(richiediChiave: false);

        Assert.Equal(HttpStatusCode.OK, (await aperta.CreateClient().GetAsync(Archivio)).StatusCode);

        var c = aperta.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ChiaveApi.Genera());
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync(Archivio)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await aperta.CreateClient().PostAsJsonAsync(Bridge, new { ownerCallsign = "ZZZZ_CTR" })).StatusCode);
    }

    /// <summary>
    /// U-018 (revisione totale 3): la regola «le API non sono mai anonime» sta nel CODICE, non solo nella
    /// configurazione di produzione. Un sito che non dice niente di <c>Api:RichiediChiave</c> tiene l'archivio
    /// chiuso; aprirlo è una scelta scritta (<c>false</c>), mai una dimenticanza. Fino al 27 settembre 2026 il
    /// default era <c>false</c> e la produzione, che non lo aveva mai acceso, rispondeva a chiunque con 46 522
    /// sessioni.
    /// </summary>
    [Fact]
    public async Task Senza_configurazione_l_archivio_vuole_la_chiave()
    {
        using var muta = new ApiChiusaFactory(richiediChiave: null);

        var res = await muta.CreateClient().GetAsync(Archivio);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);

        var chiave = await EmettiAsync(muta.Services, "archivio");
        var c = muta.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", chiave);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync(Archivio)).StatusCode);
    }

    public sealed class ApiChiusaFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"vipi-e2e-api-{Guid.NewGuid():N}.db");
        private readonly bool? _richiediChiave;

        public ApiChiusaFactory() : this(richiediChiave: true) { }
        // Interno: xUnit vuole un solo costruttore PUBBLICO nelle fixture. Null = la chiave non si scrive affatto,
        // e decide il default del codice.
        internal ApiChiusaFactory(bool? richiediChiave) => _richiediChiave = richiediChiave;

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            var config = new Dictionary<string, string?>
            {
                ["ConnectionStrings:Vipi"] = $"Data Source={_dbPath}",
                ["AuroraBridge:Enabled"] = "true",
            };
            if (_richiediChiave is { } r) config["Api:RichiediChiave"] = r ? "true" : "false";
            builder.ConfigureHostConfiguration(cfg => cfg.AddInMemoryCollection(config));
            Environment.SetEnvironmentVariable("VipiAuth__Enabled", "false");
            return base.CreateHost(builder);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { /* best-effort cleanup */ }
        }
    }
}
