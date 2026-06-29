using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Re-importa automaticamente i settori ATC degli aeroporti dalla sorgente (default ogni 24h), oltre al
/// bottone manuale nell'editor aeroporto. Job di sistema: usa direttamente porta sorgente + repository
/// (niente authz utente). Preserva IsHidden e i limiti admin. Resiliente: errori loggati senza uccidere
/// il loop; se le credenziali sorgente mancano, salta in silenzio.
/// </summary>
public sealed class AirportSectorImportHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IvaoOptions _opt;
    private readonly ILogger<AirportSectorImportHostedService> _log;

    public AirportSectorImportHostedService(
        IServiceScopeFactory scopes, IOptions<IvaoOptions> opt, ILogger<AirportSectorImportHostedService> log)
    {
        _scopes = scopes;
        _opt = opt.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Primo import poco dopo l'avvio (dopo quello degli ACC), poi a cadenza giornaliera.
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (OperationCanceledException) { return; }

        await ImportOnceAsync(stoppingToken);

        var period = TimeSpan.FromHours(Math.Max(1, _opt.AirportSectorImportHours));
        using var timer = new PeriodicTimer(period);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            await ImportOnceAsync(stoppingToken);
    }

    private async Task ImportOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IAirportSectorRepository>();
            var importer = scope.ServiceProvider.GetRequiredService<Vipi.Application.Content.IAirportSectorImporter>();
            var structure = scope.ServiceProvider.GetRequiredService<Vipi.Application.Content.IStructureEditingService>();

            var icaos = await repo.ListAirportIcaosAsync(ct);
            int created = 0, updated = 0, airports = 0, docs = 0;
            foreach (var icao in icaos)
            {
                var (c, u) = await importer.ImportAsync(icao, ct);
                if (c == 0 && u == 0) continue;
                created += c; updated += u; airports++;

                // Documento aeroporto: creato/aggiornato in automatico per ogni aeroporto con ACC (idempotente).
                try { if ((await structure.EnsureAirportDocumentSystemAsync(icao, ct)).Created) docs++; }
                catch (Exception ex) { _log.LogDebug(ex, "Generazione documento {Icao} saltata.", icao); }
            }

            // Riproietta i Sector operativi dai cataloghi aggiornati (fonte autoritativa unica, Round 20).
            var projection = scope.ServiceProvider.GetRequiredService<ISectorProjectionService>();
            await projection.SyncFromCatalogsAsync(ct);

            _log.LogInformation("Import settori aeroporto automatico: {Airports} aeroporti, settori {Created}/{Updated}, documenti nuovi {Docs}.",
                airports, created, updated, docs);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutdown: ignora
        }
        catch (InvalidOperationException ex)
        {
            // tipicamente credenziali sorgente assenti: salta senza rumore.
            _log.LogInformation("Import settori aeroporto automatico saltato: {Reason}", ex.Message);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Import settori aeroporto automatico fallito; riprovo al prossimo ciclo.");
        }
    }
}
