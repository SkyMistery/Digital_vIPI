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

    /// <summary>
    /// Quante incongruenze <b>contano</b> per la salute dell'istanza.
    ///
    /// <para>⚠️ Una funzione a sé, e con un test suo, perché è una <b>decisione</b> e non un conteggio: i
    /// rilievi dell'area <see cref="ConsistencyArea.Sectorfile"/> dicono che il sectorfile Aurora e i
    /// cataloghi IVAO non concordano — vero, utile, e che non riguarda lo stato di questa istanza. Ce n'è
    /// sempre qualcuno, perché le due sorgenti hanno cadenze diverse (IVAO in continuo, il sectorfile per
    /// ciclo AIRAC): contarli qui vorrebbe dire un endpoint di salute perennemente «Degraded», cioè un
    /// monitor che qualcuno impara a ignorare — e con lui i guasti veri.</para>
    /// </summary>
    public static int ContaIncongruenze(IReadOnlyList<ConsistencyFinding> findings) =>
        findings.Count(f => f.Area != ConsistencyArea.Sectorfile);

    /// <summary>Le divergenze col sectorfile: non muovono il verdetto, ma restano nel corpo della risposta —
    /// saperlo è comodo per chi guarda, e non costa niente.</summary>
    public static int ContaDivergenzeSectorfile(IReadOnlyList<ConsistencyFinding> findings) =>
        findings.Count(f => f.Area == ConsistencyArea.Sectorfile);

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
        //
        // ⚠️ Rete attorno al report: dal 22 agosto 2026 le singole sonde si proteggono da sé e un loro guasto
        // esce come rilievo, ma il report resta pur sempre codice che gira. Se fallisce lui, questo check
        // deve dire «degradato: non so» — non «il sito è giù». Un monitor che legge Unhealthy sveglia
        // qualcuno di notte, e la differenza fra «la sonda è rotta» e «il sito è rotto» è tutta.
        IReadOnlyList<ConsistencyFinding> findings;
        try
        {
            findings = await _reportCache.GetAsync(_consistency.RunAsync, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            data["consistencyReportError"] = ex.Message;
            return HealthCheckResult.Degraded(
                "Report di consistenza non eseguibile: le condizioni critiche sono a posto, ma le " +
                "incongruenze dati non sono state verificate.", ex, data);
        }
        // ⚠️ I rilievi di COERENZA COL SECTORFILE non contano ai fini della salute, ed è una decisione, non
        // una dimenticanza: dicono che il sectorfile Aurora e i cataloghi IVAO non concordano — una cosa
        // vera, utile, e che NON riguarda lo stato di questa istanza. Ce n'è sempre qualcuno (le due
        // sorgenti hanno cadenze diverse: IVAO in continuo, il sectorfile per ciclo AIRAC), quindi
        // contarli qui vorrebbe dire un endpoint di salute perennemente «Degraded» — cioè un monitor che
        // qualcuno impara a ignorare, e con lui i guasti veri. Restano nel corpo come numero, perché
        // saperlo è comodo, ma non muovono il verdetto.
        var divergenzeSectorfile = ContaDivergenzeSectorfile(findings);
        if (divergenzeSectorfile > 0) data["sectorfileDivergences"] = divergenzeSectorfile;

        var incongruenze = ContaIncongruenze(findings);
        if (incongruenze > 0)
        {
            data["dataConsistencyFindings"] = incongruenze;
            return HealthCheckResult.Degraded(
                $"{incongruenze} incongruenze dati rilevate (vedi /services/vsop/admin/diagnostics).", data: data);
        }

        // Snapshot mai aggiornato o troppo vecchio: degradato (il DB e la consultazione funzionano comunque).
        if (snap.AsOf == default || age > TimeSpan.FromMinutes(5))
            return HealthCheckResult.Degraded("Cache ATC online non aggiornata (API IVAO?).", data: data);

        return HealthCheckResult.Healthy("OK", data);
    }
}
