using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using static Vipi.Application.Messaggio;

namespace Vipi.Infrastructure.Persistence;

/// <summary>Implementazione EF Core di <see cref="IEditingRepository"/> (scrittura contenuti + workflow bozza→pubblicato).</summary>
public sealed class EfEditingRepository : IEditingRepository
{
    private readonly VipiDbContext _db;
    private readonly IAiracService _airac;
    private readonly Vipi.Application.Media.IMediaMaintenance _media;

    public EfEditingRepository(VipiDbContext db, IAiracService airac, Vipi.Application.Media.IMediaMaintenance media)
    {
        _db = db;
        _airac = airac;
        _media = media;
    }

    /// <summary>
    /// Sha citati dai blocchi che stanno per sparire. Vanno letti PRIMA della cancellazione: dopo, il riferimento
    /// non esiste piu' e non si saprebbe piu' quale foto controllare.
    /// </summary>
    private static List<string> ShaCitati(IEnumerable<ContentBlock> blocchi) =>
        Vipi.Application.Media.MediaReferenceScanner.ScanAll(
            blocchi.Where(b => b.Format == BlockFormat.Image).Select(b => b.BodyJson)).ToList();

    /// <summary>
    /// Libera le immagini rimaste senza padroni dopo una cancellazione. Non decide nulla da se': ripassa da
    /// <c>DeleteOrphansAsync</c>, che ricontrolla TUTTE le sorgenti — quindi una foto ancora citata da un altro
    /// blocco, da un'altra versione o da una release pubblicata resta dov'e'.
    /// </summary>
    private async Task LiberaImmaginiAsync(IReadOnlyList<string> sha, CancellationToken ct)
    {
        if (sha.Count > 0) await _media.DeleteOrphansAsync(sha, ct);
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
            IsHidden = s.IsHidden,
            BeforeParentBody = s.BeforeParentBody, Audience = s.Audience,
            LeadSentence = s.LeadSentence,
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
                    // La copia deve portarsi dietro anche i flag per-sezione: senza, «crea bozza» resettava
                    // RenderMode a Frozen (doc 10) e ora azzererebbe pure IsHidden (doc 11 §3c).
                    RenderMode = s.RenderMode, IsHidden = s.IsHidden, BeforeParentBody = s.BeforeParentBody, Audience = s.Audience,
        LeadSentence = s.LeadSentence,
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
                    throw new InvalidOperationException(Lingua($"Il settore {s.Callsign} è già descritto da un altro documento.", $"Sector {s.Callsign} is already described by another document."));
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

        if (parties is { } coppia)
        {
            // ⚠️ Una vLOA nasce con la struttura del CATALOGO, da qualunque porta la si crei.
            //
            // Prima da qui nasceva con una sezione sola — «Scopo e validità», per giunta con una chiave
            // libera (`SectionKeys.NewCustom()`) che non è nessuna delle sette del profilo Vloa — mentre la
            // stessa vLOA generata da «ACC confinanti» nasceva con le sette canoniche. Due porte per la
            // stessa cosa, due risultati diversi, e da questa usciva un documento **fuori catalogo**: le
            // sezioni obbligatorie assenti, e l'unica presente sconosciuta a chi decide chi rende il corpo
            // (doc 13 §3a). La pagina lo dichiarava perfino — «la vLOA nasce vuota» — ma un difetto
            // documentato resta un difetto.
            var codici = await _db.Sectors.AsNoTracking()
                .Where(s => s.Id == coppia.homeSectorId || s.Id == coppia.neighbourSectorId)
                .Select(s => new { s.Id, AccCode = s.Acc!.Code, AccName = s.Acc.Name })
                .ToListAsync(ct);
            var home = codici.FirstOrDefault(x => x.Id == coppia.homeSectorId);
            var foreign = codici.FirstOrDefault(x => x.Id == coppia.neighbourSectorId);

            Seed.VloaStructureSeeder.Seed(_db, version, Vipi.Application.Content.VloaSections.Canonical(
                home?.AccCode ?? "", foreign?.AccCode ?? "", foreign?.AccName));
        }
        else
        {
            // Le vIPI di questo percorso restano com'erano: la loro struttura la costruisce l'editor, e
            // `EnsureVipiDocumentAsync` — il percorso vero delle vIPI — semina già dal catalogo.
            _db.DocumentSections.Add(new DocumentSection
            {
                DocumentVersionId = version.Id,
                ParentSectionId = null,
                Title = "Scopo e validità",
                Order = 1,
                Depth = 0,
                SectionKey = SectionKeys.NewCustom(),
                RowVersion = Guid.NewGuid().ToByteArray(),
            });
        }
        await _db.SaveChangesAsync(ct);

        return doc.Id;
    }

