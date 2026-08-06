using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// Smoke in-process (WebApplicationFactory): l'app avvia, migra il DB, risolve l'intero grafo DI e serve le
/// pagine. Cattura le rotture di boot/DI/pipeline che i test unitari per-layer non vedono. DB isolato in file
/// temporaneo (non tocca il vipi.db di sviluppo); ambiente Development ⇒ identità dev admin, niente HTTPS redirect.
/// </summary>
public sealed class SmokeTests : IClassFixture<SmokeTests.VipiAppFactory>
{
    private readonly VipiAppFactory _factory;
    public SmokeTests(VipiAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_endpoint_is_up()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/vsop/health");
        // Healthy o Degraded (cache ATC vuota nei test) ⇒ 200; Unhealthy (DB giù / migrazioni pendenti) ⇒ 503.
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    /// <summary>
    /// La sonda economica per l'orchestratore (healthCheckPath di Render). Deve esistere ed essere Healthy: a
    /// differenza di /vsop/health non guarda la cache ATC, che nei test è vuota e degraderebbe l'esito.
    /// </summary>
    [Fact]
    public async Task Readiness_endpoint_is_healthy()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/vsop/health/ready");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("Healthy", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Landing_page_renders()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/vsop");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Diagnostics_page_renders()
    {
        // Identità dev = admin (fallback statico): la pagina diagnostica esegue il report end-to-end (service+repo+EF).
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/vsop/admin/diagnostica");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Live_page_renders()
    {
        // Senza callsign nell'URL: nessuna connessione IVAO nei test ⇒ stato d'attesa, che è una pagina valida.
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/vsop/live");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    /// <summary>
    /// La vista live ha una rotta a parametro <c>/vsop/live/{callsign}</c> che ricade sullo stesso prefisso
    /// dello stream SSE <c>/vsop/live/atc</c>. La precedenza del routing (segmento letterale &gt; parametro)
    /// deve continuare a mandare quell'URL allo stream, non alla pagina: se qualcuno cambia le rotte, qui si vede.
    /// </summary>
    [Fact]
    public async Task Sse_endpoint_wins_over_the_live_page_route()
    {
        var client = _factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var res = await client.GetAsync("/vsop/live/atc", HttpCompletionOption.ResponseHeadersRead, cts.Token);

        Assert.Equal("text/event-stream", res.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>
    /// L'immagine di un blocco si serve per sha256: l'URL È il contenuto, quindi la risposta va dichiarata
    /// <c>immutable</c> (un'immagine diversa è un URL diverso) e con <c>nosniff</c>, perché il tipo è quello dedotto
    /// dai byte al caricamento e il browser non deve provare a interpretarlo altrimenti.
    /// </summary>
    [Fact]
    public async Task Media_endpoint_serves_the_uploaded_image()
    {
        string sha;
        using (var scope = _factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IMediaStore>();
            sha = (await store.SaveAsync(new MemoryStream(MinimalPng()), "prova.png")).Sha256;
        }

        var res = await _factory.CreateClient().GetAsync($"/vsop/media/{sha}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("image/png", res.Content.Headers.ContentType?.MediaType);
        Assert.Contains("immutable", res.Headers.CacheControl?.ToString() ?? "");
        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task Media_endpoint_answers_404_for_an_unknown_sha()
    {
        // Una release vecchia può citare uno sha non più risolvibile: dev'essere una figura mancante, non un 500.
        var res = await _factory.CreateClient().GetAsync("/vsop/media/" + new string('a', 64));

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    /// PNG minimo valido: qui conta l'intestazione (formato e dimensioni), non i pixel.
    private static byte[] MinimalPng()
    {
        var b = new byte[24];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(b, 0);
        b[11] = 0x0D;
        "IHDR"u8.ToArray().CopyTo(b, 12);
        b[18] = 0x03; b[19] = 0x20;   // 800
        b[22] = 0x02; b[23] = 0x58;   // 600
        return b;
    }

    /// <summary>
    /// L'endpoint del bridge Aurora è superficie pubblica e anonima: non deve esistere finché qualcuno non lo
    /// accende. Questo test è l'unico posto in cui la scelta del default si vede davvero — nelle opzioni è una
    /// riga che si cambia senza accorgersene.
    ///
    /// <para>⚠️ La risposta attesa è <b>405</b>, non 404, e non è un dettaglio da correggere: il catch-all di
    /// <c>MapRazorComponents</c> risponde al GET di qualunque path, quindi per il routing quel path esiste e a
    /// mancare è solo il verbo. Ciò che conta è che il gestore non giri; il codice esatto lo decide una rotta
    /// che non è la nostra.</para>
    /// </summary>
    [Fact]
    public async Task Aurora_bridge_endpoint_is_not_mounted_unless_enabled()
    {
        var res = await _factory.CreateClient().PostAsJsonAsync(
            "/vsop/api/v1/transfers/resolve", new { ownerCallsign = "LIRR_CTR" });

        Assert.Equal(HttpStatusCode.MethodNotAllowed, res.StatusCode);
    }

    /// <summary>Acceso, l'endpoint c'è e valida: senza <c>ownerCallsign</c> risponde 400, non 500.</summary>
    [Fact]
    public async Task Aurora_bridge_endpoint_answers_when_enabled()
    {
        using var factory = new BridgeOnAppFactory();
        var client = factory.CreateClient();

        var senzaCallsign = await client.PostAsJsonAsync("/vsop/api/v1/transfers/resolve", new { });
        Assert.Equal(HttpStatusCode.BadRequest, senzaCallsign.StatusCode);

        // Callsign non riconducibile a nessuna ACC: risposta valida con un avviso, non un errore.
        var conCallsign = await client.PostAsJsonAsync(
            "/vsop/api/v1/transfers/resolve", new { ownerCallsign = "ZZZZ_CTR" });
        Assert.Equal(HttpStatusCode.OK, conCallsign.StatusCode);
    }

    /// <summary>Fabbrica gemella con il bridge acceso: il default resta spento per tutti gli altri test.</summary>
    public sealed class BridgeOnAppFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"vipi-e2e-bridge-{Guid.NewGuid():N}.db");

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureHostConfiguration(cfg => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Vipi"] = $"Data Source={_dbPath}",
                ["AuroraBridge:Enabled"] = "true",
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

    [Fact]
    public void Database_migrated_and_write_pipeline_resolves()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;

        var db = sp.GetRequiredService<VipiDbContext>();
        Assert.True(db.Database.CanConnect());
        Assert.Empty(db.Database.GetPendingMigrations());       // MigrateVipiDatabase ha girato al boot

        // Il grafo del percorso di scrittura è costruibile end-to-end (service Application reali + repo + EF).
        Assert.NotNull(sp.GetRequiredService<IEditingService>());
    }

    public sealed class VipiAppFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"vipi-e2e-{Guid.NewGuid():N}.db");

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureHostConfiguration(cfg => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Vipi"] = $"Data Source={_dbPath}",
            }));
            // E2E: niente OIDC reale in CI (nessun ClientId) → disattiva l'auth standalone; i test usano l'identità dev.
            //
            // Perché una variabile d'ambiente e non ConfigureAppConfiguration: `Program.cs` chiama
            // AddVipiStandaloneAuth alla REGISTRAZIONE, prima di builder.Build(), mentre i callback di
            // ConfigureAppConfiguration vengono applicati solo alla costruzione dell'host. La sorgente
            // in-memory arrivava quindi troppo tardi e l'app tirava «ClientId mancante» (rosso solo in CI,
            // perché in locale i user-secrets forniscono un ClientId vero e la guardia non scatta).
            // Le variabili d'ambiente sono invece già nella configurazione di default del builder, dopo
            // appsettings.Development.json — che porta VipiAuth:Enabled=true — quindi vincono su di esso.
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
