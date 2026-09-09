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
        _db.SpecialAreas.Add(new SpecialArea
        {
            IvaoId = "8963", Name = "LI R14A",
            Centers = new List<SpecialAreaCenter> { new() { IvaoId = "8963", CenterId = "LIRR" } },
        });
        await _db.SaveChangesAsync();

        var d = await _repo.LoadAsync();

        Assert.Contains("8963", d.SpecialAreaIds);
        Assert.Empty(d.RegulatedRefs);                 // nessun documento: niente da controllare
    }

    /// <summary>
    /// 🔴 Le aree scelte sotto «Bassa quota (BOAT)» contano quanto le altre (carta 2026-09-09-aree-boat.md
    /// §4): senza, un'area potata dai cataloghi sparirebbe da un vSOP militare e il rapporto tacerebbe —
    /// cioè proprio il silenzio che questo rapporto esiste per rompere.
    ///
    /// <para>⚠️ E il payload è «il primo blocco di STRUTTURA», non «il primo blocco»: qui davanti c'è una
    /// tabella scritta a mano, che è il caso normale di una sezione che fino a ieri era solo prosa.</para>
    /// </summary>
    [Fact]
    public async Task Anche_le_aree_della_bassa_quota_finiscono_nel_rapporto()
    {
        var doc = new Document
        {
            Type = DocumentType.Vipi, Title = "vSOP MIL — LIBA", Language = Language.It,
            Status = DocumentStatus.Draft, LastUpdatedAiracCycle = "2606", Edition = DocumentEdition.Military,
        };
        var draft = NewVersion(doc, 1, DocumentStatus.Draft);
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

        AddSezione(draft, "regulated", """{"OwnAuto":false,"OwnIds":["areadilavoro"],"ExtraIds":[]}""");
        var boat = AddSezione(draft, "lowlevel", """{"OwnAuto":false,"OwnIds":["areaboat"],"ExtraIds":[]}""");
        // Davanti al payload, una tabella scritta a mano: il vecchio «primo blocco» avrebbe letto questa.
        _db.ContentBlocks.Add(new ContentBlock
        {
            DocumentVersionId = draft.Id, SectionId = boat.Id, Order = 0,
            Format = BlockFormat.Table, Tier = BlockTier.Extended, Visibility = BlockVisibility.Always,
            BodyJson = """{"columns":["a"],"cells":["scritta a mano"]}""",
        });
        await _db.SaveChangesAsync();

        var d = await _repo.LoadAsync();

        Assert.Equal(2, d.RegulatedRefs.Count);
        Assert.Contains(d.RegulatedRefs, r => (r.Json ?? "").Contains("areadilavoro"));
        Assert.Contains(d.RegulatedRefs, r => (r.Json ?? "").Contains("areaboat"));
        Assert.DoesNotContain(d.RegulatedRefs, r => (r.Json ?? "").Contains("scritta a mano"));
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

    private void AddRegulated(DocumentVersion version, string json) => AddSezione(version, "regulated", json);

    /// <summary>Una sezione con dentro il payload della selezione, per chiave: le sezioni che ne portano una
    /// sono due — «Aree di lavoro» e «Bassa quota (BOAT)».</summary>
    private DocumentSection AddSezione(DocumentVersion version, string chiave, string json)
    {
        var section = new DocumentSection
        {
            DocumentVersionId = version.Id, Title = chiave, Order = 1, Depth = 0, SectionKey = chiave,
        };
        _db.DocumentSections.Add(section);
        _db.SaveChanges();
        _db.ContentBlocks.Add(new ContentBlock
        {
            DocumentVersionId = version.Id, SectionId = section.Id, Order = 1,
            Format = BlockFormat.Table, Tier = BlockTier.Extended, Visibility = BlockVisibility.Always, BodyJson = json,
        });
        return section;
    }
}
