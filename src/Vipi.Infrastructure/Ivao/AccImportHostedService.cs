using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Re-importa automaticamente ACC + settori ATC dalla sorgente (default ogni 24h), oltre al bottone manuale.
/// È un job di sistema: usa direttamente porta sorgente + repository (niente authz utente). Preserva
/// IsHidden e i limiti impostati dall'admin. Resiliente: errori loggati, senza uccidere il loop;
/// se le credenziali sorgente mancano, salta in silenzio.
/// </summary>
public sealed class AccImportHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IvaoOptions _opt;
    private readonly ILogger<AccImportHostedService> _log;

    public AccImportHostedService(
        IServiceScopeFactory scopes, IOptions<IvaoOptions> opt, ILogger<AccImportHostedService> log)
    {
        _scopes = scopes;
        _opt = opt.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Primo import poco dopo l'avvio (popola/aggiorna), poi a cadenza giornaliera.
        try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); }
        catch (OperationCanceledException) { return; }

        await ImportOnceAsync(stoppingToken);

        var period = TimeSpan.FromHours(Math.Max(1, _opt.AccImportHours));
        using var timer = new PeriodicTimer(period);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            await ImportOnceAsync(stoppingToken);
    }

    private async Task ImportOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var dir = scope.ServiceProvider.GetRequiredService<IAccDirectory>();
            var repo = scope.ServiceProvider.GetRequiredService<IAccAdminRepository>();

            var centers = await dir.GetCentersAsync(ct);
            var (ac, au) = await repo.ImportAsync(centers, ct);

            var accs = await repo.ListAccsAsync(ct);
            var subs = new List<SourceSubcenter>();
            foreach (var a in accs)
                subs.AddRange(await dir.GetSubcentersAsync(a.Code, ct));
            var (sc, su) = await repo.ImportSubcentersAsync(subs, ct);

            // Riproietta i Sector operativi dai cataloghi aggiornati (fonte autoritativa unica, Round 20).
            var projection = scope.ServiceProvider.GetRequiredService<ISectorProjectionService>();
            await projection.SyncFromCatalogsAsync(ct);

            _log.LogInformation("Import ACC automatico: ACC {AccCreated}/{AccUpdated}, settori ATC {SubCreated}/{SubUpdated}.",
                ac, au, sc, su);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutdown: ignora
        }
        catch (InvalidOperationException ex)
        {
            // tipicamente credenziali sorgente assenti: salta senza rumore.
            _log.LogInformation("Import ACC automatico saltato: {Reason}", ex.Message);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Import ACC automatico fallito; riprovo al prossimo ciclo.");
        }
    }
}
