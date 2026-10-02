using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Vipi.Host;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// <c>GET /api/tabellone/{ICAO}</c> sul server vero (carta docs/feature/2026-10-02-tabellone-partenze-arrivi.md):
/// pubblico senza chiave, 404 per uno scalo senza tabellone, ETag e 304, CORS aperto, il «niente di nuovo» fuori
/// dal registro. Nessuna fonte vera: booking senza chiave, Whazzup irraggiungibile — il tabellone risponde lo stesso.
/// </summary>
public sealed class TabelloneEndpointTests : IClassFixture<TabelloneEndpointTests.TabelloneFactory>
{
    private readonly TabelloneFactory _factory;
    public TabelloneEndpointTests(TabelloneFactory factory) => _factory = factory;

    [Fact]
    public async Task Risponde_senza_chiave_anche_senza_fonti()
    {
        var res = await _factory.CreateClient().GetAsync("/api/tabellone/lirf");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("application/json; charset=utf-8", res.Content.Headers.ContentType?.ToString());
        Assert.Equal("*", res.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains("public", res.Headers.CacheControl?.ToString());

        var radice = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("LIRF", radice.GetProperty("scalo").GetProperty("icao").GetString());
        Assert.Equal(JsonValueKind.Null, radice.GetProperty("evento").ValueKind);
        Assert.Equal("tutti", radice.GetProperty("voli").GetString());
        Assert.False(radice.GetProperty("fonti").GetProperty("booking").GetProperty("ok").GetBoolean());
        Assert.Equal(0, radice.GetProperty("partenze").GetArrayLength());
    }

    [Theory]
    [InlineData("/api/tabellone/LIRN")]     // nessun tabellone configurato
    [InlineData("/api/tabellone/LIRFX")]
    [InlineData("/api/tabellone/L%21RF")]
    public async Task Uno_scalo_senza_tabellone_e_un_404(string url)
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClient().GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Con_la_stessa_etichetta_un_304_senza_corpo()
    {
        var c = _factory.CreateClient();
        var prima = await c.GetAsync("/api/tabellone/LIRF");
        var etag = prima.Headers.ETag!.Tag;

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/tabellone/LIRF");
        req.Headers.TryAddWithoutValidation("If-None-Match", "W/" + etag);   // un proxy che comprime aggiunge la W
        var res = await c.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotModified, res.StatusCode);
        Assert.Equal("", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public void Il_registro_delle_richieste_non_scrive_i_304_del_tabellone()
    {
        Assert.True(RegistroRichieste.PollingVuoto("/api/tabellone/LIRF", 304));
        Assert.False(RegistroRichieste.PollingVuoto("/api/tabellone/LIRF", 200));
        Assert.False(RegistroRichieste.PollingVuoto("/api/tabellone/LIRF", 404));
    }

    public sealed class TabelloneFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"vipi-e2e-tabellone-{Guid.NewGuid():N}.db");

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureHostConfiguration(cfg => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Vipi"] = $"Data Source={_dbPath}",
                ["Ivao:BaseUrl"] = "http://127.0.0.1:9",
                ["Tabellone:BookingApiKey"] = "",
                ["Tabellone:Scali:LIRF:EventoRfo"] = "prova-tabellone",
            }));
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
