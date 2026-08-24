using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Stats;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Interroga le API IVAO ogni <c>PollSeconds</c>, normalizza l'ATC online e aggiorna <see cref="OnlineAtcCache"/>.
/// Una sola chiamata al minuto indipendentemente dagli utenti (RNF-1/RNF-4). Resiliente: gli errori di rete
/// vengono loggati ma non uccidono il loop. ADR-0001 D6 / PIANO §7.2.
/// </summary>
public sealed class AtcPollingHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly OnlineAtcCache _cache;
    private readonly IvaoOptions _opt;
    private readonly IHostEnvironment _env;
    private readonly ILogger<AtcPollingHostedService> _log;

    public AtcPollingHostedService(
        IServiceScopeFactory scopes,
        OnlineAtcCache cache,
        IOptions<IvaoOptions> opt,
        IHostEnvironment env,
        ILogger<AtcPollingHostedService> log)
    {
        _scopes = scopes;
        _cache = cache;
        _opt = opt.Value;
        _env = env;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var period = TimeSpan.FromSeconds(Math.Max(15, _opt.PollSeconds));
        using var timer = new PeriodicTimer(period);

        // Primo poll immediato all'avvio, poi a cadenza fissa.
        do
        {
            await PollOnceAsync(stoppingToken);
        }
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        // Verifica live (vedi docs/feature/2026-08-23-live-coordinamenti-a-colonne.md): elenco finto da
        // config, nessuna chiamata di rete. Serve perche' senza vicini online OGNI punto di trasferimento
        // risolve a UNICOM, che la vista nasconde per default: la pagina si prova vuota.
        if (!string.IsNullOrWhiteSpace(_opt.FakeOnlineCallsigns))
        {
            // ⚠️ Strumento, non prodotto. Fuori da Development si RIFIUTA e si continua col poll vero: una
            // configurazione dimenticata in produzione mostrerebbe a tutti un traffico che non esiste.
            if (!_env.IsDevelopment())
            {
                _log.LogError("Ivao:FakeOnlineCallsigns e' valorizzato in ambiente {Env}: IGNORATO. " +
                              "E' uno strumento di verifica, va usato solo in Development.", _env.EnvironmentName);
            }
            else
            {
                var finti = _opt.FakeOnlineCallsigns
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(c => c.ToUpperInvariant()).ToList();
                _cache.Set(new OnlineAtcSnapshot
                {
                    Callsigns = new HashSet<string>(finti, StringComparer.OrdinalIgnoreCase),
                    Details = finti.Select((c, i) => new OnlineAtc(c, 704798 + i, "Finto " + c, 5)).ToList(),
                    AsOf = DateTimeOffset.UtcNow,
                });
                // Nessuna scrittura di statistiche da qui: un callsign inventato non ha una sessione IVAO,
                // e l'id finto sporcherebbe l'archivio con connessioni mai esistite.
                _log.LogWarning("ATC online FINTO da config: {Lista}", string.Join(", ", finti));
                return;
            }
        }

        try
        {
            // Scope per-poll: il client (via IvaoHttp, typed HttpClient) viene risolto fresco => handler ruotato
            // dalla factory, niente captive dependency nel singleton.
            using var scope = _scopes.CreateScope();
            var source = scope.ServiceProvider.GetRequiredService<IAtcActivitySource>();
            var snapshot = await source.GetSnapshotAsync(ct);

            var atcs = snapshot.Atc
                .Select(a => new OnlineAtc(a.Callsign, a.UserId, $"UserId {a.UserId}", a.Rating))
                .ToList();
            var callsigns = new HashSet<string>(
                atcs.Select(a => a.Callsign), StringComparer.OrdinalIgnoreCase);

            _cache.Set(new OnlineAtcSnapshot
            {
                Callsigns = callsigns,
                Details = atcs,
                AsOf = snapshot.AsOf,
            });

            _log.LogInformation("Poll IVAO: {Count} ATC divisione online, {Piloti} piloti nella fotografia.",
                atcs.Count, snapshot.Pilots.Count);

            await RegistraSessioniAsync(scope, snapshot, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutdown: ignora
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Poll IVAO fallito; mantengo l'ultima fotografia.");
        }
    }

    /// <summary>
    /// Scrive in archivio le sessioni ATC viste in questa fotografia (statistiche, carta del 24 agosto 2026).
    ///
    /// <para>⚠️ Ha un <c>try</c> tutto suo, e non è pigrizia: la vista live dipende dalla cache appena
    /// riempita, e un archivio che non risponde non deve farla sparire dalle pagine. Se questo pezzo fallisce
    /// si perde un minuto di statistiche — che il backfill dallo storico può recuperare — mentre un'eccezione
    /// che risalisse spegnerebbe il pallino «in frequenza» a tutti.</para>
    /// </summary>
    private async Task RegistraSessioniAsync(IServiceScope scope, NetworkSnapshot snapshot, CancellationToken ct)
    {
        try
        {
            var store = scope.ServiceProvider.GetRequiredService<IAtcSessionStore>();
            var known = await store.GetOpenOrRecentAsync(snapshot.AsOf - AtcSessionSync.ShiftGap, ct);
            var plan = AtcSessionSync.Plan(snapshot.Atc, known, snapshot.AsOf);
            if (plan.Nothing) return;

            var toccate = await store.ApplyAsync(plan, ct);
            _log.LogDebug("Statistiche ATC: {Righe} righe di sessione ({Nuove} nuove, {Chiuse} chiuse).",
                toccate, plan.Upserts.Count(u => u.IsNew), plan.Closures.Count);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutdown: ignora
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Statistiche ATC: registrazione delle sessioni fallita; la vista live non ne risente.");
        }
    }
}

