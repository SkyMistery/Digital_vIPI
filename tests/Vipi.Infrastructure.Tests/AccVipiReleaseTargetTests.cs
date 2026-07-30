using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.ReleaseTargets;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// La chiave di release della vIPI ACC è <c>"{accCode}|{rootCallsign}"</c>: la parte root NON è decorativa, sceglie
/// QUALE documento dell'ACC si pubblica. Prima queste righe la ignoravano e restituivano il primo CTR radice per
/// <c>CoverageOrder</c>: con una ACC a più alberi (più CTR radice, un documento a testa) «Pubblica ora» promuoveva la
/// bozza del documento sbagliato — silenziosamente, perché la chiave sembrava giusta.
/// </summary>
public class AccVipiReleaseTargetTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private int _docNord, _docSud;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();

        var acc = new Acc { Code = "LIXX", Name = "Test ACC" };
        _db.Accs.Add(acc);

        // Due alberi nella stessa ACC, un documento per albero. L'ordine di copertura è deliberatamente inverso
        // all'ordine alfabetico dei callsign, così un fallback su CoverageOrder o su Callsign sbaglierebbe comunque.
        _docNord = (await AddRootWithDocAsync(acc, "LIXX_N_CTR", coverageOrder: 2, title: "vIPI Nord")).Id;
        _docSud = (await AddRootWithDocAsync(acc, "LIXX_S_CTR", coverageOrder: 1, title: "vIPI Sud")).Id;
    }

    private async Task<Document> AddRootWithDocAsync(Acc acc, string callsign, int coverageOrder, string title)
    {
        var doc = new Document
        {
            Type = DocumentType.Vipi, Title = title, Language = Language.It,
            Status = DocumentStatus.Published, LastUpdatedAiracCycle = "2607",
        };
        _db.Documents.Add(doc);
        _db.Sectors.Add(new Sector
        {
            Acc = acc, Callsign = callsign, Name = title, Type = SectorType.Ctr,
            ParentSectorId = null, IsActive = true, IsPrimary = true,
            CoverageOrder = coverageOrder, Document = doc,
        });
        await _db.SaveChangesAsync();
        return doc;
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    [Theory]
    [InlineData("LIXX|LIXX_N_CTR", "vIPI Nord")]
    [InlineData("LIXX|LIXX_S_CTR", "vIPI Sud")]
    public async Task Risolve_Il_Documento_Della_Radice_Indicata_Nella_Chiave(string key, string attesoTitolo)
    {
        var target = new AccVipiReleaseTarget(_db);

        var docId = await target.ResolveDocumentIdAsync(key);

        var titolo = await _db.Documents.Where(d => d.Id == docId).Select(d => d.Title).FirstOrDefaultAsync();
        Assert.Equal(attesoTitolo, titolo);
    }

    [Fact]
    public async Task Il_Callsign_Della_Radice_Non_Distingue_Maiuscole()
    {
        var target = new AccVipiReleaseTarget(_db);

        Assert.Equal(_docNord, await target.ResolveDocumentIdAsync("lixx|lixx_n_ctr"));
    }

    [Fact]
    public async Task Chiave_Senza_Radice_Ricade_Sulla_Prima_Per_Ordine_Di_Copertura()
    {
        // Compatibilità: le chiavi legacy sono il solo codice ACC. Il fallback è il criterio storico
        // (CoverageOrder, poi callsign) — qui il Sud, che ha CoverageOrder 1.
        var target = new AccVipiReleaseTarget(_db);

        Assert.Equal(_docSud, await target.ResolveDocumentIdAsync("LIXX"));
    }

    [Fact]
    public async Task Radice_Inesistente_O_Senza_Documento_Non_Ricade_Su_Un_Altro_Documento()
    {
        // Il punto è la pubblicazione: meglio nessun documento (il chiamante mostra «nessun contenuto da
        // pubblicare») che il documento di un ALTRO albero promosso al posto di quello chiesto.
        var acc = await _db.Accs.FirstAsync();
        _db.Sectors.Add(new Sector
        {
            Acc = acc, Callsign = "LIXX_E_CTR", Name = "Est senza documento", Type = SectorType.Ctr,
            ParentSectorId = null, IsActive = true, CoverageOrder = 3,
        });
        await _db.SaveChangesAsync();
        var target = new AccVipiReleaseTarget(_db);

        Assert.Null(await target.ResolveDocumentIdAsync("LIXX|LIXX_E_CTR"));    // radice esistente, nessun documento
        Assert.Null(await target.ResolveDocumentIdAsync("LIXX|LIXX_W_CTR"));    // radice inesistente
    }
}
