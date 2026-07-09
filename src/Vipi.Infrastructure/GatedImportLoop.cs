using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure;

/// <summary>
/// Loop comune agli import periodici: salta il primo fetch all'avvio se la categoria è ancora "fresca" (entro il
/// periodo, in base a <see cref="IImportStateStore"/> persistente) — così un riavvio dell'app non richiama la
/// sorgente. Marca il successo solo se il run riesce; su fallimento riprova a un intervallo breve (retry), non
/// dopo il periodo pieno. I trigger manuali (pagine admin/editor) restano indipendenti da questo gate.
/// </summary>
public static class GatedImportLoop
{
    public static async Task RunAsync(
        IServiceScopeFactory scopes,
        string category,
        TimeSpan period,
        Func<IServiceProvider, CancellationToken, Task<bool>> runOnce,
        ILogger log,
        CancellationToken ct,
        TimeSpan? bootDelay = null,
        TimeSpan? retry = null)
    {
        var boot = bootDelay ?? TimeSpan.FromSeconds(30);
        var retryDelay = retry ?? TimeSpan.FromHours(1);

        // Ritardo iniziale: se ancora fresco, aspetta fino alla scadenza; altrimenti parti dopo il boot delay.
        var initial = boot;
        try
        {
            using var scope = scopes.CreateScope();
            var last = await scope.ServiceProvider.GetRequiredService<IImportStateStore>().GetLastSuccessAsync(category, ct);
            if (last is DateTime l)
            {
                var due = l + period;
                initial = DateTime.UtcNow >= due ? boot : due - DateTime.UtcNow;
            }
        }
        catch (Exception ex) { log.LogDebug(ex, "Lettura stato import {Category} fallita; parto col boot delay.", category); }

        try { await Task.Delay(initial, ct); }
        catch (OperationCanceledException) { return; }

        while (!ct.IsCancellationRequested)
        {
            var success = false;
            try
            {
                using var scope = scopes.CreateScope();
                success = await runOnce(scope.ServiceProvider, ct);
                if (success)
                    await scope.ServiceProvider.GetRequiredService<IImportStateStore>()
                        .MarkSuccessAsync(category, DateTime.UtcNow, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex) { log.LogWarning(ex, "Import {Category} fallito; retry tra {Retry}.", category, retryDelay); }

            var wait = success ? period : retryDelay;
            try { await Task.Delay(wait, ct); }
            catch (OperationCanceledException) { return; }
        }
    }
}
