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
            // ⚠️ 45s, e NON 130 come nella prima stesura. Il perché va letto per intero, perché la prima
            // ragione scritta qui era SBAGLIATA e la correzione è istruttiva.
            //
            // Il fatto: sul server vero il processo vive poco. In `diagnostica/avvii.txt` del 2 settembre
            // 2026, tre avvii di fila da **1:00, 1:49 e 4:52** — Passenger lo spegne per inattività appena
            // il traffico si ferma (ogni arresto è ORDINATO: non sono crash).
            //
            // ⚠️ Da lì avevo dedotto che i giri oltre il minuto «non partono mai». È FALSO, e a smentirlo
            // è stata la pagina Sorgenti: le aree regolamentate (45s), i navaid (60s) e soprattutto la
            // DERIVA (100s) hanno tutte un ultimo esito fresco. Su una giornata gli avvii lunghi capitano,
            // e il gate delle 24 ore ha bisogno che ne capiti uno solo. Tre avvii non sono una giornata:
            // dedurre da tre campioni presi in dieci minuti era la misura sbagliata.
            //
            // Quel che resta vero, ed è la ragione buona: questa potatura prima girava a OGNI AVVIO
            // (`PruneVipiReleases`, sincrona), e spostandola in un giro l'ho resa la più lontana della
            // scaletta dopo la retention del traffico. Su un host con avvii di un minuto, 130 secondi è il
            // posto più facile da mancare, e non c'è niente da guadagnare a stare lì.
            //
            // ⚠️ Il prezzo dichiarato: si perde l'ordine con la deriva, che può ripuntare release sotto la
            // chiave viva. Vale poco: la potatura tocca solo le release **superate oltre soglia**, cioè
            // righe già destinate a sparire; potarne qualcuna prima di un ripuntamento fa perdere storia
            // che sarebbe stata cancellata comunque.
            bootDelay: TimeSpan.FromSeconds(45));

    private async Task<bool> RunOnceAsync(IServiceProvider sp, CancellationToken ct)
    {
        var versioni = await sp.GetRequiredService<IReleaseService>().PruneAllAsync(ct);
        _log.LogInformation(
            "Sweep release: stati ricalcolati su tutti i bersagli, {Versioni} versioni archiviate potate.", versioni);
        return true;
    }
}
