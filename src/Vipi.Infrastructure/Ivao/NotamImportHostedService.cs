using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Rilegge i NOTAM della divisione dalla sorgente (default ogni 30 minuti) e ne tiene lo specchio in archivio.
/// Job di sistema: porta sorgente + archivio, niente authz utente. Resiliente: un errore si registra e si
/// ritenta al giro dopo; nel frattempo i documenti mostrano i NOTAM dell'ultimo giro riuscito, e quelli scaduti
/// spariscono lo stesso — la scadenza la decide la data, non questo giro.
/// </summary>
internal sealed class NotamImportHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IvaoOptions _opt;
    private readonly ILogger<NotamImportHostedService> _log;

    public NotamImportHostedService(
        IServiceScopeFactory scopes, IOptions<IvaoOptions> opt, ILogger<NotamImportHostedService> log)
    {
        _scopes = scopes;
        _opt = opt.Value;
        _log = log;
    }

    // Gated: un riavvio entro il periodo non richiama la sorgente. ⚠️ Il ritentativo è il periodo stesso e non
    // l'ora degli altri giri: qui il periodo è già più corto di un'ora.
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        GatedImportLoop.RunAsync(_scopes, ImportCategories.Notam, _opt.NotamImportPeriod, ImportOnceAsync, _log,
            stoppingToken, bootDelay: TimeSpan.FromSeconds(50), retry: _opt.NotamImportPeriod);

    private async Task<bool> ImportOnceAsync(IServiceProvider sp, CancellationToken ct)
    {
        var r = await sp.GetRequiredService<NotamImportUseCase>().RunAsync(ct);
        // A regime non cambia niente per giorni: il giro senza novità non scrive una riga di registro ogni mezz'ora.
        if (r.Creati + r.Aggiornati + r.Tolti > 0)
            _log.LogInformation("Import NOTAM: {Creati} creati, {Aggiornati} aggiornati, {Tolti} tolti.",
                r.Creati, r.Aggiornati, r.Tolti);
        return true;
    }
}
