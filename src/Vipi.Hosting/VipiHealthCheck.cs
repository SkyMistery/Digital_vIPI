using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Vipi.Application.Diagnostics;
using Vipi.Infrastructure.Ivao;
using Vipi.Infrastructure.Persistence;

namespace Vipi.Hosting;

/// <summary>
/// Health check del modulo: connettività al DB e assenza di migrazioni pendenti (critiche: schema drift ⇒ le
/// query possono riferire colonne mancanti); incongruenze dati soft-ref e freschezza della cache ATC online
/// (degradate, non critiche: la consultazione funziona comunque). Il probe sulle migrazioni vale solo dove le
/// migrazioni girano davvero: vedi <see cref="UsesEfMigrations"/>.
/// </summary>
public sealed class VipiHealthCheck : IHealthCheck
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
    private readonly OnlineAtcCache _cache;
    private readonly IConsistencyReportService _consistency;

    public VipiHealthCheck(VipiDbContext db, OnlineAtcCache cache, IConsistencyReportService consistency)
    {
        _db = db;
        _cache = cache;
        _consistency = consistency;
    }

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

        var snap = _cache.GetCurrent();
        var age = DateTimeOffset.UtcNow - snap.AsOf;
        var data = new Dictionary<string, object>
        {
            ["atcOnline"] = snap.Callsigns.Count,
            ["atcSnapshotAgeSeconds"] = (int)age.TotalSeconds,
        };

        // Incongruenze dati soft-ref (label/ref denormalizzati divergenti): degradato, la consultazione regge.
        var findings = await _consistency.RunAsync(ct);
        if (findings.Count > 0)
        {
            data["dataConsistencyFindings"] = findings.Count;
            return HealthCheckResult.Degraded(
                $"{findings.Count} incongruenze dati rilevate (vedi /vsop/admin/diagnostica).", data: data);
        }

        // Snapshot mai aggiornato o troppo vecchio: degradato (il DB e la consultazione funzionano comunque).
        if (snap.AsOf == default || age > TimeSpan.FromMinutes(5))
            return HealthCheckResult.Degraded("Cache ATC online non aggiornata (API IVAO?).", data: data);

        return HealthCheckResult.Healthy("OK", data);
    }
}
