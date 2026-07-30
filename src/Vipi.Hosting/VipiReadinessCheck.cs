using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Vipi.Infrastructure.Persistence;

namespace Vipi.Hosting;

/// <summary>
/// Sonda economica, pensata per l'orchestratore (health check di Render): il database risponde e lo schema non è
/// indietro rispetto al codice. Due query, nessuna scansione.
/// <para>
/// Sta separata da <see cref="VipiHealthCheck"/> perché quello interroga anche il report di consistenza, che fa
/// una manciata di scansioni complete: accettabile per una pagina che apre un umano, non per una sonda che
/// l'orchestratore ripete di continuo — su Neon (Postgres serverless) lo terrebbe sveglio a bruciare compute.
/// </para>
/// </summary>
public sealed class VipiReadinessCheck : IHealthCheck
{
    /// <summary>
    /// Se su questo provider lo schema è gestito dalle migrazioni EF. Su Postgres NO: <c>MigrateVipiDatabase</c>
    /// usa <c>PostgresSchemaReconciler</c> (EnsureCreated + riconciliazione delle colonne), che non scrive mai in
    /// <c>__EFMigrationsHistory</c>. Lì <c>GetPendingMigrations</c> le riporterebbe TUTTE come pendenti anche con
    /// lo schema perfettamente allineato al modello, e l'health check direbbe sempre Unhealthy — un falso allarme
    /// che rende l'endpoint inutile proprio dove serve, cioè in produzione.
    /// </summary>
    public static bool UsesEfMigrations(string? providerName) =>
        providerName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) != true;

    private readonly VipiDbContext _db;

    public VipiReadinessCheck(VipiDbContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        if (!await _db.Database.CanConnectAsync(ct))
            return HealthCheckResult.Unhealthy("Database non raggiungibile.");

        // Schema drift: il modulo migra all'avvio (MigrateVipiDatabase). Migrazioni ancora pendenti qui ⇒ auto-migrate
        // saltato/fallito o nuova migrazione aggiunta senza riavvio: lo schema è indietro rispetto al codice → Unhealthy.
        if (UsesEfMigrations(_db.Database.ProviderName))
        {
            var pending = (await _db.Database.GetPendingMigrationsAsync(ct)).ToList();
            if (pending.Count > 0)
                return HealthCheckResult.Unhealthy(
                    $"Migrazioni pendenti ({pending.Count}): schema disallineato dal codice.",
                    data: new Dictionary<string, object> { ["pendingMigrations"] = pending });
        }

        return HealthCheckResult.Healthy("OK");
    }
}
