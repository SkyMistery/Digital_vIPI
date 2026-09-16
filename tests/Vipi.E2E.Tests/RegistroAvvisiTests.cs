using Microsoft.Extensions.Logging;
using Vipi.Host;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// Il registro degli avvisi (<see cref="RegistroAvvisi"/>): Warning ed errori del processo in <c>diagnostica/</c>, col
/// contesto delle ultime richieste. Si prova la decisione e il testo con una scrittura finta, e i <b>filtri</b> con
/// una fabbrica di log configurata come la produzione: è lì che il registro può diventare muto senza che niente
/// fallisca.
/// </summary>
public class RegistroAvvisiTests
{
    private sealed class Banco
    {
        public DateTime Ora = new(2026, 9, 16, 20, 0, 0, DateTimeKind.Utc);
        public string File = "";
        public RegistroAvvisi Registro { get; }
        public ILoggerFactory Fabbrica { get; }

        public Banco(string? esistente = null)
        {
            File = esistente ?? "";
            Registro = new RegistroAvvisi(() => Ora, (testo, intestazione) =>
            {
                if (File.Length == 0 && intestazione is not null) File = intestazione;
                File += testo;
            }, () => File);

            // Come appsettings.json in produzione: Microsoft.AspNetCore e i comandi di EF a Warning.
            Fabbrica = LoggerFactory.Create(b =>
            {
                b.SetMinimumLevel(LogLevel.Information);
                b.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
                b.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
                b.AddProvider(Registro);
                foreach (var (categoria, livello) in RegistroAvvisi.Filtri)
                    b.AddFilter<RegistroAvvisi>(categoria, livello);
            });
        }

        public void Richiesta(string percorso, int esito = 200, string query = "")
        {
            var stato = new List<KeyValuePair<string, object?>>
            {
                new("ElapsedMilliseconds", 12.4), new("StatusCode", esito), new("Method", "GET"),
                new("PathBase", ""), new("Path", percorso), new("QueryString", query),
                new("{OriginalFormat}", "Request finished {Method} {Path}{QueryString} - {StatusCode}"),
            };
            Fabbrica.CreateLogger(RegistroAvvisi.CategoriaRichieste).Log(LogLevel.Information, new EventId(2, "RequestFinished"),
                (IReadOnlyList<KeyValuePair<string, object?>>)stato, null, (_, _) => $"Request finished GET {percorso}{query} - {esito}");
        }
    }

    [Fact]
    public void Un_avviso_si_scrive_con_le_ultime_dieci_richieste_e_le_righe_nostre()
    {
        var b = new Banco();
        for (var i = 1; i <= 12; i++) b.Richiesta($"/services/vsop/p{i}");
        b.Richiesta("/vsop/health/ready");                       // il ping non ruba un posto
        b.Richiesta("/_content/Vipi.Ui/vipi-theme.css");         // nemmeno un file statico
        b.Fabbrica.CreateLogger("Vipi.Import").LogInformation("Import ACC partito per {Acc}", "LIRR");

        b.Fabbrica.CreateLogger("Vipi.Import").LogWarning("IVAO ha risposto {Codice} per {Acc}", 429, "LIRR");

        Assert.Contains("AVVISO · firma ", b.File);
        Assert.Contains("IVAO ha risposto 429 per LIRR", b.File);
        Assert.Contains("GET /services/vsop/p12 → 200 12 ms", b.File);
        Assert.Contains("/services/vsop/p3 ", b.File);
        Assert.DoesNotContain("/services/vsop/p2 ", b.File);  // l'undicesima più vecchia è uscita
        Assert.DoesNotContain("/vsop/health", b.File);
        Assert.DoesNotContain("vipi-theme.css", b.File);
        Assert.Contains("Import · Import ACC partito per LIRR", b.File);
    }

