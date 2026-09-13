using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Ri-verifica periodicamente (default 24h) il roster degli staffisti via API IVAO e disattiva chi non è
/// più staff IT. Resiliente: un giro fallito si riprova dopo un'ora, e un profilo che IVAO non restituisce
/// non cambia lo stato di nessuno.
///
/// <para>🔴 <b>Sul giro a timbro dal 13 settembre 2026 (T-033).</b> Prima era un <c>PeriodicTimer</c> senza
/// stato persistito, «prima esecuzione dopo un periodo intero»: su un host che ferma il processo ogni pochi
/// minuti il primo tick non arrivava mai, e la verifica non girava. Ora l'ultimo esito sta in
/// <see cref="ImportCategories.StaffRoster"/>, come gli altri giri: dopo un riavvio si riparte da lì. Il ritardo
/// d'avvio resta, così i login appena registrati non vengono riletti nello stesso istante.</para>
/// </summary>
internal sealed class StaffRosterVerificationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IvaoOptions _opt;
    private readonly ILogger<StaffRosterVerificationService> _log;

    public StaffRosterVerificationService(
        IServiceScopeFactory scopes,
        IOptions<IvaoOptions> opt,
        ILogger<StaffRosterVerificationService> log)
    {
        _scopes = scopes;
        _opt = opt.Value;
        _log = log;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        GatedImportLoop.RunAsync(_scopes, ImportCategories.StaffRoster,
            TimeSpan.FromHours(Math.Max(1, _opt.StaffVerifyHours)), VerifyOnceAsync, _log, stoppingToken,
            bootDelay: TimeSpan.FromMinutes(2));

    private async Task<bool> VerifyOnceAsync(IServiceProvider sp, CancellationToken ct)
    {
        var deactivated = await sp.GetRequiredService<IStaffRosterService>().VerifyAllAsync(ct);
        _log.LogInformation("Verifica roster staff: {Deactivated} disattivati.", deactivated);
        return true;
    }
}
