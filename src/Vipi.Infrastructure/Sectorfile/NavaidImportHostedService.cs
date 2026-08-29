using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;

namespace Vipi.Infrastructure.Sectorfile;

/// <summary>
/// Import automatico delle radioassistenze dal sectorfile (default 24h), gemello di
/// <see cref="SidImportHostedService"/>: stessa sorgente, stessa cadenza, stesso giro gestito.
///
/// <para>
/// ⚠️ <b>Perché un servizio suo e non una riga dentro quello delle SID</b>, visto che i file sono gli stessi.
/// Perché lo stato d'import è il modo in cui si risponde a «quando è arrivata l'ultima volta»: appeso alla
/// chiave delle SID, un import delle radioassistenze fermo da settimane sarebbe indistinguibile da uno
/// riuscito ieri. E perché il giro delle SID <b>esce prima</b> quando non ci sono aeroporti in archivio —
/// le radioassistenze non c'entrano niente con gli aeroporti, e resterebbero ostaggio di quella condizione.
/// </para>
/// <para>⚠️ La <b>cache</b> del sectorfile non si invalida qui: la invalida il giro delle SID, che gira sulla
/// stessa cadenza e sugli stessi file. Invalidarla due volte vorrebbe dire scaricare gli otto file due volte
/// per niente.</para>
/// </summary>
public sealed class NavaidImportHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly SectorfileOptions _opt;
    private readonly ILogger<NavaidImportHostedService> _log;

    public NavaidImportHostedService(
        IServiceScopeFactory scopes, IOptions<SectorfileOptions> opt, ILogger<NavaidImportHostedService> log)
    {
        _scopes = scopes;
        _opt = opt.Value;
        _log = log;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_opt.RawBaseUrl)) return Task.CompletedTask;   // sorgente non configurata

        // Dopo il giro delle SID (boot delay più lungo del suo): quello scarica e mette in cache gli otto file,
        // questo li rilegge dalla cache. All'incontrario funzionerebbe lo stesso, ma scaricherebbe due volte.
        return GatedImportLoop.RunAsync(_scopes, ImportCategories.Navaid,
            TimeSpan.FromHours(Math.Max(1, _opt.ImportHours)), RunOnceAsync, _log, stoppingToken,
            bootDelay: TimeSpan.FromSeconds(60));
    }

    private async Task<bool> RunOnceAsync(IServiceProvider sp, CancellationToken ct)
    {
        var r = await sp.GetRequiredService<INavaidImporter>().RunAsync(ct);

        // ⚠️ Saltato = NON si consuma il gate: la pagina Sorgenti direbbe «ultimo giro riuscito: adesso» su un
        // giro che non ha letto niente. E le due ragioni si distinguono nei log, perché una è una decisione
        // dell'amministratore e l'altra è un guasto.
        if (r.Saltato is NavaidImportSkip.Esclusa)
        {
            _log.LogInformation("Import radioassistenze escluso dalla policy: le gestisce una persona.");
            return false;
        }
        if (r.Saltato is NavaidImportSkip.SorgenteMuta)
        {
            _log.LogWarning("Import radioassistenze: il catalogo punti è vuoto (repo spostato o rete giù?).");
            return false;
        }

        var e = r.Esito!;
        _log.LogInformation(
            "Import radioassistenze: {Create} create, {Aggiornate} aggiornate, {Invariate} invariate (su {Totale} dalla sorgente).",
            e.Create, e.Aggiornate, e.Invariate, r.DallaSorgente);
        return true;
    }
}
