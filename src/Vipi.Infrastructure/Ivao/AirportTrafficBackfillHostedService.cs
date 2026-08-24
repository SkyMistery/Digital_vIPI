using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Stats;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Giro notturno che ricostruisce il traffico delle sessioni d'aeroporto passate.
///
/// <para>bootDelay 90s: ultimo di tutti, dopo lo storico (70s) — riempie sessioni che lo storico ha appena
/// creato, quindi ha senso che passi dopo di lui.</para>
///
/// <para>⚠️ Il tetto per giro non è prudenza generica: costa <b>una chiamata per sessione</b>. Con
/// l'impostazione predefinita l'arretrato di un anno si recupera in più notti; chi ha fretta alza
/// <c>Ivao:AirportTrafficBackfillPerRun</c> e paga sulla sorgente nella stessa misura.</para>
/// </summary>
public sealed class AirportTrafficBackfillHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IvaoOptions _opt;
    private readonly ILogger<AirportTrafficBackfillHostedService> _log;

    public AirportTrafficBackfillHostedService(
        IServiceScopeFactory scopes, IOptions<IvaoOptions> opt, ILogger<AirportTrafficBackfillHostedService> log)
    {
        _scopes = scopes;
        _opt = opt.Value;
        _log = log;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        GatedImportLoop.RunAsync(
            _scopes,
            ImportCategories.AirportTrafficBackfill,
            TimeSpan.FromHours(Math.Max(1, _opt.AirportTrafficBackfillHours)),
            RunOnceAsync,
            _log,
            stoppingToken,
            bootDelay: TimeSpan.FromSeconds(90));

    private async Task<bool> RunOnceAsync(IServiceProvider sp, CancellationToken ct)
    {
        var ora = DateTimeOffset.UtcNow;
        // Oltre l'anno la sorgente non conserva le sessioni, quindi non ne esistono da riempire.
        var da = ora.AddDays(-Math.Max(1, _opt.AtcHistoryBackfillDays));

        var esito = await sp.GetRequiredService<AirportTrafficBackfillUseCase>()
            .RunAsync(da, Math.Max(1, _opt.AirportTrafficBackfillPerRun), ora, ct);

        if (esito.Examined > 0)
            _log.LogInformation(
                "Traffico d'aeroporto a posteriori: {Esaminate} sessioni, {Riempite} riempite con {Movimenti} movimenti, " +
                "{Saltate} lasciate a una posizione più titolata.",
                esito.Examined, esito.Filled, esito.Movements, esito.Skipped);

        return true;
    }
}
