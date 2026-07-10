using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence.ReleaseTargets;

/// <summary>Descrittore vLOA (doc 09 §3a). Chiave di release = id numerico del Document; ACC = quello del party Home.</summary>
public sealed class VloaReleaseTarget : IReleaseTarget
{
    private readonly VipiDbContext _db;
    public VloaReleaseTarget(VipiDbContext db) => _db = db;

    public ReleaseTargetType Type => ReleaseTargetType.Vloa;
    public ManagedDocKind ManagedKind => ManagedDocKind.Vloa;
    public int DescribeOrder => 0;
    public bool IncludesVisibilityOverlay => true;

    public Task<int?> ResolveDocumentIdAsync(string key, CancellationToken ct = default) =>
        Task.FromResult(int.TryParse(key, out var id) ? id : (int?)null);

    public async Task<string?> AuthAccCodeAsync(string key, CancellationToken ct = default) =>
        int.TryParse(key, out var id)
            ? await _db.Documents.AsNoTracking().Where(d => d.Id == id)
                .SelectMany(d => d.Parties).Where(p => p.Role == PartyRole.Home)
                .Select(p => p.Sector!.Acc!.Code).FirstOrDefaultAsync(ct)
            : null;

    public bool TryDescribe(Document doc, bool hasDraft, out ManagedDoc managed)
    {
        managed = default!;
        if (doc.Type != DocumentType.Vloa) return false;
        var home = doc.Parties.FirstOrDefault(p => p.Role == PartyRole.Home)?.Sector?.Acc?.Code;
        var neigh = doc.Parties.FirstOrDefault(p => p.Role == PartyRole.Neighbour)?.Sector?.Acc?.Code;
        managed = new ManagedDoc(ManagedDocKind.Vloa, doc.Title, home ?? "", home,
            doc.Status == DocumentStatus.Published, hasDraft, doc.IsHidden,
            ReleaseTargetType.Vloa, doc.Id.ToString(), doc.Id, neigh);
        return true;
    }
}
