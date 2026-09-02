using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;

namespace Vipi.Infrastructure;

/// <summary>
/// Lo <b>sweep delle release</b>: una volta al giorno ricalcola gli stati e pota quel che è scaduto
/// (<see cref="IReleaseService.PruneAllAsync"/>). Carta <c>docs/feature/2026-09-02-il-ciclo-entrante.md</c> §AW4.
///
/// <para><b>Perché esiste.</b> <c>RecomputeStatuses</c> gira <b>solo in scrittura</b>, e lo sweep che lo
/// chiama girava <b>solo all'avvio</b>. Ma gli stati invecchiano <b>da soli</b>: al rollover AIRAC una
/// release schedulata entra in vigore e la precedente diventa superata <i>senza che nessuno scriva niente</i>.
/// Su un processo che resta su per settimane — ed è il caso, Plesk lo spegne per inattività, non per
/// anzianità — quel ricalcolo non arrivava mai.</para>
///
/// <para><b>Che cosa NON stava rompendo</b>, misurato leggendo i chiamanti e non dedotto: la
/// <b>visibilità</b> è salva. <c>GetEffectiveAsync</c> e <c>ListAsync</c> ordinano per <b>data</b> ed
/// escludono le sole <c>Superseded</c>, quindi scelgono la release giusta anche con gli stati vecchi; e le
/// etichette a schermo guardano <c>IsEffectiveNow</c> <b>prima</b> dello stato. Il pubblico vede il vero.</para>
///
/// <para><b>Che cosa stava rompendo</b>: la <b>retention</b>. <c>PruneReleasesAsync</c> pota le
/// <c>Superseded</c> oltre soglia, e senza ricalcolo non ne nascevano di nuove — le release superate si
/// accumulavano con i loro payload, che sono il grosso di quella tabella.</para>
///
/// <para>⚠️ <b>Non è appeso al giro della deriva</b>, benché la cadenza sia la stessa: quel giro ha un nome
/// che dice che cosa fa, e appendergli una potatura lo renderebbe un nome falso. Un file in più costa meno
/// di un nome che mente. ⚠️ Non è un import e non compare nella pagina Sorgenti (stessa ragione della
/// deriva): si legge in Diagnostica.</para>
/// </summary>
public sealed class ReleaseSweepHostedService : BackgroundService
{
    /// <summary>Ogni quanto. Un giorno: il fatto che lo muove è il <b>rollover AIRAC</b>, che capita ogni 28
    /// giorni — guardare più spesso costerebbe una passata su tutti i documenti senza poter trovare altro.</summary>
    private static readonly TimeSpan Periodo = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ReleaseSweepHostedService> _log;

    public ReleaseSweepHostedService(IServiceScopeFactory scopes, ILogger<ReleaseSweepHostedService> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        GatedImportLoop.RunAsync(
            _scopes,
            ImportCategories.ReleaseSweep,
            Periodo,
            RunOnceAsync,
            _log,
            stoppingToken,
            // ⚠️ 45s, e NON 130 come nella prima stesura. Misurato sul server vero il 2 settembre 2026,
            // in `diagnostica/avvii.txt`: il processo vive **un minuto, uno e cinquanta, quattro e
            // cinquantadue** — Passenger lo spegne per inattività appena il traffico si ferma. Un giro che
            // aspetta 130 secondi, su quel ritmo, non parte quasi mai: due dei tre avvii osservati erano
            // già finiti. E se non parte non lascia traccia — `GatedImportLoop` registra solo gli esiti,
            // non le attese interrotte, quindi il silenzio somiglia a `va tutto bene`.
            //
            // ⚠️ Il prezzo dichiarato: si perde l'ordine con la deriva (che gira a 100s e può ripuntare
            // release sotto la chiave viva). È accettabile, e vale la pena scrivere perché: la potatura
            // tocca solo le release **superate oltre soglia**, cioè righe già destinate a sparire; potarne
            // qualcuna prima di un ripuntamento fa perdere storia che sarebbe stata cancellata comunque.
            // Contro: un giro che non gira mai. Non è un pareggio.
            bootDelay: TimeSpan.FromSeconds(45));

    private async Task<bool> RunOnceAsync(IServiceProvider sp, CancellationToken ct)
    {
        var versioni = await sp.GetRequiredService<IReleaseService>().PruneAllAsync(ct);
        _log.LogInformation(
            "Sweep release: stati ricalcolati su tutti i bersagli, {Versioni} versioni archiviate potate.", versioni);
        return true;
    }
}
