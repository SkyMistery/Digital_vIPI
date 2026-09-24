using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vipi.Application.Content;

namespace Vipi.Infrastructure;

/// <summary>
/// Il giro della deriva, <b>pochi minuti dopo</b> che qualcuno ha scritto dati che finiscono nei documenti.
/// Carta <c>docs/feature/2026-09-23-da-fare-per-cambiamento.md</c> §4.
///
/// <para>Fino al 23 settembre 2026 «da ripubblicare» lo diceva solo il giro delle 24 ore
/// (<see cref="ImpactDriftHostedService"/>): si cambiava un trasferimento che sta nelle vIPI di Roma e Brindisi, e
/// le due righe comparivano il giorno dopo. Il giro intero dura un paio di secondi (misurato su 19 documenti), e
/// qui si rilancia dopo ogni finestra di modifiche. La finestra diventa la <b>causa</b> delle righe che apre.</para>
///
/// <para>⚠️ Non tocca lo stato del giro notturno (<c>ImportStates</c>): quello resta il giro garantito, e
/// segnarlo riuscito qui lo farebbe slittare di un giorno a ogni modifica, cioè mai più per chi lavora ogni giorno.</para>
/// </summary>
internal sealed class DerivaDopoLeModificheHostedService : BackgroundService
{
    /// <summary>Quanto si aspetta dalla PRIMA modifica: abbastanza per raccogliere un giro di lavoro nella stessa
    /// finestra (salvare, correggere, salvare di nuovo), poco abbastanza da vedere l'effetto prima di cambiare
    /// pagina.</summary>
    internal static readonly TimeSpan Finestra = TimeSpan.FromMinutes(2);

    private readonly IModificheInAttesa _attesa;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<DerivaDopoLeModificheHostedService> _log;

    public DerivaDopoLeModificheHostedService(IModificheInAttesa attesa, IServiceScopeFactory scopes,
        ILogger<DerivaDopoLeModificheHostedService> log)
    {
        _attesa = attesa;
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            FinestraDiModifiche finestra;
            try { finestra = await _attesa.PrendiAsync(Finestra, stoppingToken); }
            catch (OperationCanceledException) { return; }

            try
            {
                using var scope = _scopes.CreateScope();
                var esito = await scope.ServiceProvider.GetRequiredService<IImpactDriftUseCase>()
                    .RunAfterChangesAsync(finestra, stoppingToken);
                _log.LogInformation(
                    "Deriva dopo le modifiche ({Famiglie}, dalle {Da:HH:mm:ss}Z): {Esaminati} documenti, " +
                    "{Aperti} segnalazioni, {Chiusi} richiuse.",
                    string.Join(", ", finestra.Famiglie), finestra.DaUtc, esito.Esaminati, esito.Aperti, esito.Chiusi);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                // Un giro fallito non ferma i successivi: c'è sempre quello notturno, e la prossima modifica ne
                // chiede un altro. Si scrive e si va avanti.
                _log.LogWarning(ex, "Deriva dopo le modifiche fallita ({Famiglie}).", string.Join(", ", finestra.Famiglie));
            }
        }
    }
}
