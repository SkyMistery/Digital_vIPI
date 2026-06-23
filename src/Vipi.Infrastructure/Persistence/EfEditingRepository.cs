using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;

namespace Vipi.Infrastructure.Persistence;

/// <summary>Implementazione EF Core di <see cref="IEditingRepository"/> (scrittura contenuti + workflow bozza→pubblicato).</summary>
public sealed class EfEditingRepository : IEditingRepository
{
    private readonly VipiDbContext _db;
    private readonly IAiracService _airac;

    public EfEditingRepository(VipiDbContext db, IAiracService airac)
    {
        _db = db;
        _airac = airac;
    }

    public async Task<IReadOnlyList<DocumentSummary>> ListDocumentsAsync(CancellationToken ct = default)
    {
        var docs = await _db.Documents
            .Include(d => d.ScopePosition).ThenInclude(p => p!.Fir)
            .AsNoTracking()
            .ToListAsync(ct);

        var draftDocIds = (await _db.DocumentVersions
                .Where(v => v.Status == DocumentStatus.Draft)
                .Select(v => v.DocumentId)
                .Distinct().ToListAsync(ct))
            .ToHashSet();

        return docs.Select(d => new DocumentSummary
        {
            Id = d.Id,
            Type = d.Type,
            Title = d.Title,
            Status = d.Status,
            Scope = ScopeOf(d),
            HasDraft = draftDocIds.Contains(d.Id),
            CurrentVersionId = d.CurrentVersionId,
        }).ToList();
    }

    public async Task<EditableDocument?> LoadForEditAsync(int documentId, CancellationToken ct = default)
    {
        var doc = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (doc is null) return null;

        // Versione di lavoro: bozza più recente se esiste, sennò la pubblicata corrente.
        var working = await _db.DocumentVersions
            .Where(v => v.DocumentId == documentId && v.Status == DocumentStatus.Draft)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);

        working ??= doc.CurrentVersionId is int cur
            ? await _db.DocumentVersions.FirstOrDefaultAsync(v => v.Id == cur, ct)
            : await _db.DocumentVersions
                .Where(v => v.DocumentId == documentId)
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefaultAsync(ct);

        if (working is null) return null;

        var sections = await _db.DocumentSections
            .Where(s => s.DocumentVersionId == working.Id).AsNoTracking().ToListAsync(ct);
        var blocks = await _db.ContentBlocks
            .Where(b => b.DocumentVersionId == working.Id).AsNoTracking().ToListAsync(ct);