    /// <summary>🔴 La query su /signin-oidc è il code OAuth: non deve arrivare nel file per nessuna strada.</summary>
    [Fact]
    public void Le_stringhe_di_query_non_entrano_mai()
    {
        var b = new Banco();
        b.Richiesta("/signin-oidc", 302, "?code=SEGRETO&state=ALTRO");

        b.Fabbrica.CreateLogger("Vipi.Auth.Ivao")
            .LogWarning("Ritorno strano su https://atc.it.ivao.aero/signin-oidc?code=SEGRETO2 e su /signin-oidc?code=SEGRETO3");

        Assert.DoesNotContain("SEGRETO", b.File);
        Assert.Contains("/signin-oidc?…", b.File);
        Assert.Contains("GET /signin-oidc → 302", b.File);
    }

    /// <summary>
    /// 🔴 I filtri, con la configurazione di produzione. Senza la regola del provider le richieste non arriverebbero
    /// (Microsoft.AspNetCore a Warning); con una regola «Information per tutti» arriverebbe il testo di ogni query EF.
    /// </summary>
    [Fact]
    public void I_filtri_lasciano_passare_le_richieste_ma_non_le_query_di_EF()
    {
        var b = new Banco();
        var ef = b.Fabbrica.CreateLogger("Microsoft.EntityFrameworkCore.Database.Command");
        Assert.False(ef.IsEnabled(LogLevel.Information));
        Assert.True(b.Fabbrica.CreateLogger(RegistroAvvisi.CategoriaRichieste).IsEnabled(LogLevel.Information));
        Assert.False(b.Fabbrica.CreateLogger("Microsoft.AspNetCore.Routing").IsEnabled(LogLevel.Information));

        b.Richiesta("/services/vsop/lirr");
        ef.LogInformation("Executed DbCommand SELECT * FROM Segreti");
        ef.LogWarning("Comando lento");

        Assert.Contains("GET /services/vsop/lirr → 200", b.File);
        Assert.DoesNotContain("SELECT", b.File);
        Assert.Contains("Comando lento", b.File);
    }

    /// <summary>
    /// Visto dal vivo: su un 404 ASP.NET scrive anche «Request reached the end of the middleware pipeline» (evento 16),
    /// che porta percorso ed esito come «Request finished». Contata due volte, la stessa richiesta ruba un posto.
    /// </summary>
    [Fact]
    public void Una_richiesta_si_conta_una_volta_sola()
    {
        var b = new Banco();
        b.Richiesta("/services/vsop/non-esiste", 404);
        var fineDellaPipeline = new List<KeyValuePair<string, object?>>
        {
            new("Method", "GET"), new("Path", "/services/vsop/non-esiste"), new("StatusCode", 404),
            new("{OriginalFormat}", "Request reached the end of the middleware pipeline ..."),
        };
        b.Fabbrica.CreateLogger(RegistroAvvisi.CategoriaRichieste).Log(LogLevel.Information, new EventId(16, "RequestPipelineEnd"),
            (IReadOnlyList<KeyValuePair<string, object?>>)fineDellaPipeline, null, (_, _) => "fine della pipeline");

        b.Fabbrica.CreateLogger("Vipi.Import").LogWarning("avviso");

        Assert.Single(b.File.Split('\n'), r => r.Contains("/services/vsop/non-esiste"));
    }

    [Fact]
    public void Le_righe_informative_non_scrivono_niente_da_sole()
    {
        var b = new Banco();
        b.Richiesta("/services/vsop");
        b.Fabbrica.CreateLogger("Vipi.Import").LogInformation("tutto bene");
        Assert.Equal("", b.File);
    }

