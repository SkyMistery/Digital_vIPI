using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vipi.Application.Auth;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Ri-verifica periodicamente (default 24h) il roster degli staffisti via API IVAO e disattiva chi non è
/// più staff IT. Prima esecuzione dopo un intervallo intero (non all'avvio), così i login appena registrati
/// restano visibili subito. Resiliente: gli errori vengono loggati ma non uccidono il loop.
/// </summary>
public sealed class StaffRosterVerificationService : BackgroundService
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var period = TimeSpan.FromHours(Math.Max(1, _opt.StaffVerifyHours));
        using var timer = new PeriodicTimer(period);

        // Prima esecuzione dopo un periodo intero (non subito all'avvio).
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            await VerifyOnceAsync(stoppingToken);
    }

    private async Task VerifyOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var roster = scope.ServiceProvider.GetRequiredService<IStaffRosterService>();
            var deactivated = await roster.VerifyAllAsync(ct);
            _log.LogInformation("Verifica roster staff: {Deactivated} disattivati.", deactivated);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutdown: ignora
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Verifica roster staff fallita; riprovo al prossimo ciclo.");
        }
    }
}
