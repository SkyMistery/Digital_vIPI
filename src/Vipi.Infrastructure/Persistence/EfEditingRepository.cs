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

    public Task<int?> FindVloaIdByPairAsync(string homeAccCode, string foreignAccCode, CancellationToken ct = default) =>
        _db.Documents
            .Where(d => d.Type == DocumentType.Vloa
                && d.Parties.Any(p => p.Role == PartyRole.Home && p.Sector!.Acc!.Code == homeAccCode)
                && d.Parties.Any(p => p.Role == PartyRole.Neighbour && p.Sector!.Acc!.Code == foreignAccCode))
            .Select(d => (int?)d.Id)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<DocumentSummary>> ListDocumentsAsync(CancellationToken ct = default)
    {
        var docs = await _db.Documents
            .Include(d => d.Sectors).ThenInclude(s => s.Acc)
            .Include(d => d.Parties).ThenInclude(p => p.Sector).ThenInclude(s => s!.Acc)
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
            IsAirport = IsAirportDoc(d),
            IsStandaloneApp = IsStandaloneAppDoc(d),
            AccCode = AccCodeOf(d),
            HomeAccCode = d.Parties.FirstOrDefault(p => p.Role == PartyRole.Home)?.Sector?.Acc?.Code,
            NeighbourAccCode = d.Parties.FirstOrDefault(p => p.Role == PartyRole.Neighbour)?.Sector?.Acc?.Code,
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
        return await BuildEditableAsync(doc, working, ct);
    }

    public async Task<EditableDocument?> LoadForViewAsync(int documentId, CancellationToken ct = default)
    {
        var doc = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (doc?.CurrentVersionId is not int cur) return null;   // solo la versione pubblicata corrente
        var version = await _db.DocumentVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == cur, ct);
        return version is null ? null : await BuildEditableAsync(doc, version, ct);
    }

    // Costruisce il modello editabile (albero sezioni + blocchi) di una specifica versione.
    private async Task<EditableDocument> BuildEditableAsync(Document doc, DocumentVersion version, CancellationToken ct)
    {
        var sections = await _db.DocumentSections
            .Where(s => s.DocumentVersionId == version.Id).AsNoTracking().ToListAsync(ct);
        var blocks = await _db.ContentBlocks
            .Where(b => b.DocumentVersionId == version.Id).AsNoTracking().ToListAsync(ct);

        var blocksBySection = blocks.GroupBy(b => b.SectionId)
            .ToDictionary(g => g.Key, g => g.OrderBy(b => b.Order).ToList());
        var childrenByParent = sections.Where(s => s.ParentSectionId != null)
            .GroupBy(s => s.ParentSectionId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.Order).ToList());

        EditableSection Build(DocumentSection s) => new()
        {
            Id = s.Id,
            Title = s.Title,
            SectionKey = s.SectionKey,
            Depth = s.Depth,
            Order = s.Order,
            RenderMode = s.RenderMode,
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
            VersionId = version.Id,
            VersionNumber = version.VersionNumber,
            VersionStatus = version.Status,
            Title = doc.Title,
            Sections = roots,
        };
    }

    public async Task<int> CreateDraftAsync(int documentId, int authorUserId, CancellationToken ct = default)
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
            CreatedByUserId = authorUserId,
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
                    Title = s.Title, Order = s.Order, Depth = s.Depth, SectionKey = s.SectionKey,
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

    public async Task<int> CreateDocumentAsync(DocumentType type, string title, Language language,
        IReadOnlyList<int>? scopeSectorIds, int? primarySectorId,
        (int homeSectorId, int neighbourSectorId)? parties, int authorUserId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var doc = new Document
        {
            Type = type,
            Title = title,
            Language = language,
            Status = DocumentStatus.Draft,
            LastUpdatedUtc = now,
            LastUpdatedAiracCycle = _airac.GetCycle(now),
        };
        if (parties is { } p)
        {
            doc.Parties.Add(new DocumentParty { SectorId = p.homeSectorId, Role = PartyRole.Home });
            doc.Parties.Add(new DocumentParty { SectorId = p.neighbourSectorId, Role = PartyRole.Neighbour });
        }
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync(ct); // serve doc.Id

        // vIPI: aggancia i settori di scope al documento (uno primario). Uno-a-molti via Sector.DocumentId.
        if (scopeSectorIds is { Count: > 0 })
        {
            var ids = scopeSectorIds.Distinct().ToList();
            var sectors = await _db.Sectors.Where(s => ids.Contains(s.Id)).ToListAsync(ct);
            foreach (var s in sectors)
            {
                if (s.DocumentId is int existing && existing != doc.Id)
                    throw new InvalidOperationException($"Il settore {s.Callsign} è già descritto da un altro documento.");
                s.DocumentId = doc.Id;
                s.IsPrimary = s.Id == (primarySectorId ?? ids[0]);
            }
            await _db.SaveChangesAsync(ct);
        }

        var version = new DocumentVersion
        {
            DocumentId = doc.Id,
            VersionNumber = 1,
            Status = DocumentStatus.Draft,
            CreatedByUserId = authorUserId,
            CreatedUtc = now,
            AiracCycle = _airac.GetCycle(now),
            Note = "Bozza iniziale",
        };
        _db.DocumentVersions.Add(version);
        await _db.SaveChangesAsync(ct); // serve version.Id

        // Sezione radice vuota di partenza (l'editor ne aggiunge altre).
        _db.DocumentSections.Add(new DocumentSection
        {
            DocumentVersionId = version.Id,
            ParentSectionId = null,
            Title = "Scopo e validità",
            Order = 1,
            Depth = 0,
            SectionKey = "custom",
            RowVersion = Guid.NewGuid().ToByteArray(),
        });
        await _db.SaveChangesAsync(ct);

        return doc.Id;
    }

    public async Task<string?> GetAccCodeBySectorAsync(int sectorId, CancellationToken ct = default) =>
        await _db.Sectors.Where(s => s.Id == sectorId).Select(s => s.Acc!.Code).FirstOrDefaultAsync(ct);

    public async Task<int> EnsureVipiDocumentAsync(int primarySectorId, string title, Language language,
        IReadOnlyList<(string Key, string Title)> sections, int authorUserId,
        IReadOnlyCollection<string>? liveKeys = null, CancellationToken ct = default)
    {
        var live = liveKeys is null ? null : new HashSet<string>(liveKeys, StringComparer.OrdinalIgnoreCase);
        var sector = await _db.Sectors.FirstOrDefaultAsync(s => s.Id == primarySectorId, ct)
            ?? throw new InvalidOperationException($"Settore {primarySectorId} inesistente.");
        if (sector.DocumentId is int existing) return existing;   // già migrato: idempotente

        var now = DateTime.UtcNow;
        var doc = new Document
        {
            Type = DocumentType.Vipi,
            Title = title,
            Language = language,
            Status = DocumentStatus.Draft,
            LastUpdatedUtc = now,
            LastUpdatedAiracCycle = _airac.GetCycle(now),
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync(ct); // serve doc.Id

        sector.DocumentId = doc.Id;
        sector.IsPrimary = true;
        await _db.SaveChangesAsync(ct);

        var version = new DocumentVersion
        {
            DocumentId = doc.Id,
            VersionNumber = 1,
            Status = DocumentStatus.Draft,
            CreatedByUserId = authorUserId,
            CreatedUtc = now,
            AiracCycle = _airac.GetCycle(now),
            Note = "Bozza iniziale",
        };
        _db.DocumentVersions.Add(version);
        await _db.SaveChangesAsync(ct); // serve version.Id

        var order = 1;
        foreach (var (key, secTitle) in sections)
        {
            var section = new DocumentSection
            {
                DocumentVersionId = version.Id,
                ParentSectionId = null,
                Title = secTitle,
                Order = order++,
                Depth = 0,
                SectionKey = key,
                RowVersion = Guid.NewGuid().ToByteArray(),
            };
            _db.DocumentSections.Add(section);

            // Sezioni "live" (derivate/editoriali-strutturate): blocco placeholder così non vengono potate dalla vista
            // quando sono senza contenuto memorizzato (il renderer le riempie live). Doc refactor 08e.
            if (live is not null && live.Contains(key))
                _db.ContentBlocks.Add(new ContentBlock
                {
                    DocumentVersion = version,
                    Section = section,
                    Order = 1,
                    Format = BlockFormat.Table,
                    Tier = BlockTier.Extended,
                    Visibility = BlockVisibility.Always,
                    RowVersion = Guid.NewGuid().ToByteArray(),
                });
        }
        await _db.SaveChangesAsync(ct);

        return doc.Id;
    }

    public async Task<int> EnsureVipiDocumentTreeAsync(int primarySectorId, string title, Language language,
        IReadOnlyList<VipiBlockSpec> blocks, int authorUserId,
        IReadOnlyCollection<string>? liveKeys = null, CancellationToken ct = default)
    {
        var live = liveKeys is null ? null : new HashSet<string>(liveKeys, StringComparer.OrdinalIgnoreCase);
        var sector = await _db.Sectors.FirstOrDefaultAsync(s => s.Id == primarySectorId, ct)
            ?? throw new InvalidOperationException($"Settore {primarySectorId} inesistente.");
        if (sector.DocumentId is int existing) return existing;   // già migrato: idempotente

        var now = DateTime.UtcNow;
        var doc = new Document
        {
            Type = DocumentType.Vipi,
            Title = title,
            Language = language,
            Status = DocumentStatus.Draft,
            LastUpdatedUtc = now,
            LastUpdatedAiracCycle = _airac.GetCycle(now),
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync(ct); // serve doc.Id

        sector.DocumentId = doc.Id;
        sector.IsPrimary = true;
        await _db.SaveChangesAsync(ct);

        var version = new DocumentVersion
        {
            DocumentId = doc.Id,
            VersionNumber = 1,
            Status = DocumentStatus.Draft,
            CreatedByUserId = authorUserId,
            CreatedUtc = now,
            AiracCycle = _airac.GetCycle(now),
            Note = "Bozza iniziale",
        };
        _db.DocumentVersions.Add(version);
        await _db.SaveChangesAsync(ct); // serve version.Id

        var blockOrder = 1;
        foreach (var block in blocks)
        {
            var blockSection = new DocumentSection
            {
                DocumentVersionId = version.Id,
                ParentSectionId = null,
                Title = block.Title,
                Order = blockOrder++,
                Depth = 0,
                SectionKey = block.Key,
                RowVersion = Guid.NewGuid().ToByteArray(),
            };
            _db.DocumentSections.Add(blockSection);

            var childOrder = 1;
            foreach (var (key, secTitle) in block.Sections)
            {
                var child = new DocumentSection
                {
                    DocumentVersion = version,
                    ParentSection = blockSection,
                    Title = secTitle,
                    Order = childOrder++,
                    Depth = 1,
                    SectionKey = key,
                    RowVersion = Guid.NewGuid().ToByteArray(),
                };
                _db.DocumentSections.Add(child);

                // Sezioni "live" (derivate/editoriali-strutturate): blocco placeholder così non vengono potate quando
                // sono senza contenuto memorizzato (rese live dal renderer). Doc refactor 08e-acc.
                if (live is not null && live.Contains(key))
                    _db.ContentBlocks.Add(NewPlaceholderBlock(version, child));
            }
        }
        await _db.SaveChangesAsync(ct);

        return doc.Id;
    }

    // Blocco placeholder vuoto (Table) per una sezione live: la mantiene visibile nel viewer anche senza contenuto.
    private static ContentBlock NewPlaceholderBlock(DocumentVersion version, DocumentSection section) => new()
    {
        DocumentVersion = version,
        Section = section,
        Order = 1,
        Format = BlockFormat.Table,
        Tier = BlockTier.Extended,
        Visibility = BlockVisibility.Always,
        RowVersion = Guid.NewGuid().ToByteArray(),
    };

    public async Task<int> AddBlockToVersionAsync(int versionId, VipiBlockSpec block,
        IReadOnlyCollection<string>? liveKeys = null, CancellationToken ct = default)
    {
        await RequireDraftAsync(versionId, ct);
        var live = liveKeys is null ? null : new HashSet<string>(liveKeys, StringComparer.OrdinalIgnoreCase);

        var nextOrder = (await _db.DocumentSections
            .Where(s => s.DocumentVersionId == versionId && s.ParentSectionId == null)
            .MaxAsync(s => (int?)s.Order, ct) ?? 0) + 1;

        var blockSection = new DocumentSection
        {
            DocumentVersionId = versionId,
            ParentSectionId = null,
            Title = block.Title,
            Order = nextOrder,
            Depth = 0,
            SectionKey = block.Key,
            RowVersion = Guid.NewGuid().ToByteArray(),
        };
        _db.DocumentSections.Add(blockSection);

        var version = await _db.DocumentVersions.FirstAsync(v => v.Id == versionId, ct);
        var childOrder = 1;
        foreach (var (key, secTitle) in block.Sections)
        {
            var child = new DocumentSection
            {
                DocumentVersion = version,
                ParentSection = blockSection,
                Title = secTitle,
                Order = childOrder++,
                Depth = 1,
                SectionKey = key,
                RowVersion = Guid.NewGuid().ToByteArray(),
            };
            _db.DocumentSections.Add(child);
            if (live is not null && live.Contains(key))
                _db.ContentBlocks.Add(NewPlaceholderBlock(version, child));
        }
        await _db.SaveChangesAsync(ct);
        return blockSection.Id;
    }

    public async Task<string?> GetSectionBlockJsonBySectionAsync(int sectionId, CancellationToken ct = default) =>
        await _db.ContentBlocks
            .Where(b => b.SectionId == sectionId)
            .OrderBy(b => b.Order).Select(b => b.BodyJson).FirstOrDefaultAsync(ct);

    public async Task SaveSectionBlockJsonBySectionAsync(int sectionId, string? json, int authorUserId, CancellationToken ct = default)
    {
        var section = await _db.DocumentSections.FirstOrDefaultAsync(s => s.Id == sectionId, ct)
            ?? throw new InvalidOperationException($"Sezione {sectionId} inesistente.");
        await RequireDraftAsync(section.DocumentVersionId, ct);

        var normalized = string.IsNullOrWhiteSpace(json) ? null : json;
        var block = await _db.ContentBlocks
            .Where(b => b.SectionId == section.Id).OrderBy(b => b.Order).FirstOrDefaultAsync(ct);

        if (block is null)
        {
            _db.ContentBlocks.Add(new ContentBlock
            {
                DocumentVersionId = section.DocumentVersionId,
                SectionId = section.Id,
                Order = 1,
                Format = BlockFormat.Table,
                Tier = BlockTier.Extended,
                Visibility = BlockVisibility.Always,
                BodyJson = normalized,
                RowVersion = Guid.NewGuid().ToByteArray(),
            });
        }
        else
        {
            block.BodyJson = normalized;
            block.RowVersion = Guid.NewGuid().ToByteArray();
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<string?> GetSectionBlockJsonAsync(int documentId, string sectionKey, CancellationToken ct = default)
    {
        var versionId = await ResolveWorkingVersionIdAsync(documentId, ct);
        if (versionId is null) return null;

        var sectionId = await _db.DocumentSections
            .Where(s => s.DocumentVersionId == versionId && s.ParentSectionId == null && s.SectionKey == sectionKey)
            .OrderBy(s => s.Order).Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);
        if (sectionId is null) return null;

        return await _db.ContentBlocks
            .Where(b => b.SectionId == sectionId)
            .OrderBy(b => b.Order).Select(b => b.BodyJson).FirstOrDefaultAsync(ct);
    }

    public async Task SaveSectionBlockJsonAsync(int documentId, string sectionKey, string? json, int authorUserId, CancellationToken ct = default)
    {
        var versionId = await ResolveWorkingVersionIdAsync(documentId, ct)
            ?? throw new InvalidOperationException($"Documento {documentId} senza versione di lavoro.");
        await RequireDraftAsync(versionId, ct);

        var section = await _db.DocumentSections
            .Where(s => s.DocumentVersionId == versionId && s.ParentSectionId == null && s.SectionKey == sectionKey)
            .OrderBy(s => s.Order).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Sezione '{sectionKey}' assente nel documento {documentId}.");

        var normalized = string.IsNullOrWhiteSpace(json) ? null : json;
        var block = await _db.ContentBlocks
            .Where(b => b.SectionId == section.Id).OrderBy(b => b.Order).FirstOrDefaultAsync(ct);

        if (block is null)
        {
            _db.ContentBlocks.Add(new ContentBlock
            {
                DocumentVersionId = versionId,
                SectionId = section.Id,
                Order = 1,
                Format = BlockFormat.Table,
                Tier = BlockTier.Extended,
                Visibility = BlockVisibility.Always,
                BodyJson = normalized,
                RowVersion = Guid.NewGuid().ToByteArray(),
            });
        }
        else
        {
            block.BodyJson = normalized;
            block.RowVersion = Guid.NewGuid().ToByteArray();
        }
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Id della versione di lavoro: bozza più recente se esiste, sennò la pubblicata corrente, sennò l'ultima. Null se nessuna versione.</summary>
    private async Task<int?> ResolveWorkingVersionIdAsync(int documentId, CancellationToken ct)
    {
        var draft = await _db.DocumentVersions
            .Where(v => v.DocumentId == documentId && v.Status == DocumentStatus.Draft)
            .OrderByDescending(v => v.VersionNumber).Select(v => (int?)v.Id).FirstOrDefaultAsync(ct);
        if (draft is not null) return draft;

        var current = await _db.Documents.Where(d => d.Id == documentId)
            .Select(d => d.CurrentVersionId).FirstOrDefaultAsync(ct);
        if (current is not null) return current;

        return await _db.DocumentVersions.Where(v => v.DocumentId == documentId)
            .OrderByDescending(v => v.VersionNumber).Select(v => (int?)v.Id).FirstOrDefaultAsync(ct);
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

    public async Task SetSectionRenderModeAsync(int sectionId, RenderMode mode, CancellationToken ct = default)
    {
        var section = await _db.DocumentSections.FirstOrDefaultAsync(s => s.Id == sectionId, ct)
            ?? throw new InvalidOperationException($"Sezione {sectionId} inesistente.");
        await RequireDraftAsync(section.DocumentVersionId, ct);
        section.RenderMode = mode;
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
            SectionKey = SectionCatalogBridge.KeyFor(kind) ?? "custom",
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

    public async Task PublishAsync(int versionId, int actorUserId, string? note, CancellationToken ct = default)
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
            UserId = actorUserId,
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
                CreatedByUserId = v.CreatedByUserId,
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

    public async Task<LockInfo> AcquireOrInspectLockAsync(int documentId, int UserId, string? name, int ttlMinutes, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(ttlMinutes);

        // Acquisizione atomica DB-side: riesce solo se libero, scaduto o già mio.
        var rows = await _db.Documents
            .Where(d => d.Id == documentId &&
                        (d.LockedByUserId == null || d.LockExpiresUtc == null || d.LockExpiresUtc < now || d.LockedByUserId == UserId))
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.LockedByUserId, UserId)
                .SetProperty(d => d.LockedByName, name)
                .SetProperty(d => d.LockedAtUtc, now)
                .SetProperty(d => d.LockExpiresUtc, expires), ct);

        if (rows > 0)
            return new LockInfo { Locked = true, IsMine = true, ByUserId = UserId, ByName = name, ExpiresUtc = expires };

        return await InspectLockAsync(documentId, UserId, ct);
    }

    public async Task<LockInfo> InspectLockAsync(int documentId, int UserId, CancellationToken ct = default)
    {
        var d = await _db.Documents.AsNoTracking()
            .Where(x => x.Id == documentId)
            .Select(x => new { x.LockedByUserId, x.LockedByName, x.LockExpiresUtc }).FirstOrDefaultAsync(ct);
        if (d is null) return LockInfo.Free();

        var active = d.LockedByUserId != null && d.LockExpiresUtc != null && d.LockExpiresUtc > DateTime.UtcNow;
        if (!active) return LockInfo.Free();

        return new LockInfo
        {
            Locked = true,
            IsMine = d.LockedByUserId == UserId,
            ByUserId = d.LockedByUserId,
            ByName = d.LockedByName,
            ExpiresUtc = d.LockExpiresUtc,
        };
    }

    public async Task RenewLockAsync(int documentId, int UserId, int ttlMinutes, CancellationToken ct = default)
    {
        var expires = DateTime.UtcNow.AddMinutes(ttlMinutes);
        await _db.Documents.Where(d => d.Id == documentId && d.LockedByUserId == UserId)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.LockExpiresUtc, expires), ct);
    }

    public async Task ReleaseLockAsync(int documentId, int UserId, CancellationToken ct = default)
    {
        await _db.Documents.Where(d => d.Id == documentId && d.LockedByUserId == UserId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.LockedByUserId, (int?)null)
                .SetProperty(d => d.LockedByName, (string?)null)
                .SetProperty(d => d.LockedAtUtc, (DateTime?)null)
                .SetProperty(d => d.LockExpiresUtc, (DateTime?)null), ct);
    }

    public async Task ForceUnlockAsync(int documentId, CancellationToken ct = default)
    {
        await _db.Documents.Where(d => d.Id == documentId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.LockedByUserId, (int?)null)
                .SetProperty(d => d.LockedByName, (string?)null)
                .SetProperty(d => d.LockedAtUtc, (DateTime?)null)
                .SetProperty(d => d.LockExpiresUtc, (DateTime?)null), ct);
    }

    public async Task<bool> IsLockHeldByAsync(int documentId, int UserId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await _db.Documents.AnyAsync(
            d => d.Id == documentId && d.LockedByUserId == UserId && d.LockExpiresUtc != null && d.LockExpiresUtc > now, ct);
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
        // Settore primario (o primo) del documento; per le vLOA niente scope settore.
        var s = d.Sectors.FirstOrDefault(x => x.IsPrimary) ?? d.Sectors.FirstOrDefault();
        if (s is null) return "—";
        // APP standalone: lo scope è il callsign APP (chiave dell'editor dedicato), non l'ICAO dell'aeroporto.
        if (IsStandaloneApp(s)) return s.Callsign;
        if (s.Kind == SectorKind.Airport)
            return s.AirportIcao ?? (s.Callsign.IndexOf('_') is int us && us > 0 ? s.Callsign[..us] : s.Callsign);
        return s.Acc?.Code ?? s.Callsign;
    }

    // Documento di aeroporto = settore primario (o primo) Kind=Airport, ESCLUSI gli APP standalone (editor dedicato).
    private static bool IsAirportDoc(Document d)
    {
        var s = d.Sectors.FirstOrDefault(x => x.IsPrimary) ?? d.Sectors.FirstOrDefault();
        return s?.Kind == SectorKind.Airport && !IsStandaloneApp(s);
    }

    // Documento APP non remotizzato = settore primario (o primo) Type=App con ApproachKind=Standalone.
    private static bool IsStandaloneAppDoc(Document d) =>
        (d.Sectors.FirstOrDefault(x => x.IsPrimary) ?? d.Sectors.FirstOrDefault()) is { } s && IsStandaloneApp(s);

    private static bool IsStandaloneApp(Domain.Entities.Sector s) =>
        s.Type == SectorType.App && s.ApproachKind == ApproachKind.Standalone;

    // ACC del settore primario (o primo): serve a costruire i link editor.
    private static string? AccCodeOf(Document d) =>
        (d.Sectors.FirstOrDefault(x => x.IsPrimary) ?? d.Sectors.FirstOrDefault())?.Acc?.Code;
}
