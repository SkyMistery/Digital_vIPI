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

        // Il ciclo riparte dai file, non dalla copia in memoria: la cache di processo non scade da sola, e senza
        // questa riga un'applicazione che resta su per settimane completerebbe le SID (e suggerirebbe i punti agli
        // editor) su un catalogo vecchio quanto l'ultimo riavvio.
        sp.GetRequiredService<SectorfileCache>().Invalidate();

        var icaos = await repo.ListAirportIcaosAsync(ct);
        if (icaos.Count == 0) return false;   // aeroporti non ancora importati: non "consumare" il gate, riprova a breve

        int airports = 0, sids = 0, failed = 0;
        foreach (var icao in icaos)
        {
            try
            {
                var n = await importer.ImportAsync(icao, ct);
                if (n > 0) { airports++; sids += n; }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                // Warning, non Debug: a Debug un fallimento per-aeroporto era invisibile in produzione, e ha
                // tenuto nascosto per cicli interi un import rotto sugli scali principali (vedi la nota in
                // EfAirportRepository.ReplaceImportedSidsAsync sulle revisioni con StableKey condivisa).
                failed++;
                _log.LogWarning(ex, "Import SID {Icao} fallito; gli altri aeroporti proseguono.", icao);
            }
        }
        await WarnStaleAliasesAsync(sp, ct);

        if (failed > 0)
            _log.LogWarning("Import SID automatico: {Airports} aeroporti, {Sids} SID, {Failed} FALLITI su {Total}.",
                airports, sids, failed, icaos.Count);
        else
            _log.LogInformation("Import SID automatico: {Airports} aeroporti, {Sids} SID.", airports, sids);
        return true;
    }

    /// <summary>
    /// Segnala gli alias fix che puntano a un punto <b>non più presente</b> nel catalogo.
    ///
    /// <para>Serve perché un alias è <b>autoritativo</b>: <c>ResolveFix</c> lo consulta prima dell'espansione del
    /// prefisso, quindi un bersaglio sparito dal sectorfile continua a essere scritto negli import senza che
    /// niente protesti — la riga non risulta nemmeno «fix da verificare». La pagina Sorgenti lo mostra, ma
    /// dipende da qualcuno che la apra: qui la stessa cosa finisce nei log di ogni ciclo.</para>
    ///
    /// <para>Best-effort: un problema qui non deve far fallire un import riuscito.</para>
    /// </summary>
    private async Task WarnStaleAliasesAsync(IServiceProvider sp, CancellationToken ct)
    {
        try
        {
            var catalog = await sp.GetRequiredService<INavaidSource>().GetAsync(ct);
            if (catalog.Names.Count == 0) return;   // sorgente muta: non si accusa nessuno

            var aliases = await sp.GetRequiredService<ISidFixAliasRepository>().ListAsync(ct);
            var stale = aliases.Where(a => NavaidCheck.IsUnknown(a.FixName, catalog))
                               .Select(a => $"{a.Prefix}→{a.FixName}")
                               .ToList();
            if (stale.Count > 0)
                _log.LogWarning("Alias fix che puntano a punti inesistenti ({Count}): {Aliases}. " +
                    "Un alias vince sull'espansione del prefisso, quindi l'import continua a scrivere quei nomi. " +
                    "Si tolgono da /services/vsop/admin/sources.", stale.Count, string.Join(", ", stale));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { _log.LogDebug(ex, "Controllo degli alias fix non riuscito; l'import non ne dipende."); }
    }
}
