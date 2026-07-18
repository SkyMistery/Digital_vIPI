using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence.ReleaseTargets;

/// <summary>Descrittore vIPI aeroporto (doc 09 §3a). Chiave di release = ICAO; Document = quello dei settori foglia
/// dell'aeroporto. Catch-all dei Document vIPI non riconosciuti come APP/ACC (DescribeOrder più alto).</summary>
public sealed class AirportReleaseTarget : IReleaseTarget
{
    private readonly VipiDbContext _db;
    public AirportReleaseTarget(VipiDbContext db) => _db = db;

    public ReleaseTargetType Type => ReleaseTargetType.Airport;
    public ManagedDocKind ManagedKind => ManagedDocKind.AirportVipi;
    public int DescribeOrder => 3;

    public async Task<int?> ResolveDocumentIdAsync(string key, CancellationToken ct = default) =>
        await _db.Sectors.AsNoTracking()
            .Where(s => s.AirportIcao == key && s.Kind == SectorKind.Airport && s.DocumentId != null)
            .Select(s => s.DocumentId).FirstOrDefaultAsync(ct);

    public async Task<string?> AuthAccCodeAsync(string key, CancellationToken ct = default) =>
        await _db.Airports.AsNoTracking()
            .Where(a => a.Icao == key).Select(a => a.Acc!.Code).FirstOrDefaultAsync(ct);

    public bool TryDescribe(Document doc, bool hasDraft, out ManagedDoc managed)
    {
        managed = default!;
        if (doc.Type != DocumentType.Vipi) return false;
        // Catch-all: Document vIPI non APP/ACC → aeroporto. ICAO dal settore aeroporto (vuoto se assente, come pre-refactor).
        var airSec = doc.Sectors.FirstOrDefault(s => s.Kind == SectorKind.Airport && s.AirportIcao != null);
        var icao = airSec?.AirportIcao ?? "";
        managed = new ManagedDoc(ManagedDocKind.AirportVipi, doc.Title, icao, airSec?.Acc?.Code,
            doc.Status == DocumentStatus.Published, hasDraft, doc.IsHidden,
            ReleaseTargetType.Airport, icao, doc.Id);
        return true;
    }
}
