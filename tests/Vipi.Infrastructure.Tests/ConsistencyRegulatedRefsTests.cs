using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Caricamento delle sezioni «regulated» per il report di consistenza: si guarda la sola versione di lavoro
/// (bozza più recente, altrimenti la pubblicata corrente), perché le versioni storiche sono congelate e
/// segnalarne le aree sparite sarebbe rumore su qualcosa che nessuno può correggere.
/// </summary>
public class ConsistencyRegulatedRefsTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfConsistencyReportRepository _repo = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _repo = new EfConsistencyReportRepository(_db);
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    [Fact]
    public async Task Only_the_working_version_selection_is_loaded()
    {
        var doc = new Document
        {
            Type = DocumentType.Vipi, Title = "vIPI Roma", Language = Language.It,
            Status = DocumentStatus.Draft, LastUpdatedAiracCycle = "2606",
        };
        var published = NewVersion(doc, 1, DocumentStatus.Published);
        var draft = NewVersion(doc, 2, DocumentStatus.Draft);
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        doc.CurrentVersionId = published.Id;

        AddRegulated(published, """{"OwnAuto":false,"OwnIds":["vecchia"],"ExtraIds":[]}""");
        AddRegulated(draft, """{"OwnAuto":false,"OwnIds":["nuova"],"ExtraIds":[]}""");
        await _db.SaveChangesAsync();

        var d = await _repo.LoadAsync();

        var row = Assert.Single(d.RegulatedRefs);
        Assert.Equal("vIPI", row.Kind);
        Assert.Equal("vIPI Roma", row.Reference);
        Assert.Contains("nuova", row.Json);            // la bozza vince sulla pubblicata
        Assert.DoesNotContain("vecchia", row.Json);
    }

    [Fact]
    public async Task Special_area_ids_come_from_the_catalog()
    {
        _db.Accs.Add(new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" });
        await _db.SaveChangesAsync();
        _db.SpecialAreas.Add(new SpecialArea { IvaoId = "8963", CenterId = "LIRR", Name = "LI R14A" });
        await _db.SaveChangesAsync();

        var d = await _repo.LoadAsync();

        Assert.Contains("8963", d.SpecialAreaIds);
        Assert.Empty(d.RegulatedRefs);                 // nessun documento: niente da controllare
    }

    private DocumentVersion NewVersion(Document doc, int number, DocumentStatus status)
    {
        var v = new DocumentVersion
        {
            Document = doc, VersionNumber = number, Status = status,
            AiracCycle = "2606", CreatedUtc = DateTime.UtcNow,
        };
        doc.Versions.Add(v);
        return v;
    }

    private void AddRegulated(DocumentVersion version, string json)
    {
        var section = new DocumentSection
        {
            DocumentVersionId = version.Id, Title = "Aree regolamentate", Order = 1, Depth = 0, SectionKey = "regulated",
        };
        _db.DocumentSections.Add(section);
        _db.SaveChanges();
        _db.ContentBlocks.Add(new ContentBlock
        {
            DocumentVersionId = version.Id, SectionId = section.Id, Order = 1,
            Format = BlockFormat.Table, Tier = BlockTier.Extended, Visibility = BlockVisibility.Always, BodyJson = json,
        });
    }
}