    /// <summary>
    /// Il processo rinasce ogni cinquanta secondi: la stessa firma si scrive intera una volta al giorno, le
    /// ripetizioni una riga all'ora — e la memoria sta nel FILE, perché un processo nuovo non sa niente.
    /// </summary>
    [Fact]
    public void Le_ripetizioni_valgono_una_riga_all_ora_anche_fra_processi_diversi()
    {
        var b = new Banco();
        void Avviso(Banco x, string acc) => x.Fabbrica.CreateLogger("Vipi.Import").LogWarning("Rinuncio a {Acc}", acc);

        Avviso(b, "LIRR");
        var dopoLaPrima = b.File;
        b.Ora = b.Ora.AddMinutes(10);
        Avviso(b, "LIMM");                                           // stessa firma: valori diversi, stesso modello
        Assert.Equal(dopoLaPrima, b.File);

        // Un processo NUOVO, un'ora e mezza dopo, che legge il file del vecchio.
        var nuovo = new Banco(b.File) { Ora = b.Ora.AddMinutes(80) };
        Avviso(nuovo, "LIBB");
        var righe = nuovo.File.Split('\n');
        Assert.Single(righe, r => r.StartsWith("ANCORA ", StringComparison.Ordinal));
        Assert.Contains(righe, r => r.StartsWith("ANCORA ", StringComparison.Ordinal) && r.Contains("Rinuncio a LIBB"));
        Assert.Single(righe, r => r.Contains("AVVISO · firma") && !r.StartsWith("ANCORA", StringComparison.Ordinal));

        // Il giorno dopo si riscrive intera: il contesto di oggi può essere diverso da quello di ieri.
        var domani = new Banco(nuovo.File) { Ora = new DateTime(2026, 9, 17, 8, 0, 0, DateTimeKind.Utc) };
        Avviso(domani, "LIPP");
        Assert.Equal(2, domani.File.Split('\n').Count(r => r.Contains("AVVISO · firma") && !r.StartsWith("ANCORA", StringComparison.Ordinal)));
    }

    [Fact]
    public void Avvisi_diversi_hanno_firme_diverse()
    {
        var b = new Banco();
        b.Fabbrica.CreateLogger("Vipi.Import").LogWarning("Rinuncio a {Acc}", "LIRR");
        b.Fabbrica.CreateLogger("Vipi.Import").LogWarning("Sorgente muta per {Acc}", "LIRR");
        b.Fabbrica.CreateLogger("Vipi.Altro").LogWarning("Rinuncio a {Acc}", "LIRR");

        Assert.Equal(3, b.File.Split('\n').Count(r => r.Contains("AVVISO · firma")));
    }

    /// <summary>Una richiesta fallita ha già lo stack in errori-richieste.txt: qui il contesto e il rimando, non una seconda copia.</summary>
    [Fact]
    public void Un_errore_con_lo_stack_altrove_porta_il_contesto_e_il_rimando()
    {
        var b = new Banco();
        b.Richiesta("/services/vsop/libb/editor");
        Exception guasto;
        try { throw new InvalidOperationException("A second operation was started"); }
        catch (Exception e) { guasto = e; }

        b.Fabbrica.CreateLogger(DiagnosticaErrori.CategoriaLog).LogError(guasto, "Richiesta fallita — {Percorso}", "/services/vsop/libb/editor");

        Assert.Contains("ERRORE · firma", b.File);
        Assert.Contains("InvalidOperationException: A second operation was started", b.File);
        Assert.Contains($"lo stack sta in {DiagnosticaErrori.NomeFile}", b.File);
        Assert.DoesNotContain("   at ", b.File);
        Assert.Contains("GET /services/vsop/libb/editor → 200", b.File);
    }

    [Fact]
    public void Un_errore_senza_stack_altrove_porta_lo_stack_qui()
    {
        var b = new Banco();
        Exception guasto;
        try { throw new TimeoutException("IVAO non risponde"); }
        catch (Exception e) { guasto = e; }

        b.Fabbrica.CreateLogger("Vipi.Import").LogError(guasto, "Import fallito");

        Assert.Contains("System.TimeoutException: IVAO non risponde", b.File);
        Assert.Contains("   at ", b.File);
    }

    [Fact]
    public void Le_firme_si_rileggono_dal_file()
    {
        var firme = RegistroAvvisi.FirmeGiaScritte(
            "intestazione\n2026-09-16 20:00:00 UTC · AVVISO · firma 0123456789ab\nANCORA 2026-09-16 21:30:00 UTC · AVVISO · firma 0123456789ab · x\n");
        Assert.Equal(new DateTime(2026, 9, 16, 21, 30, 0, DateTimeKind.Utc), firme["0123456789ab"]);
    }
}
