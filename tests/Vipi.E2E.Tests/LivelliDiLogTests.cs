using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Vipi.E2E.Tests;

/// <summary>
/// I livelli di log che valgono in PRODUZIONE, che è il posto in cui nessuno li guarda.
///
/// <para><b>Il difetto, misurato il 27 agosto 2026.</b> <c>appsettings.Production.json</c> non ha una
/// sezione <c>Logging</c>, quindi valeva quella del file base — dove c'era <c>Default: Information</c> e la
/// categoria di EF non era nominata. A Information EF scrive <b>il testo di ogni query, con i parametri</b>,
/// per ogni richiesta. Stesso binario, stesso database, 210 aperture di una pagina da 174 query:</para>
///
/// <code>
///   Warning           2 614 byte di log
///   Information   1 036 781 byte di log      — quattrocento volte tanto
/// </code>
///
/// <para>⚠️ Il tempo di risposta, invece, <b>non cambia in modo misurabile</b>. Una prima misura diceva
/// +45%, ma era un giro solo: rimisurata alternando i due processi, l'ordine si inverte da un giro
/// all'altro — è rumore della macchina. Vale la pena scriverlo perché la tentazione, davanti a un numero
/// che fa comodo, è di non rimisurarlo. Questa riga esiste per il disco e l'I/O, non per i millisecondi.</para>
///
/// <para>⚠️ Non è un test sul contenuto di un file: è un test sul <b>livello efficace</b> che il sistema di
/// log calcola. Le regole si compongono per prefisso e la più specifica vince — scrivere la chiave giusta
/// nel posto sbagliato (per esempio in <c>appsettings.Production.json</c>, che vincerebbe su quello base)
/// darebbe un file «giusto» a leggerlo e un comportamento diverso. Qui si chiede al filtro, non al JSON.</para>
/// </summary>
public sealed class LivelliDiLogTests
{
    /// <summary>La categoria che EF usa per il testo dell'SQL. Il nome è pubblico e stabile (DbLoggerCategory).</summary>
    private const string SqlDiEf = "Microsoft.EntityFrameworkCore.Database.Command";

    /// <summary>
    /// In produzione l'SQL non si scrive. La soglia è Warning: a quel livello EF non emette il comando
    /// (che è Information) ma continua a dire quando una query fallisce, che è la cosa che serve davvero.
    /// </summary>
    [Fact]
    public void In_produzione_lSQL_di_EF_non_finisce_nei_log()
    {
        var livello = LivelloEfficace(SqlDiEf, "Production");

        Assert.True(livello >= LogLevel.Warning,
            $"In produzione «{SqlDiEf}» è a {livello}: EF tornerebbe a scrivere il testo di ogni query su " +
            "disco: un megabyte ogni duecento pagine. Vedi la sezione Logging di appsettings.json — e " +
            "controlla che appsettings.Production.json non ne abbia una propria, che vincerebbe.");
    }

    /// <summary>Il polling ATC gira per sempre: quattro righe per chiamata sono quattro righe di troppo.</summary>
    [Fact]
    public void In_produzione_le_chiamate_http_uscenti_non_si_annotano_una_per_una()
    {
        Assert.True(LivelloEfficace("System.Net.Http.HttpClient.IvaoHttp.LogicalHandler", "Production") >= LogLevel.Warning);
    }

    /// <summary>
    /// E in sviluppo l'SQL si vede, perché è lì che contarne le query è il modo normale di accorgersi di un
    /// N+1. Il rovescio del test qui sopra: senza questo, «zittire i log» finirebbe per zittirli ovunque.
    /// </summary>
    [Fact]
    public void In_sviluppo_lSQL_di_EF_si_vede()
    {
        Assert.True(LivelloEfficace(SqlDiEf, "Development") <= LogLevel.Information,
            "In sviluppo l'SQL dev'essere visibile: è così che si contano le query di una pagina.");
    }

