using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Stats;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Giro notturno che consolida il <b>traffico di ogni aeroporto italiano</b>, giorno per giorno, e quanto di
/// quel traffico ha trovato un controllore acceso.
///
/// <para><b>Perché esiste un giro suo.</b> Il riempimento retroattivo (<see
/// cref="AirportTrafficBackfillHostedService"/>) guarda le <i>nostre</i> sessioni; questo guarda i campi
/// anche nelle ore in cui non c'era nessuno — che è la metà mancante di «quanto dell'Italia copriamo».</para>
///
/// <para>bootDelay 120s: ultimo di tutti. Ha bisogno che l'anagrafica aeroporti e lo storico delle sessioni
/// siano già passati, o consoliderebbe giorni senza sapere chi era aperto.</para>
///
/// <para>⚠️ Il tetto per giro costa <b>una chiamata per blocco</b> (fino a trenta giorni di un aeroporto).
/// Chi ha fretta di vedere l'anno intero alza <c>Ivao:AirportTrafficRollupPerRun</c> e paga sulla sorgente
/// nella stessa misura.</para>
/// </summary>
public sealed class AirportTrafficRollupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IvaoOptions _opt;
    private readonly ILogger<AirportTrafficRollupHostedService> _log;

    public AirportTrafficRollupHostedService(
        IServiceScopeFactory scopes, IOptions<IvaoOptions> opt, ILogger<AirportTrafficRollupHostedService> log)
    {
        _scopes = scopes;
        _opt = opt.Value;
        _log = log;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        GatedImportLoop.RunAsync(
            _scopes,
            ImportCategories.AirportTrafficRollup,
            TimeSpan.FromHours(Math.Max(1, _opt.AirportTrafficRollupHours)),
            RunOnceAsync,
            _log,
            stoppingToken,
            bootDelay: TimeSpan.FromSeconds(120));

    private async Task<bool> RunOnceAsync(IServiceProvider sp, CancellationToken ct)
    {
        var ora = DateTimeOffset.UtcNow;

        // La stessa finestra dello storico: oltre l'anno la sorgente non conserva le sessioni, e senza
        // sapere chi era aperto un conto di traffico non risponderebbe alla domanda che ci interessa.
        var da = ora.AddDays(-Math.Max(1, _opt.AtcHistoryBackfillDays));

        var esito = await sp.GetRequiredService<AirportTrafficRollupUseCase>()
            .RunAsync(da, ora, Math.Max(1, _opt.AirportTrafficRollupPerRun), ora, ct);

        if (esito.Chunks > 0)
            _log.LogInformation(
                "Traffico d'aeroporto consolidato: {Blocchi} blocchi su {Aeroporti} aeroporti, " +
                "{Giorni} giorni scritti, {Movimenti} movimenti.",
                esito.Chunks, esito.Airports, esito.Days, esito.Movements);

        return true;
    }
}
