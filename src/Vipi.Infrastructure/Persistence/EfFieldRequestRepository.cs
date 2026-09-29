using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>EF delle richieste dal campo (S56, vedi <see cref="FieldRequest"/>).</summary>
public sealed class EfFieldRequestRepository : IFieldRequestRepository
{
    private readonly VipiDbContext _db;
    public EfFieldRequestRepository(VipiDbContext db) => _db = db;

    public async Task<int> AddAsync(FieldRequest richiesta, CancellationToken ct = default)
    {
        _db.FieldRequests.Add(richiesta);
        await _db.SaveChangesAsync(ct);
        return richiesta.Id;
    }

    public Task<int> CountOpenAsync(int reporterUserId, CancellationToken ct = default) =>
        _db.FieldRequests.CountAsync(r => r.ReporterUserId == reporterUserId
            && (r.Status == FieldRequestStatus.Nuova || r.Status == FieldRequestStatus.PresaInCarico), ct);

    public Task<int> CountSinceAsync(int reporterUserId, DateTime sinceUtc, CancellationToken ct = default) =>
        _db.FieldRequests.CountAsync(r => r.ReporterUserId == reporterUserId && r.CreatedUtc >= sinceUtc, ct);

    public async Task<FieldRequestRow?> GetAsync(int id, CancellationToken ct = default) =>
        (await ListAsync(id: id, ct: ct)).FirstOrDefault();

    public Task<IReadOnlyList<FieldRequestRow>> ListAsync(int? reporterUserId = null, int? documentId = null,
        bool soloAperte = false, CancellationToken ct = default) =>
        ListAsync(reporterUserId, documentId, soloAperte, null, ct);

    private async Task<IReadOnlyList<FieldRequestRow>> ListAsync(int? reporterUserId = null, int? documentId = null,
        bool soloAperte = false, int? id = null, CancellationToken ct = default)
    {
        var q = _db.FieldRequests.AsNoTracking();
        if (id is int i) q = q.Where(r => r.Id == i);
        if (reporterUserId is int vid) q = q.Where(r => r.ReporterUserId == vid);
        if (documentId is int doc) q = q.Where(r => r.DocumentId == doc);
        if (soloAperte) q = q.Where(r => r.Status == FieldRequestStatus.Nuova || r.Status == FieldRequestStatus.PresaInCarico);

        var righe = await q
            .OrderByDescending(r => r.CreatedUtc).ThenByDescending(r => r.Id)
            .Select(r => new
            {
                r.Id, r.ReporterUserId, r.ReporterName, r.CreatedUtc, r.DocumentId,
                Titolo = r.Document != null ? r.Document.Title : null,
                r.SectionKey, r.ReleaseNumber, r.Kind, r.Body, r.Status, r.HandledByName, r.HandledUtc, r.Reply, r.DuplicateOfId,
            })
            .ToListAsync(ct);
        if (righe.Count == 0) return Array.Empty<FieldRequestRow>();

        // L'incarico nato dalla richiesta, in una query sola per tutto l'elenco.
        var ids = righe.Select(r => r.Id).ToList();
        var incarichi = (await _db.EditorTasks.AsNoTracking()
                .Where(t => t.FromRequestId != null && ids.Contains(t.FromRequestId.Value))
                .Select(t => new { t.Id, Richiesta = t.FromRequestId!.Value })
                .ToListAsync(ct))
            .GroupBy(t => t.Richiesta).ToDictionary(g => g.Key, g => g.Min(t => t.Id));

        return righe.Select(r => new FieldRequestRow(r.Id, r.ReporterUserId, r.ReporterName, r.CreatedUtc, r.DocumentId,
                r.Titolo, r.SectionKey, r.ReleaseNumber, r.Kind, r.Body, r.Status, r.HandledByName, r.HandledUtc, r.Reply,
                r.DuplicateOfId, incarichi.TryGetValue(r.Id, out var t) ? t : null))
            .ToList();
    }

    public async Task<bool> SetStatusAsync(int id, FieldRequestStatus status, int handledByUserId, string handledByName,
        string reply, int? duplicateOfId, CancellationToken ct = default)
    {
        var r = await _db.FieldRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return false;
        r.Status = status;
        r.HandledByUserId = handledByUserId;
        r.HandledByName = handledByName.Length > 128 ? handledByName[..128] : handledByName;
        r.HandledUtc = DateTime.UtcNow;
        r.Reply = reply;
        r.DuplicateOfId = duplicateOfId;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<string?> SectionTitleAsync(int documentId, string sectionKey, CancellationToken ct = default)
    {
        // La versione corrente (la pubblicata); se non c'è, la più recente.
        var versione = await _db.Documents.AsNoTracking().Where(d => d.Id == documentId)
                           .Select(d => d.CurrentVersionId).FirstOrDefaultAsync(ct)
                       ?? await _db.DocumentVersions.AsNoTracking().Where(v => v.DocumentId == documentId)
                           .OrderByDescending(v => v.VersionNumber).Select(v => (int?)v.Id).FirstOrDefaultAsync(ct);
        if (versione is not int v) return null;
        return await _db.DocumentSections.AsNoTracking()
            .Where(s => s.DocumentVersionId == v && s.SectionKey == sectionKey)
            .Select(s => s.Title).FirstOrDefaultAsync(ct);
    }
}