    public async Task<string?> GetAccCodeBySectorAsync(int sectorId, CancellationToken ct = default) =>
        await _db.Sectors.Where(s => s.Id == sectorId).Select(s => s.Acc!.Code).FirstOrDefaultAsync(ct);

    public async Task<int> EnsureVipiDocumentAsync(int primarySectorId, string title, Language language,
        SectionProfile profile, int authorUserId, CancellationToken ct = default)
    {
        var sector = await _db.Sectors.FirstOrDefaultAsync(s => s.Id == primarySectorId, ct)
            ?? throw new InvalidOperationException(Lingua($"Settore {primarySectorId} inesistente.", $"Sector {primarySectorId} does not exist."));
        if (sector.DocumentId is int existing) return existing;   // già migrato: idempotente

        // La nascita è condivisa con l'aeroporto (Seed/DocumentBirth): documento, prima versione bozza e le
        // sezioni del profilo, coi segnaposto sulle sezioni rese dalla pagina.
        var (doc, _) = Seed.DocumentBirth.Crea(_db, _airac, title, language, profile, authorUserId);
        await _db.SaveChangesAsync(ct);   // serve doc.Id per agganciare il settore

        // Il legame, che è la sola cosa davvero per-famiglia: qui il documento è del SETTORE primario.
        sector.DocumentId = doc.Id;
        sector.IsPrimary = true;
        await _db.SaveChangesAsync(ct);

        return doc.Id;
    }

