using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Application.Abstractions;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.ReleaseTargets;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Eliminare un documento con <b>sezioni annidate</b>: la forma di ogni vIPI vera (dieci sezioni, nove
/// figlie di una radice) e di ogni vLOA.
///
/// <para>⚠️ Due vincoli sono <c>RESTRICT</c> e la cascata del database non li sa ordinare da sé:
/// <c>DocumentSections.ParentSectionId</c> verso sé stessa e <c>ContentBlocks.SectionId</c>. Cancellare il
/// documento e lasciar fare al database finisce in un «FOREIGN KEY constraint failed» — un messaggio che
/// parla di vincoli a chi voleva togliere un documento.</para>
/// </summary>
public class EliminaDocumentoAnnidatoTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    /// <summary>La forma vera: una radice, due figlie, un blocco su una figlia.</summary>
    private async Task<Document> DocumentoAnnidatoAsync()
    {
        var d = new Document { Type = DocumentType.Vipi, Title = "vIPI di prova", LastUpdatedAiracCycle = "2608" };
        _db.Documents.Add(d);
        await _db.SaveChangesAsync();

        var v = new DocumentVersion
        {
            DocumentId = d.Id, VersionNumber = 1, Status = DocumentStatus.Draft,
            AiracCycle = "2608", CreatedUtc = DateTime.UtcNow,
        };
        _db.DocumentVersions.Add(v);
        await _db.SaveChangesAsync();

        var radice = new DocumentSection { DocumentVersionId = v.Id, Title = "Radice", Order = 1, Depth = 0 };
        _db.DocumentSections.Add(radice);
        await _db.SaveChangesAsync();

        var figlia = new DocumentSection
        {
            DocumentVersionId = v.Id, Title = "Figlia", Order = 1, Depth = 1, ParentSectionId = radice.Id,
        };
        _db.DocumentSections.Add(figlia);
        await _db.SaveChangesAsync();

        _db.ContentBlocks.Add(new ContentBlock
        {
            DocumentVersionId = v.Id, SectionId = figlia.Id, Order = 1, Body = "testo",
        });
        d.CurrentVersionId = v.Id;
        await _db.SaveChangesAsync();

        // ⚠️ Il tracker si azzera: è la differenza fra il test e la vita. Appena create, le righe sono
        // tracciate e EF ordina le cancellazioni da sé; nell'applicazione il documento si rilegge da solo,
        // i figli non sono tracciati, e la cascata la deve fare il DATABASE — che sui due vincoli RESTRICT
        // non sa in che ordine andare. Senza questa riga il test passa e il difetto resta.
        _db.ChangeTracker.Clear();
        return d;
    }

    [Fact]
    public async Task Un_documento_con_sezioni_annidate_si_elimina_davvero()
    {
        var d = await DocumentoAnnidatoAsync();
        var repo = new EfDeletionRepository(_db, new EfUnitOfWork(_db), new EfDocumentImpactRepository(_db));

        await repo.DeleteUnmanagedDocumentAsync(d.Id, actorUserId: 7);

        Assert.False(await _db.Documents.AnyAsync(x => x.Id == d.Id));
        Assert.Empty(await _db.DocumentVersions.ToListAsync());
        Assert.Empty(await _db.DocumentSections.ToListAsync());
        Assert.Empty(await _db.ContentBlocks.ToListAsync());
    }

    [Fact]
    public async Task Anche_la_via_dei_documenti_gestiti_ci_riesce()
    {
        // La stessa forma, dalla porta che usa la pagina Documenti: il difetto era lì prima ancora che
        // esistesse il motore di eliminazione.
        var d = await DocumentoAnnidatoAsync();
        var registro = new ReleaseTargetRegistry(new IReleaseTarget[] { new AirportReleaseTarget(_db) });
        var repo = new EfDocumentAdminRepository(_db, registro, new EfReleaseRepository(_db, registro, new EfMediaMaintenance(_db)), new EfMediaMaintenance(_db));

        await repo.DeleteAsync(new ManagedDocRef(ReleaseTargetType.Airport, "LIRF", d.Id), actorUserId: 7);

        Assert.False(await _db.Documents.AnyAsync(x => x.Id == d.Id));
        Assert.Empty(await _db.DocumentSections.ToListAsync());
    }
}
