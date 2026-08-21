using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Implementazione EF delle release AIRAC. Lo snapshot dello stato live è generato per tipo: in Fase 1 i tipi
/// doc-based (vLOA, Aeroporto) serializzano l'albero della versione working; ACC/APP (profile-based) arrivano in Fase 2.
/// </summary>
public sealed class EfReleaseRepository : IReleaseRepository
{
    private readonly VipiDbContext _db;
    private readonly IReleaseTargetRegistry _targets;
    public EfReleaseRepository(VipiDbContext db, IReleaseTargetRegistry targets)
    {
        _db = db;
        _targets = targets;
    }

    public async Task<string?> SnapshotWorkingAsync(ReleaseTargetType type, string key, string airacCycle, CancellationToken ct = default)
    {
        // Ramo unico post-08: tutti i tipi sono su Document. Il descrittore per-tipo risolve solo l'identità (chiave→doc).
        var target = _targets.For(type);
        var docId = await target.ResolveDocumentIdAsync(key, ct);
        if (docId is null) return null;
        var doc = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == docId, ct);
        if (doc is null) return null;

        var versionId = await WorkingVersionIdAsync(doc, ct);
        if (versionId is null) return null;

        var raw = await EfContentRepository.BuildRawFromVersionAsync(_db, versionId.Value, doc.Title, airacCycle, ct);
        if (raw is null) return null;

