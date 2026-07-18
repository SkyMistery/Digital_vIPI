using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Caratterizzazione del comportamento di freeze PRE doc 10 (asse "snapshot totale + RenderMode"). Rete
/// anti-regressione dei due comportamenti che il doc 10 §3f/§3c cambierà consapevolmente:
///  (a) POST doc 10 §3f/§S6b: un Document `Published` SENZA release effettiva NON è più servito al pubblico
///      (rimosso il fallback live): visibilità pubblica ⇔ release effettiva. La migrazione A (backfill) copre i Published.
///  (b) POST doc 10 §3c/§S5: lo snapshot NON scrive più l'overlay di visibilità separato — la visibilità entra nella
///      copia congelata (`Doc` + `FrozenSections`); il payload non ha più campo `Vloa`.
/// </summary>
public class SnapshotFreezeCharacterizationTests : IAsyncLifetime
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

        _db.Accs.Add(new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" });
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    /// <summary>APP standalone con Document(Vipi) + 1 sezione statica. <paramref name="published"/> = promuove la
    /// versione a Published e imposta CurrentVersionId (altrimenti resta bozza). Ritorna (callsign, docId).</summary>
    private async Task<(string Callsign, int DocId)> SeedAppAsync(string callsign, string sectionTitle, string body, bool published)
    {
        var acc = await _db.Accs.FirstAsync();
        var status = published ? DocumentStatus.Published : DocumentStatus.Draft;
        var doc = new Document { Type = DocumentType.Vipi, Title = $"vIPI {callsign}", Language = Language.It, Status = status, LastUpdatedAiracCycle = "2606" };
        var ver = new DocumentVersion { Document = doc, VersionNumber = 1, Status = status, AiracCycle = "2606", CreatedUtc = DateTime.UtcNow };
        doc.Versions.Add(ver);
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

        var sec = new DocumentSection { DocumentVersion = ver, Title = sectionTitle, Order = 1, Depth = 0, SectionKey = "operationaltechnique", RowVersion = Guid.NewGuid().ToByteArray() };
        _db.DocumentSections.Add(sec);
        await _db.SaveChangesAsync();
        _db.ContentBlocks.Add(new ContentBlock { DocumentVersion = ver, Section = sec, Order = 1, Format = BlockFormat.Prose, Tier = BlockTier.Reduced, Visibility = BlockVisibility.Always, Body = body, RowVersion = Guid.NewGuid().ToByteArray() });
        _db.Sectors.Add(new Sector { Acc = acc, Callsign = callsign, Name = callsign, Type = SectorType.App, Kind = SectorKind.Airport, ApproachKind = ApproachKind.Standalone, IsActive = true, DocumentId = doc.Id, IsPrimary = true });
        if (published) doc.CurrentVersionId = ver.Id;   // pubblicata come versione, MA nessuna release
        await _db.SaveChangesAsync();
        return (callsign, doc.Id);
    }

    // (a) POST doc 10 §3f/§S6b: un Document Published senza release effettiva NON è più servito al pubblico (rimosso il
    // fallback live alla versione pubblicata). Visibilità pubblica = release effettiva.
    [Fact]
    public async Task PublishedApp_WithoutRelease_IsNotVisibleToPublic_PostDoc10()
    {
        var (app, _) = await SeedAppAsync("LICC_APP", "Tecnica operativa", "Testo pubblicato come versione", published: true);

        Assert.Null(await _content.LoadAppVipiAsync(app));   // nessuna release effettiva → invisibile
    }

    // (b) POST doc 10 §3c/§S5: lo snapshot App NON scrive più l'overlay di visibilità separato, anche se il
    // DocumentProfile ha dei nascosti: la struttura si congela in `Doc`, la visibilità è dentro la copia congelata.
    [Fact]
    public async Task SnapshotWorking_App_DoesNotWriteVisibilityOverlay_PostDoc10()
    {
        var (app, docId) = await SeedAppAsync("LIME_APP", "Tecnica operativa", "Testo", published: false);
        _db.Set<DocumentProfile>().Add(new DocumentProfile
        {
            DocumentId = docId,
            HiddenFrequenciesJson = JsonSerializer.Serialize(new[] { "LIME_APP" }),
            HiddenAorSectorsJson = JsonSerializer.Serialize(new[] { "LIME_TWR" }),
        });
        await _db.SaveChangesAsync();

        var json = (await _releases.SnapshotWorkingAsync(ReleaseTargetType.App, app, "2607"))!;
        var payload = JsonSerializer.Deserialize<DocReleasePayload>(json)!;

        Assert.NotNull(payload.Doc);                                   // struttura statica congelata
        Assert.DoesNotContain("Vloa", json);                          // nessun overlay separato nel payload
        Assert.DoesNotContain("HiddenFrequencies", json);             // i nascosti non viaggiano più come overlay
    }
}
