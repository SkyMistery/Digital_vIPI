using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// La tecnica operativa della torre radar passa ai blocchi (21 ottobre 2026, committente: «non ci deve essere un
/// paragrafo fisso […] per migrare i dati già inseriti mettili in un paragrafo»). La manutenzione d'avvio porta il
/// testo dell'anagrafica in un paragrafo, nella vIPI e nel vSOP dello scalo, e toglie i segnaposto di quando la
/// sezione la disegnava la pagina.
/// </summary>
public class TecnicaRadarNeiBlocchiTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfDocumentMaintenance _manutenzione = default!;
    private Acc _acc = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _manutenzione = new EfDocumentMaintenance(_db);
        _acc = new Acc { Code = "LIPP", Name = "Padova", CountryPrefix = "LI" };
        _db.Accs.Add(_acc);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    /// <summary>Un documento con le versioni date; ciascuna ha la torre radar e la tecnica col segnaposto, come le
    /// faceva nascere la pagina. La corrente è quella indicata, l'ultima è la bozza.</summary>
    private async Task<(Document Doc, List<DocumentVersion> Versioni)> DocumentoAsync(int versioni, int corrente, bool sezione = true)
    {
        var doc = new Document
        {
            Type = DocumentType.Vipi, Title = "vIPI — LIPZ", Language = Language.It,
            Status = DocumentStatus.Published, LastUpdatedAiracCycle = "2611",
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

        var lista = new List<DocumentVersion>();
        for (var n = 1; n <= versioni; n++)
        {
            var ver = new DocumentVersion
            {
                DocumentId = doc.Id, VersionNumber = n, Status = n == versioni ? DocumentStatus.Draft : DocumentStatus.Published,
                CreatedByUserId = 0, CreatedUtc = DateTime.UtcNow, AiracCycle = "2611",
            };
            _db.DocumentVersions.Add(ver);
            await _db.SaveChangesAsync();
            lista.Add(ver);
            if (!sezione) continue;

            var torre = new DocumentSection
            {
                DocumentVersionId = ver.Id, Title = "Radar di torre", Order = 5, Depth = 0,
                SectionKey = SectionKeys.RadarTower, RowVersion = Guid.NewGuid().ToByteArray(),
            };
            _db.DocumentSections.Add(torre);
            await _db.SaveChangesAsync();
            var tecnica = new DocumentSection
            {
                DocumentVersionId = ver.Id, Title = "Tecnica operativa", Order = 1, Depth = 1,
                ParentSectionId = torre.Id, SectionKey = SectionKeys.RadarTowerTechnique,
                RowVersion = Guid.NewGuid().ToByteArray(),
            };
            _db.DocumentSections.Add(tecnica);
            await _db.SaveChangesAsync();
            _db.ContentBlocks.Add(Segnaposto(ver.Id, torre.Id));
            _db.ContentBlocks.Add(Segnaposto(ver.Id, tecnica.Id));
        }
        doc.CurrentVersionId = lista[corrente - 1].Id;
        await _db.SaveChangesAsync();
        return (doc, lista);
    }

    /// <summary>Il blocco che `AggiungiPlaceholderSeServe` dava alle sezioni rese dalla pagina.</summary>
    private static ContentBlock Segnaposto(int versione, int sezione) => new()
    {
        DocumentVersionId = versione, SectionId = sezione, Order = 1, Format = BlockFormat.Table,
        Tier = BlockTier.Extended, Visibility = BlockVisibility.Always, RowVersion = Guid.NewGuid().ToByteArray(),
    };

    private async Task<Airport> ScaloAsync(string? tecnica, int? vipi, int? vsop)
    {
        var a = new Airport
        {
            Icao = "LIPZ", Name = "Venezia Tessera", Acc = _acc, RadarTower = true,
            RadarTowerTechnique = tecnica, DocumentId = vipi, MilDocumentId = vsop,
        };
        _db.Airports.Add(a);
        await _db.SaveChangesAsync();
        return a;
    }

    private Task<List<ContentBlock>> BlocchiDellaTecnicaAsync(int versione) =>
        _db.ContentBlocks.AsNoTracking()
            .Where(b => b.DocumentVersionId == versione && b.Section!.SectionKey == SectionKeys.RadarTowerTechnique)
            .ToListAsync();

    /// <summary>
    /// Il testo arriva in un paragrafo nella vIPI (bozza e pubblicata corrente) e nel vSOP, con gli a-capo; i
    /// segnaposto della tecnica se ne vanno, la colonna si svuota, e la seconda volta non c'è più niente da fare.
    /// </summary>
    [Fact]
    public async Task Il_testo_diventa_un_paragrafo_nella_vipi_e_nel_vsop()
    {
        var (vipi, versioniVipi) = await DocumentoAsync(versioni: 2, corrente: 1);
        var (vsop, versioniVsop) = await DocumentoAsync(versioni: 1, corrente: 1);
        await ScaloAsync("  Identificazione radar entro 5 NM.\nSeconda riga.  ", vipi.Id, vsop.Id);

        Assert.True(await _manutenzione.TecnicaRadarNeiBlocchiAsync() > 0);

        foreach (var ver in versioniVipi.Concat(versioniVsop))
        {
            var blocco = Assert.Single(await BlocchiDellaTecnicaAsync(ver.Id));
            Assert.Equal(BlockFormat.Prose, blocco.Format);
            Assert.Equal("Identificazione radar entro 5 NM.\nSeconda riga.", blocco.Body);
            Assert.Null(blocco.BodyJson);
        }
        Assert.Null((await _db.Airports.AsNoTracking().SingleAsync()).RadarTowerTechnique);
        // Il padre resta reso dalla pagina, e il suo segnaposto con lui.
        Assert.Equal(3, await _db.ContentBlocks.CountAsync(b => b.Section!.SectionKey == SectionKeys.RadarTower));

        Assert.Equal(0, await _manutenzione.TecnicaRadarNeiBlocchiAsync());
    }

    /// <summary>
    /// Il segnaposto si toglie anche dove non c'è un testo da portare: nell'editor a blocchi sarebbe una tabella
    /// senza colonne che nessuno ha chiesto.
    /// </summary>
    [Fact]
    public async Task Senza_testo_si_toglie_solo_il_segnaposto()
    {
        var (vipi, versioni) = await DocumentoAsync(versioni: 1, corrente: 1);
        await ScaloAsync(null, vipi.Id, null);

        Assert.Equal(1, await _manutenzione.TecnicaRadarNeiBlocchiAsync());

        Assert.Empty(await BlocchiDellaTecnicaAsync(versioni[0].Id));
    }

    /// <summary>Una sezione con un blocco scritto da una persona è sua: il testo vecchio non ci si aggiunge.</summary>
    [Fact]
    public async Task Una_tecnica_gia_scritta_non_riceve_il_testo_vecchio()
    {
        var (vipi, versioni) = await DocumentoAsync(versioni: 1, corrente: 1);
        var sezione = await _db.DocumentSections.SingleAsync(s => s.SectionKey == SectionKeys.RadarTowerTechnique);
        _db.ContentBlocks.Add(new ContentBlock
        {
            DocumentVersionId = versioni[0].Id, SectionId = sezione.Id, Order = 2, Format = BlockFormat.Callout,
            CalloutKind = CalloutKind.Warning, Tier = BlockTier.Extended, Visibility = BlockVisibility.Always,
            Body = "Il testo nuovo.", RowVersion = Guid.NewGuid().ToByteArray(),
        });
        await _db.SaveChangesAsync();
        await ScaloAsync("Il testo vecchio.", vipi.Id, null);

        await _manutenzione.TecnicaRadarNeiBlocchiAsync();

        var blocco = Assert.Single(await BlocchiDellaTecnicaAsync(versioni[0].Id));
        Assert.Equal("Il testo nuovo.", blocco.Body);
        Assert.Null((await _db.Airports.AsNoTracking().SingleAsync()).RadarTowerTechnique);
    }

    /// <summary>
    /// Uno scalo con un testo e nessuna sezione dove metterlo tiene il testo: svuotare la colonna lo perderebbe, e
    /// la prossima passata ci riprova.
    /// </summary>
    [Fact]
    public async Task Senza_una_sezione_dove_metterlo_il_testo_resta_nell_anagrafica()
    {
        var (vipi, _) = await DocumentoAsync(versioni: 1, corrente: 1, sezione: false);
        await ScaloAsync("Il testo.", vipi.Id, null);

        Assert.Equal(0, await _manutenzione.TecnicaRadarNeiBlocchiAsync());

        Assert.Equal("Il testo.", (await _db.Airports.AsNoTracking().SingleAsync()).RadarTowerTechnique);
    }
}
