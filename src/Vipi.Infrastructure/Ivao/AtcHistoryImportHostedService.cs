using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Stats;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Giro giornaliero dello storico connessioni ATC.
///
/// <para><b>Il primo giro è diverso da tutti gli altri</b>: se non risulta nessun giro riuscito, recupera i
/// dodici mesi che la sorgente conserva (~220 chiamate, una volta sola nella vita dell'installazione).
/// Dopo, ripassa solo gli ultimi giorni — che costano una manciata di chiamate e servono a due cose: mettere
/// la fine <b>vera</b> alle sessioni che il poller ha chiuso a occhio, e recuperare quel che non ha visto
/// perché l'applicazione era giù.</para>
///
/// <para>bootDelay 70s: dopo tutti gli altri giri (ACC 15s, anagrafica 25s, SID 30s, settori 40s, TA/piste
/// 50s), perché è il più lungo e il meno urgente.</para>
/// </summary>
public sealed class AtcHistoryImportHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IvaoOptions _opt;
    private readonly ILogger<AtcHistoryImportHostedService> _log;

    public AtcHistoryImportHostedService(
        IServiceScopeFactory scopes, IOptions<IvaoOptions> opt, ILogger<AtcHistoryImportHostedService> log)
    {
        _scopes = scopes;
        _opt = opt.Value;
        _log = log;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        GatedImportLoop.RunAsync(
            _scopes,
            ImportCategories.AtcHistory,
            TimeSpan.FromHours(Math.Max(1, _opt.AtcHistoryImportHours)),
            RunOnceAsync,
            _log,
            stoppingToken,
            bootDelay: TimeSpan.FromSeconds(70));

    private async Task<bool> RunOnceAsync(IServiceProvider sp, CancellationToken ct)
    {
        var stato = sp.GetRequiredService<IImportStateStore>();
        var primoGiro = await stato.GetLastSuccessAsync(ImportCategories.AtcHistory, ct) is null;

        var giorni = primoGiro
            ? Math.Max(1, _opt.AtcHistoryBackfillDays)
            : Math.Max(1, _opt.AtcHistoryRefreshDays);

        var a = DateTimeOffset.UtcNow;
        var da = a.AddDays(-giorni);

        if (primoGiro)
            _log.LogInformation(
                "Storico ATC: primo giro, recupero {Giorni} giorni (oltre non esiste: la sorgente conserva ~366 giorni).",
                giorni);

        var esito = await sp.GetRequiredService<AtcHistoryImportUseCase>().RunAsync(da, a, ct: ct);

        _log.LogInformation(
            "Storico ATC {Da:yyyy-MM-dd}→{A:yyyy-MM-dd}: {Lette} sessioni lette da {Prefissi} prefissi, " +
            "{Create} create, {Agg} aggiornate, {Turni} turni corretti.",
            da, a, esito.Fetched, esito.Prefixes, esito.Created, esito.Updated, esito.ShiftsFixed);

        return esito.Fetched > 0 || !primoGiro;
    }
}
