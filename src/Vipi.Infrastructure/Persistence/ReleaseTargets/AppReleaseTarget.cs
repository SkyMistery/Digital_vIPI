using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence.ReleaseTargets;

/// <summary>Descrittore APP standalone (doc 09 §3a). Chiave di release = callsign del settore APP primario; ACC = quello del settore.</summary>
public sealed class AppReleaseTarget : IReleaseTarget
{
    private readonly VipiDbContext _db;
    public AppReleaseTarget(VipiDbContext db) => _db = db;

    public ReleaseTargetType Type => ReleaseTargetType.App;
    public ManagedDocKind ManagedKind => ManagedDocKind.AppVipi;
    public int DescribeOrder => 1;

    public async Task<int?> ResolveDocumentIdAsync(string key, CancellationToken ct = default) =>
        await _db.Sectors.AsNoTracking()
            .Where(s => s.Callsign == key && s.Type == SectorType.App
                        && s.ApproachKind == ApproachKind.Standalone && s.DocumentId != null)
            .Select(s => s.DocumentId).FirstOrDefaultAsync(ct);

    public async Task<string?> AuthAccCodeAsync(string key, CancellationToken ct = default) =>
        await _db.Sectors.AsNoTracking()
            .Where(s => s.Callsign == key).Select(s => s.Acc!.Code).FirstOrDefaultAsync(ct);

    public bool TryDescribe(Document doc, bool hasDraft, out ManagedDoc managed)
    {
        managed = default!;
        if (doc.Type != DocumentType.Vipi) return false;
        var primary = doc.Sectors.FirstOrDefault(s => s.IsPrimary) ?? doc.Sectors.FirstOrDefault();
        if (primary is not { Type: SectorType.App, ApproachKind: ApproachKind.Standalone }) return false;
        managed = new ManagedDoc(ManagedDocKind.AppVipi, doc.Title, primary.Callsign, primary.Acc?.Code,
            doc.Status == DocumentStatus.Published, hasDraft, doc.IsHidden,
            ReleaseTargetType.App, primary.Callsign, doc.Id);
        return true;
    }
}
