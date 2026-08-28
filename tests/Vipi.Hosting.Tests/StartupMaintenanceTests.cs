using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vipi.Application.Diagnostics;
using Vipi.Hosting;
using Xunit;

namespace Vipi.Hosting.Tests;

/// <summary>
/// L'isolamento delle manutenzioni d'avvio non critiche.
///
/// <para><b>Cosa protegge.</b> Quelle quattro passate sono l'ultimo pezzo di <c>Program.cs</c> prima che
/// l'app cominci a servire, e giravano nude. Con <c>Restart=always</c> e <c>RestartSec=10</c> in
/// <c>vipi.service</c>, un guasto lì non è un degrado: è un <b>ciclo di riavvii</b> ogni dieci secondi, cioè
/// il sito giù per un difetto in una riconciliazione di dati storici.</para>
///
/// <para><b>Come si prova senza costruire un guasto finto.</b> Un host che non ha i servizi vIPI registrati
/// fa fallire <i>tutte</i> le passate — <c>GetRequiredService</c> non trova niente — che è la forma più
/// onesta del caso «una di queste esplode»: l'eccezione è vera e viene da dentro la passata, non da un mock
/// che l'abbiamo messa lì.</para>
/// </summary>
public class StartupMaintenanceTests
{
    private static IHost HostSenzaServiziVipi(out IStartupMaintenanceReport report)
    {
        var host = new HostBuilder()
            .ConfigureServices(s => s.AddSingleton<IStartupMaintenanceReport, StartupMaintenanceReport>())
            .Build();

        report = host.Services.GetRequiredService<IStartupMaintenanceReport>();
        return host;
    }

    [Fact]
    public void Un_guasto_in_una_passata_non_impedisce_lavvio()
    {
        using var host = HostSenzaServiziVipi(out _);

        // Non deve lanciare: è tutto il punto. Prima, la prima passata rotta portava giù l'avvio.
        var ex = Record.Exception(() => host.RunVipiStartupMaintenance());

        Assert.Null(ex);
    }

    [Fact]
    public void Le_passate_successive_girano_lo_stesso()
    {
        using var host = HostSenzaServiziVipi(out var report);

        host.RunVipiStartupMaintenance();

        // Tutte e CINQUE hanno provato e fallito: se il guasto della prima avesse fermato la sequenza, qui ci
        // sarebbe una segnalazione sola. Il numero segue le passate — quattro dal 17 agosto 2026, quando il
        // travaso dei flussi in accordi e' stato tolto con le tabelle che leggeva; cinque dal 28 agosto, con
        // il caricamento delle promozioni a mano — ed e' voluto che aggiungerne o toglierne una faccia
        // fallire questo test: e' il promemoria che una passata nuova va anche isolata, o il suo guasto
        // porterebbe giu' l'avvio. Questo test ha gia' fatto il suo mestiere due volte.
        Assert.Equal(5, report.Findings.Count);
    }

    /// <summary>
    /// Il guasto non deve fermarsi al log. «Logga e prosegui» che finisce in un file che nessuno apre è un
    /// modo per non accorgersene mai: da qui la segnalazione entra nel report di consistenza, quindi nella
    /// diagnostica e in <c>/vsop/health</c> → Degraded.
    /// </summary>
    [Fact]
    public void Ogni_guasto_finisce_nella_diagnostica_col_nome_della_passata()
    {
        using var host = HostSenzaServiziVipi(out var report);

        host.RunVipiStartupMaintenance();

        Assert.All(report.Findings, f =>
        {
            Assert.Equal(StartupMaintenanceReport.Category, f.Category);
            Assert.Equal(ConsistencySeverity.Error, f.Severity);
            Assert.False(string.IsNullOrWhiteSpace(f.Entity));
        });

        Assert.Contains(report.Findings, f => f.Entity.Contains("riconciliazioni", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Findings, f => f.Entity.Contains("settori", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Findings, f => f.Entity.Contains("release", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Findings, f => f.Entity.Contains("promozioni", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Un_avvio_intero_non_lascia_segnalazioni()
        => Assert.Empty(new StartupMaintenanceReport().Findings);

    /// <summary>
    /// Il messaggio deve dire che la passata è idempotente e che un riavvio la rifà: è l'informazione che
    /// serve a chi legge la diagnostica per decidere se deve fare qualcosa o soltanto riavviare.
    /// </summary>
    [Fact]
    public void Il_messaggio_dice_cosa_farne()
    {
        var report = new StartupMaintenanceReport();
        report.Record("passata di prova", new InvalidOperationException("motivo vero"));

        var f = Assert.Single(report.Findings);
        Assert.Contains("motivo vero", f.Detail, StringComparison.Ordinal);
        Assert.Contains("riavvio", f.Detail, StringComparison.OrdinalIgnoreCase);
    }
}
