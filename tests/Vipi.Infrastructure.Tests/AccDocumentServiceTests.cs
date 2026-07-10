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

        var repo = new EfAccProfileRepository(_db);
        var editing = new EfEditingRepository(_db, new AiracService());
        _service = new AccDocumentService(repo, editing, new AllowAuthz());
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
        Assert.Equal(expected, block.Block.SectionOrder.ToArray());

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
        Assert.Equal(expected, grp.Block.SectionOrder.ToArray());

        await _service.RemoveGroupAsync(Acc, groupId);
        Assert.Single((await _service.LoadForEditAsync(Acc)).Blocks);
    }

    [Fact]
    public async Task LoadForView_Synthetic_Before_Publish_Then_Published_Tree()
    {
        // Non ancora migrato: blocco Aerovia sintetico (docId 0), sezioni derivate rese live dai cataloghi.
        var pre = await _service.LoadForViewAsync(Acc);
        Assert.NotNull(pre);
        Assert.Equal(0, pre!.DocumentId);
        var synth = Assert.Single(pre.Blocks);
        Assert.Equal(AccBlockKind.Aerovia, synth.Block.Kind);
        Assert.NotEmpty(synth.Block.SectionOrder);

        // Crea + pubblica la bozza.
        var docId = await _service.EnsureAsync(Acc);
        var editing = new EfEditingRepository(_db, new AiracService());
        var draftVer = await _db.DocumentVersions.Where(v => v.DocumentId == docId).Select(v => v.Id).FirstAsync();
        await editing.PublishAsync(draftVer, actorUserId: 1, note: "pub");

        // Ora la vista pubblica legge l'albero pubblicato.
        var post = await _service.LoadForViewAsync(Acc);
        Assert.NotNull(post);
        Assert.Equal(docId, post!.DocumentId);
        Assert.False(post.IsDraft);
        Assert.Equal(AccBlockKind.Aerovia, Assert.Single(post.Blocks).Block.Kind);
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
        public Task<IReadOnlyList<GrantRow>> ListGrantsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GrantRow>>(Array.Empty<GrantRow>());
        public Task<int> AddGrantAsync(int UserId, string? displayName, string accCode, CancellationToken ct = default) => Task.FromResult(0);
        public Task RevokeGrantAsync(int grantId, CancellationToken ct = default) => Task.CompletedTask;
        public void EnsureAdmin() { }
    }
}
