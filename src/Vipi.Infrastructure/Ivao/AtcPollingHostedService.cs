using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;

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
    private readonly ILogger<AtcPollingHostedService> _log;

    public AtcPollingHostedService(
        IServiceScopeFactory scopes,
        OnlineAtcCache cache,
        IOptions<IvaoOptions> opt,
        ILogger<AtcPollingHostedService> log)
    {
        _scopes = scopes;
        _cache = cache;
        _opt = opt.Value;
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
        try
        {
            // Scope per-poll: il typed HttpClient (IvaoApiClient) viene risolto fresco => handler ruotato
            // dalla factory, niente captive dependency nel singleton.
            using var scope = _scopes.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<IvaoApiClient>();
            var atcs = await client.GetOnlineAtcAsync(ct);
            var callsigns = new HashSet<string>(
                atcs.Select(a => a.Callsign), StringComparer.OrdinalIgnoreCase);

            _cache.Set(new OnlineAtcSnapshot
            {
                Callsigns = callsigns,
                Details = atcs,
                AsOf = DateTimeOffset.UtcNow,
            });

            _log.LogInformation("Poll IVAO: {Count} ATC divisione online.", atcs.Count);
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
}

/// <summary>Registrazione del polling IVAO (client, token, cache, hosted service). Chiamata dall'Host.</summary>
public static class IvaoServiceCollectionExtensions
{
    public static IServiceCollection AddVipiIvao(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        services.Configure<IvaoOptions>(configuration.GetSection(IvaoOptions.SectionName));

        // Token: singleton (cache token persistente) con HttpClient dalla factory.
        services.AddHttpClient(IvaoTokenProvider.HttpClientName);
        services.AddSingleton<IvaoTokenProvider>();

        // Adapter API: typed client (transient), risolto in scope dal poller / dalle pagine.
        services.AddHttpClient<IvaoApiClient>();

        // Cache condivisa: un singolo stato letto da tutti (anche via IOnlineAtcProvider).
        services.AddSingleton<OnlineAtcCache>();
        services.AddSingleton<IOnlineAtcProvider>(sp => sp.GetRequiredService<OnlineAtcCache>());

        // L'elenco membri divisione è lo stesso adapter HTTP.
        services.AddScoped<IDivisionMembersProvider>(sp => sp.GetRequiredService<IvaoApiClient>());

        services.AddHostedService<AtcPollingHostedService>();
        return services;
    }
}