    /// <summary>
    /// Le sezioni «rese dalla pagina» ricevono un blocco placeholder alla creazione, così NON vengono potate dalla
    /// vista quando sono senza contenuto memorizzato (il renderer le riempie live). Chi sono lo dice il CATALOGO
    /// (doc 14 §3f): prima era un elenco di chiavi che ogni chiamante scriveva a mano, e i due elenchi non
    /// combaciavano — l'ACC ne aveva cinque, l'APP otto, per la stessa domanda.
    /// </summary>
    private void AggiungiPlaceholderSeServe(DocumentVersion version, DocumentSection section,
        SectionProfile profile, string key)
    {
        if (!SectionCatalog.IsHostRendered(profile, key)) return;
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

    /// <summary>Come nasce una sezione: Live se la sua derivazione è vera solo adesso (il meteo, il timbro di
    /// validità), Frozen altrimenti — perché quello è il senso di pubblicare.</summary>
    private static RenderMode ModoAllaNascita(string key) =>
        SectionCatalog.IsAlwaysLive(key) ? RenderMode.Live : RenderMode.Frozen;

    public async Task<int> EnsureVipiDocumentTreeAsync(int primarySectorId, string title, Language language,
        IReadOnlyList<VipiBlockSpec> blocks, int authorUserId, CancellationToken ct = default)
    {
        var sector = await _db.Sectors.FirstOrDefaultAsync(s => s.Id == primarySectorId, ct)
            ?? throw new InvalidOperationException(Lingua($"Settore {primarySectorId} inesistente.", $"Sector {primarySectorId} does not exist."));
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
            foreach (var d in SectionCatalog.For(block.Profile).OrderBy(d => d.Order))
            {
                var key = d.Key;
                var child = new DocumentSection
                {
                    DocumentVersion = version,
                    ParentSection = blockSection,
                    Title = d.Title,
                    Order = childOrder++,
                    Depth = 1,
                    SectionKey = key,
                    RowVersion = Guid.NewGuid().ToByteArray(),
                    RenderMode = ModoAllaNascita(key),
                };
                _db.DocumentSections.Add(child);

                AggiungiPlaceholderSeServe(version, child, block.Profile, key);
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

    public async Task<int> AddBlockToVersionAsync(int versionId, VipiBlockSpec block, CancellationToken ct = default)
    {
        await RequireDraftAsync(versionId, ct);

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
        foreach (var d in SectionCatalog.For(block.Profile).OrderBy(d => d.Order))
        {
            var child = new DocumentSection
            {
                DocumentVersion = version,
                ParentSection = blockSection,
                Title = d.Title,
                Order = childOrder++,
                Depth = 1,
                SectionKey = d.Key,
                RowVersion = Guid.NewGuid().ToByteArray(),
                RenderMode = ModoAllaNascita(d.Key),
            };
            _db.DocumentSections.Add(child);
            AggiungiPlaceholderSeServe(version, child, block.Profile, d.Key);
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
            ?? throw new InvalidOperationException(Lingua($"Documento {documentId} senza versione di lavoro.", $"Document {documentId} has no working version."));
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
        var sha = ShaCitati(new[] { block });
        _db.ContentBlocks.Remove(block);
        await _db.SaveChangesAsync(ct);
        await LiberaImmaginiAsync(sha, ct);
    }

    public async Task RenameSectionAsync(int sectionId, string title, CancellationToken ct = default)
    {
        var section = await _db.DocumentSections.FirstOrDefaultAsync(s => s.Id == sectionId, ct)
            ?? throw new InvalidOperationException($"Sezione {sectionId} inesistente.");
        await RequireDraftAsync(section.DocumentVersionId, ct);
        section.Title = string.IsNullOrWhiteSpace(title) ? section.Title : title.Trim();
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetSectionAudienceAsync(int sectionId, SectionAudience audience, CancellationToken ct = default)
    {
        var section = await _db.DocumentSections.FirstOrDefaultAsync(s => s.Id == sectionId, ct)
            ?? throw new InvalidOperationException($"Sezione {sectionId} inesistente.");
        await RequireDraftAsync(section.DocumentVersionId, ct);
        section.Audience = audience;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetSectionBeforeParentBodyAsync(int sectionId, bool before, CancellationToken ct = default)
    {
        var section = await _db.DocumentSections.FirstOrDefaultAsync(s => s.Id == sectionId, ct)
            ?? throw new InvalidOperationException($"Sezione {sectionId} inesistente.");
        await RequireDraftAsync(section.DocumentVersionId, ct);
        section.BeforeParentBody = before;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetSectionLeadSentenceAsync(int sectionId, bool lead, CancellationToken ct = default)
    {
        var section = await _db.DocumentSections.FirstOrDefaultAsync(s => s.Id == sectionId, ct)
            ?? throw new InvalidOperationException($"Sezione {sectionId} inesistente.");
        await RequireDraftAsync(section.DocumentVersionId, ct);
        section.LeadSentence = lead;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetSectionHiddenAsync(int sectionId, bool hidden, CancellationToken ct = default)
    {
        var section = await _db.DocumentSections.FirstOrDefaultAsync(s => s.Id == sectionId, ct)
            ?? throw new InvalidOperationException($"Sezione {sectionId} inesistente.");
        await RequireDraftAsync(section.DocumentVersionId, ct);
        section.IsHidden = hidden;
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
            // Sezione libera ⇒ chiave UNIVOCA (doc 11 §3a): con la vecchia costante "custom" due sezioni libere
            // dello stesso documento collidevano per chi indicizza per chiave (viewer ACC, nascondi APP, anchor).
            SectionKey = SectionCatalogBridge.KeyFor(kind) ?? SectionKeys.NewCustom(),
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
        var sha = ShaCitati(blocks);          // include le sotto-sezioni: sparisce l'albero, spariscono le loro foto
        _db.ContentBlocks.RemoveRange(blocks);
        _db.DocumentSections.RemoveRange(subtree);
        await _db.SaveChangesAsync(ct);
        await LiberaImmaginiAsync(sha, ct);
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

    public async Task MoveSectionBeforeAsync(int sectionId, int? beforeSectionId, CancellationToken ct = default)
    {
        if (sectionId == beforeSectionId) return;   // «prima di se stessa» = nessuna mossa

        var section = await _db.DocumentSections.FirstOrDefaultAsync(s => s.Id == sectionId, ct)
            ?? throw new InvalidOperationException($"Sezione {sectionId} inesistente.");
        await RequireDraftAsync(section.DocumentVersionId, ct);

        var siblings = await _db.DocumentSections
            .Where(s => s.DocumentVersionId == section.DocumentVersionId && s.ParentSectionId == section.ParentSectionId)
            .OrderBy(s => s.Order).ThenBy(s => s.Id).ToListAsync(ct);

        // ⚠️ Il riferimento deve essere un FRATELLO: una sezione di un altro blocco (o di un altro documento)
        // non è una destinazione, è una riparentazione — e questa mossa non riparenta. Vedi IEditingRepository.
        var target = beforeSectionId is int b ? siblings.FirstOrDefault(s => s.Id == b) : null;
        if (beforeSectionId is not null && target is null) return;

        siblings.Remove(section);
        var at = target is null ? siblings.Count : siblings.IndexOf(target);
        siblings.Insert(at, section);

        // Rinumerazione densa del solo gruppo: l'Order è una posizione, non un identificativo (nessun indice
        // unico, nessun altro lettore lo confronta fra gruppi diversi).
        var changed = false;
        for (var i = 0; i < siblings.Count; i++)
            if (siblings[i].Order != i) { siblings[i].Order = i; changed = true; }
        if (changed) await _db.SaveChangesAsync(ct);
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

        // Il titolo sta nella riga, non solo l'Id: il registro deve restare leggibile anche dopo che il
        // documento è stato eliminato — è proprio allora che qualcuno lo va a rileggere.
        AuditScribe.Write(_db, actorUserId, AuditAction.Publish, "DocumentVersion", ver.Id.ToString(),
            new { doc.Id, doc.Title, ver.VersionNumber, ver.AiracCycle }, now);

        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> PruneArchivedVersionsAsync(int documentId, int keepN, CancellationToken ct = default)
    {
        // Le Archived più recenti prima; salta le keepN da tenere, pota le rimanenti. Current (Published) e Draft escluse.
        var archivedIds = await _db.DocumentVersions
            .Where(v => v.DocumentId == documentId && v.Status == DocumentStatus.Archived)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => v.Id)
            .ToListAsync(ct);
        var toDelete = archivedIds.Skip(Math.Max(0, keepN)).ToList();
        if (toDelete.Count == 0) return 0;

        var shaPotati = new List<string>();

        foreach (var versionId in toDelete)
            shaPotati.AddRange(await EliminaVersioneAsync(versionId, ct));

        // Dopo TUTTE le versioni potate, non a ogni giro: una foto puo' essere citata da due delle versioni in
        // potatura, e chiedersi se e' orfana a meta' lavoro darebbe la risposta sbagliata.
        await LiberaImmaginiAsync(shaPotati, ct);
        return toDelete.Count;
    }

    /// <summary>
    /// Elimina UNA versione col suo contenuto e ritorna gli sha delle immagini che vi comparivano (la
    /// liberazione la decide il chiamante, che sa se ha altre versioni in corso di cancellazione).
    ///
    /// <para>Ordine esplicito per i FK <c>Restrict</c> (Block→Section, Section→ParentSection self-ref): non ci
    /// si affida al cascade del database. Blocchi → sezioni figli-prima-dei-genitori → versione, lo stesso
    /// pattern di <c>DeleteSectionAsync</c>.</para>
    /// </summary>
    private async Task<IReadOnlyList<string>> EliminaVersioneAsync(int versionId, CancellationToken ct)
    {
        var blocks = await _db.ContentBlocks.Where(b => b.DocumentVersionId == versionId).ToListAsync(ct);
        var sha = ShaCitati(blocks);
        _db.ContentBlocks.RemoveRange(blocks);

        var sections = await _db.DocumentSections.Where(s => s.DocumentVersionId == versionId).ToListAsync(ct);
        var childrenByParent = sections.Where(s => s.ParentSectionId != null)
            .GroupBy(s => s.ParentSectionId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());
        var ordered = new List<DocumentSection>();
        void Collect(DocumentSection s)
        {
            if (childrenByParent.TryGetValue(s.Id, out var kids))
                foreach (var k in kids) Collect(k);
            ordered.Add(s); // post-order: figli prima dei genitori
        }
        foreach (var root in sections.Where(s => s.ParentSectionId == null)) Collect(root);
        _db.DocumentSections.RemoveRange(ordered);

        var ver = await _db.DocumentVersions.FirstOrDefaultAsync(v => v.Id == versionId, ct);
        if (ver is not null) _db.DocumentVersions.Remove(ver);
        await _db.SaveChangesAsync(ct);
        return sha;
    }

    public async Task<int> DiscardDraftAsync(int versionId, int actorUserId, CancellationToken ct = default)
    {
        var ver = await _db.DocumentVersions.FirstOrDefaultAsync(v => v.Id == versionId, ct)
                  ?? throw new KeyNotFoundException($"Versione {versionId} inesistente.");
        var numero = ver.VersionNumber;
        var documentId = ver.DocumentId;
        var titolo = await _db.Documents.Where(d => d.Id == documentId).Select(d => d.Title).FirstOrDefaultAsync(ct);

        // L'audit va scritto PRIMA della cancellazione: dopo, la versione non esiste più e resterebbe solo un
        // documento che ha perso una bozza senza che nessuno sappia chi e quando.
        AuditScribe.Write(_db, actorUserId, AuditAction.Discard, "DocumentVersion", versionId.ToString(),
            new { DocumentId = documentId, Title = titolo, VersionNumber = numero });
        await _db.SaveChangesAsync(ct);

        await LiberaImmaginiAsync(await EliminaVersioneAsync(versionId, ct), ct);
        return numero;
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

    public async Task ForceUnlockAsync(int documentId, int actorUserId, CancellationToken ct = default)
    {
        // Chi teneva il lock si legge PRIMA di toglierlo: è l'unica cosa che rende utile la riga di registro.
        // Un lock già libero (o scaduto) non è un atto d'autorità: niente riga.
        var chi = await _db.Documents.AsNoTracking().Where(d => d.Id == documentId)
            .Select(d => new { d.Title, d.LockedByUserId, d.LockedByName, d.LockExpiresUtc }).FirstOrDefaultAsync(ct);
        if (chi?.LockedByUserId is null) return;

        await _db.Documents.Where(d => d.Id == documentId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.LockedByUserId, (int?)null)
                .SetProperty(d => d.LockedByName, (string?)null)
                .SetProperty(d => d.LockedAtUtc, (DateTime?)null)
                .SetProperty(d => d.LockExpiresUtc, (DateTime?)null), ct);

        // ⚠️ ExecuteUpdate scrive subito e non passa dal change-tracker: la riga di audit ha bisogno del suo
        // SaveChanges esplicito, qui non c'è un salvataggio dell'atto a cui accodarsi.
        AuditScribe.Write(_db, actorUserId, AuditAction.ForceUnlock, "Document", documentId.ToString(),
            new { chi.Title, HeldByUserId = chi.LockedByUserId, HeldByName = chi.LockedByName, chi.LockExpiresUtc });
        await _db.SaveChangesAsync(ct);
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
