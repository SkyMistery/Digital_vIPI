using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Re-importa automaticamente le aree speciali/regolamentate dalla sorgente (default ogni 24h).
/// Job di sistema: porta sorgente + repository (niente authz utente). Upsert per IvaoId.
/// Resiliente: errori loggati senza uccidere il loop; se le credenziali mancano, salta in silenzio.
/// </summary>
public sealed class SpecialAreaImportHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IvaoOptions _opt;
    private readonly ILogger<SpecialAreaImportHostedService> _log;

    public SpecialAreaImportHostedService(
        IServiceScopeFactory scopes, IOptions<IvaoOptions> opt, ILogger<SpecialAreaImportHostedService> log)
    {
        _scopes = scopes;
        _opt = opt.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Ritardo iniziale un po' più lungo dell'import ACC: le aree hanno bisogno degli ACC già presenti (FK).
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
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

            var accs = await repo.ListAccsAsync(ct);
            int created = 0, updated = 0, removed = 0;
            foreach (var a in accs)
            {
                // Per-ACC: se la fetch fallisce non facciamo il prune di quell'ACC (evita cancellazioni su errori transitori).
                try
                {
                    var areas = await dir.GetSpecialAreasAsync(a.Code, ct);
                    var (c, u) = await repo.ImportSpecialAreasAsync(areas, ct);
                    var r = await repo.PruneSpecialAreasNotInAsync(a.Code, areas.Select(x => x.IvaoId).ToList(), ct);
                    created += c; updated += u; removed += r;
                }
                catch (InvalidOperationException) { throw; }   // credenziali assenti: gestito a monte, abortisce tutto
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Import aree speciali per {Acc} fallito; salto (nessun prune).", a.Code);
                }
            }
            _log.LogInformation("Import aree speciali automatico: {Created} create, {Updated} aggiornate, {Removed} rimosse.", created, updated, removed);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutdown: ignora
        }
        catch (InvalidOperationException ex)
        {
            // tipicamente credenziali sorgente assenti: salta senza rumore.
            _log.LogInformation("Import aree speciali saltato: {Reason}", ex.Message);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Import aree speciali fallito; riprovo al prossimo ciclo.");
        }
    }
}
