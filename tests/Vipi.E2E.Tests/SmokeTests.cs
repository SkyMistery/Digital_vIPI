using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
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
        var res = await client.GetAsync("/services/vsop");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Diagnostics_page_renders()
    {
        // Identità dev = admin (fallback statico): la pagina diagnostica esegue il report end-to-end (service+repo+EF).
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/services/vsop/admin/diagnostics");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Live_page_renders()
    {
        // Senza callsign nell'URL: nessuna connessione IVAO nei test ⇒ stato d'attesa, che è una pagina valida.
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/services/vsop/live");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    /// <summary>
    /// La vista live ha una rotta a parametro <c>/services/vsop/live/{callsign}</c> che ricade sullo stesso prefisso
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
    /// <para>⚠️ <b>Il codice esatto dipende dal TFM dell'host, e nessuno dei due è un difetto.</b> Su net10 il
    /// catch-all che <c>MapRazorComponents</c> registra per la pagina «non trovato» risponde al GET di qualunque
    /// path: per il routing quel path esiste e a mancare è solo il verbo, quindi <b>405</b>. Su net8 — cioè
    /// l'host che va in produzione su <c>atc.it.ivao.aero</c> — quel catch-all non esiste e la risposta è
    /// <b>404</b>. Ciò che il test presidia è che il gestore non giri; il codice lo decide una rotta che non è
    /// la nostra. Il tool desktop traduce entrambi in «su questo sito il bridge non è attivo».</para>
    /// </summary>
    [Fact]
    public async Task Aurora_bridge_endpoint_is_not_mounted_unless_enabled()
    {
        var res = await _factory.CreateClient().PostAsJsonAsync(
            "/vsop/api/v1/transfers/resolve", new { ownerCallsign = "LIRR_CTR" });

        Assert.True(
            res.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound,
            $"con il bridge spento la rotta non deve esistere: atteso 404 (host net8) o 405 (host net10), ricevuto {(int)res.StatusCode}");
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

    /// <summary>
    /// Il cache-busting degli asset deve essere <b>per file</b>, non per build. Su net8 non c'è
    /// <c>@Assets[...]</c> e la prima versione usava un'impronta sola per tutti (il MVID dell'assembly):
    /// bastava ricompilare per far riscaricare al browser anche i file identici byte per byte.
    ///
    /// <para>Il test guarda la pagina servita, non l'implementazione: se i suffissi <c>?v=</c> di due asset
    /// diversi coincidono, siamo tornati all'impronta unica — o il file non è stato trovato e si è ricaduti
    /// sul ripiego, che è lo stesso valore per tutti. In entrambi i casi la regressione è reale.</para>
    /// </summary>
    [Fact]
    public async Task Ogni_asset_ha_la_propria_impronta_di_contenuto()
    {
        var html = await _factory.CreateClient().GetStringAsync("/services/vsop");

        var versioni = Regex.Matches(html, @"(?<file>[\w\-./]+\.(?:css|js))\?v=(?<impronta>[0-9a-f]+)")
            .Select(m => (File: m.Groups["file"].Value, Impronta: m.Groups["impronta"].Value))
            .DistinctBy(x => x.File)
            .ToList();

        Assert.True(versioni.Count >= 2, $"attesi almeno due asset versionati nella pagina, trovati {versioni.Count}");
        Assert.True(versioni.Select(v => v.Impronta).Distinct().Count() > 1,
            "tutti gli asset hanno la stessa impronta: o è tornata quella per build, o i file non si " +
            "risolvono e si sta usando il ripiego.\n  " +
            string.Join("\n  ", versioni.Select(v => $"{v.File} -> {v.Impronta}")));
    }

    /// <summary>
    /// Niente codice di terzi caricato a runtime. Fino all'11 agosto 2026 Leaflet arrivava da
    /// <c>unpkg.com</c>: l'SRI copriva la manomissione, non la <b>disponibilità</b> — CDN irraggiungibile
    /// significava tutte le mappe vuote, senza ripiego — ed era l'unica eccezione in un progetto dove font e
    /// three.js sono self-hosted apposta.
    ///
    /// <para>Il test guarda <c>src</c> e <c>href</c> degli elementi che eseguono o disegnano
    /// (<c>&lt;script&gt;</c>, <c>&lt;link&gt;</c>): un <c>&lt;a href&gt;</c> verso l'esterno è un
    /// collegamento, non una dipendenza.</para>
    /// </summary>
    [Fact]
    public async Task Nessuna_dipendenza_esterna_caricata_dalla_pagina()
    {
        var html = await _factory.CreateClient().GetStringAsync("/services/vsop");

        var esterni = Regex.Matches(html, @"<(?:script|link)\b[^>]*\b(?:src|href)\s*=\s*[""'](?<url>[^""']+)[""']")
            .Select(m => m.Groups["url"].Value)
            .Where(u => u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        u.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                        u.StartsWith("//", StringComparison.Ordinal))
            .Distinct()
            .ToList();

        Assert.True(esterni.Count == 0,
            "la pagina carica codice o fogli di stile da host esterni: il sito deve funzionare anche quando " +
            "quegli host non rispondono, e ogni host in più è una voce da aprire nella CSP.\n  " +
            string.Join("\n  ", esterni));
    }

    /// <summary>
    /// Le intestazioni di sicurezza ci sono su OGNI risposta, non solo su quelle che qualcuno si ricorda.
    /// Non chiudono una falla nota — le due funzioni che costruiscono HTML a mano encodano prima e
    /// costruiscono dopo — ma rendono innocuo l'errore di domani.
    /// </summary>
    [Theory]
    [InlineData("/services/vsop")]
    [InlineData("/vsop/health/ready")]
    public async Task Le_intestazioni_di_sicurezza_ci_sono(string percorso)
    {
        var res = await _factory.CreateClient().GetAsync(percorso);

        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", res.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("strict-origin-when-cross-origin", res.Headers.GetValues("Referrer-Policy").Single());
        // Report-only finché le due `unsafe-inline` non sono state tolte: vedi Program.cs.
        Assert.Contains("frame-ancestors 'none'", res.Headers.GetValues("Content-Security-Policy-Report-Only").Single());
    }

    /// <summary>
    /// Nessuno <c>&lt;script&gt;</c> inline nella pagina servita. È il presupposto di
    /// <c>script-src 'self'</c> senza <c>'unsafe-inline'</c>: quella clausola è ciò che separa una CSP che
    /// protegge da una che è solo un'intestazione in più.
    ///
    /// <para>I due che c'erano — lo zoom nel <c>&lt;head&gt;</c> e il riaggancio dopo le navigazioni
    /// «enhanced» — sono diventati <c>vipi-zoom.js</c> e <c>vipi-boot.js</c>. Chi ne aggiunge un terzo lo
    /// scopre qui invece che dal browser di un utente.</para>
    /// </summary>
    [Fact]
    public async Task La_pagina_non_contiene_script_inline()
    {
        var html = await _factory.CreateClient().GetStringAsync("/services/vsop");

        var inline = Regex.Matches(html, @"<script\b(?<attributi>[^>]*)>(?<corpo>.*?)</script>", RegexOptions.Singleline)
            .Where(m => !m.Groups["attributi"].Value.Contains("src=", StringComparison.OrdinalIgnoreCase))
            .Where(m => m.Groups["corpo"].Value.Trim().Length > 0)
            .Select(m => m.Groups["corpo"].Value.Trim().Replace("\n", " ")[..Math.Min(120, m.Groups["corpo"].Value.Trim().Length)])
            .ToList();

        Assert.True(inline.Count == 0,
            $"{inline.Count} <script> inline nella pagina: obbligano a tenere `script-src 'unsafe-inline'`, " +
            "e con quella una CSP non ferma uno script iniettato. Si spostano in un file sotto " +
            "wwwroot e si citano con AssetVersion.Url(...).\n  " + string.Join("\n  ", inline));
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
