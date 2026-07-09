using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;

namespace Vipi.Infrastructure.Sectorfile;

/// <summary>
/// Import automatico delle SID dal sectorfile GitHub (default 24h), oltre al bottone manuale nell'editor.
/// Gated (<see cref="GatedImportLoop"/>): non richiama la sorgente a ogni riavvio se ancora fresco. Job di
/// sistema (nessuna authz utente): rimpiazza le SID importate preservando manuali/priorità.
/// </summary>
public sealed class SidImportHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly SectorfileOptions _opt;
    private readonly ILogger<SidImportHostedService> _log;

    public SidImportHostedService(
        IServiceScopeFactory scopes, IOptions<SectorfileOptions> opt, ILogger<SidImportHostedService> log)
    {
        _scopes = scopes;
        _opt = opt.Value;
        _log = log;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_opt.RawBaseUrl)) return Task.CompletedTask;   // sorgente non configurata
        return GatedImportLoop.RunAsync(_scopes, ImportCategories.Sid,
            TimeSpan.FromHours(Math.Max(1, _opt.ImportHours)), RunOnceAsync, _log, stoppingToken);
    }

    private async Task<bool> RunOnceAsync(IServiceProvider sp, CancellationToken ct)
    {
        var repo = sp.GetRequiredService<IAirportSectorRepository>();
        var importer = sp.GetRequiredService<ISidImporter>();

        var icaos = await repo.ListAirportIcaosAsync(ct);
        if (icaos.Count == 0) return false;   // aeroporti non ancora importati: non "consumare" il gate, riprova a breve

        int airports = 0, sids = 0;
        foreach (var icao in icaos)
        {
            try
            {
                var n = await importer.ImportAsync(icao, ct);
                if (n > 0) { airports++; sids += n; }
            }
            catch (Exception ex) { _log.LogDebug(ex, "Import SID {Icao} saltato.", icao); }
        }
        _log.LogInformation("Import SID automatico: {Airports} aeroporti, {Sids} SID.", airports, sids);
        return true;
    }
}
