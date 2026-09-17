using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vipi.Host;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// Il registro del giorno (§A59): <see cref="RegistroGiornaliero"/> (file per giorno, sette giorni, tetto),
/// <see cref="RegistroRichieste"/> (una riga per richiesta) e <see cref="RegistroInformativo"/> (le righe <c>Vipi.*</c>).
/// Lo scrittore si prova su una cartella temporanea vera; la riga di richiesta sull'host vero, perché la rotta viene
/// dall'endpoint e un middleware messo nel posto sbagliato non la vedrebbe.
/// </summary>
public sealed class RegistroDelGiornoTests : IDisposable
{
    private static readonly DateTime Oggi = new(2026, 9, 17, 10, 0, 0, DateTimeKind.Utc);
    private readonly string _cartella = Directory.CreateTempSubdirectory("vipi-giorno-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_cartella, recursive: true); } catch { /* best-effort */ }
    }

    private RegistroGiornaliero Scrittore(long tetto = RegistroGiornaliero.TettoByte) =>
        new("richieste", "tsv", () => "# testa\ncolonne\n", () => _cartella, tetto);

    private string Leggi(DateTime giorno) =>
        File.ReadAllText(Path.Combine(_cartella, $"richieste-{giorno:yyyy-MM-dd}.tsv"));

