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
/// L'ACC che si sta guardando è evidenziato nella barra in cima.
///
/// <para><b>Perché un test e perché qui.</b> Fino al 23 agosto 2026 non lo era, e nessuno se n'era accorto per
/// un anno: <c>SopLayout</c> leggeva il codice ACC contando i segmenti dell'indirizzo a mano e confrontando il
/// primo con la stringa <c>"vsop"</c>, che dal rename del 22 agosto (<c>/vsop</c> → <c>/services/vsop</c>) vale
/// <c>"services"</c>. Il codice compilava, i test unitari passavano, e la barra semplicemente non segnava più
/// nulla — né la classe <c>active</c> né <c>aria-current</c>.</para>
///
/// <para>È un difetto che si vede solo nell'<b>HTML servito</b>: sta nell'incontro fra la rotta registrata e il
/// layout che la rilegge, e nessuno dei due da solo è sbagliato. Per questo il test vive nel progetto E2E, che
/// la pagina la chiede davvero, e non fra i test di componente.</para>
///
/// <para>⚠️ Si asserisce su <c>aria-current="page"</c> e non sulla classe CSS: la classe è l'aspetto — cambia
/// con il foglio di stile — mentre <c>aria-current</c> è l'informazione, ed è l'unica cosa che arriva a chi la
/// barra non la vede.</para>
/// </summary>
public sealed class TopbarAccNavTests : IClassFixture<TopbarAccNavTests.AccSeededFactory>
{
    private readonly AccSeededFactory _factory;
    public TopbarAccNavTests(AccSeededFactory factory) => _factory = factory;

    [Fact]
    public async Task L_acc_della_rotta_e_segnato_come_pagina_corrente()
    {
        var html = await _factory.CreateClient().GetStringAsync("/services/vsop/lirr");

        // Il link dell'ACC visitato porta aria-current="page"; l'altro no. Si guardano i due <a> dell'elenco
        // ACC, che sono gli unici a puntare a /services/vsop/{codice} senza altro dopo.
        Assert.True(HaAriaCurrent(html, "lirr"),
            "l'ACC della rotta non è segnato come pagina corrente: la barra non dice più dove ci si trova.\n" +
            "È il difetto del 22 agosto 2026 — il prefisso è cambiato e SopLayout contava i segmenti a mano.\n" +
            Estratto(html));

        Assert.False(HaAriaCurrent(html, "limm"),
            "anche un ACC che non è quello della rotta risulta corrente: il confronto non discrimina più.\n" +
            Estratto(html));
    }

    [Fact]
    public async Task Fuori_dalle_pagine_per_acc_nessuno_e_corrente()
    {
        // La home dei servizi non sta sotto nessun ACC: segnarne uno sarebbe peggio che non segnarne nessuno.
        var html = await _factory.CreateClient().GetStringAsync("/services/vsop");

        Assert.False(HaAriaCurrent(html, "lirr"));
        Assert.False(HaAriaCurrent(html, "limm"));
    }

    /// <summary>Esiste un &lt;a&gt; verso <c>/services/vsop/{codice}</c> che porta <c>aria-current="page"</c>?</summary>
    private static bool HaAriaCurrent(string html, string codice) =>
        Regex.Matches(html, @"<a\b[^>]*>", RegexOptions.IgnoreCase)
            .Select(m => m.Value)
            .Where(tag => Regex.IsMatch(tag, $@"href\s*=\s*[""']/services/vsop/{Regex.Escape(codice)}[""']", RegexOptions.IgnoreCase))
            .Any(tag => tag.Contains("aria-current=\"page\"", StringComparison.OrdinalIgnoreCase));

    /// <summary>I tag dell'elenco ACC, per leggere il messaggio di errore senza aprire il browser.</summary>
    private static string Estratto(string html) =>
        "  " + string.Join("\n  ", Regex.Matches(html, @"<a\b[^>]*href\s*=\s*[""']/services/vsop/[a-z]{4}[""'][^>]*>", RegexOptions.IgnoreCase)
            .Select(m => m.Value)
            .Distinct());

    /// <summary>
    /// Fabbrica con due ACC in archivio: senza, l'elenco in barra è vuoto e il test passerebbe per assenza
    /// di link — cioè proprio il caso che deve distinguere.
    /// </summary>
    public sealed class AccSeededFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"vipi-e2e-topbar-{Guid.NewGuid():N}.db");

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
                db.Accs.AddRange(
                    new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" },
                    new Acc { Code = "LIMM", Name = "Milano", CountryPrefix = "LI" });
                db.SaveChanges();
            }

            // ⚠️ Il catalogo delle stazioni è in cache e il contatore è singleton: scritto il DB fuori dal giro
            // normale, senza questo l'elenco in barra resterebbe quello di prima (cioè vuoto).
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