        var blocksBySection = blocks.GroupBy(b => b.SectionId)
            .ToDictionary(g => g.Key, g => g.OrderBy(b => b.Order).ToList());
        var childrenByParent = sections.Where(s => s.ParentSectionId != null)
            .GroupBy(s => s.ParentSectionId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.Order).ToList());

        EditableSection Build(DocumentSection s) => new()
        {
            Id = s.Id,
            Title = s.Title,
            Depth = s.Depth,
            Order = s.Order,
            Blocks = (blocksBySection.TryGetValue(s.Id, out var bs) ? bs : new())
                .Select(b => new EditableBlock
                {
                    Id = b.Id, Order = b.Order, Format = b.Format, Tier = b.Tier,
                    Visibility = b.Visibility, CalloutKind = b.CalloutKind, Body = b.Body, BodyJson = b.BodyJson,
                    RowVersion = b.RowVersion is null ? null : Convert.ToBase64String(b.RowVersion),
                }).ToList(),
            Children = (childrenByParent.TryGetValue(s.Id, out var cs) ? cs : new()).Select(Build).ToList(),
        };

        var roots = sections.Where(s => s.ParentSectionId is null)
            .OrderBy(s => s.Order).Select(Build).ToList();

        return new EditableDocument
        {
            DocumentId = doc.Id,
            VersionId = working.Id,
            VersionNumber = working.VersionNumber,
            VersionStatus = working.Status,
            Title = doc.Title,
            Sections = roots,
        };
    }

    public async Task<int> CreateDraftAsync(int documentId, int authorVid, CancellationToken ct = default)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw new InvalidOperationException($"Documento {documentId} inesistente.");

        // Idempotente: se c'è già una bozza, la riuso.
        var existingDraft = await _db.DocumentVersions
            .Where(v => v.DocumentId == documentId && v.Status == DocumentStatus.Draft)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => (int?)v.Id)
            .FirstOrDefaultAsync(ct);
        if (existingDraft is int id) return id;

        var srcVersionId = doc.CurrentVersionId
            ?? await _db.DocumentVersions.Where(v => v.DocumentId == documentId)
                .OrderByDescending(v => v.VersionNumber).Select(v => (int?)v.Id).FirstOrDefaultAsync(ct);

        var nextNumber = (await _db.DocumentVersions.Where(v => v.DocumentId == documentId)
            .MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0) + 1;

        var now = DateTime.UtcNow;
        var draft = new DocumentVersion
        {
            DocumentId = documentId,
            VersionNumber = nextNumber,
            Status = DocumentStatus.Draft,
            CreatedByVid = authorVid,
            CreatedUtc = now,
            AiracCycle = _airac.GetCycle(now),
            Note = srcVersionId is null ? "Bozza iniziale" : $"Bozza da versione {srcVersionId}",
        };
        _db.DocumentVersions.Add(draft);

        if (srcVersionId is int src)
        {
            var srcSections = await _db.DocumentSections.Where(s => s.DocumentVersionId == src).AsNoTracking().ToListAsync(ct);
            var srcBlocks = await _db.ContentBlocks.Where(b => b.DocumentVersionId == src).AsNoTracking().ToListAsync(ct);

            var map = new Dictionary<int, DocumentSection>();
            foreach (var s in srcSections.OrderBy(s => s.Depth).ThenBy(s => s.Order))
            {
                var ns = new DocumentSection
                {
                    DocumentVersion = draft,
                    ParentSection = s.ParentSectionId is int pid ? map[pid] : null,
                    Title = s.Title, Order = s.Order, Depth = s.Depth, SectionKind = s.SectionKind,
                    RowVersion = Guid.NewGuid().ToByteArray(),
                };
                map[s.Id] = ns;
                _db.DocumentSections.Add(ns);
            }
            foreach (var b in srcBlocks)
            {
                _db.ContentBlocks.Add(new ContentBlock
                {
                    DocumentVersion = draft, Section = map[b.SectionId], Order = b.Order,
                    Tier = b.Tier, Format = b.Format, Visibility = b.Visibility,
                    CollapsedByDefault = b.CollapsedByDefault, CalloutKind = b.CalloutKind,
                    ScopeSectorId = b.ScopeSectorId, FromSectorId = b.FromSectorId, ToSectorId = b.ToSectorId,
                    SharedBlockId = b.SharedBlockId, Body = b.Body, BodyJson = b.BodyJson,
                    RowVersion = Guid.NewGuid().ToByteArray(),
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        return draft.Id;
    }

    public async Task UpdateBlockAsync(int blockId, BlockEdit edit, CancellationToken ct = default)
    {
        var block = await _db.ContentBlocks.FirstOrDefaultAsync(b => b.Id == blockId, ct)
            ?? throw new InvalidOperationException($"Blocco {blockId} inesistente.");
        await RequireDraftAsync(block.DocumentVersionId, ct);

        // Concorrenza ottimistica: confronta col token originale del client; bump per la prossima modifica.
        if (!string.IsNullOrEmpty(edit.RowVersion))
            _db.Entry(block).Property(b => b.RowVersion).OriginalValue = Convert.FromBase64String(edit.RowVersion);

        block.Tier = edit.Tier;
        block.Visibility = edit.Visibility;
        block.CalloutKind = edit.CalloutKind;
        block.Body = edit.Body;
        block.BodyJson = edit.BodyJson;
        block.RowVersion = Guid.NewGuid().ToByteArray();

        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            throw new Vipi.Application.Content.EditConflictException(
                "Il blocco è stato modificato nel frattempo: ricarica l'editor prima di salvare.");
        }
    }

    public async Task<int> AddBlockAsync(int sectionId, BlockFormat format, BlockTier tier, BlockVisibility visibility, CancellationToken ct = default)
    {
        var section = await _db.DocumentSections.FirstOrDefaultAsync(s => s.Id == sectionId, ct)
            ?? throw new InvalidOperationException($"Sezione {sectionId} inesistente.");
        await RequireDraftAsync(section.DocumentVersionId, ct);

        var nextOrder = (await _db.ContentBlocks.Where(b => b.SectionId == sectionId)
            .MaxAsync(b => (int?)b.Order, ct) ?? 0) + 1;

        var block = new ContentBlock
        {
            DocumentVersionId = section.DocumentVersionId,
            SectionId = sectionId,
            Order = nextOrder,
            Format = format,
            Tier = tier,
            Visibility = visibility,
            Body = format == BlockFormat.Prose ? "Nuovo testo…" : null,
            RowVersion = Guid.NewGuid().ToByteArray(),
        };
        _db.ContentBlocks.Add(block);
        await _db.SaveChangesAsync(ct);
        return block.Id;
    }

    public async Task DeleteBlockAsync(int blockId, CancellationToken ct = default)
    {
        var block = await _db.ContentBlocks.FirstOrDefaultAsync(b => b.Id == blockId, ct);
        if (block is null) return;
        await RequireDraftAsync(block.DocumentVersionId, ct);
        _db.ContentBlocks.Remove(block);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RenameSectionAsync(int sectionId, string title, CancellationToken ct = default)
    {
        var section = await _db.DocumentSections.FirstOrDefaultAsync(s => s.Id == sectionId, ct)
            ?? throw new InvalidOperationException($"Sezione {sectionId} inesistente.");
        await RequireDraftAsync(section.DocumentVersionId, ct);
        section.Title = string.IsNullOrWhiteSpace(title) ? section.Title : title.Trim();
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> AddSectionAsync(int versionId, int? parentSectionId, string title, BlockSection kind, CancellationToken ct = default)
    {
        await RequireDraftAsync(versionId, ct);

        var depth = 0;
        if (parentSectionId is int pid)
        {
            var parent = await _db.DocumentSections.FirstOrDefaultAsync(s => s.Id == pid, ct)
                ?? throw new InvalidOperationException($"Sezione padre {pid} inesistente.");
            if (parent.DocumentVersionId != versionId)
                throw new InvalidOperationException("La sezione padre appartiene a un'altra versione.");
            depth = parent.Depth + 1;
        }
        if (depth > DocumentSection.MaxDepth)
            throw new InvalidOperationException($"Profondità massima superata (max {DocumentSection.MaxDepth} livelli).");

        var nextOrder = (await _db.DocumentSections
            .Where(s => s.DocumentVersionId == versionId && s.ParentSectionId == parentSectionId)
            .MaxAsync(s => (int?)s.Order, ct) ?? 0) + 1;

        var section = new DocumentSection
        {
            DocumentVersionId = versionId,
            ParentSectionId = parentSectionId,
            Title = string.IsNullOrWhiteSpace(title) ? "Nuova sezione" : title.Trim(),
            Order = nextOrder,
            Depth = depth,
            SectionKind = kind,
            RowVersion = Guid.NewGuid().ToByteArray(),
        };
        _db.DocumentSections.Add(section);
        await _db.SaveChangesAsync(ct);
        return section.Id;
    }

    public async Task DeleteSectionAsync(int sectionId, CancellationToken ct = default)
    {
        var section = await _db.DocumentSections.FirstOrDefaultAsync(s => s.Id == sectionId, ct);
        if (section is null) return;
        await RequireDraftAsync(section.DocumentVersionId, ct);

        // Eliminazione ricorsiva bottom-up (ParentSection è Restrict): raccoglie sottoalbero, poi cancella figli→radice.
        var all = await _db.DocumentSections
            .Where(s => s.DocumentVersionId == section.DocumentVersionId).ToListAsync(ct);
        var childrenByParent = all.Where(s => s.ParentSectionId != null)
            .GroupBy(s => s.ParentSectionId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var subtree = new List<DocumentSection>();
        void Collect(DocumentSection s)
        {
            if (childrenByParent.TryGetValue(s.Id, out var kids))
                foreach (var k in kids) Collect(k);
            subtree.Add(s); // post-order: figli prima dei genitori
        }
        Collect(section);

        var ids = subtree.Select(s => s.Id).ToHashSet();
        var blocks = await _db.ContentBlocks.Where(b => ids.Contains(b.SectionId)).ToListAsync(ct);
        _db.ContentBlocks.RemoveRange(blocks);
        _db.DocumentSections.RemoveRange(subtree);
        await _db.SaveChangesAsync(ct);
    }

    public async Task MoveSectionAsync(int sectionId, int direction, CancellationToken ct = default)
    {
        var section = await _db.DocumentSections.FirstOrDefaultAsync(s => s.Id == sectionId, ct)
            ?? throw new InvalidOperationException($"Sezione {sectionId} inesistente.");
        await RequireDraftAsync(section.DocumentVersionId, ct);

        var siblings = await _db.DocumentSections
            .Where(s => s.DocumentVersionId == section.DocumentVersionId && s.ParentSectionId == section.ParentSectionId)
            .OrderBy(s => s.Order).ToListAsync(ct);
        if (SwapOrder(siblings, s => s.Id, s => s.Order, (s, o) => s.Order = o, sectionId, direction))
            await _db.SaveChangesAsync(ct);
    }

    public async Task MoveBlockAsync(int blockId, int direction, CancellationToken ct = default)
    {
        var block = await _db.ContentBlocks.FirstOrDefaultAsync(b => b.Id == blockId, ct)
            ?? throw new InvalidOperationException($"Blocco {blockId} inesistente.");
        await RequireDraftAsync(block.DocumentVersionId, ct);

        var siblings = await _db.ContentBlocks
            .Where(b => b.SectionId == block.SectionId)
            .OrderBy(b => b.Order).ToListAsync(ct);
        if (SwapOrder(siblings, b => b.Id, b => b.Order, (b, o) => b.Order = o, blockId, direction))
            await _db.SaveChangesAsync(ct);
    }

    /// <summary>Scambia l'Order dell'elemento target col fratello adiacente (direction -1 su, +1 giù). False se non c'è scambio possibile.</summary>
    private static bool SwapOrder<T>(IReadOnlyList<T> ordered, Func<T, int> id, Func<T, int> getOrder,
        Action<T, int> setOrder, int targetId, int direction)
    {
        var i = -1;
        for (var k = 0; k < ordered.Count; k++)
            if (id(ordered[k]) == targetId) { i = k; break; }
        var j = i + Math.Sign(direction);
        if (i < 0 || j < 0 || j >= ordered.Count) return false;

        var oi = getOrder(ordered[i]);
        setOrder(ordered[i], getOrder(ordered[j]));
        setOrder(ordered[j], oi);
        return true;
    }

    public async Task PublishAsync(int versionId, int actorVid, string? note, CancellationToken ct = default)
    {
        var ver = await _db.DocumentVersions.Include(v => v.Document)
            .FirstOrDefaultAsync(v => v.Id == versionId, ct)
            ?? throw new InvalidOperationException($"Versione {versionId} inesistente.");
        if (ver.Status != DocumentStatus.Draft)
            throw new InvalidOperationException("Solo una bozza può essere pubblicata.");

        var doc = ver.Document!;
        var now = DateTime.UtcNow;

        // Archivia la pubblicata precedente (se diversa).
        if (doc.CurrentVersionId is int prevId && prevId != versionId)
        {
            var prev = await _db.DocumentVersions.FirstOrDefaultAsync(v => v.Id == prevId, ct);
            if (prev is not null) prev.Status = DocumentStatus.Archived;
        }

        ver.Status = DocumentStatus.Published;
        if (!string.IsNullOrWhiteSpace(note)) ver.Note = note;
        doc.CurrentVersionId = ver.Id;
        doc.Status = DocumentStatus.Published;
        doc.LastUpdatedUtc = now;
        doc.LastUpdatedAiracCycle = _airac.GetCycle(now);

        _db.AuditLogs.Add(new AuditLog
        {
            Vid = actorVid,
            Action = AuditAction.Publish,
            EntityType = "DocumentVersion",
            EntityId = ver.Id.ToString(),
            TimestampUtc = now,
            DetailsJson = JsonSerializer.Serialize(new { doc.Id, ver.VersionNumber, ver.AiracCycle }),
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<VersionInfo>> ListVersionsAsync(int documentId, CancellationToken ct = default)
    {
        var currentVersionId = await _db.Documents.Where(d => d.Id == documentId)
            .Select(d => d.CurrentVersionId).FirstOrDefaultAsync(ct);

        return await _db.DocumentVersions
            .Where(v => v.DocumentId == documentId)
            .OrderByDescending(v => v.VersionNumber)
            .AsNoTracking()
            .Select(v => new VersionInfo
            {
                Id = v.Id,
                VersionNumber = v.VersionNumber,
                Status = v.Status,
                CreatedByVid = v.CreatedByVid,
                CreatedUtc = v.CreatedUtc,
                AiracCycle = v.AiracCycle,
                Note = v.Note,
                IsCurrent = v.Id == currentVersionId,
            })
            .ToListAsync(ct);
    }

    public async Task<int?> GetDocumentIdByVersionAsync(int versionId, CancellationToken ct = default) =>
        await _db.DocumentVersions.Where(v => v.Id == versionId).Select(v => (int?)v.DocumentId).FirstOrDefaultAsync(ct);

    public async Task<int?> GetDocumentIdBySectionAsync(int sectionId, CancellationToken ct = default) =>
        await _db.DocumentSections.Where(s => s.Id == sectionId)
            .Select(s => (int?)s.DocumentVersion!.DocumentId).FirstOrDefaultAsync(ct);

    public async Task<int?> GetDocumentIdByBlockAsync(int blockId, CancellationToken ct = default) =>
        await _db.ContentBlocks.Where(b => b.Id == blockId)
            .Select(b => (int?)b.DocumentVersion!.DocumentId).FirstOrDefaultAsync(ct);

    // --- Lock di editing esclusivo ---

    public async Task<LockInfo> AcquireOrInspectLockAsync(int documentId, int vid, string? name, int ttlMinutes, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(ttlMinutes);

        // Acquisizione atomica DB-side: riesce solo se libero, scaduto o già mio.
        var rows = await _db.Documents
            .Where(d => d.Id == documentId &&
                        (d.LockedByVid == null || d.LockExpiresUtc == null || d.LockExpiresUtc < now || d.LockedByVid == vid))
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.LockedByVid, vid)
                .SetProperty(d => d.LockedByName, name)
                .SetProperty(d => d.LockedAtUtc, now)
                .SetProperty(d => d.LockExpiresUtc, expires), ct);

        if (rows > 0)
            return new LockInfo { Locked = true, IsMine = true, ByVid = vid, ByName = name, ExpiresUtc = expires };

        return await InspectLockAsync(documentId, vid, ct);
    }

    public async Task<LockInfo> InspectLockAsync(int documentId, int vid, CancellationToken ct = default)
    {
        var d = await _db.Documents.AsNoTracking()
            .Where(x => x.Id == documentId)
            .Select(x => new { x.LockedByVid, x.LockedByName, x.LockExpiresUtc }).FirstOrDefaultAsync(ct);
        if (d is null) return LockInfo.Free();

        var active = d.LockedByVid != null && d.LockExpiresUtc != null && d.LockExpiresUtc > DateTime.UtcNow;
        if (!active) return LockInfo.Free();

        return new LockInfo
        {
            Locked = true,
            IsMine = d.LockedByVid == vid,
            ByVid = d.LockedByVid,
            ByName = d.LockedByName,
            ExpiresUtc = d.LockExpiresUtc,
        };
    }

    public async Task RenewLockAsync(int documentId, int vid, int ttlMinutes, CancellationToken ct = default)
    {
        var expires = DateTime.UtcNow.AddMinutes(ttlMinutes);
        await _db.Documents.Where(d => d.Id == documentId && d.LockedByVid == vid)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.LockExpiresUtc, expires), ct);
    }

    public async Task ReleaseLockAsync(int documentId, int vid, CancellationToken ct = default)
    {
        await _db.Documents.Where(d => d.Id == documentId && d.LockedByVid == vid)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.LockedByVid, (int?)null)
                .SetProperty(d => d.LockedByName, (string?)null)
                .SetProperty(d => d.LockedAtUtc, (DateTime?)null)
                .SetProperty(d => d.LockExpiresUtc, (DateTime?)null), ct);
    }

    public async Task ForceUnlockAsync(int documentId, CancellationToken ct = default)
    {
        await _db.Documents.Where(d => d.Id == documentId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.LockedByVid, (int?)null)
                .SetProperty(d => d.LockedByName, (string?)null)
                .SetProperty(d => d.LockedAtUtc, (DateTime?)null)
                .SetProperty(d => d.LockExpiresUtc, (DateTime?)null), ct);
    }

    public async Task<bool> IsLockHeldByAsync(int documentId, int vid, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await _db.Documents.AnyAsync(
            d => d.Id == documentId && d.LockedByVid == vid && d.LockExpiresUtc != null && d.LockExpiresUtc > now, ct);
    }

    /// <summary>Verifica che la versione sia una bozza editabile; errore altrimenti.</summary>
    private async Task RequireDraftAsync(int versionId, CancellationToken ct)
    {
        var status = await _db.DocumentVersions.Where(v => v.Id == versionId)
            .Select(v => (DocumentStatus?)v.Status).FirstOrDefaultAsync(ct);
        if (status != DocumentStatus.Draft)
            throw new InvalidOperationException("Modifica consentita solo su una versione in bozza.");
    }

    private static string ScopeOf(Document d)
    {
        var p = d.ScopePosition;
        if (p is null) return "—";
        if (p.Kind == PositionKind.Airport)
        {
            var us = p.Callsign.IndexOf('_');
            return us > 0 ? p.Callsign[..us] : p.Callsign;
        }
        return p.Fir?.Code ?? p.Callsign;
    }
}
