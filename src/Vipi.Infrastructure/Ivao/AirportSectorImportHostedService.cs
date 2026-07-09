using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Re-importa automaticamente i settori ATC degli aeroporti dalla sorgente (default ogni 24h), oltre al
/// bottone manuale nell'editor aeroporto. Job di sistema: usa direttamente porta sorgente + repository
/// (niente authz utente). Preserva IsHidden e i limiti admin. Resiliente: errori loggati senza uccidere
/// il loop; se le credenziali sorgente mancano, salta in silenzio.
/// </summary>
public sealed class AirportSectorImportHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IvaoOptions _opt;
    private readonly ILogger<AirportSectorImportHostedService> _log;

    public AirportSectorImportHostedService(
        IServiceScopeFactory scopes, IOptions<IvaoOptions> opt, ILogger<AirportSectorImportHostedService> log)
    {
        _scopes = scopes;
        _opt = opt.Value;
        _log = log;
    }

    // Gated: dopo gli ACC, poi a cadenza giornaliera; salta il fetch all'avvio se ancora fresco (retry 1h su errore).
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        GatedImportLoop.RunAsync(_scopes, Vipi.Application.Abstractions.ImportCategories.AirportSector,
            TimeSpan.FromHours(Math.Max(1, _opt.AirportSectorImportHours)), ImportOnceAsync, _log, stoppingToken,
            bootDelay: TimeSpan.FromSeconds(40));

    private async Task<bool> ImportOnceAsync(IServiceProvider sp, CancellationToken ct)
    {
        var repo = sp.GetRequiredService<IAirportSectorRepository>();
        var importer = sp.GetRequiredService<Vipi.Application.Content.IAirportSectorImporter>();
        var structure = sp.GetRequiredService<Vipi.Application.Content.IStructureEditingService>();

        // Import + proiezione dalla sorgente: se la sorgente non è configurata fallisce, ma NON deve impedire
        // il fallback shape (che lavora sul catalogo già in DB). Perciò è isolato in un proprio try.
        int created = 0, updated = 0, airports = 0, docs = 0;
        try
        {
            var icaos = await repo.ListAirportIcaosAsync(ct);
            foreach (var icao in icaos)
            {
                var (c, u) = await importer.ImportAsync(icao, ct);
                if (c == 0 && u == 0) continue;
                created += c; updated += u; airports++;

                // Documento aeroporto: creato/aggiornato in automatico per ogni aeroporto con ACC (idempotente).
                try { if ((await structure.EnsureAirportDocumentSystemAsync(icao, ct)).Created) docs++; }
                catch (Exception ex) { _log.LogDebug(ex, "Generazione documento {Icao} saltata.", icao); }
            }

            // Riproietta i Sector operativi dai cataloghi aggiornati (fonte autoritativa unica, Round 20).
            var projection = sp.GetRequiredService<ISectorProjectionService>();
            await projection.SyncFromCatalogsAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            // tipicamente credenziali sorgente assenti: salta l'import, ma prosegui col fallback shape.
            _log.LogInformation("Import settori aeroporto da sorgente saltato: {Reason}", ex.Message);
        }

        // Fallback shape tonda 5 NM per le TWR senza poligono (marcata sintetica; mai sovrascrive shape reali).
        int circles = 0;
        try
        {
            var fallback = sp.GetRequiredService<Vipi.Application.Content.ITowerShapeFallbackService>();
            circles = await fallback.ApplyAsync(ct: ct);
        }
        catch (Exception ex) { _log.LogDebug(ex, "Fallback shape TWR saltato."); }

        _log.LogInformation("Import settori aeroporto automatico: {Airports} aeroporti, settori {Created}/{Updated}, documenti nuovi {Docs}, shape TWR sintetiche {Circles}.",
            airports, created, updated, docs, circles);
        return true;
    }
}
