using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Rilegge automaticamente <b>TA e piste</b> di tutti gli aeroporti dalla sorgente (default ogni 24h),
/// oltre ai bottoni che già esistono (reimport nell'editor aeroporto, massivo su
/// <c>/services/vsop/admin/airports</c>, «Genera documenti»).
///
/// <para>Job di sistema: nessuna authz utente, delega al core condiviso col manual
/// (<see cref="IAirportDataImportUseCase"/>, che passa per lo stesso <c>SourceMergeInputs</c> del bottone).
/// Il gate della policy sta lì, non qui.</para>
///
/// <para>⚠️ Il documento <b>non</b> viene rigenerato: import e generazione sono scollegati (doc 03 §4.3),
/// come per Settori e SID. Il dato nuovo entra nel sito al prossimo «Genera documenti».</para>
/// </summary>
public sealed class AirportDataImportHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IvaoOptions _opt;
    private readonly ILogger<AirportDataImportHostedService> _log;

    public AirportDataImportHostedService(
        IServiceScopeFactory scopes, IOptions<IvaoOptions> opt, ILogger<AirportDataImportHostedService> log)
    {
        _scopes = scopes;
        _opt = opt.Value;
        _log = log;
    }

    // Gated: ultimo della fila (50s) perché lavora sugli aeroporti che i giri precedenti hanno creato —
    // Acc 15s, Sid 30s, AirportSector 40s, SpecialArea 45s. Retry 1h su errore.
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        GatedImportLoop.RunAsync(_scopes, ImportCategories.AirportData,
            TimeSpan.FromHours(Math.Max(1, _opt.AirportDataImportHours)), ImportOnceAsync, _log, stoppingToken,
            bootDelay: TimeSpan.FromSeconds(50));

    private async Task<bool> ImportOnceAsync(IServiceProvider sp, CancellationToken ct)
    {
        var import = sp.GetRequiredService<IAirportDataImportUseCase>();

        try
        {
            var r = await import.RunAsync(ct);
            if (r.Skipped)
            {
                _log.LogInformation("Giro TA/piste saltato: entrambe le categorie sono escluse in Sorgenti.");
                return true;
            }

            foreach (var f in r.Failures)
                _log.LogWarning(f.Error, "Rilettura TA/piste di {Icao} fallita; gli altri aeroporti proseguono.", f.Icao);

            _log.LogInformation("Giro TA/piste automatico: {Airports} aeroporti aggiornati, {Failed} falliti. " +
                "Documento non generato (scollegato, doc 03).", r.Airports, r.Failures.Count);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            // tipicamente credenziali sorgente assenti: salta senza rumore (non un fallimento da ritentare a 1h).
            _log.LogInformation("Giro TA/piste saltato: {Reason}", ex.Message);
            return true;
        }
    }
}