    [Fact]
    public void Un_file_per_giorno_con_la_testa_una_volta_sola_e_il_BOM()
    {
        var s = Scrittore();
        s.Scrivi(Oggi, "uno");
        s.Scrivi(Oggi.AddHours(1), "due");
        s.Scrivi(Oggi.AddDays(1).Date.AddMinutes(1), "tre");      // passata la mezzanotte UTC: file nuovo

        Assert.Equal("# testa\ncolonne\nuno\ndue\n", Leggi(Oggi));
        Assert.Equal("# testa\ncolonne\ntre\n", Leggi(Oggi.AddDays(1)));
        var byte_ = File.ReadAllBytes(Path.Combine(_cartella, "richieste-2026-09-17.tsv"));
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, byte_[..3]);
    }

    /// <summary>Sette giorni: oggi e i sei prima. Gli altri file della cartella — e gli altri prefissi — non si toccano.</summary>
    [Fact]
    public void Si_tengono_sette_giorni_e_non_si_tocca_nient_altro()
    {
        foreach (var n in new[] { 1, 6, 7, 30 })
            File.WriteAllText(Path.Combine(_cartella, $"richieste-{Oggi.AddDays(-n):yyyy-MM-dd}.tsv"), "x");
        File.WriteAllText(Path.Combine(_cartella, $"log-{Oggi.AddDays(-30):yyyy-MM-dd}.txt"), "x");
        File.WriteAllText(Path.Combine(_cartella, "richieste-copia.tsv"), "x");
        File.WriteAllText(Path.Combine(_cartella, "avvii.txt"), "x");

        Scrittore().Scrivi(Oggi, "riga");

        var rimasti = Directory.GetFiles(_cartella).Select(Path.GetFileName).OrderBy(x => x).ToArray();
        Assert.Equal(new[]
        {
            "avvii.txt", "log-2026-08-18.txt", "richieste-2026-09-11.tsv", "richieste-2026-09-16.tsv",
            "richieste-2026-09-17.tsv", "richieste-copia.tsv",
        }, rimasti);
    }

    /// <summary>
    /// Il tetto si legge dal FILE: due scrittori (due processi) non lo raddoppiano, e la riga di troncamento esce una
    /// volta sola.
    /// </summary>
    [Fact]
    public void Oltre_il_tetto_una_riga_di_troncamento_e_poi_silenzio_anche_da_due_scrittori()
    {
        var a = Scrittore(tetto: 100);
        var b = Scrittore(tetto: 100);
        for (var i = 0; i < 30; i++)
        {
            a.Scrivi(Oggi, $"riga a {i:00}");
            b.Scrivi(Oggi, $"riga b {i:00}");
        }

        var testo = Leggi(Oggi);
        Assert.Single(testo.Split('\n'), r => r.StartsWith(RegistroGiornaliero.Troncato, StringComparison.Ordinal));
        Assert.EndsWith("il resto del giorno non si scrive.\n", testo);
        Assert.True(testo.Length < 200, testo);
    }

    [Fact]
    public void Una_cartella_che_non_c_e_non_solleva()
    {
        new RegistroGiornaliero("richieste", "tsv", () => "", () => null).Scrivi(Oggi, "riga");
        new RegistroGiornaliero("richieste", "tsv", () => "", () => Path.Combine(_cartella, "non", "esiste")).Scrivi(Oggi, "riga");
    }

    [Fact]
    public void La_riga_di_richiesta_ha_nove_colonne_e_niente_tab_rubati()
    {
        var riga = RegistroRichieste.Riga(Oggi.AddMilliseconds(412), 4182, "1.30.2 · 76aceb3", "GET",
            "/services/vsop/{Acc}", "/services/vsop/LI\tRR", 200, 142.6, autenticato: true);

        Assert.Equal("10:00:00.412\t4182\t1.30.2 · 76aceb3\tGET\t/services/vsop/{Acc}\t/services/vsop/LI RR\t200\t143\t1", riga);
        Assert.Equal(RegistroRichieste.Colonne.Split('\t').Length, riga.Split('\t').Length);
        Assert.Contains("\t-\t", RegistroRichieste.Riga(Oggi, 1, "v", "GET", null, "/nulla", 404, 1, false));
    }

    /// <summary>
    /// Sull'host vero: la rotta viene dall'endpoint (<c>{Acc}</c>, non <c>LIRR</c>), la query non entra, il ping non
    /// lascia righe. ⚠️ La riga si scrive a risposta FINITA, quindi si aspetta.
    /// </summary>
    [Fact]
    public async Task Una_pagina_lascia_la_riga_con_la_rotta_e_il_ping_no()
    {
        var righe = new ConcurrentQueue<string>();
        using var fabbrica = new Fabbrica(new RegistroRichieste((_, r) => righe.Enqueue(r), "prova"));
        var client = fabbrica.CreateClient();

        await client.GetAsync("/vsop/health/ready");
        await client.GetAsync("/services/vsop/LIRR?code=SEGRETISSIMO");

        for (var i = 0; i < 100 && !righe.Any(r => r.Contains("LIRR")); i++) await Task.Delay(50);

        var riga = Assert.Single(righe, r => r.Contains("/services/vsop/LIRR"));
        var c = riga.Split('\t');
        Assert.Equal("prova", c[2]);
        Assert.Equal("GET", c[3]);
        Assert.Equal("/services/vsop/{Acc}", c[4]);
        Assert.Equal("/services/vsop/LIRR", c[5]);
        Assert.Equal("0", c[8]);
        Assert.DoesNotContain(righe, r => r.Contains("SEGRETISSIMO") || r.Contains("/vsop/health"));
    }

    /// <summary>
    /// ⚠️ Il cancello che conta: i filtri sono del provider. Con la configurazione di produzione passano le righe
    /// <c>Vipi.*</c> informative, e NON passano né <c>Microsoft.AspNetCore</c> né il testo delle query di EF — che con
    /// Default=Information arriverebbe, se la regola «spento per tutti» mancasse.
    /// </summary>
    [Fact]
    public void Il_log_del_giorno_prende_solo_le_righe_nostre()
    {
        var righe = new List<string>();
        var registro = new RegistroInformativo(() => Oggi, (_, r) => righe.Add(r));
        using var fabbrica = LoggerFactory.Create(b =>
        {
            b.SetMinimumLevel(LogLevel.Information);
            b.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
            b.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
            b.AddProvider(registro);
            foreach (var (categoria, livello) in RegistroInformativo.Filtri)
                b.AddFilter<RegistroInformativo>(categoria, livello);
        });

        fabbrica.CreateLogger("Vipi.Infrastructure.AtcPollingHostedService").LogInformation("Poll IVAO: {N} ATC", 3);
        fabbrica.CreateLogger("Vipi.Import").LogDebug("dettaglio che non serve");
        fabbrica.CreateLogger("Vipi.Import").LogWarning(new InvalidOperationException("riga uno\nriga due"),
            "Rinuncia su {Url}", "https://api.ivao.aero/v2/x?token=SEGRETO");
        fabbrica.CreateLogger("Microsoft.EntityFrameworkCore.Database.Command").LogInformation("SELECT * FROM Accs");
        fabbrica.CreateLogger("Microsoft.Hosting.Lifetime").LogInformation("Application started");
        fabbrica.CreateLogger("System.Net.Http.HttpClient.Ivao").LogInformation("Sending HTTP request");

        Assert.Equal(2, righe.Count);
        Assert.Equal($"10:00:00.000 {Environment.ProcessId} INF AtcPollingHostedService · Poll IVAO: 3 ATC", righe[0]);
        Assert.StartsWith($"10:00:00.000 {Environment.ProcessId} WRN Import · Rinuncia su https://api.ivao.aero/v2/x?…", righe[1]);
        Assert.EndsWith("‖ InvalidOperationException: riga uno ⏎ riga due", righe[1]);
        Assert.DoesNotContain(righe, r => r.Contains("SEGRETO"));
    }

    private sealed class Fabbrica(RegistroRichieste registro) : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"vipi-giorno-{Guid.NewGuid():N}.db");

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseEnvironment("Staging");
            builder.ConfigureHostConfiguration(cfg => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Vipi"] = $"Data Source={_dbPath}",
            }));
            builder.ConfigureServices(s =>
            {
                s.RemoveAll<RegistroRichieste>();
                s.AddSingleton(registro);
            });
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
