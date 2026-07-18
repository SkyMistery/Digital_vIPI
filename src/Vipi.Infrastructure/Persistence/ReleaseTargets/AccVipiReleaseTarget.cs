using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence.ReleaseTargets;

/// <summary>Descrittore vIPI ACC (doc 09 §3a). Chiave di release = "{accCode}|{root}"; Document = quello del CTR radice
/// primario dell'ACC. ACC-wide su un solo Document (doc 08e-acc), nessun overlay (visibilità nel blockmeta).</summary>
public sealed class AccVipiReleaseTarget : IReleaseTarget
{
    private readonly VipiDbContext _db;
    public AccVipiReleaseTarget(VipiDbContext db) => _db = db;

    public ReleaseTargetType Type => ReleaseTargetType.AccVipi;
    public ManagedDocKind ManagedKind => ManagedDocKind.AccVipi;
    public int DescribeOrder => 2;

    public async Task<int?> ResolveDocumentIdAsync(string key, CancellationToken ct = default)
    {
        var accCode = key.Split('|', 2)[0];
        return await _db.Sectors.AsNoTracking()
            .Where(s => s.Acc!.Code == accCode && s.Type == SectorType.Ctr
                        && s.ParentSectorId == null && s.IsActive && s.DocumentId != null)
            .OrderBy(s => s.CoverageOrder).ThenBy(s => s.Callsign)
            .Select(s => s.DocumentId).FirstOrDefaultAsync(ct);
    }

    public Task<string?> AuthAccCodeAsync(string key, CancellationToken ct = default) =>
        Task.FromResult<string?>(key.Split('|', 2)[0]);

    public bool TryDescribe(Document doc, bool hasDraft, out ManagedDoc managed)
    {
        managed = default!;
        if (doc.Type != DocumentType.Vipi) return false;
        var primary = doc.Sectors.FirstOrDefault(s => s.IsPrimary) ?? doc.Sectors.FirstOrDefault();
        if (primary is not { Type: SectorType.Ctr, ParentSectorId: null }) return false;
        var acc = primary.Acc?.Code ?? "";
        managed = new ManagedDoc(ManagedDocKind.AccVipi, doc.Title, primary.Callsign, acc,
            doc.Status == DocumentStatus.Published, hasDraft, doc.IsHidden,
            ReleaseTargetType.AccVipi, $"{acc}|{primary.Callsign}", doc.Id);
        return true;
    }
}
