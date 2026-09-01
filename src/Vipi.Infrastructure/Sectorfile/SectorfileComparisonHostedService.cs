using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Diagnostics;

namespace Vipi.Infrastructure.Sectorfile;

/// <summary>
/// Il giro che confronta i cataloghi IVAO col sectorfile e lascia la fotografia in
/// <see cref="ISectorfileComparisonReport"/>. Cadenza: quella del sectorfile
/// (<see cref="SectorfileOptions.ImportHours"/>, default 24 ore).
///
/// <para>⚠️ <b>Non passa da <see cref="GatedImportLoop"/></b>, che pure è il loop di tutti i giri periodici, e
/// la ragione è una sola: quel loop tiene lo stato in <c>ImportStates</c>, cioè nel registro di ciò che
/// <b>scrive</b>, e questo giro non scrive niente. Una riga lì lo farebbe comparire nella pagina Sorgenti
/// come una categoria d'import fra le altre — con la pill «ultimo import», che sarebbe una bugia. Qui
/// «quando è girato» vive nella fotografia, dove ha senso.</para>
///
/// <para>⚠️ <b>Un guasto non ferma il giro</b>: resta scritto in <see cref="ISectorfileComparisonReport.LastError"/>
/// e la fotografia precedente <b>non si cancella</b>. Una fotografia vecchia dice ancora qualcosa; «nessun
/// rilievo» direbbe una cosa falsa.</para>
/// </summary>
public sealed class SectorfileComparisonHostedService : BackgroundService
{
    /// <summary>Ritardo d'avvio: dopo i giri d'import, che sui primi secondi hanno più diritto della rete.</summary>
    private static readonly TimeSpan Avvio = TimeSpan.FromMinutes(3);

    /// <summary>Riprova dopo un guasto: il sectorfile è su GitHub, e GitHub torna.</summary>
    private static readonly TimeSpan Riprova = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly ISectorfileComparisonReport _report;
    private readonly SectorfileOptions _opt;
    private readonly ILogger<SectorfileComparisonHostedService> _log;

    public SectorfileComparisonHostedService(IServiceScopeFactory scopes, ISectorfileComparisonReport report,
        IOptions<SectorfileOptions> opt, ILogger<SectorfileComparisonHostedService> log)
    {
        _scopes = scopes;
        _report = report;
        _opt = opt.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_opt.RawBaseUrl)) return;   // sorgente non configurata: niente confronto

        var periodo = TimeSpan.FromHours(Math.Max(1, _opt.ImportHours));
        var attesa = Avvio;

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(attesa, stoppingToken); }
            catch (OperationCanceledException) { return; }

            attesa = await ConfrontaAsync(stoppingToken) ? periodo : Riprova;
        }
    }

    /// <summary>
    /// Un giro. True se è riuscito.
    /// <para>⚠️ Il confronto vero sta in <see cref="ISectorfileComparisonRunner"/>, che è lo <b>stesso</b>
    /// codice del tasto «confronta adesso» della pagina: due implementazioni dello stesso confronto
    /// divergerebbero alla prima tolleranza che cambia.</para>
    /// </summary>
    private async Task<bool> ConfrontaAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<ISectorfileComparisonRunner>();

        var ok = await runner.RunAsync(ct);
        if (ok) _log.LogInformation("Coerenza sectorfile: {N} divergenze.", _report.Findings.Count);
        else _log.LogWarning("Coerenza sectorfile: giro fallito — {Errore}", _report.LastError);
        return ok;
    }
}
