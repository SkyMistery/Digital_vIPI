using Microsoft.Extensions.Diagnostics.HealthChecks;
using Vipi.Application.Diagnostics;
using Vipi.Infrastructure.Ivao;

namespace Vipi.Hosting;

/// <summary>
/// Quadro completo dello stato del modulo, per un umano: parte da <see cref="VipiReadinessCheck"/> (DB
/// raggiungibile e schema allineato, le condizioni critiche) e vi aggiunge le incongruenze dati soft-ref e la
/// freschezza della cache ATC online — degradate, non critiche: la consultazione funziona comunque.
/// <para>
/// Costa: il report di consistenza fa scansioni complete. Per la sonda che l'orchestratore ripete di continuo
/// usare <see cref="VipiReadinessCheck"/> (endpoint <c>/vsop/health/ready</c>), non questo.
/// </para>
/// </summary>
public sealed class VipiHealthCheck : IHealthCheck
{
    private readonly VipiReadinessCheck _readiness;
    private readonly OnlineAtcCache _cache;
    private readonly IConsistencyReportService _consistency;
    private readonly ConsistencyReportCache _reportCache;

    public VipiHealthCheck(VipiReadinessCheck readiness, OnlineAtcCache cache,
        IConsistencyReportService consistency, ConsistencyReportCache reportCache)
    {
        _readiness = readiness;
        _cache = cache;
        _consistency = consistency;
        _reportCache = reportCache;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        // Le condizioni critiche sono le stesse della sonda: se il DB non c'è o lo schema è indietro, il resto
        // non ha senso misurarlo. Un'unica definizione di «critico», non due che possono divergere.
        var readiness = await _readiness.CheckHealthAsync(context, ct);
        if (readiness.Status != HealthStatus.Healthy)
            return readiness;

        var snap = _cache.GetCurrent();
        var age = DateTimeOffset.UtcNow - snap.AsOf;
        var data = new Dictionary<string, object>
        {
            ["atcOnline"] = snap.Callsigns.Count,
            ["atcSnapshotAgeSeconds"] = (int)age.TotalSeconds,
        };

        // Incongruenze dati soft-ref (label/ref denormalizzati divergenti): degradato, la consultazione regge.
        // Dalla cache: l'endpoint è anonimo e il report fa scansioni complete. Vedi ConsistencyReportCache.
        var findings = await _reportCache.GetAsync(_consistency.RunAsync, ct);
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