        // Fotografia editoriale della struttura; l'output derivato Frozen lo aggiunge ReleaseService (doc 10 §3c). La
        // visibilità (nascosti) è già dentro la copia congelata → nessun overlay separato (rimosso in doc 10 §S5).
        var payload = new DocReleasePayload { Doc = raw };
        return JsonSerializer.Serialize(payload);
    }

    public async Task<int> SaveReleaseAsync(ReleaseTargetType type, string key, string releaseCycle, DateTime effectiveUtc,
        string payloadJson, int createdByUserId, string? note, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var existing = await _db.DocReleases
            .Where(r => r.TargetType == type && r.TargetKey == key).ToListAsync(ct);

        // Una release per ciclo: le precedenti non-superate dello stesso ciclo diventano Superseded.
        foreach (var r in existing.Where(r => r.ReleaseAiracCycle == releaseCycle && r.Status != ReleaseStatus.Superseded))
            r.Status = ReleaseStatus.Superseded;

        var nextNumber = (existing.Count == 0 ? 0 : existing.Max(r => r.VersionNumber)) + 1;
        var row = new DocRelease
        {
            TargetType = type, TargetKey = key, VersionNumber = nextNumber,
            ReleaseAiracCycle = releaseCycle, ReleaseEffectiveUtc = effectiveUtc,
            Status = effectiveUtc <= now ? ReleaseStatus.Effective : ReleaseStatus.Scheduled,
            PayloadJson = payloadJson, CreatedByUserId = createdByUserId, CreatedUtc = now, Note = note,
        };
        _db.DocReleases.Add(row);

        // Ricalcola gli stati: l'effettiva è quella con ReleaseEffectiveUtc <= now più recente; le altre passate
        // diventano Superseded, le future Scheduled.
        RecomputeStatuses(existing.Append(row).ToList(), now);

        await _db.SaveChangesAsync(ct);
        return row.Id;
    }

    public async Task PublishWorkingVersionAsync(ReleaseTargetType type, string key, int actorUserId, string airacCycle, CancellationToken ct = default)
    {
        var docId = await _targets.For(type).ResolveDocumentIdAsync(key, ct);
        if (docId is null) return;

        // Promuove la bozza più recente (se c'è); no-op se il documento non ha bozze in lavorazione.
        var draft = await _db.DocumentVersions.Include(v => v.Document)
            .Where(v => v.DocumentId == docId && v.Status == DocumentStatus.Draft)
            .OrderByDescending(v => v.VersionNumber).FirstOrDefaultAsync(ct);
        if (draft is null) return;

        var doc = draft.Document!;
        var now = DateTime.UtcNow;

        // Archivia la pubblicata precedente (se diversa) — stessa semantica di EfEditingRepository.PublishAsync.
        if (doc.CurrentVersionId is int prevId && prevId != draft.Id)
        {
            var prev = await _db.DocumentVersions.FirstOrDefaultAsync(v => v.Id == prevId, ct);
            if (prev is not null) prev.Status = DocumentStatus.Archived;
        }

        draft.Status = DocumentStatus.Published;
        doc.CurrentVersionId = draft.Id;
        doc.Status = DocumentStatus.Published;
        doc.LastUpdatedUtc = now;
        doc.LastUpdatedAiracCycle = airacCycle;

        AuditScribe.Write(_db, actorUserId, AuditAction.Publish, "DocumentVersion", draft.Id.ToString(),
            new { doc.Id, draft.VersionNumber, Reason = "publish-now-release" }, now);

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ReleaseInfo>> ListAsync(ReleaseTargetType type, string key, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var rows = await _db.DocReleases.AsNoTracking()
            .Where(r => r.TargetType == type && r.TargetKey == key)
            .OrderByDescending(r => r.ReleaseEffectiveUtc).ThenByDescending(r => r.VersionNumber)
            .ToListAsync(ct);

        var effectiveId = rows.Where(r => r.ReleaseEffectiveUtc <= now && r.Status != ReleaseStatus.Superseded)
            .OrderByDescending(r => r.ReleaseEffectiveUtc).ThenByDescending(r => r.VersionNumber)
            .Select(r => (int?)r.Id).FirstOrDefault();

        return rows.Select(r => new ReleaseInfo(
            r.Id, r.VersionNumber, r.ReleaseAiracCycle, r.ReleaseEffectiveUtc, r.Status,
            r.CreatedByUserId, r.CreatedUtc, r.Note, r.Id == effectiveId)).ToList();
    }

    public async Task<DocRelease?> GetEffectiveAsync(ReleaseTargetType type, string key, DateTime atUtc, CancellationToken ct = default) =>
        await _db.DocReleases.AsNoTracking()
            .Where(r => r.TargetType == type && r.TargetKey == key
                        && r.Status != ReleaseStatus.Superseded && r.ReleaseEffectiveUtc <= atUtc)
            .OrderByDescending(r => r.ReleaseEffectiveUtc).ThenByDescending(r => r.VersionNumber)
            .FirstOrDefaultAsync(ct);

    public Task<DocRelease?> GetByIdAsync(int releaseId, CancellationToken ct = default) =>
        _db.DocReleases.AsNoTracking().FirstOrDefaultAsync(r => r.Id == releaseId, ct);

    public async Task<(ReleaseTargetType Type, string Key)?> CancelAsync(int releaseId, CancellationToken ct = default)
    {
        var rel = await _db.DocReleases.FirstOrDefaultAsync(r => r.Id == releaseId, ct);
        if (rel is null) return null;
        var (type, key) = (rel.TargetType, rel.TargetKey);
        _db.DocReleases.Remove(rel);
        await _db.SaveChangesAsync(ct);

        // Ricalcola gli stati delle rimanenti dello stesso bersaglio (una potrebbe tornare effettiva).
        var rest = await _db.DocReleases.Where(r => r.TargetType == type && r.TargetKey == key).ToListAsync(ct);
        if (rest.Count > 0) { RecomputeStatuses(rest, DateTime.UtcNow); await _db.SaveChangesAsync(ct); }
        return (type, key);
    }

    public Task<string?> GetAuthAccCodeAsync(ReleaseTargetType type, string key, CancellationToken ct = default) =>
        _targets.For(type).AuthAccCodeAsync(key, ct);

    public async Task<IReadOnlyDictionary<(ReleaseTargetType Type, string Key), ReleaseSummary>> SummariesAsync(
        IReadOnlyList<(ReleaseTargetType Type, string Key)> targets, CancellationToken ct = default)
    {
        var result = new Dictionary<(ReleaseTargetType, string), ReleaseSummary>();
        if (targets.Count == 0) return result;

        var now = DateTime.UtcNow;
        var types = targets.Select(t => t.Type).Distinct().ToList();
        var keys = targets.Select(t => t.Key).Distinct().ToList();

        // Una sola query sul prodotto (types × keys); le combinazioni non richieste sono scartate col JOIN in memoria.
        var rows = await _db.DocReleases.AsNoTracking()
            .Where(r => types.Contains(r.TargetType) && keys.Contains(r.TargetKey) && r.Status != ReleaseStatus.Superseded)
            .Select(r => new { r.TargetType, r.TargetKey, r.ReleaseAiracCycle, r.ReleaseEffectiveUtc })
            .ToListAsync(ct);

        var byTarget = rows.GroupBy(r => (r.TargetType, r.TargetKey))
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var (type, key) in targets.Distinct())
        {
            if (!byTarget.TryGetValue((type, key), out var grp) || grp.Count == 0) continue;
            var effective = grp.Where(r => r.ReleaseEffectiveUtc <= now)
                .OrderByDescending(r => r.ReleaseEffectiveUtc).FirstOrDefault();
            var next = grp.Where(r => r.ReleaseEffectiveUtc > now)
                .OrderBy(r => r.ReleaseEffectiveUtc).FirstOrDefault();
            result[(type, key)] = new ReleaseSummary(effective?.ReleaseAiracCycle, next?.ReleaseAiracCycle, HasAnyRelease: true);
        }
        return result;
    }

    public async Task<int> PruneReleasesAsync(ReleaseTargetType type, string key, DateTime keepFromUtc, CancellationToken ct = default)
    {
        // Solo le Superseded oltre soglia: l'Effective e le Scheduled hanno stato diverso → escluse per costruzione.
        var stale = await _db.DocReleases
            .Where(r => r.TargetType == type && r.TargetKey == key
                        && r.Status == ReleaseStatus.Superseded && r.ReleaseEffectiveUtc < keepFromUtc)
            .ToListAsync(ct);
        if (stale.Count == 0) return 0;
        _db.DocReleases.RemoveRange(stale);
        await _db.SaveChangesAsync(ct);
        return stale.Count;
    }

    // ---- helper ----
    private async Task<int?> WorkingVersionIdAsync(Document doc, CancellationToken ct)
    {
        var draft = await _db.DocumentVersions.AsNoTracking()
            .Where(v => v.DocumentId == doc.Id && v.Status == DocumentStatus.Draft)
            .OrderByDescending(v => v.VersionNumber).Select(v => (int?)v.Id).FirstOrDefaultAsync(ct);
        if (draft is not null) return draft;
        if (doc.CurrentVersionId is int cur) return cur;
        return await _db.DocumentVersions.AsNoTracking()
            .Where(v => v.DocumentId == doc.Id)
            .OrderByDescending(v => v.VersionNumber).Select(v => (int?)v.Id).FirstOrDefaultAsync(ct);
    }

    private static void RecomputeStatuses(List<DocRelease> all, DateTime now)
    {
        var effective = all.Where(r => r.ReleaseEffectiveUtc <= now)
            .OrderByDescending(r => r.ReleaseEffectiveUtc).ThenByDescending(r => r.VersionNumber)
            .FirstOrDefault();
        foreach (var r in all)
        {
            if (ReferenceEquals(r, effective)) r.Status = ReleaseStatus.Effective;
            else if (r.ReleaseEffectiveUtc > now) r.Status = ReleaseStatus.Scheduled;
            else r.Status = ReleaseStatus.Superseded;
        }
    }

}