/// <summary>Registrazione del polling IVAO (client, token, cache, hosted service). Chiamata dall'Host.</summary>
public static class IvaoServiceCollectionExtensions
{
    public static IServiceCollection AddVipiIvao(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        services.Configure<IvaoOptions>(configuration.GetSection(IvaoOptions.SectionName));

        services.AddTransient<TransientRetryHandler>();

        // Token: singleton (cache token persistente) con HttpClient dalla factory. Timeout + retry transitori.
        services.AddHttpClient(IvaoTokenProvider.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(15))
            .AddHttpMessageHandler<TransientRetryHandler>();
        services.AddSingleton<IvaoTokenProvider>();

        // Plumbing HTTP condiviso: typed client (transient), iniettato nei client per porta.
        // ⚠️ Decompressione automatica: il whazzup è 705 KB di JSON che con Brotli diventano 119 KB sul filo
        // (misurato). Senza questa riga si scaricherebbero i 705 KB pieni, ogni minuto, per sempre.
        services.AddHttpClient<IvaoHttp>(c => c.Timeout = TimeSpan.FromSeconds(15))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.Brotli
                                       | System.Net.DecompressionMethods.GZip
                                       | System.Net.DecompressionMethods.Deflate,
            })
            .AddHttpMessageHandler<TransientRetryHandler>();

        // Cache condivisa: un singolo stato letto da tutti (anche via IOnlineAtcProvider).
        services.AddSingleton<OnlineAtcCache>();
        services.AddSingleton<IOnlineAtcProvider>(sp => sp.GetRequiredService<OnlineAtcCache>());

        // Un client per porta (doc refactor 01 §4.2): ognuno inietta IvaoHttp.
        // Riepilogo ATC online (fetch grezzo, endpoint autenticato: resta come porta di servizio).
        services.AddScoped<IvaoOnlineAtcClient>();

        // Fotografia della rete (ATC + piloti) dall'endpoint pubblico: è la sorgente del poller dal 24
        // agosto 2026, e quella su cui poggiano le statistiche ATC.
        services.AddScoped<IvaoWhazzupClient>();
        services.AddScoped<IAtcActivitySource>(sp => sp.GetRequiredService<IvaoWhazzupClient>());

        // Profilo del singolo utente (il roster staff si popola dai login, non dall'elenco membri divisione).
        services.AddScoped<IvaoUserClient>();
        services.AddScoped<IUserDirectory>(sp => sp.GetRequiredService<IvaoUserClient>());

        // Anagrafica aeroporti IVAO: cache di processo (singleton) condivisa dal client aeroporti.
        services.AddSingleton<IvaoAirportCache>();
        services.AddScoped<IvaoAirportClient>();
        services.AddScoped<IAirportDirectory>(sp => sp.GetRequiredService<IvaoAirportClient>());
        services.AddScoped<IvaoAirportDetailClient>();
        services.AddScoped<IAirportDetailProvider>(sp => sp.GetRequiredService<IvaoAirportDetailClient>());

        // Anagrafica ACC/center IVAO.
        services.AddScoped<IvaoAccClient>();
        services.AddScoped<IAccDirectory>(sp => sp.GetRequiredService<IvaoAccClient>());

        services.AddHostedService<AtcPollingHostedService>();
        services.AddHostedService<StaffRosterVerificationService>();
        services.AddHostedService<AccImportHostedService>();
        services.AddHostedService<AirportDirectoryImportHostedService>();
        services.AddHostedService<AirportSectorImportHostedService>();
        services.AddHostedService<AirportDataImportHostedService>();
        services.AddHostedService<SpecialAreaImportHostedService>();
        return services;
    }
}
