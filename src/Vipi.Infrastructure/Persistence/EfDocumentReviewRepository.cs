using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Impl. EF di <see cref="IDocumentReviewRepository"/>. Reverse-lookup settore→documenti:
/// - ACC vIPI + APP: i <c>Sector</c> con <c>DocumentId</c> nell'ACC del settore (radice ACC vIPI, APP standalone,
///   o il settore stesso se ha un documento proprio);
/// - vLOA: le <c>NeighbourCandidate</c> confermate (con <c>VloaDocumentId</c>) dell'ACC home il cui elenco di
///   settori domestici confinanti contiene il callsign.
/// </summary>
public sealed class EfDocumentReviewRepository : IDocumentReviewRepository
{
    private readonly VipiDbContext _db;
    public EfDocumentReviewRepository(VipiDbContext db) => _db = db;

    private static readonly StringComparer OIC = StringComparer.OrdinalIgnoreCase;

    public async Task<IReadOnlyList<AffectedDoc>> FindDocumentsForSectorAsync(
        string composePosition, string accCode, CancellationToken ct = default)
    {
        var ids = new HashSet<int>();

        var accId = await _db.Accs.Where(a => a.Code == accCode).Select(a => (int?)a.Id).FirstOrDefaultAsync(ct);
        if (accId is int aid)
        {
            // Documento ACC vIPI (settore primario) + documenti APP standalone + eventuale documento proprio del settore.
            var sectorDocs = await _db.Sectors
                .Where(s => s.DocumentId != null && s.AccId == aid
                            && (s.IsPrimary || s.Type == SectorType.App || s.Callsign == composePosition))
                .Select(s => s.DocumentId!.Value)
                .Distinct()
                .ToListAsync(ct);
            foreach (var id in sectorDocs) ids.Add(id);
        }

        // vLOA confinanti: il callsign è tra i settori domestici confinanti di una coppia con vLOA generata.
        var cands = await _db.NeighbourCandidates
            .Where(c => c.VloaDocumentId != null && c.HomeAccCode == accCode && c.AdjacentHomeCallsigns != null)
            .Select(c => new { c.VloaDocumentId, c.AdjacentHomeCallsigns })
            .ToListAsync(ct);
        foreach (var c in cands)
        {
            List<string>? list;
            try { list = JsonSerializer.Deserialize<List<string>>(c.AdjacentHomeCallsigns!); }
            catch (JsonException) { list = null; }
            if (list is not null && list.Any(x => OIC.Equals(x, composePosition)))
                ids.Add(c.VloaDocumentId!.Value);
        }

        if (ids.Count == 0) return Array.Empty<AffectedDoc>();

        return await _db.Documents
            .Where(d => ids.Contains(d.Id))
            .Select(d => new AffectedDoc(d.Id, d.Title))
            .ToListAsync(ct);
    }

    public async Task SetReviewAsync(int documentId, DateTime whenUtc, string reason, CancellationToken ct = default)
    {
        var d = await _db.Documents.FirstOrDefaultAsync(x => x.Id == documentId, ct);
        if (d is null) return;
        d.NeedsReviewUtc = whenUtc;
        d.ReviewReason = reason;
        await _db.SaveChangesAsync(ct);
    }

    public async Task ClearReviewAsync(int documentId, CancellationToken ct = default)
    {
        var d = await _db.Documents.FirstOrDefaultAsync(x => x.Id == documentId, ct);
        if (d is null) return;
        d.NeedsReviewUtc = null;
        d.ReviewReason = null;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<DocumentReviewState?> GetReviewAsync(int documentId, CancellationToken ct = default) =>
        await _db.Documents.AsNoTracking()
            .Where(d => d.Id == documentId)
            .Select(d => new DocumentReviewState(d.NeedsReviewUtc, d.ReviewReason))
            .FirstOrDefaultAsync(ct);

    public async Task<string?> GetDocAccCodeAsync(int documentId, CancellationToken ct = default) =>
        await _db.Sectors.AsNoTracking()
            .Where(s => s.DocumentId == documentId && s.Acc != null)
            .OrderByDescending(s => s.IsPrimary)
            .Select(s => s.Acc!.Code)
            .FirstOrDefaultAsync(ct);
}
