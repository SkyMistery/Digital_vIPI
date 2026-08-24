using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vipi.Application.Diagnostica;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// Quando due operazioni si incontrano sullo stesso <c>DbContext</c>, il registro deve dire <b>chi c'era
/// già</b>: lo stack dell'eccezione dice soltanto chi è morto, e con quella metà sola il 24 agosto 2026 si
/// è speso un giro di deploy su un sospettato sbagliato (<c>docs/lavori-aperti.md</c> §E9).
/// </summary>
public sealed class CollisioniDbContextTests : IClassFixture<CollisioniDbContextTests.Fabbrica>
{
    private readonly Fabbrica _fabbrica;
    public CollisioniDbContextTests(Fabbrica fabbrica) => _fabbrica = fabbrica;

    /// <summary>
    /// Il cuore: mentre qualcosa è aperto, il lancio dell'eccezione di EF fotografa la scena.
    ///
    /// <para>⚠️ La scena si costruisce a mano invece di provocare una vera corsa, e la ragione è stata
    /// misurata: il rilevatore di concorrenza di EF <b>non</b> copre i comandi grezzi
    /// (<c>ExecuteSqlRawAsync</c>), quindi il modo ovvio di tenere occupato il contesto non fa scattare
    /// niente. Qui si prova ciò che è stato scritto — l'aggancio a <c>FirstChanceException</c> e la
    /// fotografia — e l'aggancio a EF lo prova il test dopo.</para>
    /// </summary>
    [Fact]
    public void Al_lancio_si_fotografa_chi_era_aperto()
    {
        var contestoFinto = new object();
        CollisioniDbContext.Apre(contestoFinto, "SELECT \"a\".\"Code\" FROM \"Accs\" AS \"a\" -- la prima");

        try
        {
            // Il messaggio è quello vero di EF: è su quello che si aggancia.
            throw new InvalidOperationException(
                "A second operation was started on this context instance before a previous operation completed.");
        }
        catch (InvalidOperationException) { /* atteso: serviva solo il lancio */ }
        finally { CollisioniDbContext.Chiude(contestoFinto, "SELECT \"a\".\"Code\" FROM \"Accs\" AS \"a\" -- la prima"); }

        var ultimo = Assert.Single(CollisioniDbContext.Scatti_(), s => s.Contains("-- la prima"));
        Assert.Contains("erano aperte", ultimo);
        Assert.Contains("Vipi.E2E.Tests", ultimo);   // il chiamante, che è la riga che mancava
    }

    /// <summary>L'aggancio a EF: l'intercettore dev'essere davvero montato sul contesto dell'applicazione.</summary>
    [Fact]
    public void L_intercettore_e_montato_sul_contesto_vero()
    {
        using var scope = _fabbrica.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VipiDbContext>();

        var interceptors = db.GetService<IDbContextOptions>()
            .FindExtension<CoreOptionsExtension>()!.Interceptors ?? Enumerable.Empty<IInterceptor>();

        Assert.Contains(interceptors, i => i is TracciaCollisioniInterceptor);
    }

    public sealed class Fabbrica : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"vipi-coll-{Guid.NewGuid():N}.db");

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureHostConfiguration(cfg => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Vipi"] = $"Data Source={_dbPath}",
            }));
            Environment.SetEnvironmentVariable("VipiAuth__Enabled", "false");
            return base.CreateHost(builder);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { /* best-effort */ }
        }
    }
}
