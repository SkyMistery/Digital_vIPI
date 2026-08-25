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

    /// <summary>
    /// Repository vero, con un solo passo che si rifiuta di eseguire. Serve a provare che la pubblicazione è
    /// atomica: senza transazione, la release del passo 1 resterebbe scritta e la promozione della bozza no.
    /// </summary>
    private sealed class RepoCheRompeAllaPromozione : IReleaseRepository
    {
        private readonly IReleaseRepository _vero;
        public RepoCheRompeAllaPromozione(IReleaseRepository vero) => _vero = vero;

        public Task PublishWorkingVersionAsync(ReleaseTargetType type, string key, int actorUserId, string airacCycle, CancellationToken ct = default) =>
            throw new InvalidOperationException("guasto simulato fra la release e la promozione");

        public Task<string?> SnapshotWorkingAsync(ReleaseTargetType type, string key, string airacCycle, CancellationToken ct = default) =>
            _vero.SnapshotWorkingAsync(type, key, airacCycle, ct);
        public Task<int> SaveReleaseAsync(ReleaseTargetType type, string key, string releaseCycle, DateTime effectiveUtc, string payloadJson, int createdByUserId, string? note, CancellationToken ct = default) =>
            _vero.SaveReleaseAsync(type, key, releaseCycle, effectiveUtc, payloadJson, createdByUserId, note, ct);
        public Task<IReadOnlyList<ReleaseInfo>> ListAsync(ReleaseTargetType type, string key, CancellationToken ct = default) =>
            _vero.ListAsync(type, key, ct);
        public Task<DocRelease?> GetEffectiveAsync(ReleaseTargetType type, string key, DateTime atUtc, CancellationToken ct = default) =>
            _vero.GetEffectiveAsync(type, key, atUtc, ct);
        public Task<DocRelease?> GetByIdAsync(int releaseId, CancellationToken ct = default) => _vero.GetByIdAsync(releaseId, ct);
        public Task<(ReleaseTargetType Type, string Key)?> CancelAsync(int releaseId, CancellationToken ct = default) => _vero.CancelAsync(releaseId, ct);
        public Task<string?> GetAuthAccCodeAsync(ReleaseTargetType type, string key, CancellationToken ct = default) => _vero.GetAuthAccCodeAsync(type, key, ct);
        public Task<int> PruneReleasesAsync(ReleaseTargetType type, string key, DateTime keepFromUtc, CancellationToken ct = default) => _vero.PruneReleasesAsync(type, key, keepFromUtc, ct);
        public Task<IReadOnlyDictionary<(ReleaseTargetType Type, string Key), ReleaseSummary>> SummariesAsync(IReadOnlyList<(ReleaseTargetType Type, string Key)> targets, CancellationToken ct = default) =>
            _vero.SummariesAsync(targets, ct);
    }

    /// <summary>
    /// Pubblicare è tre scritture: la release, la promozione della bozza, la potatura delle versioni
    /// archiviate. Fino all'11 agosto 2026 erano tre <c>SaveChanges</c> separati, quindi un guasto in mezzo
    /// lasciava una release pubblicata di un documento la cui bozza non era stata promossa — la pagina
    /// pubblica col nuovo, l'editor col vecchio, e nessun errore da nessuna parte.
    ///
    /// <para>È l'operazione più importante che l'applicazione compie, ed era l'unica senza rete.</para>
    /// </summary>
    [Fact]
    public async Task PublishNow_Non_Lascia_Release_Se_La_Promozione_Fallisce()
    {
        var vero = new EfReleaseRepository(_db, Registry(), new EfMediaMaintenance(_db));
        var svc = new ReleaseService(new RepoCheRompeAllaPromozione(vero), new AllowAuthz(),
            new Vipi.Domain.Services.AiracService(),
            new FrozenSectionRegistry(Array.Empty<IFrozenSectionProvider>()),
            new EfDocumentAdminRepository(_db, Registry(), new EfReleaseRepository(_db, Registry(), new EfMediaMaintenance(_db)), new EfMediaMaintenance(_db)),
            new EfEditingRepository(_db, new Vipi.Domain.Services.AiracService(), new EfMediaMaintenance(_db)), Registry(),
            Microsoft.Extensions.Options.Options.Create(new Vipi.Application.ReleaseRetentionOptions()),
            new EfUnitOfWork(_db));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.PublishNowAsync(FakeType, "fake-key", "review"));

        // Il punto del test: la release del PRIMO passo non deve essere sopravvissuta al guasto del secondo.
        Assert.Empty(await vero.ListAsync(FakeType, "fake-key"));
        Assert.Null(await vero.GetEffectiveAsync(FakeType, "fake-key", DateTime.UtcNow));
    }

    [Fact]
    public async Task Engine_Snapshots_And_Authorizes_UnknownType_ViaDescriptorOnly()
    {
        var repo = new EfReleaseRepository(_db, Registry(), new EfMediaMaintenance(_db));

        var json = await repo.SnapshotWorkingAsync(FakeType, "qualsiasi-chiave", "2606");
        Assert.NotNull(json);
        Assert.Contains("Sezione Fittizia", json);

        Assert.Equal("FAKE", await repo.GetAuthAccCodeAsync(FakeType, "qualsiasi-chiave"));
    }

    [Fact]
    public async Task AdminList_Describes_UnknownType_ViaDescriptorOnly()
    {
        var admin = new EfDocumentAdminRepository(_db, Registry(), new EfReleaseRepository(_db, Registry(), new EfMediaMaintenance(_db)), new EfMediaMaintenance(_db));

        var all = await admin.ListAsync();
        var m = Assert.Single(all);
        Assert.Equal(FakeKind, m.Kind);
        Assert.Equal("fake-key", m.ReleaseKey);
        Assert.Equal("FAKE", await admin.GetAccCodeAsync(new ManagedDocRef(FakeKind, m.ReleaseKey, m.DocumentId)));
    }

    [Fact]
    public async Task ReleaseService_PublishPreviewDiff_UnknownType_ViaDescriptorOnly()
    {
        var repo = new EfReleaseRepository(_db, Registry(), new EfMediaMaintenance(_db));
        var svc = new ReleaseService(repo, new AllowAuthz(), new Vipi.Domain.Services.AiracService(),
            new FrozenSectionRegistry(Array.Empty<IFrozenSectionProvider>()), new EfDocumentAdminRepository(_db, Registry(), new EfReleaseRepository(_db, Registry(), new EfMediaMaintenance(_db)), new EfMediaMaintenance(_db)),
            new EfEditingRepository(_db, new Vipi.Domain.Services.AiracService(), new EfMediaMaintenance(_db)), Registry(),
            Microsoft.Extensions.Options.Options.Create(new Vipi.Application.ReleaseRetentionOptions()), new EfUnitOfWork(_db));

        await svc.PublishNowAsync(FakeType, "qualsiasi-chiave", "review");

        var list = await svc.ListAsync(FakeType, "qualsiasi-chiave");
        var rel = Assert.Single(list);
        Assert.True(rel.IsEffectiveNow);

        var preview = await svc.GetPreviewAsync(rel.Id);
        Assert.NotNull(preview);
        Assert.Contains(preview!.Doc!.Roots, s => s.Title == "Sezione Fittizia");

        var diff = await svc.DiffAsync(rel.Id);   // prima release: nessuna precedente → tutte "Aggiunta"
        Assert.False(diff.HasBaseline);
        Assert.Contains(diff.Rows, r => r.Label == "Sezione Fittizia");

        // Seconda pubblicazione identica: la baseline è la release PRECEDENTE (non «l'effettiva ora», che
        // per la release in vigore era se stessa → null → il diff fingeva una prima pubblicazione).
        await svc.PublishNowAsync(FakeType, "qualsiasi-chiave", "bis");
        var rel2 = (await svc.ListAsync(FakeType, "qualsiasi-chiave")).First(r => r.IsEffectiveNow);
        var diff2 = await svc.DiffAsync(rel2.Id);
        Assert.True(diff2.HasBaseline);
        Assert.Empty(diff2.Rows);   // contenuto identico → nessuna differenza, non «tutto aggiunto»
    }

    [Fact]
    public async Task Backfill_Creates_Effective_Release_For_Published_Without_One_And_Is_Idempotent()
    {
        var repo = new EfReleaseRepository(_db, Registry(), new EfMediaMaintenance(_db));
        var svc = new ReleaseService(repo, new AllowAuthz(), new Vipi.Domain.Services.AiracService(),
            new FrozenSectionRegistry(Array.Empty<IFrozenSectionProvider>()), new EfDocumentAdminRepository(_db, Registry(), new EfReleaseRepository(_db, Registry(), new EfMediaMaintenance(_db)), new EfMediaMaintenance(_db)),
            new EfEditingRepository(_db, new Vipi.Domain.Services.AiracService(), new EfMediaMaintenance(_db)), Registry(),
            Microsoft.Extensions.Options.Options.Create(new Vipi.Application.ReleaseRetentionOptions()), new EfUnitOfWork(_db));

        // Il doc fittizio è Published SENZA release → il backfill ne genera una effettiva ora.
        Assert.Null(await repo.GetEffectiveAsync(FakeType, "fake-key", DateTime.UtcNow));
        Assert.Equal(1, await svc.BackfillMissingReleasesAsync());

        var eff = await repo.GetEffectiveAsync(FakeType, "fake-key", DateTime.UtcNow);
        Assert.NotNull(eff);
        Assert.Contains("Sezione Fittizia", eff!.PayloadJson);

        // Idempotente: una seconda passata non crea nulla (già coperto).
        Assert.Equal(0, await svc.BackfillMissingReleasesAsync());
    }

    [Fact]
    public async Task PublishNow_EnforcesArchivedCap_Exactly_AfterVersionArchived_NotOffByOne()
    {
        // Retention versioni con cap=1: dopo ogni PublishNow (che archivia la versione precedente) le Archived
        // devono restare esattamente 1, NON 2. Regressione off-by-one: se il prune Archived gira prima della
        // promozione della bozza, conta una versione in meno e ne lascia N+1.
        var svc = new ReleaseService(new EfReleaseRepository(_db, Registry(), new EfMediaMaintenance(_db)), new AllowAuthz(), new Vipi.Domain.Services.AiracService(),
            new FrozenSectionRegistry(Array.Empty<IFrozenSectionProvider>()), new EfDocumentAdminRepository(_db, Registry(), new EfReleaseRepository(_db, Registry(), new EfMediaMaintenance(_db)), new EfMediaMaintenance(_db)),
            new EfEditingRepository(_db, new Vipi.Domain.Services.AiracService(), new EfMediaMaintenance(_db)), Registry(),
            Microsoft.Extensions.Options.Options.Create(new Vipi.Application.ReleaseRetentionOptions { KeepArchivedVersionsPerDocument = 1 }), new EfUnitOfWork(_db));

        // Publish #1: promuove v2, archivia v1 → 1 Archived (al cap).
        await AddDraftAsync(2);
        await svc.PublishNowAsync(FakeType, "qualsiasi-chiave", null);
        Assert.Equal(1, await ArchivedCountAsync());

        // Publish #2: promuove v3, archivia v2. Il prune Archived deve girare DOPO l'archiviazione → resta 1, non 2.
        await AddDraftAsync(3);
        await svc.PublishNowAsync(FakeType, "qualsiasi-chiave", null);
        Assert.Equal(1, await ArchivedCountAsync());
    }

    /// <summary>
    /// Il lock di editing vale anche per il pannello release: lo snapshot fotografa la BOZZA e «Pubblica ora»
    /// la promuove pure. Fino a questo giro il publish-versione dell'editor pretendeva il lock
    /// (EditingService.EnsureLockAsync) ma le release lo ignoravano: un secondo editor poteva congelare e
    /// promuovere il lavoro a metà di chi stava scrivendo, rompendogli la sessione senza errore.
    /// </summary>
    [Fact]
    public async Task Publish_Rifiutato_Se_Il_Documento_E_In_Modifica_Da_Un_Altro()
    {
        var repo = new EfReleaseRepository(_db, Registry(), new EfMediaMaintenance(_db));
        var svc = new ReleaseService(repo, new AllowAuthz(), new Vipi.Domain.Services.AiracService(),
            new FrozenSectionRegistry(Array.Empty<IFrozenSectionProvider>()), new EfDocumentAdminRepository(_db, Registry(), new EfReleaseRepository(_db, Registry(), new EfMediaMaintenance(_db)), new EfMediaMaintenance(_db)),
            new EfEditingRepository(_db, new Vipi.Domain.Services.AiracService(), new EfMediaMaintenance(_db)), Registry(),
            Microsoft.Extensions.Options.Options.Create(new Vipi.Application.ReleaseRetentionOptions()), new EfUnitOfWork(_db));

        // Un ALTRO editor (VID 999 ≠ 1 di AllowAuthz) detiene il lock, non scaduto.
        var doc = await _db.Documents.FirstAsync(d => d.Id == _docId);
        doc.LockedByUserId = 999; doc.LockedByName = "Altro Editor";
        doc.LockedAtUtc = DateTime.UtcNow; doc.LockExpiresUtc = DateTime.UtcNow.AddMinutes(3);
        await _db.SaveChangesAsync();

        // Né immediata né schedulata: entrambe fotografano la sua bozza.
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => svc.PublishNowAsync(FakeType, "fake-key", null));
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => svc.PublishAsync(FakeType, "fake-key", "2613", null));
        Assert.Empty(await repo.ListAsync(FakeType, "fake-key"));

        // Il lock MIO non blocca; a pubblicazione avvenuta il documento resta libero (come dal publish dell'editor).
        doc.LockedByUserId = 1; doc.LockedByName = "test"; doc.LockExpiresUtc = DateTime.UtcNow.AddMinutes(3);
        await _db.SaveChangesAsync();
        await svc.PublishNowAsync(FakeType, "fake-key", null);
        Assert.Single(await repo.ListAsync(FakeType, "fake-key"));
        var after = await _db.Documents.AsNoTracking().FirstAsync(d => d.Id == _docId);
        Assert.Null(after.LockedByUserId);
    }

    private async Task AddDraftAsync(int versionNumber)
    {
        var draft = new DocumentVersion { DocumentId = _docId, VersionNumber = versionNumber, Status = DocumentStatus.Draft, AiracCycle = "2606", CreatedUtc = DateTime.UtcNow };
        _db.DocumentVersions.Add(draft);
        await _db.SaveChangesAsync();
        _db.DocumentSections.Add(new DocumentSection { DocumentVersionId = draft.Id, Title = "Sezione Fittizia", Order = 1, Depth = 0, SectionKey = "custom", RowVersion = Guid.NewGuid().ToByteArray() });
        await _db.SaveChangesAsync();
    }

    private Task<int> ArchivedCountAsync() =>
        _db.DocumentVersions.CountAsync(v => v.DocumentId == _docId && v.Status == DocumentStatus.Archived);

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
