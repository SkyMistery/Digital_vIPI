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
internal sealed class AtcHistoryImportHostedService : BackgroundService
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
        var ultimo = await stato.GetLastSuccessAsync(ImportCategories.AtcHistory, ct);
        var primoGiro = ultimo is null;

        var a = DateTimeOffset.UtcNow;
        var da = InizioFinestra(a, ultimo, _opt.AtcHistoryBackfillDays, _opt.AtcHistoryRefreshDays);
        var giorni = (int)Math.Ceiling((a - da).TotalDays);

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

    /// <summary>Margine sull'ultimo giro riuscito: le sessioni a cavallo dell'ultimo ripasso si rileggono.</summary>
    internal static readonly TimeSpan MargineSullUltimoGiro = TimeSpan.FromDays(1);

    /// <summary>
    /// Da dove ripassare lo storico.
    ///
    /// <para>🔴 T-031 (revisione del 13 settembre 2026): dopo il primo giro si ripassavano sempre gli ultimi
    /// <c>RefreshDays</c> (2), qualunque cosa fosse successa. Un fermo di una settimana lasciava cinque giorni di
    /// sessioni mai lette, per sempre. Ora si riparte dall'ultimo giro riuscito (meno un margine) se è più
    /// indietro, con il tetto di <c>BackfillDays</c>: oltre la sorgente non conserva niente.</para>
    /// </summary>
    internal static DateTimeOffset InizioFinestra(DateTimeOffset adesso, DateTime? ultimoRiuscito,
        int backfillDays, int refreshDays)
    {
        var tetto = adesso.AddDays(-Math.Max(1, backfillDays));
        if (ultimoRiuscito is not DateTime ultimo) return tetto;

        var ripasso = adesso.AddDays(-Math.Max(1, refreshDays));
        var dallUltimo = new DateTimeOffset(DateTime.SpecifyKind(ultimo, DateTimeKind.Utc)) - MargineSullUltimoGiro;
        var da = dallUltimo < ripasso ? dallUltimo : ripasso;
        return da < tetto ? tetto : da;
    }
}
