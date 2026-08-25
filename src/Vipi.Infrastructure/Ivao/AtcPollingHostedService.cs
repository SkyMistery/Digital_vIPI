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
    private readonly AtcTrafficRecorder _traffico;
    private readonly OnlineAtcCache _cache;
    private readonly IvaoOptions _opt;
    private readonly IHostEnvironment _env;
    private readonly ILogger<AtcPollingHostedService> _log;

    public AtcPollingHostedService(
        IServiceScopeFactory scopes,
        AtcTrafficRecorder traffico,
        OnlineAtcCache cache,
        IOptions<IvaoOptions> opt,
        IHostEnvironment env,
        ILogger<AtcPollingHostedService> log)
    {
        _scopes = scopes;
        _traffico = traffico;
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
            await RegistraTrafficoAsync(scope, snapshot, ct);
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
    /// Tempo che il salvataggio finale ha per scrivere. ⚠️ Un tetto ci vuole — uno spegnimento non può
    /// appendersi al database — ma è un tetto SUO: cinque secondi bastano per una manciata di righe, e non
    /// dipendono da quanto il gettone di arresto sia già scaduto.
    /// </summary>
    private static readonly TimeSpan TempoPerSalvare = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Allo spegnimento salva quel che è rimasto in memoria: senza, l'ultimo tratto fra un checkpoint e
    /// l'arresto (fino a dieci minuti di traffico per ogni sessione in corso) andrebbe perso a ogni deploy.
    ///
    /// <para>⚠️ <b>Non si usa <paramref name="cancellationToken"/> per scrivere.</b> Quel gettone significa
    /// «fermati», e questo salvataggio esiste proprio per non fermarsi prima di aver scritto: quando arriva
    /// già annullato — un secondo Ctrl+C, un arresto forzato, il tempo di shutdown scaduto — la scrittura
    /// moriva sull'<b>apertura della connessione</b> con una <c>TaskCanceledException</c>, e il messaggio
    /// «salvataggio finale del traffico fallito» sembrava un guasto del database. Non lo era.</para>
    ///
    /// <para>⚠️ E il danno non era solo «non scritto»: <c>FlushAsync</c> chiama <c>TakeAll</c>, che
    /// <b>svuota</b> il registro in memoria prima di salvare. Fallita la scrittura, quei minuti non erano
    /// più né su disco né in RAM.</para>
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var tempo = new CancellationTokenSource(TempoPerSalvare);
            using var scope = _scopes.CreateScope();
            await _traffico.FlushAsync(
                scope.ServiceProvider.GetRequiredService<IAtcTrafficStore>(), DateTimeOffset.UtcNow, tempo.Token);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Statistiche ATC: salvataggio finale del traffico fallito.");
        }

        await base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Registra le piste in uso quando <b>cambiano</b> durante la sessione.
    ///
    /// <para>⚠️ Non è un valore, è una sequenza: le configurazioni cambiano a turno in corso, e scrivere
    /// quella del primo giro come «la pista della sessione» sarebbe falso per metà turno.</para>
    ///
    /// <para>Lo stato in memoria evita di interrogare l'archivio quando non è cambiato niente — cioè quasi
    /// sempre: in un'ora di turno la configurazione cambia zero o una volta, non sessanta.</para>
    /// </summary>
    private async Task RegistraPisteAsync(IAtcSessionStore store, NetworkSnapshot snapshot, CancellationToken ct)
    {
        foreach (var atc in snapshot.Atc)
        {
            var piste = AtisRunways.Leggi(atc.AtisLines);
            if (piste.Vuoto) continue;

            if (_pisteViste.TryGetValue(atc.SessionId, out var ultima) && ultima == piste) continue;

            if (await store.AppendRunwayAsync(atc.SessionId, piste.Arrival, piste.Departure, snapshot.AsOf, ct))
                _log.LogDebug("Piste {Callsign}: {Piste}", atc.Callsign, piste);

            _pisteViste[atc.SessionId] = piste;
        }

        // Le sessioni finite escono dalla memoria: il dizionario non deve crescere per sempre.
        foreach (var id in _pisteViste.Keys.Where(k => snapshot.Atc.All(a => a.SessionId != k)).ToList())
            _pisteViste.Remove(id);
    }

    /// <summary>Ultima configurazione vista per sessione: serve a non chiedere all'archivio a ogni giro.</summary>
    private readonly Dictionary<long, RunwaysInUse> _pisteViste = new();

    /// <summary>
    /// Attribuisce i piloti della fotografia alle sessioni in frequenza e tiene aggiornate le tratte
    /// (carta §4.1). Come per le sessioni, un <c>try</c> suo: le statistiche non devono poter spegnere la
    /// vista live.
    ///
    /// <para>Gira <b>dopo</b> le sessioni, e non è un dettaglio d'ordine: una riga di traffico ha una chiave
    /// esterna verso la sua sessione, che dev'essere già in archivio.</para>
    /// </summary>
    private async Task RegistraTrafficoAsync(IServiceScope scope, NetworkSnapshot snapshot, CancellationToken ct)
    {
        try
        {
            var policy = await scope.ServiceProvider.GetRequiredService<IImportPolicyStore>().GetAsync(ct);
            if (!policy.IsImported(Vipi.Domain.ImportCategory.AtcSessions)) return;

            var store = scope.ServiceProvider.GetRequiredService<IAtcTrafficStore>();
            var esito = await _traffico.RecordAsync(snapshot, store, ct);
            if (esito.Attributed == 0 && esito.WrittenLegs == 0) return;

            _log.LogInformation(
                "Statistiche ATC: {Attribuiti} aerei attribuiti a {Sessioni} sessioni ({Nuove} tratte nuove, {Scritte} righe scritte).",
                esito.Attributed, esito.Sessions, esito.NewLegs, esito.WrittenLegs);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutdown: ignora
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Statistiche ATC: attribuzione del traffico fallita; la vista live non ne risente.");
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
            // Stessa categoria di policy dello storico: spegnere le statistiche le spegne davvero, non solo
            // il giro notturno. La lettura è una riga sola, e questo giro ne fa già altre.
            var policy = await scope.ServiceProvider.GetRequiredService<IImportPolicyStore>().GetAsync(ct);
            if (!policy.IsImported(Vipi.Domain.ImportCategory.AtcSessions)) return;

            var store = scope.ServiceProvider.GetRequiredService<IAtcSessionStore>();
            var known = await store.GetOpenOrRecentAsync(snapshot.AsOf - AtcSessionSync.ShiftGap, ct);
            var plan = AtcSessionSync.Plan(snapshot.Atc, known, snapshot.AsOf);
            if (plan.Nothing) return;

            var toccate = await store.ApplyAsync(plan, ct);
            await RegistraPisteAsync(store, snapshot, ct);
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

        // Storico connessioni ATC (token app, scope tracker): backfill dei dodici mesi e ripasso quotidiano.
        services.AddScoped<IvaoAirportTrafficClient>();
        services.AddScoped<IAirportTrafficSource>(sp => sp.GetRequiredService<IvaoAirportTrafficClient>());
        services.AddScoped<Vipi.Application.Stats.AirportTrafficBackfillUseCase>();
        services.AddScoped<Vipi.Application.Stats.AirportTrafficRollupUseCase>();
        services.AddScoped<Vipi.Application.Stats.TrafficRetentionUseCase>();
        services.AddScoped<IvaoAtcHistoryClient>();
        services.AddScoped<IAtcHistorySource>(sp => sp.GetRequiredService<IvaoAtcHistoryClient>());
        services.AddScoped<Vipi.Application.Stats.AtcHistoryImportUseCase>();

        // Attribuzione del traffico: SINGLETON, perche' il registro delle tratte in corso vive in memoria fra
        // un giro e l'altro (e' quello che evita di riscrivere ogni riga ogni minuto). Lo usa il solo poller.
        services.AddSingleton<AtcTrafficRecorder>(sp => new AtcTrafficRecorder(
            new Persistence.ScopedSectorVolumeCatalog(sp.GetRequiredService<IServiceScopeFactory>())));

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
        services.AddHostedService<AtcHistoryImportHostedService>();
        services.AddHostedService<AirportTrafficBackfillHostedService>();
        services.AddHostedService<AirportTrafficRollupHostedService>();
        services.AddHostedService<TrafficRetentionHostedService>();
        services.AddHostedService<SpecialAreaImportHostedService>();
        // ⚠️ Non è un import — non interroga nessuna sorgente — ma vive nella stessa lista perché è un giro
        // gestito uguale agli altri, e parte per ultimo: guarda il mondo DOPO che gli import l'hanno aggiornato.
        services.AddHostedService<ImpactDriftHostedService>();
        return services;
    }
}
