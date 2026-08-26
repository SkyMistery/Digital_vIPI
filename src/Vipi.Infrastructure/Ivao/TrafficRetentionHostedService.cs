using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Stats;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Giro notturno che pota il dettaglio delle tratte oltre i dodici mesi.
///
/// <para>⚠️ Non chiama <b>nessuna sorgente</b>: sta fra questi servizi perché condivide il loro giro
/// gestito (gate, stato, periodo), non perché importi qualcosa. Per la stessa ragione non ha nessun gate di
/// policy: la policy dice che cosa si <i>scarica</i>, e questa non scarica niente — spegnere la raccolta
/// non è una ragione per far crescere il database senza limite.</para>
///
/// <para>bootDelay 150s: dopo tutti gli import, così una potatura non si accavalla a una scrittura grossa.</para>
/// </summary>
public sealed class TrafficRetentionHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IvaoOptions _opt;
    private readonly ILogger<TrafficRetentionHostedService> _log;

    public TrafficRetentionHostedService(
        IServiceScopeFactory scopes, IOptions<IvaoOptions> opt, ILogger<TrafficRetentionHostedService> log)
    {
        _scopes = scopes;
        _opt = opt.Value;
        _log = log;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        GatedImportLoop.RunAsync(
            _scopes,
            ImportCategories.TrafficRetention,
            TimeSpan.FromHours(Math.Max(1, _opt.TrafficRetentionHours)),
            RunOnceAsync,
            _log,
            stoppingToken,
            bootDelay: TimeSpan.FromSeconds(150));

    private async Task<bool> RunOnceAsync(IServiceProvider sp, CancellationToken ct)
    {
        var esito = await sp.GetRequiredService<TrafficRetentionUseCase>()
            .RunAsync(DateTimeOffset.UtcNow, Math.Max(1, _opt.TrafficRetentionPerRun), ct: ct);

        if (esito.Removed > 0)
            _log.LogInformation(
                "Potatura del dettaglio traffico: {Tolte} righe oltre i dodici mesi{Ancora}.",
                esito.Removed, esito.MoreToGo ? ", altre ne restano" : "");

        // Le SESSIONI, nello stesso giro e con la stessa finestra: prima si riassumono nel mensile, poi si
        // tolgono. Due giri separati farebbero due categorie di stato e due racconti su una cosa sola.
        // ⚠️ Scaglioni più piccoli del dettaglio: ogni riga porta con sé il suo traffico in cascata.
        var sessioni = await sp.GetRequiredService<AtcSessionRetentionUseCase>()
            .RunAsync(DateTimeOffset.UtcNow, Math.Max(1, _opt.SessionRetentionPerRun), ct: ct);

        if (sessioni.Removed > 0)
            _log.LogInformation(
                "Potatura delle sessioni ATC: {Tolte} riassunte nel mensile e tolte{Ancora}.",
                sessioni.Removed, sessioni.MoreToGo ? ", altre ne restano" : "");

        return true;
    }
}
