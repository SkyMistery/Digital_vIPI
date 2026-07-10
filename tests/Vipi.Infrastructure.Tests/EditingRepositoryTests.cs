using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Round-trip del workflow di editing su DB reale (SQLite in-memory): clona bozza dalla vIPI Roma seedata,
/// modifica un blocco, pubblica, e verifica che la nuova versione diventi corrente con audit registrato.
/// </summary>
public class EditingRepositoryTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfEditingRepository _repo = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        await RomaStructureSeed.SeedAsync(_db);
        await RomaContentSeed.SeedAsync(_db);
        await RomaVloaSeed.SeedAsync(_db);
        _repo = new EfEditingRepository(_db, new AiracService());
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private async Task<int> AccDocIdAsync() =>
        await _db.Documents.Where(d => d.Type == DocumentType.Vipi).Select(d => d.Id).FirstAsync();

    [Fact]
    public async Task CreateDraft_Clones_Sections_And_Blocks()
    {
        var docId = await AccDocIdAsync();
        var srcVer = await _db.Documents.Where(d => d.Id == docId).Select(d => d.CurrentVersionId!.Value).FirstAsync();
        var srcSections = await _db.DocumentSections.CountAsync(s => s.DocumentVersionId == srcVer);
        var srcBlocks = await _db.ContentBlocks.CountAsync(b => b.DocumentVersionId == srcVer);

        var draftId = await _repo.CreateDraftAsync(docId, authorUserId: 111);

        Assert.NotEqual(srcVer, draftId);
        Assert.Equal(srcSections, await _db.DocumentSections.CountAsync(s => s.DocumentVersionId == draftId));
        Assert.Equal(srcBlocks, await _db.ContentBlocks.CountAsync(b => b.DocumentVersionId == draftId));

        // Idempotente: una seconda chiamata riusa la stessa bozza.
        Assert.Equal(draftId, await _repo.CreateDraftAsync(docId, authorUserId: 111));
    }

    [Fact]
    public async Task CreateDocument_Vipi_From_Scratch_Has_Draft_And_Root_Section()
    {
        // Settore non ancora descritto da nessun documento (gli ACC sono già assegnati dal content seed).
        var scopeSec = await _db.Sectors.Where(s => s.DocumentId == null).Select(s => s.Id).FirstAsync();

        var newDocId = await _repo.CreateDocumentAsync(
            DocumentType.Vipi, "vIPI di test", Language.It, new[] { scopeSec }, scopeSec, parties: null, authorUserId: 7);

        var doc = await _db.Documents.AsNoTracking().FirstAsync(d => d.Id == newDocId);
        Assert.Equal(DocumentType.Vipi, doc.Type);
        Assert.Equal(DocumentStatus.Draft, doc.Status);

        // Il settore di scope è ora agganciato al documento, come primario.
        var sec = await _db.Sectors.AsNoTracking().FirstAsync(s => s.Id == scopeSec);
        Assert.Equal(newDocId, sec.DocumentId);
        Assert.True(sec.IsPrimary);

        var ver = await _db.DocumentVersions.AsNoTracking().FirstAsync(v => v.DocumentId == newDocId);
        Assert.Equal(1, ver.VersionNumber);
        Assert.Equal(DocumentStatus.Draft, ver.Status);
        Assert.Equal(1, await _db.DocumentSections.CountAsync(s => s.DocumentVersionId == ver.Id && s.ParentSectionId == null));

        // ACC risolta dal settore di scope.
        Assert.False(string.IsNullOrEmpty(await _repo.GetAccCodeBySectorAsync(scopeSec)));
    }

    [Fact]
    public async Task EnsureVipiDocument_Creates_Keyed_Sections_And_Is_Idempotent()
    {
        var sec = await _db.Sectors.Where(s => s.DocumentId == null).Select(s => s.Id).FirstAsync();
        var sections = new (string Key, string Title)[]
        {
            ("separations", "Separazioni"), ("aor", "AOR"), ("validity", "Validità e revisione"),
        };

        var docId = await _repo.EnsureVipiDocumentAsync(sec, "vIPI APP di test", Language.It, sections, authorUserId: 9);

        var doc = await _db.Documents.AsNoTracking().FirstAsync(d => d.Id == docId);
        Assert.Equal(DocumentType.Vipi, doc.Type);
        var ver = await _db.DocumentVersions.AsNoTracking().FirstAsync(v => v.DocumentId == docId);
        var keys = await _db.DocumentSections.AsNoTracking()
            .Where(s => s.DocumentVersionId == ver.Id).OrderBy(s => s.Order).Select(s => s.SectionKey).ToListAsync();
        Assert.Equal(new[] { "separations", "aor", "validity" }, keys);

        var linked = await _db.Sectors.AsNoTracking().FirstAsync(s => s.Id == sec);
        Assert.Equal(docId, linked.DocumentId);
        Assert.True(linked.IsPrimary);

        // Idempotente: seconda chiamata ritorna lo stesso documento, senza duplicare sezioni.
        Assert.Equal(docId, await _repo.EnsureVipiDocumentAsync(sec, "altro titolo", Language.It, sections, authorUserId: 9));
        Assert.Equal(3, await _db.DocumentSections.CountAsync(s => s.DocumentVersionId == ver.Id));
    }

    [Fact]
    public async Task CreateDocument_Vloa_Adds_Two_Parties()
    {
        var sectors = await _db.Sectors.Select(s => s.Id).Take(2).ToListAsync();

        var newDocId = await _repo.CreateDocumentAsync(
            DocumentType.Vloa, "vLOA di test", Language.En, scopeSectorIds: null, primarySectorId: null,
            parties: (sectors[0], sectors[1]), authorUserId: 7);

        var roles = await _db.Set<Vipi.Domain.Entities.DocumentParty>()
            .Where(p => p.DocumentId == newDocId).Select(p => p.Role).ToListAsync();
        Assert.Contains(PartyRole.Home, roles);
        Assert.Contains(PartyRole.Neighbour, roles);
    }

    [Fact]
    public async Task Edit_Then_Publish_Makes_Draft_Current_With_Audit()
    {
        var docId = await AccDocIdAsync();
        var draftId = await _repo.CreateDraftAsync(docId, authorUserId: 222);

        var firstBlock = await _db.ContentBlocks
            .Where(b => b.DocumentVersionId == draftId && b.Format == BlockFormat.Prose)
            .OrderBy(b => b.Id).FirstAsync();

        await _repo.UpdateBlockAsync(firstBlock.Id, new BlockEdit
        {
            Tier = firstBlock.Tier,
            Visibility = firstBlock.Visibility,
            Body = "TESTO MODIFICATO",
        });

        await _repo.PublishAsync(draftId, actorUserId: 222, note: "pubblicazione test");

        var doc = await _db.Documents.AsNoTracking().FirstAsync(d => d.Id == docId);
        Assert.Equal(draftId, doc.CurrentVersionId);
        Assert.Equal(DocumentStatus.Published, doc.Status);

        var publishedBlock = await _db.ContentBlocks.AsNoTracking().FirstAsync(b => b.Id == firstBlock.Id);
        Assert.Equal("TESTO MODIFICATO", publishedBlock.Body);

        Assert.True(await _db.AuditLogs.AnyAsync(a => a.Action == AuditAction.Publish && a.UserId == 222));
    }

    [Fact]
    public async Task AddSection_Respects_MaxDepth()
    {
        var docId = await AccDocIdAsync();
        var draftId = await _repo.CreateDraftAsync(docId, authorUserId: 1);

        var l0 = await _repo.AddSectionAsync(draftId, null, "L0", Vipi.Domain.BlockSection.Other);
        var l1 = await _repo.AddSectionAsync(draftId, l0, "L1", Vipi.Domain.BlockSection.Other);
        var l2 = await _repo.AddSectionAsync(draftId, l1, "L2", Vipi.Domain.BlockSection.Other);
        var l3 = await _repo.AddSectionAsync(draftId, l2, "L3", Vipi.Domain.BlockSection.Other); // depth 3 = OK

        Assert.True(l3 > 0);
        // depth 4 → rifiutato
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.AddSectionAsync(draftId, l3, "L4", Vipi.Domain.BlockSection.Other));
    }

    [Fact]
    public async Task DeleteSection_Removes_Subtree_And_Blocks()
    {
        var docId = await AccDocIdAsync();
        var draftId = await _repo.CreateDraftAsync(docId, authorUserId: 1);

        var root = await _repo.AddSectionAsync(draftId, null, "Root", Vipi.Domain.BlockSection.Other);
        var child = await _repo.AddSectionAsync(draftId, root, "Child", Vipi.Domain.BlockSection.Other);
        await _repo.AddBlockAsync(child, BlockFormat.Prose, BlockTier.Reduced, BlockVisibility.Always);

        await _repo.DeleteSectionAsync(root);

        Assert.False(await _db.DocumentSections.AnyAsync(s => s.Id == root || s.Id == child));
        Assert.Equal(0, await _db.ContentBlocks.CountAsync(b => b.SectionId == child));
    }

    [Fact]
    public async Task MoveSection_Swaps_Order_With_Sibling()
    {
        var docId = await AccDocIdAsync();
        var draftId = await _repo.CreateDraftAsync(docId, authorUserId: 1);

        var a = await _repo.AddSectionAsync(draftId, null, "A", Vipi.Domain.BlockSection.Other);
        var b = await _repo.AddSectionAsync(draftId, null, "B", Vipi.Domain.BlockSection.Other);
        var oa = await _db.DocumentSections.Where(s => s.Id == a).Select(s => s.Order).FirstAsync();
        var ob = await _db.DocumentSections.Where(s => s.Id == b).Select(s => s.Order).FirstAsync();
        Assert.True(oa < ob);

        await _repo.MoveSectionAsync(b, -1); // B su

        Assert.Equal(oa, await _db.DocumentSections.Where(s => s.Id == b).Select(s => s.Order).FirstAsync());
        Assert.Equal(ob, await _db.DocumentSections.Where(s => s.Id == a).Select(s => s.Order).FirstAsync());
    }

    [Fact]
    public async Task Vloa_Document_Is_Editable_RoundTrip()
    {
        var vloaId = await _db.Documents.Where(d => d.Type == DocumentType.Vloa).Select(d => d.Id).FirstAsync();
        var draftId = await _repo.CreateDraftAsync(vloaId, authorUserId: 9);

        var block = await _db.ContentBlocks
            .Where(b => b.DocumentVersionId == draftId && b.Format == BlockFormat.Prose)
            .OrderBy(b => b.Id).FirstAsync();
        await _repo.UpdateBlockAsync(block.Id, new BlockEdit { Tier = block.Tier, Visibility = block.Visibility, Body = "EDIT vLOA" });
        await _repo.PublishAsync(draftId, actorUserId: 9, note: "vloa test");

        var doc = await _db.Documents.AsNoTracking().FirstAsync(d => d.Id == vloaId);
        Assert.Equal(draftId, doc.CurrentVersionId);
        Assert.Equal("EDIT vLOA", (await _db.ContentBlocks.AsNoTracking().FirstAsync(b => b.Id == block.Id)).Body);
    }

    [Fact]
    public async Task Editing_A_Published_Version_Is_Rejected()
    {
        var docId = await AccDocIdAsync();
        var publishedVer = await _db.Documents.Where(d => d.Id == docId).Select(d => d.CurrentVersionId!.Value).FirstAsync();
        var block = await _db.ContentBlocks.Where(b => b.DocumentVersionId == publishedVer).OrderBy(b => b.Id).FirstAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.UpdateBlockAsync(block.Id, new BlockEdit { Tier = block.Tier, Visibility = block.Visibility, Body = "x" }));
    }
}
