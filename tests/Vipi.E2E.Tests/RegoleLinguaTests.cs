using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Vipi.Application.Content;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// Le cose che <b>non</b> seguono la lingua (<c>docs/design/regole-lingua.md</c>).
///
/// <para>
/// Sono decisioni del committente, non conseguenze del codice, e per questo hanno bisogno di un test che le
/// difenda: senza, la prima persona che passa di qui le legge come dimenticanze e le «corregge» — un marchio
/// tradotto e una briciola di pane italiana sembrano un miglioramento, finché non si sa perché stanno così.
/// </para>
///
/// <para>⚠️ Vivono nel progetto E2E perché si vedono solo nell'<b>HTML servito</b>: nascono dall'incontro fra
/// il layout, le risorse e la cultura risolta per la richiesta, e nessuno dei tre da solo è sbagliato.</para>
/// </summary>
public sealed class RegoleLinguaTests : IClassFixture<RegoleLinguaTests.RegoleFactory>
{
    private readonly RegoleFactory _factory;
    public RegoleLinguaTests(RegoleFactory factory) => _factory = factory;

    /// <summary>Le pagine su cui si guarda: una pubblica, una d'elenco, una di documento.</summary>
    public static TheoryData<string> Pagine => new()
    {
        "/services/vsop",
        "/services/vsop/lirr",
        "/services/vsop/lirr/airports",
    };

    [Theory]
    [MemberData(nameof(Pagine))]
    public async Task La_briciola_di_pane_e_IDENTICA_nelle_due_lingue(string pagina)
    {
        // R3: la briciola è sempre in inglese, anche dentro una pagina italiana. Non «tradotta bene»:
        // IDENTICA — è lo stesso testo, e se un giorno divergesse vorrebbe dire che qualcuno l'ha rimessa
        // sul localizzatore normale.
        var client = _factory.CreateClient();

        var italiano = Briciola(await client.GetStringAsync($"{pagina}?culture=it"));
        var inglese = Briciola(await client.GetStringAsync($"{pagina}?culture=en"));

        Assert.NotNull(italiano);
        Assert.Equal(inglese, italiano);
    }

    [Theory]
    [InlineData("it")]
    [InlineData("en")]
    public async Task Il_marchio_dice_sempre_ATC_Services(string lingua)
    {
        // R1: un marchio è un nome proprio. Fino al 28 agosto 2026 diceva «Servizi ATC» in italiano.
        var html = await _factory.CreateClient().GetStringAsync($"/services/vsop?culture={lingua}");

        var marchio = Regex.Match(html, @"<a class=""brand""[^>]*>.*?</a>", RegexOptions.Singleline).Value;

        Assert.Contains("ATC Services", marchio, StringComparison.Ordinal);
        Assert.DoesNotContain("Servizi ATC", marchio, StringComparison.Ordinal);
        // R2: il sottotitolo è il nome della divisione, e viene dalla configurazione.
        Assert.Contains("IVAO Italy", marchio, StringComparison.Ordinal);
    }

    [Fact]
    public async Task La_Guida_non_ha_piu_un_selettore_suo()
    {
        // Il vecchio `?lang=` non decide più: rimanda al selettore di tutta l'applicazione, o si finirebbe
        // con la Guida in una lingua e il resto della pagina nell'altra.
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var risposta = await client.GetAsync("/services/vsop/guide?lang=en");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, risposta.StatusCode);
        Assert.Contains("culture=en", risposta.Headers.Location?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Il contenuto della briciola di pane, o null se la pagina non ne ha una.</summary>
    private static string? Briciola(string html) =>
        Regex.Match(html, @"<div class=""breadcrumb"">(.*?)</div>", RegexOptions.Singleline) is { Success: true } m
            ? m.Groups[1].Value
            : null;

    public sealed class RegoleFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"vipi-e2e-regole-{Guid.NewGuid():N}.db");

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureHostConfiguration(cfg => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Vipi"] = $"Data Source={_dbPath}",
                // Il sottotitolo del marchio viene di qui: senza, varrebbe il default del tipo.
                ["Division:Name"] = "Italy",
            }));
            Environment.SetEnvironmentVariable("VipiAuth__Enabled", "false");

            var host = base.CreateHost(builder);

            using (var scope = host.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<VipiDbContext>();
                db.Accs.Add(new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" });
                db.SaveChanges();
            }

            host.Services.GetRequiredService<IStationCatalogVersion>().Bump();

            return host;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { /* best-effort cleanup */ }
        }
    }
}
