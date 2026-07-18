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
///  (a) un Document `Published` SENZA release effettiva è servito al pubblico via fallback live (§3f lo rimuove:
///      visibilità pubblica ⇔ release effettiva);
///  (b) lo snapshot di App/vLOA scrive l'overlay `payload.Vloa` (`VloaOverlaySnapshot`), oggi mai riletto
///      (§3c lo rimuove: la visibilità entra nella copia congelata).
/// Questi test sono VERDI ora e documentano lo stato di partenza; vanno AGGIORNATI (non cancellati alla cieca)
/// quando S5 rimuove fallback/overlay — così il diff di comportamento è esplicito e tracciato.
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

    // (a) PRE doc 10 §3f: un Document Published senza alcuna release è visibile al pubblico (fallback live alla
    // versione pubblicata). Dopo S5 questo diventerà NON visibile (serve una release effettiva) → aggiornare qui.
    [Fact]
    public async Task PublishedApp_WithoutRelease_IsVisibleToPublic_PreDoc10()
    {
        var (app, _) = await SeedAppAsync("LICC_APP", "Tecnica operativa", "Testo pubblicato come versione", published: true);

        var pub = await _content.LoadAppVipiAsync(app);   // nessuna release: oggi fallback allo stato Published
        Assert.NotNull(pub);
        Assert.Contains(pub!.Roots, s => s.Title == "Tecnica operativa");
    }

    // (b) PRE doc 10 §3c: lo snapshot App scrive l'overlay di visibilità (VloaOverlaySnapshot) dal DocumentProfile,
    // oggi mai riletto al view. Dopo S5 l'overlay separato sparisce (assorbito nella copia congelata) → aggiornare qui.
    [Fact]
    public async Task SnapshotWorking_App_WritesDeadVisibilityOverlay_PreDoc10()
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
        Assert.NotNull(payload.Vloa);                                  // overlay scritto (morto: mai riletto al view)
        Assert.Contains("LIME_APP", payload.Vloa!.HiddenFrequencies);
        Assert.Contains("LIME_TWR", payload.Vloa!.HiddenAorSectors);
    }
}
