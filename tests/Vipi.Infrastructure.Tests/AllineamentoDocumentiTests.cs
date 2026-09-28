using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain.Services;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Revisione 3, lotto L10 «Allineamento dei documenti» (allegato C, `allineamento-documenti.md`): le passate d'avvio
/// devono lasciare i documenti già scritti come li farebbe nascere oggi il catalogo.
/// <list type="bullet">
/// <item>U-245 — le STAR aggiunte dalla manutenzione nascevano Frozen accanto a SID Live;</item>
/// <item>U-246 — le sezioni «sempre live» rimaste Frozen (la validità della vLOA);</item>
/// <item>U-248 — le passate toccavano solo l'ultima versione: con una bozza aperta la pubblicata restava vecchia, e
/// dopo «Scarta bozza» la bozza seguente tornava alla struttura di prima;</item>
/// <item>U-105 — dove lo spostamento del VFR ha lasciato un lavoro da fare a mano, lo si dice.</item>
/// </list>
/// </summary>
public class AllineamentoDocumentiTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private Acc _acc = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _acc = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        _db.Accs.Add(_acc);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private EfDocumentMaintenance Manutenzione() => new(_db, new EfImportStateStore(_db));

    /// <summary>Una vIPI d'aeroporto civile nata col catalogo, e poi privata di una sezione: è il documento scritto
    /// prima che quella sezione entrasse nel catalogo.</summary>
    private async Task<(Document Doc, DocumentVersion Ver)> VipiCivileSenzaAsync(string icao, string chiaveTolta)
    {
        var apt = new Airport { Icao = icao, Name = icao, Acc = _acc };
        _db.Airports.Add(apt);
        var (doc, ver) = DocumentBirth.Crea(_db, new AiracService(), $"vIPI — {icao}", Language.It,
            SectionProfile.Airport, authorUserId: 0, nasceLive: DocumentBirth.NasceLive(SectionProfile.Airport),
            conSegnaposto: false);
        await _db.SaveChangesAsync();
        apt.DocumentId = doc.Id;
        _db.DocumentSections.RemoveRange(_db.DocumentSections.Where(s => s.DocumentVersionId == ver.Id && s.SectionKey == chiaveTolta));
        await _db.SaveChangesAsync();
        return (doc, ver);
    }

    /// <summary>Pubblica la versione e apre una bozza nuova, copia della pubblicata.</summary>
    private async Task<DocumentVersion> PubblicaEApriBozzaAsync(Document doc, DocumentVersion ver)
    {
        var repo = new EfEditingRepository(_db, new AiracService(), new EfMediaMaintenance(_db));
        await repo.PublishAsync(ver.Id, actorUserId: 1, note: null);
        var bozzaId = await repo.CreateDraftAsync(doc.Id, authorUserId: 1);
        return await _db.DocumentVersions.FirstAsync(v => v.Id == bozzaId);
    }

    private Task<DocumentSection?> SezioneAsync(int versionId, string chiave) =>
        _db.DocumentSections.AsNoTracking().FirstOrDefaultAsync(s => s.DocumentVersionId == versionId && s.SectionKey == chiave);

    // ── U-245 ──────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Le_STAR_aggiunte_dalla_manutenzione_nascono_Live_come_le_SID()
    {
        var (_, ver) = await VipiCivileSenzaAsync("LIRF", "stars");

        await Manutenzione().AddMissingCatalogSectionsAsync();

        var stars = await SezioneAsync(ver.Id, "stars");
        Assert.NotNull(stars);
        Assert.Equal(RenderMode.Live, stars!.RenderMode);
        Assert.Equal(RenderMode.Live, (await SezioneAsync(ver.Id, "sids"))!.RenderMode);
        // Il resto come prima: una sezione derivata qualunque nasce Frozen.
        Assert.Equal(RenderMode.Frozen, (await SezioneAsync(ver.Id, "runways"))!.RenderMode);
    }

    [Fact]
    public async Task Le_STAR_Frozen_esistenti_diventano_Live_una_volta_sola()
    {
        var (_, ver) = await VipiCivileSenzaAsync("LIRF", "none");
        var stars = await _db.DocumentSections.FirstAsync(s => s.DocumentVersionId == ver.Id && s.SectionKey == "stars");
        stars.RenderMode = RenderMode.Frozen;   // com'erano le 45 in produzione
        await _db.SaveChangesAsync();

        Assert.Equal(1, await Manutenzione().StarCiviliLiveAsync());
        Assert.Equal(RenderMode.Live, (await SezioneAsync(ver.Id, "stars"))!.RenderMode);

        // Un Editor, dopo, le congela apposta: la consegna successiva non deve disfarlo.
        _db.ChangeTracker.Clear();
        var dopo = await _db.DocumentSections.FirstAsync(s => s.DocumentVersionId == ver.Id && s.SectionKey == "stars");
        dopo.RenderMode = RenderMode.Frozen;
        await _db.SaveChangesAsync();

        Assert.Equal(0, await Manutenzione().StarCiviliLiveAsync());
        Assert.Equal(RenderMode.Frozen, (await SezioneAsync(ver.Id, "stars"))!.RenderMode);
    }

    // ── U-246 ──────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Le_sezioni_sempre_live_rimaste_Frozen_tornano_Live()
    {
        var (_, ver) = await VipiCivileSenzaAsync("LIRF", "none");
        var validita = await _db.DocumentSections.FirstAsync(s => s.DocumentVersionId == ver.Id && s.SectionKey == "validity");
        validita.RenderMode = RenderMode.Frozen;
        await _db.SaveChangesAsync();

        Assert.Equal(1, await Manutenzione().RiallineaSezioniSempreLiveAsync());
        Assert.Equal(RenderMode.Live, (await SezioneAsync(ver.Id, "validity"))!.RenderMode);
        Assert.Equal(0, await Manutenzione().RiallineaSezioniSempreLiveAsync());   // idempotente
    }

    [Fact]
    public void La_vLOA_nasce_con_la_validita_Live()
    {
        var doc = new Document { Type = DocumentType.Vloa, Title = "vLOA", Language = Language.En, LastUpdatedAiracCycle = "2610" };
        var ver = new DocumentVersion { Document = doc, VersionNumber = 1, Status = DocumentStatus.Draft, AiracCycle = "2610" };
        _db.Documents.Add(doc);
        _db.DocumentVersions.Add(ver);

        VloaStructureSeeder.Seed(_db, ver, VloaSections.Canonical("LIRR", "LIMM", "Milano"));

        var validita = ver.Sections.Single(s => s.SectionKey == "validity");
        Assert.Equal(RenderMode.Live, validita.RenderMode);
    }

    // ── U-248 ──────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Con_una_bozza_aperta_la_passata_cura_anche_la_pubblicata()
    {
        var (doc, v1) = await VipiCivileSenzaAsync("LIBA", "stars");
        var v2 = await PubblicaEApriBozzaAsync(doc, v1);
        Assert.Null(await SezioneAsync(v1.Id, "stars"));
        Assert.Null(await SezioneAsync(v2.Id, "stars"));

        await Manutenzione().AddMissingCatalogSectionsAsync();

        // Tutte e due: la bozza e la pubblicata corrente, da cui nascerebbe la bozza dopo uno «Scarta bozza».
        Assert.NotNull(await SezioneAsync(v2.Id, "stars"));
        Assert.NotNull(await SezioneAsync(v1.Id, "stars"));
    }

    [Fact]
    public async Task Scartata_la_bozza_la_nuova_ha_ancora_la_sezione_aggiunta()
    {
        var (doc, v1) = await VipiCivileSenzaAsync("LIBA", "stars");
        var v2 = await PubblicaEApriBozzaAsync(doc, v1);
        await Manutenzione().AddMissingCatalogSectionsAsync();

        var repo = new EfEditingRepository(_db, new AiracService(), new EfMediaMaintenance(_db));
        await repo.DiscardDraftAsync(v2.Id, actorUserId: 1);
        var v3 = await repo.CreateDraftAsync(doc.Id, authorUserId: 1);

        Assert.NotNull(await SezioneAsync(v3, "stars"));
    }

    // ── U-105 ──────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Dice_dove_la_gestione_del_traffico_va_sistemata_a_mano()
    {
        var doc = new Document { Type = DocumentType.Vipi, Title = "Perugia Approach", Language = Language.It, LastUpdatedAiracCycle = "2610" };
        var ver = new DocumentVersion { Document = doc, VersionNumber = 1, Status = DocumentStatus.Draft, AiracCycle = "2610" };
        _db.Documents.Add(doc);
        _db.DocumentVersions.Add(ver);
        DocumentSection Sez(string key, string titolo, DocumentSection? padre, int ordine)
        {
            var s = new DocumentSection
            {
                DocumentVersion = ver, ParentSection = padre, SectionKey = key, Title = titolo, Order = ordine,
                Depth = padre is null ? 0 : padre.Depth + 1, RowVersion = Guid.NewGuid().ToByteArray(),
            };
            _db.DocumentSections.Add(s);
            return s;
        }
        var gt = Sez(SectionKeys.TrafficManagement, "Gestione del traffico", null, 1);
        Sez("ifr", "IFR", gt, 1);
        Sez("vfr", "VFR", gt, 2);
        Sez(SectionKeys.NewCustom(), "Traffico IFR in arrivo", null, 2);
        Sez(SectionKeys.NewCustom(), "Gestione del traffico", null, 3);
        await _db.SaveChangesAsync();

        var righe = await Manutenzione().TrafficoDaSistemareAManoAsync();

        Assert.Equal(2, righe.Count);
        Assert.Contains(righe, r => r.Contains("IFR di catalogo vuota") && r.Contains("Traffico IFR in arrivo"));
        Assert.Contains(righe, r => r.Contains("due «Gestione del traffico»"));
    }

    [Fact]
    public async Task Una_gestione_del_traffico_in_ordine_non_da_avvisi()
    {
        var doc = new Document { Type = DocumentType.Vipi, Title = "Pisa Approach", Language = Language.It, LastUpdatedAiracCycle = "2610" };
        var ver = new DocumentVersion { Document = doc, VersionNumber = 1, Status = DocumentStatus.Draft, AiracCycle = "2610" };
        _db.Documents.Add(doc);
        _db.DocumentVersions.Add(ver);
        var gt = new DocumentSection { DocumentVersion = ver, SectionKey = SectionKeys.TrafficManagement, Title = "Gestione del traffico", Order = 1, RowVersion = Guid.NewGuid().ToByteArray() };
        _db.DocumentSections.Add(gt);
        _db.DocumentSections.Add(new DocumentSection { DocumentVersion = ver, ParentSection = gt, SectionKey = "ifr", Title = "IFR", Order = 1, Depth = 1, RowVersion = Guid.NewGuid().ToByteArray() });
        _db.DocumentSections.Add(new DocumentSection { DocumentVersion = ver, SectionKey = SectionKeys.NewCustom(), Title = "Note", Order = 2, RowVersion = Guid.NewGuid().ToByteArray() });
        await _db.SaveChangesAsync();

        Assert.Empty(await Manutenzione().TrafficoDaSistemareAManoAsync());
    }
}
