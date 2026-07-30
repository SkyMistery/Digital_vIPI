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

    private DocumentSection Section(DocumentVersion ver, string key, int order) => new()
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
}
