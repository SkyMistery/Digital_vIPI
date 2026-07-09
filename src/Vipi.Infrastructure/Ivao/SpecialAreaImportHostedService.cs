using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;

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

    // Gated: dopo gli ACC (FK); salta il fetch all'avvio se ancora fresco (retry 1h su errore).
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        GatedImportLoop.RunAsync(_scopes, ImportCategories.SpecialArea,
            TimeSpan.FromHours(Math.Max(1, _opt.AccImportHours)), ImportOnceAsync, _log, stoppingToken,
            bootDelay: TimeSpan.FromSeconds(45));

    private async Task<bool> ImportOnceAsync(IServiceProvider sp, CancellationToken ct)
    {
        // Auto: nessun authz utente, delega al core condiviso col manual (doc refactor 02 §4.2-4.3).
        var import = sp.GetRequiredService<ISpecialAreaImportUseCase>();

        try
        {
            var r = await import.RunAsync(ct);
            foreach (var f in r.Failures)
                _log.LogWarning(f.Error, "Import aree speciali per {Acc} fallito; salto (nessun prune).", f.AccCode);
            _log.LogInformation("Import aree speciali automatico: {Created} create, {Updated} aggiornate, {Removed} rimosse.", r.Created, r.Updated, r.Removed);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            // tipicamente credenziali sorgente assenti: salta senza rumore.
            _log.LogInformation("Import aree speciali saltato: {Reason}", ex.Message);
            return true;
        }
    }
}
