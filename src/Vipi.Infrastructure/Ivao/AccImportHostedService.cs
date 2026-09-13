using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Re-importa automaticamente ACC + settori ATC dalla sorgente (default ogni 24h), oltre al bottone manuale.
/// È un job di sistema: usa direttamente porta sorgente + repository (niente authz utente). Preserva
/// IsHidden e i limiti impostati dall'admin. Resiliente: errori loggati, senza uccidere il loop;
/// se le credenziali sorgente mancano, salta in silenzio.
/// </summary>
internal sealed class AccImportHostedService : BackgroundService
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

    // Gated: prima degli altri import; salta il fetch all'avvio se ancora fresco (retry 1h su errore).
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        GatedImportLoop.RunAsync(_scopes, ImportCategories.Acc,
            TimeSpan.FromHours(Math.Max(1, _opt.AccImportHours)), ImportOnceAsync, _log, stoppingToken,
            bootDelay: TimeSpan.FromSeconds(15));

    private async Task<bool> ImportOnceAsync(IServiceProvider sp, CancellationToken ct)
    {
        // Auto: nessun authz utente, delega al core condiviso col manual (doc refactor 01 §4.4).
        var import = sp.GetRequiredService<IAccImportUseCase>();

        try
        {
            var r = await import.RunAsync(ct);
            _log.LogInformation("Import ACC automatico: ACC {AccCreated}/{AccUpdated}, settori ATC {SubCreated}/{SubUpdated}.",
                r.AccsCreated, r.AccsUpdated, r.SubcentersCreated, r.SubcentersUpdated);
            return true;
        }
        catch (SorgenteNonConfigurataException ex)
        {
            // 🔴 SOLO «non configurato», e senza timbrare (T-006, 13 settembre 2026). Qui si catturava ogni
            // InvalidOperationException e si rispondeva «riuscito», anche per un 503 o un 403 di IVAO: il giro
            // fallito veniva timbrato, niente retry, Sorgenti verde, e la soglia di eliminazione avanzava. Ora un
            // guasto vero risale a GatedImportLoop, che lo registra e riprova fra un'ora; un'installazione senza
            // credenziali non timbra niente e ricontrolla fra un'ora (costa un confronto).
            _log.LogInformation("Import ACC automatico saltato: {Reason}", ex.Message);
            return false;
        }
    }
}
