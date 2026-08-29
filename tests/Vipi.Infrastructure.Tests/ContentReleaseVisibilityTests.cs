using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Visibilità pubblica vs release AIRAC (fix): una release effettiva deve essere servita al pubblico anche quando il
/// Document è ancora Draft (release e pubblicazione-versione sono due layer). Senza release, un Document mai pubblicato
/// resta invisibile (nessun leak). Regressione osservata: vIPI APP pubblicata come release ma non come versione →
/// il viewer pubblico non la mostrava.
/// </summary>
public class ContentReleaseVisibilityTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfContentRepository _content = default!;
    private EfReleaseRepository _releases = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _releases = TestReleaseTargets.ReleaseRepo(_db);
        _content = new EfContentRepository(_db, _releases);

        var acc = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        _db.Accs.Add(acc);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    /// <summary>Crea un APP standalone con Document(Vipi) DRAFT (mai pubblicato come versione) + 1 sezione con testo.</summary>
    private async Task<string> SeedDraftAppAsync(string callsign, string sectionTitle, string body)
    {
        var acc = await _db.Accs.FirstAsync();
        var doc = new Document { Type = DocumentType.Vipi, Title = $"vIPI {callsign}", Language = Language.It, Status = DocumentStatus.Draft, LastUpdatedAiracCycle = "2606" };
        var ver = new DocumentVersion { Document = doc, VersionNumber = 1, Status = DocumentStatus.Draft, AiracCycle = "2606", CreatedUtc = DateTime.UtcNow };
        doc.Versions.Add(ver);
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

        var sec = new DocumentSection { DocumentVersion = ver, Title = sectionTitle, Order = 1, Depth = 0, SectionKey = "operationaltechnique", RowVersion = Guid.NewGuid().ToByteArray() };
        _db.DocumentSections.Add(sec);
        await _db.SaveChangesAsync();
        _db.ContentBlocks.Add(new ContentBlock { DocumentVersion = ver, Section = sec, Order = 1, Format = BlockFormat.Prose, Tier = BlockTier.Reduced, Visibility = BlockVisibility.Always, Body = body, RowVersion = Guid.NewGuid().ToByteArray() });
        _db.Sectors.Add(new Sector { Acc = acc, Callsign = callsign, Name = callsign, Type = SectorType.App, Kind = SectorKind.Airport, ApproachKind = ApproachKind.Standalone, IsActive = true, DocumentId = doc.Id, IsPrimary = true });
        // Document resta Draft: CurrentVersionId NON impostato.
        await _db.SaveChangesAsync();
        return callsign;
    }

    [Fact]
    public async Task DraftApp_WithEffectiveRelease_IsServedToPublic()
    {
        var app = await SeedDraftAppAsync("LICC_APP", "Tecnica operativa", "Testo pubblicato via release");

        // Pubblica come RELEASE (non come versione): snapshot dello stato working, effettiva adesso.
        var json = (await _releases.SnapshotWorkingAsync(ReleaseTargetType.App, app, "2607"))!;
        await _releases.SaveReleaseAsync(ReleaseTargetType.App, app, "2607", DateTime.UtcNow.AddSeconds(-5), json, 1, null);

        var pub = await _content.LoadAppVipiAsync(app);   // vista pubblica (default)
        Assert.NotNull(pub);
        Assert.Contains(pub!.Roots, s => s.Title == "Tecnica operativa");
    }

    [Fact]
    public async Task DraftApp_WithoutRelease_IsNotVisibleToPublic()
    {
        var app = await SeedDraftAppAsync("LIPZ_APP", "Tecnica operativa", "Bozza mai pubblicata");
        Assert.Null(await _content.LoadAppVipiAsync(app));   // nessuna release + mai pubblicato → invisibile
    }

    [Fact]
    public async Task HiddenApp_WithEffectiveRelease_StaysHidden()
    {
        var app = await SeedDraftAppAsync("LIME_APP", "Tecnica operativa", "Testo");
        var doc = await _db.Documents.FirstAsync(d => d.Title == "vIPI LIME_APP");
        doc.IsHidden = true;
        await _db.SaveChangesAsync();

        var json = (await _releases.SnapshotWorkingAsync(ReleaseTargetType.App, app, "2607"))!;
        await _releases.SaveReleaseAsync(ReleaseTargetType.App, app, "2607", DateTime.UtcNow.AddSeconds(-5), json, 1, null);

        Assert.Null(await _content.LoadAppVipiAsync(app));   // nascosto dall'admin → invisibile anche con release
    }

    // ---- L'edizione MILITARE (trovata A SCHERMO il 29 agosto 2026) -----------------------------------

    /// <summary>Un vSOP militare d'aeroporto pubblicato: documento con <c>Edition = Military</c> agganciato
    /// allo scalo da <c>Airport.MilDocumentId</c>, più una sezione con del testo.</summary>
    private async Task<string> SeedVsopMilitareAsync(string icao, string titoloSezione, string body)
    {
        var acc = await _db.Accs.FirstAsync();
        var doc = new Document
        {
            Type = DocumentType.Vipi, Title = $"vSOP MIL — {icao}", Language = Language.It,
            Status = DocumentStatus.Draft, LastUpdatedAiracCycle = "2606", Edition = DocumentEdition.Military,
        };
        var ver = new DocumentVersion { Document = doc, VersionNumber = 1, Status = DocumentStatus.Draft, AiracCycle = "2606", CreatedUtc = DateTime.UtcNow };
        doc.Versions.Add(ver);
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

        var sec = new DocumentSection { DocumentVersion = ver, Title = titoloSezione, Order = 1, Depth = 0, SectionKey = "operationaltechnique", RowVersion = Guid.NewGuid().ToByteArray() };
        _db.DocumentSections.Add(sec);
        await _db.SaveChangesAsync();
        _db.ContentBlocks.Add(new ContentBlock { DocumentVersion = ver, Section = sec, Order = 1, Format = BlockFormat.Prose, Tier = BlockTier.Reduced, Visibility = BlockVisibility.Always, Body = body, RowVersion = Guid.NewGuid().ToByteArray() });
        // ⚠️ Il legame è MilDocumentId, non DocumentId: lo scalo qui non ha un documento civile.
        _db.Airports.Add(new Airport { Icao = icao, Name = icao, Acc = acc, MilDocumentId = doc.Id });
        await _db.SaveChangesAsync();
        return icao;
    }

    [Fact]
    public async Task UN_vSOP_MILITARE_PUBBLICATO_SI_VEDE_DAL_PUBBLICO()
    {
        // ⚠️ È IL test del difetto trovato a schermo il 29 agosto 2026, e la suite non poteva vederlo perché
        // nessun test caricava un documento MILITARE dal percorso PUBBLICO.
        //
        // `LoadVipiAsync` chiede a `ResolveReleaseTargetAsync` di quale release il documento sia il bersaglio.
        // Quella risoluzione è scritta a mano — il documento arriva da una query senza `Include`, quindi i
        // descrittori non si possono usare — e guardava solo `Sector.DocumentId` e `Airport.DocumentId`: per
        // un documento militare rispondeva `(null, null)`. Da lì lo snapshot della release non veniva
        // nemmeno cercato, e il percorso pubblico concludeva «nessuna release» tornando null.
        //
        // ⚠️ A schermo: un vSOP militare **pubblicato e in vigore** mostrava «Nessun vSOP militare
        // pubblicato». Non si era visto perché l'unico documento militare guardato a schermo era in BOZZA,
        // e la bozza prende l'altro ramo (`ignoreRelease`).
        var icao = await SeedVsopMilitareAsync("LIPI", "Procedure generali", "Testo del SOP militare");

        var json = (await _releases.SnapshotWorkingAsync(ReleaseTargetType.AirportMil, icao, "2607"))!;
        await _releases.SaveReleaseAsync(ReleaseTargetType.AirportMil, icao, "2607", DateTime.UtcNow.AddSeconds(-5), json, 1, null);

        var pub = await _content.LoadAirportMilVipiAsync(icao);   // vista pubblica (default)

        Assert.NotNull(pub);
        Assert.Contains(pub!.Roots, s => s.Title == "Procedure generali");
    }

    [Fact]
    public async Task Un_vSOP_militare_SENZA_release_resta_invisibile()
    {
        // L'altra metà: il gate pubblico vale identico per le due edizioni. Senza questa prova, «si vede» si
        // potrebbe ottenere anche togliendo il gate, che è il modo sbagliato di far passare il test di sopra.
        var icao = await SeedVsopMilitareAsync("LIPL", "Procedure generali", "Bozza mai pubblicata");

        Assert.Null(await _content.LoadAirportMilVipiAsync(icao));
    }
}
