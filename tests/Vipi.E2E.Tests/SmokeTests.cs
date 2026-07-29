using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
            // ConfigureAppConfiguration (non Host) così vince su appsettings.Development.json, che ha VipiAuth:Enabled=true.
            builder.ConfigureAppConfiguration(cfg => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["VipiAuth:Enabled"] = "false",
            }));
            return base.CreateHost(builder);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { /* best-effort cleanup */ }
        }
    }
}
