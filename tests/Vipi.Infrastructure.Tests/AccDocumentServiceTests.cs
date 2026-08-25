using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Storage della vIPI ACC su Document (doc 08e-acc): EnsureAsync crea il Document chiavizzato sul settore CTR radice
/// primario col blocco Aerovia di default; LoadForEditAsync riassembla i blocchi dall'albero DocumentSection.
/// </summary>
public class AccDocumentServiceTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private AccDocumentService _service = default!;

    private const string Acc = "LIRR";

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        await RomaStructureSeed.SeedAsync(_db);

        var repo = new EfAccDerivationRepository(_db);
        var editing = new EfEditingRepository(_db, new AiracService(), new EfMediaMaintenance(_db));
        _service = new AccDocumentService(repo, editing, new AllowAuthz(), TestReleaseTargets.ReleaseRepo(_db));
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    [Fact]
    public async Task Ensure_Creates_Document_Keyed_On_Primary_Ctr_Root_With_Default_Aerovia_Block()
    {
        var id0 = await _service.GetIdentityAsync(Acc);
        Assert.NotNull(id0);
        Assert.Null(id0!.DocumentId);   // non ancora migrato

        var docId = await _service.EnsureAsync(Acc);

        // Il settore radice primario è ora agganciato al Document.
        var linked = await _db.Sectors.AsNoTracking().FirstAsync(s => s.Id == id0.SectorId);
        Assert.Equal(docId, linked.DocumentId);
        Assert.True(linked.IsPrimary);

        // GetIdentity riflette il DocumentId; il documento è vIPI.
        Assert.Equal(docId, (await _service.GetIdentityAsync(Acc))!.DocumentId);
        Assert.Equal(DocumentType.Vipi, (await _db.Documents.AsNoTracking().FirstAsync(d => d.Id == docId)).Type);

        // Idempotente.
        Assert.Equal(docId, await _service.EnsureAsync(Acc));
    }

    [Fact]
    public async Task LoadForEdit_Assembles_Single_Aerovia_Block_With_Catalog_Children()
    {
        var model = await _service.LoadForEditAsync(Acc);

        Assert.True(model.IsDraft);
        Assert.Equal(Acc, model.AccCode);

        var block = Assert.Single(model.Blocks);
        Assert.Equal(AccBlockKind.Aerovia, block.Block.Kind);
        Assert.Equal("aerovia", block.Block.Key);

        // Le figlie corrispondono alle sezioni del catalogo AccAerovia (stesse chiavi, stesso ordine).
        var expected = SectionCatalog.For(SectionProfile.AccAerovia).Select(d => d.Key).ToArray();
        Assert.Equal(expected, block.Block.Sections.Select(s => s.Key).ToArray());

        // La mappa chiave-figlia → Id copre le sezioni live (per i salvataggi by-section).
        Assert.True(block.ChildSectionIdsByKey.ContainsKey("aor"));
        Assert.True(block.ChildSectionIdsByKey.ContainsKey("frequencies"));
    }

    [Fact]
    public async Task Save_Configurations_And_Separations_RoundTrip_Through_Document()
    {
        var model = await _service.LoadForEditAsync(Acc);
        var block = Assert.Single(model.Blocks);
        var configId = block.ChildSectionIdsByKey["configurations"];
        var sepId = block.ChildSectionIdsByKey["separations"];

        await _service.SaveConfigurationsAsync(Acc, configId, new[]
        {
            new AccConfiguration { Key = "cfg:1", Name = "Conf 1", Open = new() { new AccConfigOpen { Callsign = "LIRR_CTR" } } },
        });
        await _service.SaveSeparationsAsync(Acc, sepId, new[] { new AppSeparationRow("1000 ft", "5 NM") });

        var reloaded = Assert.Single((await _service.LoadForEditAsync(Acc)).Blocks).Block;
        Assert.Equal("Conf 1", Assert.Single(reloaded.Configurations).Name);
        Assert.Equal("1000 ft", Assert.Single(reloaded.Separations).Vertical);

        // Azzeramento: lista vuota → sparisce.
        await _service.SaveSeparationsAsync(Acc, sepId, Array.Empty<AppSeparationRow>());
        Assert.Empty(Assert.Single((await _service.LoadForEditAsync(Acc)).Blocks).Block.Separations);
    }

    [Fact]
    public async Task AddGroup_Then_RemoveGroup_On_Draft()
    {
        var model = await _service.LoadForEditAsync(Acc);   // bozza v1 col solo blocco Aerovia
        var groupId = await _service.AddGroupAsync(Acc, model.VersionId, "Gruppo Pisa");

        var afterAdd = await _service.LoadForEditAsync(Acc);
        Assert.Equal(2, afterAdd.Blocks.Count);
        var grp = afterAdd.Blocks[1];
        Assert.Equal(AccBlockKind.AppGroup, grp.Block.Kind);
        Assert.Equal("Gruppo Pisa", grp.Block.Title);
        // Sezioni-catalogo del profilo AccAppBlock (include vfr, assente in Aerovia).
        var expected = SectionCatalog.For(SectionProfile.AccAppBlock).Select(d => d.Key).ToArray();
        Assert.Equal(expected, grp.Block.Sections.Select(s => s.Key).ToArray());

        await _service.RemoveGroupAsync(Acc, groupId);
        Assert.Single((await _service.LoadForEditAsync(Acc)).Blocks);
    }

    [Fact]
    public async Task LoadForView_Null_Without_Effective_Release()
    {
        // Doc 10 §S6b: visibilità pubblica = release effettiva (uniforme alle altre famiglie). Rimosso il guscio
        // sintetico e il fallback alla versione pubblicata live.
        // Non ancora migrato: nessuna release → invisibile (null).
        Assert.Null(await _service.LoadForViewAsync(Acc));

        // Anche dopo aver pubblicato la VERSIONE (senza release effettiva) resta invisibile: serve una release.
        var docId = await _service.EnsureAsync(Acc);
        var editing = new EfEditingRepository(_db, new AiracService(), new EfMediaMaintenance(_db));
        var draftVer = await _db.DocumentVersions.Where(v => v.DocumentId == docId).Select(v => v.Id).FirstAsync();
        await editing.PublishAsync(draftVer, actorUserId: 1, note: "pub");
        Assert.Null(await _service.LoadForViewAsync(Acc));
    }

    [Fact]
    public async Task LoadForView_HiddenDocument_IsNotServedToPublic()
    {
        // Stesso gate degli altri tipi (HiddenApp_WithEffectiveRelease_StaysHidden): «nascosto» vale anche
        // all'URL diretto, non solo in landing/ricerca. Qui l'ACC lo saltava: release effettiva → pagina servita.
        var docId = await _service.EnsureAsync(Acc);
        var releases = TestReleaseTargets.ReleaseRepo(_db);
        var key = $"{Acc}|{(await _service.GetIdentityAsync(Acc))!.RootCallsign}";
        var snap = await releases.SnapshotWorkingAsync(ReleaseTargetType.AccVipi, key, "2607");
        await releases.SaveReleaseAsync(ReleaseTargetType.AccVipi, key, "2607", DateTime.UtcNow.AddMinutes(-1), snap!, createdByUserId: 1, note: null);
        Assert.NotNull(await _service.LoadForViewAsync(Acc));

        var doc = await _db.Documents.FirstAsync(d => d.Id == docId);
        doc.IsHidden = true;
        await _db.SaveChangesAsync();

        Assert.Null(await _service.LoadForViewAsync(Acc));
    }

    [Fact]
    public async Task Release_Snapshot_Is_Frozen_Then_Served_By_View()
    {
        var releases = TestReleaseTargets.ReleaseRepo(_db);
        var editing = new EfEditingRepository(_db, new AiracService(), new EfMediaMaintenance(_db));

        // Migra + edita (config "Conf A") + pubblica.
        var model = await _service.LoadForEditAsync(Acc);
        var block = Assert.Single(model.Blocks);
        await _service.SaveConfigurationsAsync(Acc, block.ChildSectionIdsByKey["configurations"], new[]
        {
            new AccConfiguration { Key = "cfg:a", Name = "Conf A", Open = new() { new AccConfigOpen { Callsign = "LIRR_CTR" } } },
        });
        var draftVer = await _db.DocumentVersions.Where(v => v.DocumentId == model.DocumentId).Select(v => v.Id).FirstAsync();
        await editing.PublishAsync(draftVer, actorUserId: 1, note: null);

        var key = $"{Acc}|{(await _service.GetIdentityAsync(Acc))!.RootCallsign}";

        // Release AIRAC in vigore ORA: snapshot dello stato pubblicato.
        var snap = await releases.SnapshotWorkingAsync(ReleaseTargetType.AccVipi, key, "2607");
        Assert.NotNull(snap);
        await releases.SaveReleaseAsync(ReleaseTargetType.AccVipi, key, "2607", DateTime.UtcNow.AddMinutes(-1), snap!, createdByUserId: 1, note: null);

        // Ora modifico e RIpubblico "Conf B" (stato live cambia dopo la release). Serve una nuova bozza (come StartEditing).
        await editing.CreateDraftAsync(model.DocumentId, authorUserId: 1);
        var m2 = await _service.LoadForEditAsync(Acc);
        var b2 = Assert.Single(m2.Blocks);
        await _service.SaveConfigurationsAsync(Acc, b2.ChildSectionIdsByKey["configurations"], new[]
        {
            new AccConfiguration { Key = "cfg:b", Name = "Conf B", Open = new() { new AccConfigOpen { Callsign = "LIRR_CTR" } } },
        });
        var draft2 = await _db.DocumentVersions.Where(v => v.DocumentId == m2.DocumentId && v.Status == DocumentStatus.Draft).Select(v => v.Id).FirstAsync();
        await editing.PublishAsync(draft2, actorUserId: 1, note: null);

        // La vista pubblica serve lo snapshot CONGELATO (Conf A), non il live (Conf B).
        var view = await _service.LoadForViewAsync(Acc);
        var configs = Assert.Single(view!.Blocks).Block.Configurations;
        Assert.Equal("Conf A", Assert.Single(configs).Name);

        // …e col ciclo AIRAC di QUELLA release (doc 13 §3h): la pagina scriveva il ciclo di oggi accanto a un
        // contenuto congelato a un ciclo diverso.
        Assert.Equal("2607", view.AiracCycle);

        // LoadForRelease per Id ritorna lo stesso snapshot col ciclo.
        var relId = await _db.DocReleases.Where(r => r.TargetKey == key).Select(r => r.Id).FirstAsync();
        var rv = await _service.LoadForReleaseAsync(Acc, relId);
        Assert.Equal("2607", rv!.AiracCycle);
        Assert.Equal("Conf A", Assert.Single(Assert.Single(rv.Data.Blocks).Configurations).Name);
    }

    /// <summary>Authz permissiva per i ctor dei service in test.</summary>
    private sealed class AllowAuthz : IEditAuthorizationService
    {
        public bool IsAdmin => true;
        public int? CurrentUserId => 1;
        public string? CurrentName => "test";
        public Task EnsureCanEditAccAsync(string accCode, CancellationToken ct = default) => Task.CompletedTask;
        public Task EnsureCanEditDocumentAsync(int documentId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> CanEditAccAsync(string accCode, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> CanEditDocumentAsync(int documentId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> CanEditAnythingAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<GrantRow>> ListGrantsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GrantRow>>(Array.Empty<GrantRow>());
        public Task<int> AddGrantAsync(int UserId, string? displayName, string accCode, CancellationToken ct = default) => Task.FromResult(0);
        public Task RevokeGrantAsync(int grantId, CancellationToken ct = default) => Task.CompletedTask;
        public void EnsureAdmin() { }
    }
}
