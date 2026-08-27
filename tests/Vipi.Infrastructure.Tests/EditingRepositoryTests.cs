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
        _repo = new EfEditingRepository(_db, new AiracService(), new EfMediaMaintenance(_db));
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
    public async Task CreateDraft_Preserves_Per_Section_Flags()
    {
        // La copia bozza portava titolo/ordine/chiave ma NON i flag per-sezione: aprire una bozza resettava
        // RenderMode a Frozen (doc 10) e avrebbe azzerato IsHidden (doc 11 §3c).
        var docId = await AccDocIdAsync();
        var srcVer = await _db.Documents.Where(d => d.Id == docId).Select(d => d.CurrentVersionId!.Value).FirstAsync();
        var source = await _db.DocumentSections.Where(s => s.DocumentVersionId == srcVer).OrderBy(s => s.Id).FirstAsync();
        source.RenderMode = RenderMode.Live;
        source.IsHidden = true;
        source.BeforeParentBody = true;
        await _db.SaveChangesAsync();

        var draftId = await _repo.CreateDraftAsync(docId, authorUserId: 111);

        var copy = await _db.DocumentSections
            .Where(s => s.DocumentVersionId == draftId && s.Title == source.Title).FirstAsync();
        Assert.Equal(RenderMode.Live, copy.RenderMode);
        Assert.True(copy.IsHidden);
        Assert.True(copy.BeforeParentBody);
    }

    [Fact]
    public async Task SetSectionBeforeParentBody_Requires_A_Draft()
    {
        // Stessa regola degli altri flag per-sezione: si tocca solo la bozza (doc 11 §3g).
        var docId = await AccDocIdAsync();
        var published = await _db.Documents.Where(d => d.Id == docId).Select(d => d.CurrentVersionId!.Value).FirstAsync();
        var onPublished = await _db.DocumentSections.Where(s => s.DocumentVersionId == published).OrderBy(s => s.Id).FirstAsync();

        await Assert.ThrowsAnyAsync<Exception>(() => _repo.SetSectionBeforeParentBodyAsync(onPublished.Id, true));

        var draftId = await _repo.CreateDraftAsync(docId, authorUserId: 111);
        var onDraft = await _db.DocumentSections.Where(s => s.DocumentVersionId == draftId).OrderBy(s => s.Id).FirstAsync();
        await _repo.SetSectionBeforeParentBodyAsync(onDraft.Id, true);

        Assert.True((await _db.DocumentSections.FindAsync(onDraft.Id))!.BeforeParentBody);
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
        // ⚠️ Le sezioni non si passano più: le dice il CATALOGO, per profilo (doc 14 §3f). È il punto della
        // modifica — prima ogni chiamante ne portava una lista, e accanto un secondo elenco di «chiavi live»
        // scritto a mano: l'ACC ne aveva cinque, l'APP otto, per la stessa domanda.
        var sec = await _db.Sectors.Where(s => s.DocumentId == null).Select(s => s.Id).FirstAsync();
        var atteso = SectionCatalog.For(SectionProfile.App).OrderBy(d => d.Order).Select(d => d.Key).ToArray();

        var docId = await _repo.EnsureVipiDocumentAsync(sec, "vIPI APP di test", Language.It, SectionProfile.App, authorUserId: 9);

        var doc = await _db.Documents.AsNoTracking().FirstAsync(d => d.Id == docId);
        Assert.Equal(DocumentType.Vipi, doc.Type);
        var ver = await _db.DocumentVersions.AsNoTracking().FirstAsync(v => v.DocumentId == docId);
        var keys = await _db.DocumentSections.AsNoTracking()
            .Where(s => s.DocumentVersionId == ver.Id).OrderBy(s => s.Order).Select(s => s.SectionKey).ToListAsync();
        Assert.Equal(atteso, keys);

        // E le sezioni «rese dalla pagina» hanno il loro blocco placeholder, le altre no: senza, sparirebbero
        // dalla vista quando sono vuote — che per una derivata è sempre.
        foreach (var d in SectionCatalog.For(SectionProfile.App))
        {
            var haBlocchi = await _db.ContentBlocks.AnyAsync(b => b.Section!.SectionKey == d.Key && b.DocumentVersionId == ver.Id);
            Assert.Equal(SectionCatalog.IsHostRendered(SectionProfile.App, d.Key), haBlocchi);
        }

        var linked = await _db.Sectors.AsNoTracking().FirstAsync(s => s.Id == sec);
        Assert.Equal(docId, linked.DocumentId);
        Assert.True(linked.IsPrimary);

        // Idempotente: seconda chiamata ritorna lo stesso documento, senza duplicare sezioni.
        Assert.Equal(docId, await _repo.EnsureVipiDocumentAsync(sec, "altro titolo", Language.It, SectionProfile.App, authorUserId: 9));
        Assert.Equal(atteso.Length, await _db.DocumentSections.CountAsync(s => s.DocumentVersionId == ver.Id));
    }

    [Fact]
    public async Task EnsureVipiDocumentTree_Creates_Block_Sections_With_Keyed_Children_And_Live_Placeholders()
    {
        var sec = await _db.Sectors.Where(s => s.DocumentId == null).Select(s => s.Id).FirstAsync();
        var blocks = new[]
        {
            new Vipi.Application.Abstractions.VipiBlockSpec("aerovia", "Settori di aerovia", SectionProfile.AccAerovia),
            new Vipi.Application.Abstractions.VipiBlockSpec("appgroup", "Gruppo APP", SectionProfile.AccAppBlock),
        };

        var docId = await _repo.EnsureVipiDocumentTreeAsync(sec, "vIPI ACC di test", Language.It, blocks,
            authorUserId: 3);

        var ver = await _db.DocumentVersions.AsNoTracking().FirstAsync(v => v.DocumentId == docId);

        // Due sezioni-blocco a depth 0, nell'ordine dato.
        var rootKeys = await _db.DocumentSections.AsNoTracking()
            .Where(s => s.DocumentVersionId == ver.Id && s.ParentSectionId == null)
            .OrderBy(s => s.Order).Select(s => s.SectionKey).ToListAsync();
        Assert.Equal(new[] { "aerovia", "appgroup" }, rootKeys);

        // Figli del blocco Aerovia a depth 1, nell'ordine dato.
        var aeroviaId = await _db.DocumentSections
            .Where(s => s.DocumentVersionId == ver.Id && s.ParentSectionId == null && s.SectionKey == "aerovia")
            .Select(s => s.Id).FirstAsync();
        var childKeys = await _db.DocumentSections.AsNoTracking()
            .Where(s => s.ParentSectionId == aeroviaId).OrderBy(s => s.Order).Select(s => new { s.SectionKey, s.Depth }).ToListAsync();
        // ⚠️ Le figlie le dice il CATALOGO del profilo del blocco (doc 14 §3f), non una lista passata a mano.
        Assert.Equal(SectionCatalog.For(SectionProfile.AccAerovia).OrderBy(d => d.Order).Select(d => d.Key),
            childKeys.Select(c => c.SectionKey));
        Assert.All(childKeys, c => Assert.Equal(1, c.Depth));

        // Il placeholder va alle sezioni «rese dalla pagina», e a quelle sole: chi sono lo dice il catalogo.
        var attesiPlaceholder =
            SectionCatalog.For(SectionProfile.AccAerovia).Count(d => SectionCatalog.IsHostRendered(SectionProfile.AccAerovia, d.Key))
            + SectionCatalog.For(SectionProfile.AccAppBlock).Count(d => SectionCatalog.IsHostRendered(SectionProfile.AccAppBlock, d.Key));
        Assert.Equal(attesiPlaceholder, await _db.ContentBlocks.CountAsync(b => b.DocumentVersion!.DocumentId == docId));

        var linked = await _db.Sectors.AsNoTracking().FirstAsync(s => s.Id == sec);
        Assert.Equal(docId, linked.DocumentId);
        Assert.True(linked.IsPrimary);

        // Idempotente: seconda chiamata ritorna lo stesso documento senza duplicare sezioni.
        Assert.Equal(docId, await _repo.EnsureVipiDocumentTreeAsync(sec, "altro", Language.It, blocks, authorUserId: 3));
        var atteseSezioni = 2   // le due sezioni-blocco
            + SectionCatalog.For(SectionProfile.AccAerovia).Count
            + SectionCatalog.For(SectionProfile.AccAppBlock).Count;
        Assert.Equal(atteseSezioni, await _db.DocumentSections.CountAsync(s => s.DocumentVersionId == ver.Id));
    }

    [Fact]
    public async Task SectionBlockJsonBySection_RoundTrips_And_Guards_Draft()
    {
        var sec = await _db.Sectors.Where(s => s.DocumentId == null).Select(s => s.Id).FirstAsync();
        var blocks = new[]
        {
            new Vipi.Application.Abstractions.VipiBlockSpec("aerovia", "Settori di aerovia", SectionProfile.AccAerovia),
        };
        var docId = await _repo.EnsureVipiDocumentTreeAsync(sec, "vIPI ACC di test", Language.It, blocks, authorUserId: 4);
        var ver = await _db.DocumentVersions.AsNoTracking().FirstAsync(v => v.DocumentId == docId);
        var sepId = await _db.DocumentSections
            .Where(s => s.DocumentVersionId == ver.Id && s.SectionKey == "separations").Select(s => s.Id).FirstAsync();

        // Nessun contenuto → null.
        Assert.Null(await _repo.GetSectionBlockJsonBySectionAsync(sepId));

        // Upsert crea il blocco.
        await _repo.SaveSectionBlockJsonBySectionAsync(sepId, "[{\"Vertical\":\"1000 ft\"}]", authorUserId: 4);
        Assert.Equal("[{\"Vertical\":\"1000 ft\"}]", await _repo.GetSectionBlockJsonBySectionAsync(sepId));
        Assert.Equal(1, await _db.ContentBlocks.CountAsync(b => b.SectionId == sepId));

        // Secondo upsert aggiorna lo stesso blocco (niente duplicati).
        await _repo.SaveSectionBlockJsonBySectionAsync(sepId, "[{\"Vertical\":\"2000 ft\"}]", authorUserId: 4);
        Assert.Equal("[{\"Vertical\":\"2000 ft\"}]", await _repo.GetSectionBlockJsonBySectionAsync(sepId));
        Assert.Equal(1, await _db.ContentBlocks.CountAsync(b => b.SectionId == sepId));

        // json vuoto → azzera.
        await _repo.SaveSectionBlockJsonBySectionAsync(sepId, "  ", authorUserId: 4);
        Assert.Null(await _repo.GetSectionBlockJsonBySectionAsync(sepId));

        // Sezione inesistente → errore.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.SaveSectionBlockJsonBySectionAsync(999999, "x", authorUserId: 4));
    }

    [Fact]
    public async Task SectionBlockJson_RoundTrips_And_Clears_On_Keyed_Section()
    {
        // Documento vIPI APP seedato a mano (bozza v1) con la sezione radice keyed "separations".
        var sec = await _db.Sectors.Where(s => s.DocumentId == null).Select(s => s.Id).FirstAsync();
        var docId = await _repo.EnsureVipiDocumentAsync(
            sec, "vIPI APP di test", Language.It, SectionProfile.App, authorUserId: 5);

        // Prima del salvataggio: nessun blocco → null.
        Assert.Null(await _repo.GetSectionBlockJsonAsync(docId, "separations"));

        // Upsert (crea il blocco).
        await _repo.SaveSectionBlockJsonAsync(docId, "separations", "[{\"Vertical\":\"1000 ft\"}]", authorUserId: 5);
        Assert.Equal("[{\"Vertical\":\"1000 ft\"}]", await _repo.GetSectionBlockJsonAsync(docId, "separations"));
        Assert.Equal(1, await _db.ContentBlocks.CountAsync(b => b.Section!.SectionKey == "separations"
            && b.DocumentVersion!.DocumentId == docId));

        // Secondo upsert: aggiorna lo stesso blocco (niente duplicati).
        await _repo.SaveSectionBlockJsonAsync(docId, "separations", "[{\"Vertical\":\"2000 ft\"}]", authorUserId: 5);
        Assert.Equal("[{\"Vertical\":\"2000 ft\"}]", await _repo.GetSectionBlockJsonAsync(docId, "separations"));
        Assert.Equal(1, await _db.ContentBlocks.CountAsync(b => b.Section!.SectionKey == "separations"
            && b.DocumentVersion!.DocumentId == docId));

        // json vuoto → azzera (blocco resta ma BodyJson null).
        await _repo.SaveSectionBlockJsonAsync(docId, "separations", "  ", authorUserId: 5);
        Assert.Null(await _repo.GetSectionBlockJsonAsync(docId, "separations"));

        // Sezione inesistente → errore.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.SaveSectionBlockJsonAsync(docId, "nope", "x", authorUserId: 5));
    }

    [Fact]
    public async Task SetSectionRenderMode_Persists_On_Draft_And_Guards_Published()
    {
        var docId = await AccDocIdAsync();
        var draftId = await _repo.CreateDraftAsync(docId, authorUserId: 9);
        var sec = await _db.DocumentSections
            .Where(s => s.DocumentVersionId == draftId && s.SectionKey == "frequencies").FirstAsync();
        Assert.Equal(RenderMode.Frozen, sec.RenderMode);   // default

        await _repo.SetSectionRenderModeAsync(sec.Id, RenderMode.Live);
        Assert.Equal(RenderMode.Live, (await _db.DocumentSections.AsNoTracking().FirstAsync(s => s.Id == sec.Id)).RenderMode);

        // Sezione di una versione PUBBLICATA (non bozza) → errore.
        var pubVer = await _db.Documents.Where(d => d.Id == docId).Select(d => d.CurrentVersionId!.Value).FirstAsync();
        var pubSec = await _db.DocumentSections.Where(s => s.DocumentVersionId == pubVer).Select(s => s.Id).FirstAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => _repo.SetSectionRenderModeAsync(pubSec, RenderMode.Live));

        // Sezione inesistente → errore.
        await Assert.ThrowsAsync<InvalidOperationException>(() => _repo.SetSectionRenderModeAsync(999999, RenderMode.Live));
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
    public async Task PruneArchivedVersions_KeepsNewestN_DeletesRest_WithChildren_PreservesCurrentAndDraft()
    {
        var docId = await AccDocIdAsync();

        // Genera 3 versioni Archived pubblicando in sequenza (ogni publish archivia la corrente precedente).
        for (var i = 0; i < 3; i++)
        {
            var draftId = await _repo.CreateDraftAsync(docId, authorUserId: 1);
            await _repo.PublishAsync(draftId, actorUserId: 1, note: $"v{i}");
        }
        var archived = await _db.DocumentVersions.AsNoTracking()
            .Where(v => v.DocumentId == docId && v.Status == DocumentStatus.Archived)
            .OrderBy(v => v.VersionNumber).Select(v => v.Id).ToListAsync();
        Assert.Equal(3, archived.Count);   // la vIPI seedata era Published → prima archiviata + 2 successive
        var currentId = await _db.Documents.Where(d => d.Id == docId).Select(d => d.CurrentVersionId!.Value).FirstAsync();

        // Una bozza pendente non deve essere toccata.
        var pendingDraft = await _repo.CreateDraftAsync(docId, authorUserId: 1);

        var oldest = archived[0];
        var oldestSections = await _db.DocumentSections.CountAsync(s => s.DocumentVersionId == oldest);
        Assert.True(oldestSections > 0);   // porta righe figlie → verifica la cancellazione ordinata

        var removed = await _repo.PruneArchivedVersionsAsync(docId, keepN: 1);
        Assert.Equal(2, removed);   // tiene la più recente Archived, pota le altre 2

        // Le due potate (incl. la più vecchia) spariscono con sezioni e blocchi.
        Assert.False(await _db.DocumentVersions.AnyAsync(v => v.Id == oldest || v.Id == archived[1]));
        Assert.Equal(0, await _db.DocumentSections.CountAsync(s => s.DocumentVersionId == oldest));
        Assert.Equal(0, await _db.ContentBlocks.CountAsync(b => b.DocumentVersionId == oldest));

        // La Archived più recente, la corrente e la bozza restano intatte.
        Assert.True(await _db.DocumentVersions.AnyAsync(v => v.Id == archived[2]));
        Assert.True(await _db.DocumentVersions.AnyAsync(v => v.Id == currentId));
        Assert.True(await _db.DocumentVersions.AnyAsync(v => v.Id == pendingDraft && v.Status == DocumentStatus.Draft));

        // Idempotente: seconda passata non rimuove altro (resta 1 Archived).
        Assert.Equal(0, await _repo.PruneArchivedVersionsAsync(docId, keepN: 1));
    }

    [Fact]
    public async Task EditingService_Publish_EnforcesArchivedCap_NotOffByOne()
    {
        // Il version-publish (bozza→pubblicata) archivia la precedente: deve potare le Archived oltre il cap NELLO STESSO
        // giro, non lasciarne N+1 in attesa del boot sweep. keepN=2: dopo ogni publish le Archived restano ≤ 2.
        var docId = await AccDocIdAsync();
        var svc = new EditingService(_repo, new AllowAuthz(),
            Microsoft.Extensions.Options.Options.Create(new Vipi.Application.ReleaseRetentionOptions { KeepArchivedVersionsPerDocument = 2 }));

        for (var i = 0; i < 5; i++)
        {
            var draftId = await svc.CreateDraftAsync(docId);
            await svc.PublishAsync(draftId, note: null);
            var archived = await _db.DocumentVersions.CountAsync(v => v.DocumentId == docId && v.Status == DocumentStatus.Archived);
            Assert.True(archived <= 2, $"Archived {archived} oltre il cap 2 dopo il publish #{i}");
        }
    }

    /// <summary>
    /// «Scarta bozza» (voce E5): la bozza sparisce col suo contenuto, il documento torna alla versione
    /// pubblicata e resta un'impronta in audit — senza la quale un documento perderebbe una bozza senza che
    /// nessuno sappia chi e quando.
    /// </summary>
    [Fact]
    public async Task DiscardDraft_RemovesDraftWithChildren_KeepsPublished_AndAudits()
    {
        var docId = await AccDocIdAsync();
        var svc = Servizio();

        var draftId = await svc.CreateDraftAsync(docId);
        var sezioni = await _db.DocumentSections.CountAsync(s => s.DocumentVersionId == draftId);
        Assert.True(sezioni > 0);   // la bozza clona il contenuto ⇒ la cancellazione deve essere ordinata
        var correnteId = await _db.Documents.Where(d => d.Id == docId).Select(d => d.CurrentVersionId!.Value).FirstAsync();

        var numero = await svc.DiscardDraftAsync(draftId);

        Assert.False(await _db.DocumentVersions.AnyAsync(v => v.Id == draftId));
        Assert.Equal(0, await _db.DocumentSections.CountAsync(s => s.DocumentVersionId == draftId));
        Assert.Equal(0, await _db.ContentBlocks.CountAsync(b => b.DocumentVersionId == draftId));

        // Il documento resta in piedi sulla versione pubblicata: scartare non tocca ciò che il pubblico vede.
        var doc = await _db.Documents.AsNoTracking().FirstAsync(d => d.Id == docId);
        Assert.Equal(correnteId, doc.CurrentVersionId);
        Assert.Equal(DocumentStatus.Published, doc.Status);

        Assert.True(numero > 0);
        Assert.True(await _db.AuditLogs.AnyAsync(a => a.Action == AuditAction.Discard && a.EntityId == draftId.ToString()));
    }

    /// <summary>
    /// Le due cose che «scarta» NON deve fare: toccare una versione pubblicata (è storia, e le release
    /// dichiarano di averla fotografata) e svuotare un documento che non ha altro — lì la bozza È il
    /// documento, e chi vuole disfarsene ha l'eliminazione, che è un'altra azione con altre conseguenze.
    /// </summary>
    [Fact]
    public async Task DiscardDraft_RefusesPublishedVersion_AndLastRemainingVersion()
    {
        var docId = await AccDocIdAsync();
        var svc = Servizio();

        // Il lock si prende prima, come fa la pagina: scartare è una scrittura, e la guardia del lock
        // precede la validazione esattamente come nella pubblicazione.
        await svc.AcquireLockAsync(docId);

        var correnteId = await _db.Documents.Where(d => d.Id == docId).Select(d => d.CurrentVersionId!.Value).FirstAsync();
        var suPubblicata = await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => svc.DiscardDraftAsync(correnteId));
        Assert.Contains("non è una bozza", suPubblicata.Message);

        // Documento nuovo, mai pubblicato: la sua unica versione è una bozza.
        var nuovoId = await _repo.CreateDocumentAsync(DocumentType.Vipi, "Solo bozza", Language.It,
            Array.Empty<int>(), null, null, authorUserId: 1);
        var unica = await _db.DocumentVersions.Where(v => v.DocumentId == nuovoId).Select(v => v.Id).SingleAsync();
        await svc.AcquireLockAsync(nuovoId);

        var unicaVersione = await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => svc.DiscardDraftAsync(unica));
        Assert.Contains("unica versione", unicaVersione.Message);
        Assert.True(await _db.DocumentVersions.AnyAsync(v => v.Id == unica));   // non è stata toccata
    }

    private EditingService Servizio() => new(_repo, new AllowAuthz(),
        Microsoft.Extensions.Options.Options.Create(new Vipi.Application.ReleaseRetentionOptions()));

    private sealed class AllowAuthz : Vipi.Application.Auth.IEditAuthorizationService
    {
        public bool IsAdmin => true;
        public int? CurrentUserId => 111;
        public string? CurrentName => "test";
        public Task EnsureCanEditAccAsync(string accCode, CancellationToken ct = default) => Task.CompletedTask;
        public Task EnsureCanEditDocumentAsync(int documentId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> CanEditAccAsync(string accCode, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> CanEditDocumentAsync(int documentId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> CanEditAnythingAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<Vipi.Application.Auth.GrantRow>> ListGrantsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Vipi.Application.Auth.GrantRow>>(Array.Empty<Vipi.Application.Auth.GrantRow>());
        public Task<int> AddGrantAsync(int UserId, string? displayName, string accCode, CancellationToken ct = default) => Task.FromResult(0);
        public Task RevokeGrantAsync(int grantId, CancellationToken ct = default) => Task.CompletedTask;
        public void EnsureAdmin() { }
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

    // Trascinamento nel menu-sezioni: la sezione salta N posti in un colpo, e il gruppo si rinumera.
    [Fact]
    public async Task MoveSectionBefore_Moves_Across_Several_Places()
    {
        var docId = await AccDocIdAsync();
        var draftId = await _repo.CreateDraftAsync(docId, authorUserId: 1);

        var ids = new List<int>();
        foreach (var t in new[] { "A", "B", "C", "D" })
            ids.Add(await _repo.AddSectionAsync(draftId, null, t, Vipi.Domain.BlockSection.Other));

        // D prima di B  ->  A, D, B, C
        await _repo.MoveSectionBeforeAsync(ids[3], ids[1]);

        Assert.Equal(new[] { ids[0], ids[3], ids[1], ids[2] }, await OrderedRootsAsync(draftId, ids));

        // ...e in coda (riferimento null): A, B, C, D di nuovo.
        await _repo.MoveSectionBeforeAsync(ids[3], null);
        Assert.Equal(new[] { ids[0], ids[1], ids[2], ids[3] }, await OrderedRootsAsync(draftId, ids));
    }

    // ⚠️ Il vincolo «solo dentro il suo gruppo» sta nel motore, non nella UI: un riferimento che non e' un
    // FRATELLO non sposta niente — altrimenti sarebbe una riparentazione silenziosa.
    [Fact]
    public async Task MoveSectionBefore_Ignores_A_Target_From_Another_Group()
    {
        var docId = await AccDocIdAsync();
        var draftId = await _repo.CreateDraftAsync(docId, authorUserId: 1);

        var a = await _repo.AddSectionAsync(draftId, null, "A", Vipi.Domain.BlockSection.Other);
        var b = await _repo.AddSectionAsync(draftId, null, "B", Vipi.Domain.BlockSection.Other);
        var childOfA = await _repo.AddSectionAsync(draftId, a, "A1", Vipi.Domain.BlockSection.Other);

        await _repo.MoveSectionBeforeAsync(b, childOfA);

        Assert.Equal(new[] { a, b }, await OrderedRootsAsync(draftId, new[] { a, b }));
        Assert.Equal(a, await _db.DocumentSections.Where(s => s.Id == childOfA)
            .Select(s => s.ParentSectionId).FirstAsync());
    }

    /// <summary>Le sezioni radice della bozza nell'ordine del documento, ristrette a quelle del test: la bozza
    /// nasce copiando la versione precedente, quindi di radici ne ha gia' di sue.</summary>
    private async Task<int[]> OrderedRootsAsync(int draftId, IReadOnlyCollection<int> only) => await _db.DocumentSections
        .Where(s => s.DocumentVersionId == draftId && s.ParentSectionId == null && only.Contains(s.Id))
        .OrderBy(s => s.Order).ThenBy(s => s.Id).Select(s => s.Id).ToArrayAsync();

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
