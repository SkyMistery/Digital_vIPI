using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// <c>Document.CurrentVersionId</c> vuol dire <b>«la versione PUBBLICATA corrente»</b>, e una cosa sola.
/// Doc 14 §3i.
///
/// <para>
/// Lo scrive <c>PublishAsync</c>; l'eliminazione lo azzera. Ma due porte su quattro — l'aeroporto e la vLOA
/// generata da «ACC confinanti» — lo puntavano alla <b>bozza</b> appena creata: un documento mai pubblicato
/// che dichiarava di avere una versione pubblicata.
/// </para>
///
/// <para>
/// ⚠️ Perché conta, visto che oggi non si vedeva: ogni lettore pubblico ha un secondo cancello più forte
/// (release effettiva, e stato <c>Published</c>). È il <b>prossimo</b> lettore il rischio — chi si fida del
/// nome del campo e non mette il secondo cancello si porta a casa una bozza. Queste prove tolgono
/// l'ambiguità dal campo, invece di lasciarla difendere dai cancelli a valle.
/// </para>
/// </summary>
public class CurrentVersionSignificatoTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfDocumentMaintenance _manutenzione = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _manutenzione = new EfDocumentMaintenance(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    /// <summary>Un documento con una versione nello stato dato, e il puntatore che gli si vuole dare.</summary>
    private async Task<Document> SeedAsync(DocumentStatus statoVersione, bool puntatoreImpostato,
        DocumentStatus statoDoc = DocumentStatus.Draft)
    {
        var doc = new Document
        {
            Type = DocumentType.Vipi, Title = "doc", Language = Language.It,
            Status = statoDoc, LastUpdatedAiracCycle = "2609",
        };
        _db.Documents.Add(doc);
        var ver = new DocumentVersion
        {
            Document = doc, VersionNumber = 1, Status = statoVersione, AiracCycle = "2609",
        };
        _db.DocumentVersions.Add(ver);
        await _db.SaveChangesAsync();

        if (puntatoreImpostato)
        {
            doc.CurrentVersionId = ver.Id;
            await _db.SaveChangesAsync();
        }
        return doc;
    }

    [Fact]
    public async Task Un_puntatore_su_una_BOZZA_si_azzera()
    {
        var doc = await SeedAsync(DocumentStatus.Draft, puntatoreImpostato: true);

        Assert.Equal(1, await _manutenzione.ClearUnpublishedCurrentVersionAsync());

        await _db.Entry(doc).ReloadAsync();
        Assert.Null(doc.CurrentVersionId);
    }

    [Fact]
    public async Task Un_puntatore_su_una_versione_PUBBLICATA_resta()
    {
        var doc = await SeedAsync(DocumentStatus.Published, puntatoreImpostato: true,
            statoDoc: DocumentStatus.Published);

        Assert.Equal(0, await _manutenzione.ClearUnpublishedCurrentVersionAsync());

        await _db.Entry(doc).ReloadAsync();
        Assert.NotNull(doc.CurrentVersionId);
    }

    [Fact]
    public async Task Guarda_lo_stato_della_VERSIONE_non_quello_del_documento()
    {
        // ⚠️ Un documento ARCHIVIATO che ha davvero pubblicato qualcosa tiene il suo puntatore: la storia di
        // che cosa è stato pubblicato non si cancella perché il documento è stato ritirato.
        var doc = await SeedAsync(DocumentStatus.Published, puntatoreImpostato: true,
            statoDoc: DocumentStatus.Archived);

        Assert.Equal(0, await _manutenzione.ClearUnpublishedCurrentVersionAsync());

        await _db.Entry(doc).ReloadAsync();
        Assert.NotNull(doc.CurrentVersionId);
    }

    [Fact]
    public async Task E_idempotente()
    {
        await SeedAsync(DocumentStatus.Draft, puntatoreImpostato: true);

        Assert.Equal(1, await _manutenzione.ClearUnpublishedCurrentVersionAsync());
        Assert.Equal(0, await _manutenzione.ClearUnpublishedCurrentVersionAsync());
        Assert.Equal(0, await _manutenzione.ClearUnpublishedCurrentVersionAsync());
    }

    [Fact]
    public async Task Un_documento_che_il_puntatore_non_ce_l_ha_non_si_tocca()
    {
        var doc = await SeedAsync(DocumentStatus.Draft, puntatoreImpostato: false);

        Assert.Equal(0, await _manutenzione.ClearUnpublishedCurrentVersionAsync());

        await _db.Entry(doc).ReloadAsync();
        Assert.Null(doc.CurrentVersionId);
    }

    [Fact]
    public async Task Le_versioni_non_si_toccano_mai()
    {
        // Si azzera un PUNTATORE, non si cancella una versione: il contenuto resta dov'è.
        await SeedAsync(DocumentStatus.Draft, puntatoreImpostato: true);
        var prima = await _db.DocumentVersions.CountAsync();

        await _manutenzione.ClearUnpublishedCurrentVersionAsync();

        Assert.Equal(prima, await _db.DocumentVersions.CountAsync());
    }
}
