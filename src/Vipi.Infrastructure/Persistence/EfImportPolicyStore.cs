using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>EF: policy di import globale come riga singola (Id=1), creata col default opt-out se assente.</summary>
public sealed class EfImportPolicyStore : IImportPolicyStore
{
    private readonly VipiDbContext _db;
    public EfImportPolicyStore(VipiDbContext db) => _db = db;

    public async Task<ImportPolicySnapshot> GetAsync(CancellationToken ct = default)
    {
        var row = await _db.ImportPolicies.AsNoTracking().FirstOrDefaultAsync(ct);
        return row is null
            ? ImportPolicySnapshot.AllImported
            : new ImportPolicySnapshot(row.ImportTransitionAltitude, row.ImportRunways, row.ImportSectors,
                row.ImportSids, row.ImportSpecialAreas);
    }

    public async Task SaveAsync(ImportPolicySnapshot policy, int updatedByUserId, CancellationToken ct = default)
    {
        var row = await _db.ImportPolicies.FirstOrDefaultAsync(ct);
        if (row is null)
        {
            row = new ImportPolicy { Id = 1 };
            _db.ImportPolicies.Add(row);
        }
        row.ImportTransitionAltitude = policy.TransitionAltitude;
        row.ImportRunways = policy.Runways;
        row.ImportSectors = policy.Sectors;
        row.ImportSids = policy.Sids;
        row.ImportSpecialAreas = policy.SpecialAreas;
        row.UpdatedUtc = DateTime.UtcNow;
        row.UpdatedByUserId = updatedByUserId;
        await _db.SaveChangesAsync(ct);
    }
}
