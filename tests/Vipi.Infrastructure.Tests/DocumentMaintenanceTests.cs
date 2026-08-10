using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>Riconciliazione delle chiavi di sezione libere (doc 11 §3a): idempotente, tocca solo le «custom» storiche.</summary>
public class DocumentMaintenanceTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfDocumentMaintenance _maintenance = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _maintenance = new EfDocumentMaintenance(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private async Task<DocumentVersion> SeedVersionAsync()
    {
        var doc = new Document { Type = DocumentType.Vipi, Title = "doc", Language = Language.It, Status = DocumentStatus.Draft, LastUpdatedAiracCycle = "2607" };
        _db.Documents.Add(doc);
        var ver = new DocumentVersion { Document = doc, VersionNumber = 1, Status = DocumentStatus.Draft, AiracCycle = "2607" };
        _db.DocumentVersions.Add(ver);
        await _db.SaveChangesAsync();
        return ver;
    }

    private static DocumentSection Section(DocumentVersion ver, string key, int order) => new()
    {
        DocumentVersion = ver, Title = $"s{order}", Order = order, Depth = 0, SectionKey = key,
        RowVersion = Guid.NewGuid().ToByteArray(),
    };

    [Fact]
    public async Task Reconcile_Gives_A_Distinct_Key_To_Every_Legacy_Custom_Section()
    {
        var ver = await SeedVersionAsync();
        _db.DocumentSections.AddRange(
            Section(ver, SectionKeys.LegacyCustom, 1),
            Section(ver, SectionKeys.LegacyCustom, 2),
            Section(ver, "aor", 3));
        await _db.SaveChangesAsync();

        var touched = await _maintenance.ReconcileCustomSectionKeysAsync();

        Assert.Equal(2, touched);
        var keys = await _db.DocumentSections.Select(s => s.SectionKey).ToListAsync();
        Assert.Equal(3, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains("aor", keys);
        Assert.DoesNotContain(SectionKeys.LegacyCustom, keys);
        Assert.Equal(2, keys.Count(SectionKeys.IsCustom));
    }

    [Fact]
    public async Task Reconcile_Is_Idempotent()
    {
        var ver = await SeedVersionAsync();
        _db.DocumentSections.Add(Section(ver, SectionKeys.LegacyCustom, 1));
        await _db.SaveChangesAsync();

        Assert.Equal(1, await _maintenance.ReconcileCustomSectionKeysAsync());
        var after = await _db.DocumentSections.Select(s => s.SectionKey).SingleAsync();

        Assert.Equal(0, await _maintenance.ReconcileCustomSectionKeysAsync());
        Assert.Equal(after, await _db.DocumentSections.Select(s => s.SectionKey).SingleAsync());
    }

    [Fact]
    public async Task Hidden_Sections_Move_From_DocumentProfile_To_The_Section_Flag()
    {
        // APP: chiavi; vLOA: titoli. Stessa colonna, stessa migrazione.
        var ver = await SeedVersionAsync();
        var vfr = Section(ver, "vfr", 1);
        var aor = Section(ver, "aor", 2);
        var libera = Section(ver, SectionKeys.NewCustom(), 3);
        libera.Title = "Note";
        _db.DocumentSections.AddRange(vfr, aor, libera);
        _db.DocumentProfiles.Add(new DocumentProfile
        {
            DocumentId = ver.DocumentId,
            HiddenSectionsJson = "[\"vfr\",\"Note\"]",
        });
        await _db.SaveChangesAsync();

        var touched = await _maintenance.MigrateHiddenSectionsAsync();

        Assert.Equal(2, touched);
        Assert.True((await _db.DocumentSections.FindAsync(vfr.Id))!.IsHidden);
        Assert.True((await _db.DocumentSections.FindAsync(libera.Id))!.IsHidden);
        Assert.False((await _db.DocumentSections.FindAsync(aor.Id))!.IsHidden);
        // Sorgente azzerata ⇒ rieseguire non rimette nascosto ciò che è stato rimesso pubblico.
        Assert.Null((await _db.DocumentProfiles.FirstAsync()).HiddenSectionsJson);
        Assert.Equal(0, await _maintenance.MigrateHiddenSectionsAsync());
    }

    [Fact]
    public async Task Ambiguous_Custom_Entry_Hides_Every_Free_Section()
    {
        // Conservativo: dopo la riconciliazione delle chiavi "custom" non identifica più una sezione sola,
        // quindi si nascondono tutte le libere (non si scopre in pubblico ciò che era nascosto).
        var ver = await SeedVersionAsync();
        var a = Section(ver, SectionKeys.NewCustom(), 1);
        var b = Section(ver, SectionKeys.NewCustom(), 2);
        var aor = Section(ver, "aor", 3);
        _db.DocumentSections.AddRange(a, b, aor);
        _db.DocumentProfiles.Add(new DocumentProfile { DocumentId = ver.DocumentId, HiddenSectionsJson = "[\"custom\"]" });
        await _db.SaveChangesAsync();

        Assert.Equal(2, await _maintenance.MigrateHiddenSectionsAsync());
        Assert.True((await _db.DocumentSections.FindAsync(a.Id))!.IsHidden);
        Assert.True((await _db.DocumentSections.FindAsync(b.Id))!.IsHidden);
        Assert.False((await _db.DocumentSections.FindAsync(aor.Id))!.IsHidden);
    }

    [Fact]
    public async Task Hidden_Sections_Move_From_Acc_BlockMeta_To_The_Section_Flag()
    {
        var ver = await SeedVersionAsync();
        var blocco = Section(ver, "aerovia", 1);
        _db.DocumentSections.Add(blocco);
        await _db.SaveChangesAsync();

        var vfr = Section(ver, "vfr", 1);
        vfr.ParentSectionId = blocco.Id;
        var aor = Section(ver, "aor", 2);
        aor.ParentSectionId = blocco.Id;
        _db.DocumentSections.AddRange(vfr, aor);
        _db.ContentBlocks.Add(new ContentBlock
        {
            DocumentVersionId = ver.Id, SectionId = blocco.Id, Order = 1, Format = BlockFormat.Table,
            Tier = BlockTier.Extended, Visibility = BlockVisibility.Always,
            BodyJson = "{\"Key\":\"aerovia\",\"HiddenSections\":[\"vfr\"]}",
            RowVersion = Guid.NewGuid().ToByteArray(),
        });
        await _db.SaveChangesAsync();

        Assert.Equal(1, await _maintenance.MigrateHiddenSectionsAsync());
        Assert.True((await _db.DocumentSections.FindAsync(vfr.Id))!.IsHidden);
        Assert.False((await _db.DocumentSections.FindAsync(aor.Id))!.IsHidden);
        // Blockmeta riscritto senza la proprietà ⇒ idempotente.
        Assert.DoesNotContain("HiddenSections", (await _db.ContentBlocks.FirstAsync()).BodyJson);
        Assert.Equal(0, await _maintenance.MigrateHiddenSectionsAsync());
    }

    // ---- doc 13 §3b: «minima» è tornata editoriale ----

    [Fact]
    public async Task Minima_Loses_Its_Empty_Placeholder_But_Keeps_Real_Content()
    {
        var ver = await SeedVersionAsync();
        var empty = Section(ver, "minima", 1);
        var written = Section(ver, "minima", 2);
        var other = Section(ver, "separations", 3);
        _db.DocumentSections.AddRange(empty, written, other);
        _db.ContentBlocks.AddRange(
            new ContentBlock { DocumentVersion = ver, Section = empty, Order = 1, Format = BlockFormat.Table, Tier = BlockTier.Extended, Visibility = BlockVisibility.Always, RowVersion = Guid.NewGuid().ToByteArray() },
            new ContentBlock { DocumentVersion = ver, Section = written, Order = 1, Format = BlockFormat.Prose, Tier = BlockTier.Extended, Visibility = BlockVisibility.Always, Body = "MVA 3000 ft", RowVersion = Guid.NewGuid().ToByteArray() },
            // Placeholder identico ma su un'altra sezione: non si tocca, là il blocco è il contenitore del BodyJson.
            new ContentBlock { DocumentVersion = ver, Section = other, Order = 1, Format = BlockFormat.Table, Tier = BlockTier.Extended, Visibility = BlockVisibility.Always, RowVersion = Guid.NewGuid().ToByteArray() });
        await _db.SaveChangesAsync();

        var removed = await _maintenance.ClearMinimaPlaceholderBlocksAsync();

        Assert.Equal(1, removed);
        Assert.Empty(_db.ContentBlocks.Where(b => b.SectionId == empty.Id));
        Assert.Single(_db.ContentBlocks.Where(b => b.SectionId == written.Id));
        Assert.Single(_db.ContentBlocks.Where(b => b.SectionId == other.Id));
    }

    [Fact]
    public async Task Clearing_Minima_Placeholders_Is_Idempotent()
    {
        var ver = await SeedVersionAsync();
        var sec = Section(ver, "minima", 1);
        _db.DocumentSections.Add(sec);
        _db.ContentBlocks.Add(new ContentBlock { DocumentVersion = ver, Section = sec, Order = 1, Format = BlockFormat.Table, Tier = BlockTier.Extended, Visibility = BlockVisibility.Always, RowVersion = Guid.NewGuid().ToByteArray() });
        await _db.SaveChangesAsync();

        Assert.Equal(1, await _maintenance.ClearMinimaPlaceholderBlocksAsync());
        Assert.Equal(0, await _maintenance.ClearMinimaPlaceholderBlocksAsync());
    }

    // ---- doc 13 §3c: la vLOA sulle chiavi del catalogo ----

    private async Task<DocumentVersion> SeedVloaVersionAsync()
    {
        var doc = new Document { Type = DocumentType.Vloa, Title = "vLOA", Language = Language.En, Status = DocumentStatus.Draft, LastUpdatedAiracCycle = "2609" };
        _db.Documents.Add(doc);
        var ver = new DocumentVersion { Document = doc, VersionNumber = 1, Status = DocumentStatus.Draft, AiracCycle = "2609" };
        _db.DocumentVersions.Add(ver);
        await _db.SaveChangesAsync();
        return ver;
    }

    private static DocumentSection Child(DocumentVersion ver, DocumentSection parent, string key, string title, int order) => new()
    {
        DocumentVersion = ver, ParentSection = parent, Title = title, Order = order, Depth = 1, SectionKey = key,
        RowVersion = Guid.NewGuid().ToByteArray(),
    };

    [Fact]
    public async Task Coordination_Directions_Get_A_Key_Each_And_Lose_Their_Invisible_Blocks()
    {
        var ver = await SeedVloaVersionAsync();
        var parent = Section(ver, SectionKeys.Coordination, 1);
        parent.Title = "Coordination";
        _db.DocumentSections.Add(parent);
        await _db.SaveChangesAsync();

        var outbound = Child(ver, parent, SectionKeys.Coordination, "LIBB → LDZO", 1);
        var inbound = Child(ver, parent, SectionKeys.Coordination, "LDZO → LIBB", 2);
        _db.DocumentSections.AddRange(outbound, inbound);
        _db.ContentBlocks.Add(new ContentBlock { DocumentVersion = ver, Section = outbound, Order = 1, Format = BlockFormat.Prose, Tier = BlockTier.Reduced, Visibility = BlockVisibility.Always, Body = "**LIBB transfers**…", RowVersion = Guid.NewGuid().ToByteArray() });
        await _db.SaveChangesAsync();

        var touched = await _maintenance.ReconcileVloaSectionKeysAsync();

        Assert.Equal(2, touched);
        Assert.Equal(SectionKeys.CoordinationOut, _db.DocumentSections.Single(s => s.Id == outbound.Id).SectionKey);
        Assert.Equal(SectionKeys.CoordinationIn, _db.DocumentSections.Single(s => s.Id == inbound.Id).SectionKey);
        Assert.Equal(SectionKeys.Coordination, _db.DocumentSections.Single(s => s.Id == parent.Id).SectionKey);
        Assert.Empty(_db.ContentBlocks.Where(b => b.SectionId == outbound.Id));
    }

    [Fact]
    public async Task Purpose_Gets_The_Catalog_Key_Only_Inside_A_Vloa()
    {
        var vloa = await SeedVloaVersionAsync();
        var vloaPurpose = Section(vloa, SectionKeys.NewCustom(), 1);
        vloaPurpose.Title = "Purpose";
        var vipi = await SeedVersionAsync();
        var vipiPurpose = Section(vipi, SectionKeys.NewCustom(), 1);
        vipiPurpose.Title = "Purpose";   // stesso titolo in una vIPI: non è la sezione del catalogo vLOA
        _db.DocumentSections.AddRange(vloaPurpose, vipiPurpose);
        await _db.SaveChangesAsync();

        var touched = await _maintenance.ReconcileVloaSectionKeysAsync();

        Assert.Equal(1, touched);
        Assert.Equal("purpose", _db.DocumentSections.Single(s => s.Id == vloaPurpose.Id).SectionKey);
        Assert.True(SectionKeys.IsCustom(_db.DocumentSections.Single(s => s.Id == vipiPurpose.Id).SectionKey));
    }

    [Fact]
    public async Task Reconciling_Vloa_Keys_Is_Idempotent()
    {
        var ver = await SeedVloaVersionAsync();
        var parent = Section(ver, SectionKeys.Coordination, 1);
        _db.DocumentSections.Add(parent);
        await _db.SaveChangesAsync();
        _db.DocumentSections.AddRange(
            Child(ver, parent, SectionKeys.Coordination, "A → B", 1),
            Child(ver, parent, SectionKeys.Coordination, "B → A", 2));
        var purpose = Section(ver, SectionKeys.NewCustom(), 2);
        purpose.Title = "Purpose";
        _db.DocumentSections.Add(purpose);
        await _db.SaveChangesAsync();

        Assert.Equal(3, await _maintenance.ReconcileVloaSectionKeysAsync());
        Assert.Equal(0, await _maintenance.ReconcileVloaSectionKeysAsync());
    }
}
