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
///
/// <para>⚠️ <b>Allo spegnimento la finestra aperta si consuma subito</b> (U-236, revisione 3; scelta del committente
/// del 29 settembre 2026). La finestra vive solo in memoria, e su Passenger il processo si spegne poco dopo l'ultima
/// richiesta: chi salvava e chiudeva la scheda entro due minuti perdeva la causa, e le righe arrivavano solo col
/// giro notturno. Ora <see cref="StopAsync"/> fa il giro nel tempo che l'host concede allo spegnimento, e un giro
/// già partito non si interrompe al segnale di arresto ma solo quando quel tempo scade. Se il processo viene
/// ucciso di colpo, la finestra si perde come prima: la rete resta il giro notturno.</para>
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

    /// <summary>Si annulla quando scade il tempo di spegnimento dell'host, non al segnale di arresto: un giro di
    /// un paio di secondi deve poter finire.</summary>
    private readonly CancellationTokenSource _tempoScaduto = new();

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

            await GiroAsync(finestra, _tempoScaduto.Token, alloSpegnimento: false);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        using (cancellationToken.Register(() => _tempoScaduto.Cancel()))
        {
            await base.StopAsync(cancellationToken);   // ferma l'attesa; un giro in corso finisce
            if (_attesa.PrendiSubito() is { } finestra)
                await GiroAsync(finestra, _tempoScaduto.Token, alloSpegnimento: true);
        }
    }

    public override void Dispose()
    {
        _tempoScaduto.Dispose();
        base.Dispose();
    }

    private async Task GiroAsync(FinestraDiModifiche finestra, CancellationToken ct, bool alloSpegnimento)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var esito = await scope.ServiceProvider.GetRequiredService<IImpactDriftUseCase>()
                .RunAfterChangesAsync(finestra, ct);
            _log.LogInformation(
                "Deriva dopo le modifiche{Quando} ({Famiglie}, dalle {Da:HH:mm:ss}Z): {Esaminati} documenti, " +
                "{Aperti} segnalazioni, {Chiusi} richiuse.",
                alloSpegnimento ? " allo spegnimento" : "",
                string.Join(", ", finestra.Famiglie), finestra.DaUtc, esito.Esaminati, esito.Aperti, esito.Chiusi);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _log.LogWarning("Deriva dopo le modifiche interrotta: è scaduto il tempo di spegnimento ({Famiglie}).",
                string.Join(", ", finestra.Famiglie));
        }
        catch (Exception ex)
        {
            // Un giro fallito non ferma i successivi: c'è sempre quello notturno, e la prossima modifica ne
            // chiede un altro. Si scrive e si va avanti.
            _log.LogWarning(ex, "Deriva dopo le modifiche fallita ({Famiglie}).", string.Join(", ", finestra.Famiglie));
        }
    }
}
