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
/// Dalla barra si passa da una lingua all'altra.
///
/// <para><b>Perché un test e perché qui.</b> Fino al 28 agosto 2026 il meccanismo c'era <b>tutto</b> —
/// <c>?culture=</c> risolto per richiesta, il cookie che lo fa sopravvivere al circuito, i documenti tradotti,
/// il badge «non revisionata» — e mancava l'unica cosa che il lettore poteva usare: <b>il comando</b>. La
/// lingua si poteva chiedere solo scrivendola a mano nell'indirizzo. Nessun test poteva accorgersene, perché
/// ogni pezzo, da solo, funzionava.</para>
///
/// <para>È un difetto che si vede solo nell'<b>HTML servito</b>, come quello della barra ACC (vedi
/// <see cref="TopbarAccNavTests"/>): sta nell'incontro fra il layout e la localizzazione, e nessuno dei due
/// da solo è sbagliato. Per questo il test vive qui e la pagina la chiede davvero.</para>
/// </summary>
public sealed class SelettoreLinguaTests : IClassFixture<SelettoreLinguaTests.LinguaFactory>
{
    private readonly LinguaFactory _factory;
    public SelettoreLinguaTests(LinguaFactory factory) => _factory = factory;

    [Fact]
    public async Task La_barra_offre_UNA_via_per_ogni_lingua_servita()
    {
        var html = await _factory.CreateClient().GetStringAsync("/services/vsop");

        foreach (var lingua in LinguaDiLettura.Supportate)
            Assert.True(Links(html).Any(a => a.Contains($"culture={lingua}", StringComparison.OrdinalIgnoreCase)),
                $"dalla barra non si può chiedere la lingua «{lingua}»: il comando non c'è, e la lingua si " +
                "potrebbe cambiare solo scrivendola nell'indirizzo.\n" + Estratto(html));
    }

    [Fact]
    public async Task La_lingua_in_cui_si_sta_leggendo_e_segnata()
    {
        // ⚠️ Si asserisce su `aria-current` e non sulla classe CSS: la classe è l'aspetto, `aria-current` è
        // l'informazione — ed è l'unica cosa che arriva a chi la barra non la vede.
        var html = await _factory.CreateClient().GetStringAsync("/services/vsop?culture=en");

        // ⚠️ I gruppi sono DUE — quello in barra e quello dentro il «☰» — e si rendono sempre tutti e due:
        // a nasconderne uno è il CSS, per scaglione. Quindi non si conta quanti sono i link segnati, si
        // guarda CHE COSA segnano: tutti la lingua in cui si sta leggendo, e nessuno l'altra.
        var lingue = Links(html)
            .Where(a => a.Contains("hreflang=", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var attivi = lingue.Where(a => a.Contains("aria-current=\"true\"", StringComparison.OrdinalIgnoreCase)).ToList();

        Assert.NotEmpty(attivi);
        Assert.All(attivi, a => Assert.Contains("hreflang=\"en\"", a, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(lingue,
            a => a.Contains("hreflang=\"it\"", StringComparison.OrdinalIgnoreCase)
              && a.Contains("aria-current=", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Cambiare_lingua_non_fa_perdere_DOVE_SEI()
    {
        // ⚠️ Il difetto che questo test esclude: un link fisso a `?culture=en` riporterebbe all'ELENCO degli
        // aeroporti invece che a questo aeroporto. Cambiare lingua deve cambiare la lingua e basta.
        var html = await _factory.CreateClient().GetStringAsync("/services/vsop/lirr/airports?icao=LIRF");

        var link = Links(html).FirstOrDefault(a => a.Contains("hreflang=\"en\"", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(link);
        Assert.Contains("icao=LIRF", link, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/services/vsop/lirr/airports", link, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task La_lingua_gia_chiesta_non_si_somma_a_quella_nuova()
    {
        // Due `culture=` nello stesso indirizzo non danno errore: a decidere sarebbe l'ordine, cioè il caso.
        var html = await _factory.CreateClient().GetStringAsync("/services/vsop?culture=it");

        foreach (var link in Links(html).Where(a => a.Contains("hreflang=", StringComparison.OrdinalIgnoreCase)))
            Assert.Single(Regex.Matches(link, @"culture=", RegexOptions.IgnoreCase));
    }

    [Fact]
    public async Task Chiesta_in_inglese_la_pagina_risponde_in_inglese()
    {
        var client = _factory.CreateClient();

        var italiano = await client.GetStringAsync("/services/vsop?culture=it");
        var inglese = await client.GetStringAsync("/services/vsop?culture=en");

        // Una stringa del chrome, che c'è su ogni pagina: se questa non cambia, non ha cambiato niente.
        Assert.Contains("aria-label=\"Lingua\"", italiano, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("aria-label=\"Language\"", inglese, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>I tag &lt;a&gt; della pagina, interi: il selettore è fatto di link, non di tasti.</summary>
    private static IEnumerable<string> Links(string html) =>
        Regex.Matches(html, @"<a\b[^>]*>", RegexOptions.IgnoreCase).Select(m => m.Value);

    private static string Estratto(string html) =>
        "  " + string.Join("\n  ", Links(html).Where(a => a.Contains("hreflang", StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// Un ACC in archivio: la barra si rende comunque, ma senza questo l'elenco è vuoto e la pagina
    /// dell'aeroporto non avrebbe un ACC da risolvere.
    /// </summary>
    public sealed class LinguaFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"vipi-e2e-lingua-{Guid.NewGuid():N}.db");

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureHostConfiguration(cfg => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Vipi"] = $"Data Source={_dbPath}",
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
