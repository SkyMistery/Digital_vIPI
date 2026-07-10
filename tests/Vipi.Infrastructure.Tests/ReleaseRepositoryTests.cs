using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Release AIRAC (snapshot editoriale per ciclo): verifica snapshot dello stato working, selezione della release
/// effettiva per data, schedulazione futura e sostituzione (Superseded) di release dello stesso ciclo.
/// </summary>
public class ReleaseRepositoryTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfReleaseRepository _repo = default!;
    private int _docId;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _repo = new EfReleaseRepository(_db);

        // vLOA minimale: Acc+Sector Home, Document(Vloa) Published con 1 versione, 1 sezione, 1 blocco, party Home.
        var acc = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        var home = new Sector { Acc = acc, Callsign = "LIRR_CTR", Type = SectorType.Ctr, Kind = SectorKind.Acc, Name = "Roma", IsActive = true };
        _db.Accs.Add(acc); _db.Sectors.Add(home);

        var doc = new Document { Type = DocumentType.Vloa, Title = "vLOA LIRR ↔ DAAA", Language = Language.En, Status = DocumentStatus.Published, LastUpdatedAiracCycle = "2606" };
        var ver = new DocumentVersion { Document = doc, VersionNumber = 1, Status = DocumentStatus.Published, AiracCycle = "2606", CreatedUtc = DateTime.UtcNow };
        doc.Versions.Add(ver);
        doc.Parties.Add(new DocumentParty { Document = doc, Sector = home, Role = PartyRole.Home });
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

        var sec = new DocumentSection { DocumentVersion = ver, Title = "Purpose", Order = 1, Depth = 0, SectionKey = "custom", RowVersion = Guid.NewGuid().ToByteArray() };
        _db.DocumentSections.Add(sec);
        await _db.SaveChangesAsync();
        _db.ContentBlocks.Add(new ContentBlock { DocumentVersion = ver, Section = sec, Order = 1, Format = BlockFormat.Prose, Tier = BlockTier.Reduced, Visibility = BlockVisibility.Always, Body = "Testo di prova", RowVersion = Guid.NewGuid().ToByteArray() });
        doc.CurrentVersionId = ver.Id;
        await _db.SaveChangesAsync();
        _docId = doc.Id;
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    [Fact]
    public async Task Snapshot_Captures_Working_Structure()
    {
        var json = await _repo.SnapshotWorkingAsync(ReleaseTargetType.Vloa, _docId.ToString(), "2606");
        Assert.NotNull(json);
        Assert.Contains("Purpose", json);
        Assert.Contains("Testo di prova", json);
    }

    [Fact]
    public async Task ScheduledFuture_NotEffectiveNow_ButBecomesEffectiveAtCycle()
    {
        var key = _docId.ToString();
        var json = (await _repo.SnapshotWorkingAsync(ReleaseTargetType.Vloa, key, "2606"))!;
        var future = DateTime.UtcNow.AddDays(28);

        await _repo.SaveReleaseAsync(ReleaseTargetType.Vloa, key, "9901", future, json, 1, null);

        Assert.Null(await _repo.GetEffectiveAsync(ReleaseTargetType.Vloa, key, DateTime.UtcNow));       // futura: non ancora
        Assert.NotNull(await _repo.GetEffectiveAsync(ReleaseTargetType.Vloa, key, future.AddMinutes(1))); // al ciclo: sì
    }

    [Fact]
    public async Task Snapshot_AccVipi_Captures_Document_Tree()
    {
        // ACC ora su Document (doc 08e-acc): il settore CTR radice porta un Document vIPI con un blocco Aerovia.
        var home = await _db.Sectors.FirstAsync(s => s.Callsign == "LIRR_CTR");
        var doc = new Document { Type = DocumentType.Vipi, Title = "vIPI Roma", Language = Language.It, Status = DocumentStatus.Published, LastUpdatedAiracCycle = "2606" };
        var ver = new DocumentVersion { Document = doc, VersionNumber = 1, Status = DocumentStatus.Published, AiracCycle = "2606", CreatedUtc = DateTime.UtcNow };
        doc.Versions.Add(ver);
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        home.DocumentId = doc.Id; home.IsPrimary = true;
        _db.DocumentSections.Add(new DocumentSection { DocumentVersion = ver, Title = "Settori di aerovia", Order = 1, Depth = 0, SectionKey = "aerovia", RowVersion = Guid.NewGuid().ToByteArray() });
        doc.CurrentVersionId = ver.Id;
        await _db.SaveChangesAsync();

        var json = await _repo.SnapshotWorkingAsync(ReleaseTargetType.AccVipi, "LIRR|LIRR_CTR", "2606");
        Assert.NotNull(json);
        Assert.Contains("aerovia", json);
        Assert.Contains("Settori di aerovia", json);
    }

    [Fact]
    public async Task Cancel_RemovesRelease_AndPromotesPrevious()
    {
        var key = _docId.ToString();
        var json = (await _repo.SnapshotWorkingAsync(ReleaseTargetType.Vloa, key, "2606"))!;
        var now = DateTime.UtcNow;
        await _repo.SaveReleaseAsync(ReleaseTargetType.Vloa, key, "2606", now.AddDays(-2), json, 1, "vecchia");
        var newId = await _repo.SaveReleaseAsync(ReleaseTargetType.Vloa, key, "2607", now.AddSeconds(-5), json, 1, "nuova");

        // Ora è effettiva la "nuova" (2607). Annullandola, torna effettiva la "vecchia" (2606).
        var effBefore = await _repo.GetEffectiveAsync(ReleaseTargetType.Vloa, key, DateTime.UtcNow);
        Assert.Equal("nuova", effBefore!.Note);

        var target = await _repo.CancelAsync(newId);
        Assert.Equal((ReleaseTargetType.Vloa, key), target);
        Assert.Null(await _repo.GetByIdAsync(newId));

        var effAfter = await _repo.GetEffectiveAsync(ReleaseTargetType.Vloa, key, DateTime.UtcNow);
        Assert.Equal("vecchia", effAfter!.Note);
    }

    // ---- Caratterizzazione identità per-tipo (rete pre-refactor doc 09 §3a: key→docId via Snapshot, key→accCode) ----

    private async Task<int> SeedVipiDocAsync(string title, string sectionKey)
    {
        var doc = new Document { Type = DocumentType.Vipi, Title = title, Language = Language.It, Status = DocumentStatus.Published, LastUpdatedAiracCycle = "2606" };
        var ver = new DocumentVersion { Document = doc, VersionNumber = 1, Status = DocumentStatus.Published, AiracCycle = "2606", CreatedUtc = DateTime.UtcNow };
        doc.Versions.Add(ver);
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        _db.DocumentSections.Add(new DocumentSection { DocumentVersion = ver, Title = title, Order = 1, Depth = 0, SectionKey = sectionKey, RowVersion = Guid.NewGuid().ToByteArray() });
        doc.CurrentVersionId = ver.Id;
        await _db.SaveChangesAsync();
        return doc.Id;
    }

    [Fact]
    public async Task AuthAccCode_Vloa_FromHomeParty()
    {
        Assert.Equal("LIRR", await _repo.GetAuthAccCodeAsync(ReleaseTargetType.Vloa, _docId.ToString()));
    }

    [Fact]
    public async Task AuthAccCode_AccVipi_FromKeyPrefix()
    {
        Assert.Equal("LIRR", await _repo.GetAuthAccCodeAsync(ReleaseTargetType.AccVipi, "LIRR|LIRR_CTR"));
    }

    [Fact]
    public async Task AuthAccCode_And_Snapshot_App_Standalone()
    {
        var acc = await _db.Accs.FirstAsync();
        var docId = await SeedVipiDocAsync("vIPI LIRR_APP", "separations");
        _db.Sectors.Add(new Sector { Acc = acc, Callsign = "LIRR_APP", Name = "Roma APP", Type = SectorType.App, Kind = SectorKind.Airport, ApproachKind = ApproachKind.Standalone, IsActive = true, DocumentId = docId, IsPrimary = true });
        await _db.SaveChangesAsync();

        Assert.Equal("LIRR", await _repo.GetAuthAccCodeAsync(ReleaseTargetType.App, "LIRR_APP"));
        var json = await _repo.SnapshotWorkingAsync(ReleaseTargetType.App, "LIRR_APP", "2606");
        Assert.NotNull(json);
        Assert.Contains("separations", json);
    }

    [Fact]
    public async Task AuthAccCode_And_Snapshot_Airport()
    {
        var acc = await _db.Accs.FirstAsync();
        var docId = await SeedVipiDocAsync("vIPI LIRA", "airportextra");
        _db.Airports.Add(new Airport { Icao = "LIRA", Name = "Ciampino", Acc = acc });
        _db.Sectors.Add(new Sector { Acc = acc, Callsign = "LIRA_TWR", Name = "Ciampino TWR", Type = SectorType.Twr, Kind = SectorKind.Airport, AirportIcao = "LIRA", IsActive = true, DocumentId = docId });
        await _db.SaveChangesAsync();

        Assert.Equal("LIRR", await _repo.GetAuthAccCodeAsync(ReleaseTargetType.Airport, "LIRA"));
        var json = await _repo.SnapshotWorkingAsync(ReleaseTargetType.Airport, "LIRA", "2606");
        Assert.NotNull(json);
        Assert.Contains("airportextra", json);
    }

    [Fact]
    public async Task PublishNow_IsImmediatelyEffective_AndSupersedesSameCycle()
    {
        var key = _docId.ToString();
        var json = (await _repo.SnapshotWorkingAsync(ReleaseTargetType.Vloa, key, "2606"))!;
        var now = DateTime.UtcNow;

        await _repo.SaveReleaseAsync(ReleaseTargetType.Vloa, key, "2606", now.AddSeconds(-10), json, 1, "prima");
        await _repo.SaveReleaseAsync(ReleaseTargetType.Vloa, key, "2606", now, json, 1, "review");

        var eff = await _repo.GetEffectiveAsync(ReleaseTargetType.Vloa, key, DateTime.UtcNow);
        Assert.NotNull(eff);
        Assert.Equal("review", eff!.Note);   // l'ultima dello stesso ciclo vince

        var list = await _repo.ListAsync(ReleaseTargetType.Vloa, key);
        Assert.Single(list, r => r.IsEffectiveNow);                       // una sola in vigore
        Assert.Contains(list, r => r.Status == ReleaseStatus.Superseded); // la prima superata
    }
}
