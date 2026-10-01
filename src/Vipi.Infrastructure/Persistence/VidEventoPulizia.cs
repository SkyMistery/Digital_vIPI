using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vipi.Application.EventKits;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Cancella da sola la lista dei VID degli account dell'evento quando arriva il suo giorno: sette giorni dopo la fine
/// dell'evento, o la data scritta dallo staff (committente, 1 ottobre 2026; carta 2026-10-01-account-evento.md §4).
/// <para>⚠️ Ogni ora, e una volta subito all'avvio: un sito rimasto spento il giorno giusto la cancella appena riparte.
/// Un'ora di ritardo non conta — a evento finito la lista non apre già più niente (vale solo mentre l'evento si vede);
/// la pulizia è perché quei VID non restino in archivio, non un cancello.</para>
/// </summary>
public sealed class VidEventoPulizia : BackgroundService
{
    public static readonly TimeSpan Intervallo = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<VidEventoPulizia> _log;

    public VidEventoPulizia(IServiceScopeFactory scopes, ILogger<VidEventoPulizia> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                if (await scope.ServiceProvider.GetRequiredService<IEventKitService>().CancellaVidScadutiAsync(stop))
                    _log.LogInformation("Account dell'evento: lista dei VID cancellata, era arrivato il suo giorno");
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Account dell'evento: pulizia della lista dei VID non riuscita, riprovo fra un'ora");
            }

            try { await Task.Delay(Intervallo, stop); }
            catch (OperationCanceledException) { return; }
        }
    }
}