    /// <summary>
    /// Le NOSTRE righe restano. Sono quelle che raccontano gli import e le manutenzioni d'avvio, sono poche,
    /// e su un host dove i log del processo non li legge nessuno sono l'unico racconto che resta.
    /// ⚠️ Se un domani si abbassasse <c>Default</c> a Warning per far tacere qualcos'altro, queste sparirebbero
    /// insieme: è il motivo per cui il rumore si toglie NOMINANDO la categoria rumorosa, non alzando la soglia.
    /// </summary>
    [Fact]
    public void In_produzione_le_righe_del_modulo_restano_visibili()
    {
        Assert.True(LivelloEfficace("Vipi.DocumentMaintenance", "Production") <= LogLevel.Information);
        Assert.True(LivelloEfficace("Vipi.Infrastructure.Ivao.AtcPollingHostedService", "Production") <= LogLevel.Information);
    }

    /// <summary>
    /// Il livello sotto il quale la categoria non emette più, calcolato come lo calcola il sistema di log:
    /// si costruisce la configurazione come la costruisce l'host (base + file d'ambiente), la si dà a
    /// <c>AddConfiguration</c>, e poi si interroga il logger vero.
    /// </summary>
    private static LogLevel LivelloEfficace(string categoria, string ambiente)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{ambiente}.json", optional: true)
            .Build();

        var servizi = new ServiceCollection();
        servizi.AddLogging(b =>
        {
            b.AddConfiguration(config.GetSection("Logging"));
            // ⚠️ SERVE un provider, e non è un dettaglio di costruzione: senza, la fabbrica non ha nessuno a
            // cui consegnare le righe e `IsEnabled` risponde NO a ogni livello. Il primo giro di questi test
            // non ce l'aveva: i due che chiedevano «≥ Warning» passavano — perché «mai» è ≥ Warning — e i due
            // che chiedevano «≤ Information» fallivano. Due verdi che non dimostravano niente, ed è
            // esattamente il modo in cui un test di configurazione può mentire.
            b.AddProvider(new ProviderCheNonScrive());
        });
        using var provider = servizi.BuildServiceProvider();

        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(categoria);
        foreach (var l in new[] { LogLevel.Trace, LogLevel.Debug, LogLevel.Information, LogLevel.Warning, LogLevel.Error, LogLevel.Critical })
            if (logger.IsEnabled(l)) return l;
        return LogLevel.None;
    }

    /// <summary>Un provider che accetta tutto e non scrive niente: serve solo a rendere reale il filtro.</summary>
    private sealed class ProviderCheNonScrive : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new LoggerMuto();
        public void Dispose() { }

        private sealed class LoggerMuto : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) { }
        }
    }

    /// <summary>
    /// 🔴 <b>Il registro eventi di Windows resta fuori.</b> <c>WebApplication.CreateBuilder</c> aggiunge da
    /// sé <c>EventLogLoggerProvider</c> quando gira su Windows, e non lo vuole nessuno: la produzione è Linux
    /// (quel canale là non esiste), quel che si legge sta in <c>diagnostica/</c>, e in sviluppo il costo è
    /// reale — <b>misurato il 2 settembre 2026: 535 voci</b> nel registro Applicazione della macchina in tre
    /// ore di suite, sorgente «.NET Runtime», id 1000.
    ///
    /// <para>⚠️ E non era solo rumore: quel provider tiene un <c>SafeEventLogWriteHandle</c> che muore quando
    /// il provider viene disposto. Una riga di log scritta <b>tardi</b> nello spegnimento —
    /// <c>AtcPollingHostedService.StopAsync</c> ne scrive una quando il salvataggio finale non riesce —
    /// trovava l'handle già chiuso, e l'<c>ObjectDisposedException</c> risaliva fino a far fallire
    /// <c>Host.StopAsync</c>: cioè il <c>Dispose</c> della fabbrica di prova, cioè il test. Era il rosso
    /// intermittente di <c>CorsaDbContextPagineTests</c>.</para>
    ///
    /// <para>Questa prova guarda l'host <b>vero</b>, quello che i test d'integrazione avviano dal punto
    /// d'ingresso: se qualcuno rimettesse il provider, qui si vede.</para>
    /// </summary>
    [Fact]
    public void Il_registro_eventi_di_Windows_non_e_fra_i_provider()
    {
        using var fabbrica = new SmokeTests.VipiAppFactory();

        var provider = fabbrica.Services.GetServices<ILoggerProvider>().ToList();

        Assert.DoesNotContain(provider, p =>
            p is Microsoft.Extensions.Logging.EventLog.EventLogLoggerProvider);
        // ⚠️ E il controllo: se la lista fosse vuota questa prova direbbe verde senza provare niente.
        Assert.NotEmpty(provider);
    }
}
