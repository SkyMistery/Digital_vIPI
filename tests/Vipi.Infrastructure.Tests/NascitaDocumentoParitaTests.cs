using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// <b>La parità delle quattro porte di nascita</b> (doc 14 §3i) — la prova che impedisce il ritorno.
///
/// <para>
/// Un documento vIPI può nascere da quattro porte diverse: <c>EnsureVipiDocumentAsync</c> (ACC e APP),
/// <c>CreateDocumentAsync</c> (nuovo documento), <c>EfAirportRepository.EnsureDocumentAsync</c> (aeroporto) e
/// <c>EfNeighbourRepository</c> (vLOA da «ACC confinanti»). Due di quelle quattro puntavano
/// <c>CurrentVersionId</c> alla bozza appena creata, le altre due lo lasciavano null.
/// </para>
///
/// <para>
/// ⚠️ Nessun test le confrontava, ed è per questo che erano divergenti. Qui si fa a tutte la stessa domanda:
/// <b>un documento appena nato NON ha una versione pubblicata</b>, perché non è stato pubblicato. Chi
/// aggiungerà una quinta porta la eredita — basta aggiungere un caso.
/// </para>
/// </summary>
public class NascitaDocumentoParitaTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        await RomaStructureSeed.SeedAsync(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private EfEditingRepository Editing() => new(_db, new AiracService(), new EfMediaMaintenance(_db));

    private async Task AssertNascePulito(int documentId)
    {
        var doc = await _db.Documents.AsNoTracking().FirstAsync(d => d.Id == documentId);

        // La regola, in una riga: nessuna versione pubblicata, perché non si è pubblicato niente.
        Assert.Null(doc.CurrentVersionId);
        Assert.Equal(DocumentStatus.Draft, doc.Status);

        // E la versione che c'è è una bozza: il puntatore è null perché non c'è nulla da puntare, non
        // perché ci si è dimenticati di scriverlo.
        var versioni = await _db.DocumentVersions.AsNoTracking()
            .Where(v => v.DocumentId == documentId).ToListAsync();
        var sola = Assert.Single(versioni);
        Assert.Equal(DocumentStatus.Draft, sola.Status);
        Assert.Equal(1, sola.VersionNumber);
    }

    [Fact]
    public async Task Porta_1_EnsureVipiDocument_APP()
    {
        var settore = await _db.Sectors.Where(s => s.DocumentId == null).Select(s => s.Id).FirstAsync();

        var id = await Editing().EnsureVipiDocumentAsync(
            settore, "vIPI APP", Language.It, SectionProfile.App, authorUserId: 1);

        await AssertNascePulito(id);
    }

    [Fact]
    public async Task Porta_2_EnsureVipiDocumentTree_ACC()
    {
        var settore = await _db.Sectors.Where(s => s.DocumentId == null).Select(s => s.Id).FirstAsync();
        var blocchi = new[]
        {
            new Vipi.Application.Abstractions.VipiBlockSpec("aerovia", "Settori di aerovia", SectionProfile.AccAerovia),
        };

        var id = await Editing().EnsureVipiDocumentTreeAsync(
            settore, "vIPI ACC", Language.It, blocchi, authorUserId: 1);

        await AssertNascePulito(id);
    }

    [Fact]
    public async Task Porta_3_CreateDocument_nuovo_documento()
    {
        var settore = await _db.Sectors.Where(s => s.DocumentId == null).Select(s => s.Id).FirstAsync();

        var id = await Editing().CreateDocumentAsync(
            DocumentType.Vipi, "documento nuovo", Language.It,
            scopeSectorIds: new[] { settore }, primarySectorId: settore, parties: null, authorUserId: 1);

        await AssertNascePulito(id);
    }

    [Fact]
    public async Task Porta_4_EnsureDocument_AEROPORTO()
    {
        // ⚠️ Era una delle due che puntavano il campo a una bozza.
        var struttura = new EfStructureEditingRepository(_db);
        var acc = await _db.Accs.Select(a => a.Code).FirstAsync();
        await struttura.CreateAirportAsync(acc, "LIPZ", "Venezia Tessera");
        var aeroporti = new EfAirportRepository(_db, new EfMediaMaintenance(_db));

        var id = await aeroporti.EnsureDocumentAsync("LIPZ");

        await AssertNascePulito(id);
    }

    [Fact]
    public async Task Il_puntatore_lo_scrive_la_PUBBLICAZIONE_e_nessun_altro()
    {
        // L'altra metà della regola: dopo che si pubblica, il campo c'è ed è quello giusto. Senza questa, la
        // prova di sopra si potrebbe soddisfare non scrivendolo mai.
        var settore = await _db.Sectors.Where(s => s.DocumentId == null).Select(s => s.Id).FirstAsync();
        var repo = Editing();
        var id = await repo.EnsureVipiDocumentAsync(settore, "vIPI APP", Language.It, SectionProfile.App, authorUserId: 1);
        var versione = await _db.DocumentVersions.Where(v => v.DocumentId == id).Select(v => v.Id).FirstAsync();

        await repo.PublishAsync(versione, actorUserId: 1, note: null);

        var doc = await _db.Documents.AsNoTracking().FirstAsync(d => d.Id == id);
        Assert.Equal(versione, doc.CurrentVersionId);
        Assert.Equal(DocumentStatus.Published, doc.Status);
    }
}
