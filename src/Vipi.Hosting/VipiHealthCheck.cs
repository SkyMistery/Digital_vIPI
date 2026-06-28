using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Vipi.Infrastructure.Ivao;
using Vipi.Infrastructure.Persistence;

namespace Vipi.Hosting;

/// <summary>
/// Health check del modulo: connettività al DB (critica) e freschezza della cache ATC online
/// (degradata, non critica: l'API IVAO può essere temporaneamente irraggiungibile).
/// </summary>
public sealed class VipiHealthCheck : IHealthCheck
{
    private readonly VipiDbContext _db;
    private readonly OnlineAtcCache _cache;

    public VipiHealthCheck(VipiDbContext db, OnlineAtcCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        if (!await _db.Database.CanConnectAsync(ct))
            return HealthCheckResult.Unhealthy("Database non raggiungibile.");

        var snap = _cache.GetCurrent();
        var age = DateTimeOffset.UtcNow - snap.AsOf;
        var data = new Dictionary<string, object>
        {
            ["atcOnline"] = snap.Callsigns.Count,
            ["atcSnapshotAgeSeconds"] = (int)age.TotalSeconds,
        };

        // Snapshot mai aggiornato o troppo vecchio: degradato (il DB e la consultazione funzionano comunque).
        if (snap.AsOf == default || age > TimeSpan.FromMinutes(5))
            return HealthCheckResult.Degraded("Cache ATC online non aggiornata (API IVAO?).", data: data);

        return HealthCheckResult.Healthy("OK", data);
    }
}
