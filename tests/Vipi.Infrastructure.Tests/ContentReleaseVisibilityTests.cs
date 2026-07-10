using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Visibilità pubblica vs release AIRAC (fix): una release effettiva deve essere servita al pubblico anche quando il
/// Document è ancora Draft (release e pubblicazione-versione sono due layer). Senza release, un Document mai pubblicato
/// resta invisibile (nessun leak). Regressione osservata: vIPI APP pubblicata come release ma non come versione →
/// il viewer pubblico non la mostrava.
/// </summary>
public class ContentReleaseVisibilityTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfContentRepository _content = default!;
    private EfReleaseRepository _releases = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _releases = TestReleaseTargets.ReleaseRepo(_db);
        _content = new EfContentRepository(_db, _releases);

        var acc = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        _db.Accs.Add(acc);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    /// <summary>Crea un APP standalone con Document(Vipi) DRAFT (mai pubblicato come versione) + 1 sezione con testo.</summary>
    private async Task<string> SeedDraftAppAsync(string callsign, string sectionTitle, string body)
    {
        var acc = await _db.Accs.FirstAsync();
        var doc = new Document { Type = DocumentType.Vipi, Title = $"vIPI {callsign}", Language = Language.It, Status = DocumentStatus.Draft, LastUpdatedAiracCycle = "2606" };
        var ver = new DocumentVersion { Document = doc, VersionNumber = 1, Status = DocumentStatus.Draft, AiracCycle = "2606", CreatedUtc = DateTime.UtcNow };
        doc.Versions.Add(ver);
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

        var sec = new DocumentSection { DocumentVersion = ver, Title = sectionTitle, Order = 1, Depth = 0, SectionKey = "operationaltechnique", RowVersion = Guid.NewGuid().ToByteArray() };
        _db.DocumentSections.Add(sec);
        await _db.SaveChangesAsync();
        _db.ContentBlocks.Add(new ContentBlock { DocumentVersion = ver, Section = sec, Order = 1, Format = BlockFormat.Prose, Tier = BlockTier.Reduced, Visibility = BlockVisibility.Always, Body = body, RowVersion = Guid.NewGuid().ToByteArray() });
        _db.Sectors.Add(new Sector { Acc = acc, Callsign = callsign, Name = callsign, Type = SectorType.App, Kind = SectorKind.Airport, ApproachKind = ApproachKind.Standalone, IsActive = true, DocumentId = doc.Id, IsPrimary = true });
        // Document resta Draft: CurrentVersionId NON impostato.
        await _db.SaveChangesAsync();
        return callsign;
    }

    [Fact]
    public async Task DraftApp_WithEffectiveRelease_IsServedToPublic()
    {
        var app = await SeedDraftAppAsync("LICC_APP", "Tecnica operativa", "Testo pubblicato via release");

        // Pubblica come RELEASE (non come versione): snapshot dello stato working, effettiva adesso.
        var json = (await _releases.SnapshotWorkingAsync(ReleaseTargetType.App, app, "2607"))!;
        await _releases.SaveReleaseAsync(ReleaseTargetType.App, app, "2607", DateTime.UtcNow.AddSeconds(-5), json, 1, null);

        var pub = await _content.LoadAppVipiAsync(app);   // vista pubblica (default)
        Assert.NotNull(pub);
        Assert.Contains(pub!.Roots, s => s.Title == "Tecnica operativa");
    }

    [Fact]
    public async Task DraftApp_WithoutRelease_IsNotVisibleToPublic()
    {
        var app = await SeedDraftAppAsync("LIPZ_APP", "Tecnica operativa", "Bozza mai pubblicata");
        Assert.Null(await _content.LoadAppVipiAsync(app));   // nessuna release + mai pubblicato → invisibile
    }

    [Fact]
    public async Task HiddenApp_WithEffectiveRelease_StaysHidden()
    {
        var app = await SeedDraftAppAsync("LIME_APP", "Tecnica operativa", "Testo");
        var doc = await _db.Documents.FirstAsync(d => d.Title == "vIPI LIME_APP");
        doc.IsHidden = true;
        await _db.SaveChangesAsync();

        var json = (await _releases.SnapshotWorkingAsync(ReleaseTargetType.App, app, "2607"))!;
        await _releases.SaveReleaseAsync(ReleaseTargetType.App, app, "2607", DateTime.UtcNow.AddSeconds(-5), json, 1, null);

        Assert.Null(await _content.LoadAppVipiAsync(app));   // nascosto dall'admin → invisibile anche con release
    }
}
