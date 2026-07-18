using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Verifica dell'obiettivo utente del doc 09 (§5): un tipo di documento MAI previsto dai motori generici deve poter
/// essere pubblicato / elencato / risolto registrando SOLO un descrittore <see cref="IReleaseTarget"/>, senza toccare
/// EfReleaseRepository, EfDocumentAdminRepository né gli switch (che non esistono più). Il tipo fittizio usa valori di
/// enum fuori intervallo — il codice generico non li conosce e li tratta comunque, perché itera/consulta il registry.
/// </summary>
public class ReleaseGenericFlowTests : IAsyncLifetime
{
    private const ReleaseTargetType FakeType = (ReleaseTargetType)99;
    private const ManagedDocKind FakeKind = (ManagedDocKind)99;

    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private int _docId;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        var doc = new Document { Type = DocumentType.Vipi, Title = "Documento Fittizio", Language = Language.It, Status = DocumentStatus.Published, LastUpdatedAiracCycle = "2606" };
        var ver = new DocumentVersion { Document = doc, VersionNumber = 1, Status = DocumentStatus.Published, AiracCycle = "2606", CreatedUtc = DateTime.UtcNow };
        doc.Versions.Add(ver);
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        _db.DocumentSections.Add(new DocumentSection { DocumentVersion = ver, Title = "Sezione Fittizia", Order = 1, Depth = 0, SectionKey = "custom", RowVersion = Guid.NewGuid().ToByteArray() });
        doc.CurrentVersionId = ver.Id;
        await _db.SaveChangesAsync();
        _docId = doc.Id;
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private IReleaseTargetRegistry Registry() =>
        new ReleaseTargetRegistry(new IReleaseTarget[] { new FakeReleaseTarget(_docId) });

    [Fact]
    public async Task Engine_Snapshots_And_Authorizes_UnknownType_ViaDescriptorOnly()
    {
        var repo = new EfReleaseRepository(_db, Registry());

        var json = await repo.SnapshotWorkingAsync(FakeType, "qualsiasi-chiave", "2606");
        Assert.NotNull(json);
        Assert.Contains("Sezione Fittizia", json);

        Assert.Equal("FAKE", await repo.GetAuthAccCodeAsync(FakeType, "qualsiasi-chiave"));
    }

    [Fact]
    public async Task AdminList_Describes_UnknownType_ViaDescriptorOnly()
    {
        var admin = new EfDocumentAdminRepository(_db, Registry(), new EfReleaseRepository(_db, Registry()));

        var all = await admin.ListAsync();
        var m = Assert.Single(all);
        Assert.Equal(FakeKind, m.Kind);
        Assert.Equal("fake-key", m.ReleaseKey);
        Assert.Equal("FAKE", await admin.GetAccCodeAsync(new ManagedDocRef(FakeKind, m.ReleaseKey, m.DocumentId)));
    }

    [Fact]
    public async Task ReleaseService_PublishPreviewDiff_UnknownType_ViaDescriptorOnly()
    {
        var repo = new EfReleaseRepository(_db, Registry());
        var svc = new ReleaseService(repo, new AllowAuthz(), new Vipi.Domain.Services.AiracService(),
            new FrozenSectionRegistry(Array.Empty<IFrozenSectionProvider>()), new EfDocumentAdminRepository(_db, Registry(), new EfReleaseRepository(_db, Registry())));

        await svc.PublishNowAsync(FakeType, "qualsiasi-chiave", "review");

        var list = await svc.ListAsync(FakeType, "qualsiasi-chiave");
        var rel = Assert.Single(list);
        Assert.True(rel.IsEffectiveNow);

        var preview = await svc.GetPreviewAsync(rel.Id);
        Assert.NotNull(preview);
        Assert.Contains(preview!.Doc!.Roots, s => s.Title == "Sezione Fittizia");

        var diff = await svc.DiffAsync(rel.Id);   // nessuna baseline in vigore → tutte "Aggiunta"
        Assert.Contains(diff.Rows, r => r.Label == "Sezione Fittizia");
    }

    [Fact]
    public async Task Backfill_Creates_Effective_Release_For_Published_Without_One_And_Is_Idempotent()
    {
        var repo = new EfReleaseRepository(_db, Registry());
        var svc = new ReleaseService(repo, new AllowAuthz(), new Vipi.Domain.Services.AiracService(),
            new FrozenSectionRegistry(Array.Empty<IFrozenSectionProvider>()), new EfDocumentAdminRepository(_db, Registry(), new EfReleaseRepository(_db, Registry())));

        // Il doc fittizio è Published SENZA release → il backfill ne genera una effettiva ora.
        Assert.Null(await repo.GetEffectiveAsync(FakeType, "fake-key", DateTime.UtcNow));
        Assert.Equal(1, await svc.BackfillMissingReleasesAsync());

        var eff = await repo.GetEffectiveAsync(FakeType, "fake-key", DateTime.UtcNow);
        Assert.NotNull(eff);
        Assert.Contains("Sezione Fittizia", eff!.PayloadJson);

        // Idempotente: una seconda passata non crea nulla (già coperto).
        Assert.Equal(0, await svc.BackfillMissingReleasesAsync());
    }

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

    /// <summary>Descrittore di un tipo che i motori generici non conoscono: risolve tutto verso il Document seminato.</summary>
    private sealed class FakeReleaseTarget : IReleaseTarget
    {
        private readonly int _docId;
        public FakeReleaseTarget(int docId) => _docId = docId;

        public ReleaseTargetType Type => FakeType;
        public ManagedDocKind ManagedKind => FakeKind;
        public int DescribeOrder => 0;

        public Task<int?> ResolveDocumentIdAsync(string key, CancellationToken ct = default) => Task.FromResult<int?>(_docId);
        public Task<string?> AuthAccCodeAsync(string key, CancellationToken ct = default) => Task.FromResult<string?>("FAKE");

        public bool TryDescribe(Document doc, bool hasDraft, out ManagedDoc managed)
        {
            managed = new ManagedDoc(FakeKind, doc.Title, "fake", "FAKE",
                doc.Status == DocumentStatus.Published, hasDraft, doc.IsHidden,
                FakeType, "fake-key", doc.Id);
            return true;
        }
    }
}
