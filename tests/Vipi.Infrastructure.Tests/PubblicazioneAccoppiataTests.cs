using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// La pubblicazione di un'unione di documenti (carta <c>docs/feature/2026-09-03-documenti-uniti.md</c> §6):
/// un clic, N release, <b>stesso ciclo e stessa data efficace</b>.
///
/// <para>⚠️ La cosa che questi test tengono ferma non è «esce una release in più»: è che le due escano
/// <b>insieme o per niente</b>. <c>SaveReleaseAsync</c> fa un <c>SaveChanges</c> per chiamata e
/// <c>VersionNumber</c> è <c>max+1</c> letto in memoria sotto un indice UNICO — senza transazione un secondo
/// membro che collide lascerebbe il primo pubblicato da solo, cioè mezza unione a un ciclo e mezza a un
/// altro. È lo stato incoerente che l'accoppiamento doveva togliere.</para>
///
/// <para>Lo scenario è quello vero, preso dall'archivio: <b>LIMN Cameri</b>, che ha la vIPI civile e il vSOP
/// militare dello stesso scalo — e quindi la <b>stessa</b> chiave di release, l'ICAO, con due tipi diversi.</para>
/// </summary>
public class PubblicazioneAccoppiataTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private int _civileId, _militareId;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        var acc = new Acc { Code = "LIMM", Name = "Milano" };
        _db.Accs.Add(acc);

        _civileId = (await CreaDocumentoAsync("vIPI — LIMN Cameri", DocumentEdition.Civil)).Id;
        _militareId = (await CreaDocumentoAsync("vSOP MIL — LIMN Cameri", DocumentEdition.Military)).Id;

        _db.Airports.Add(new Airport
        {
            Icao = "LIMN", Name = "Cameri", Acc = acc,
            DocumentId = _civileId, MilDocumentId = _militareId,
            HasMilitaryPresence = true,
        });
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private async Task<Document> CreaDocumentoAsync(string titolo, DocumentEdition edizione)
    {
        var doc = new Document
        {
            Type = DocumentType.Vipi, Title = titolo, Language = Language.It, Edition = edizione,
            Status = DocumentStatus.Published, LastUpdatedAiracCycle = "2609",
        };
        var ver = new DocumentVersion
        {
            Document = doc, VersionNumber = 1, Status = DocumentStatus.Published,
            AiracCycle = "2609", CreatedUtc = DateTime.UtcNow,
        };
        doc.Versions.Add(ver);
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        _db.DocumentSections.Add(new DocumentSection
        {
            DocumentVersion = ver, Title = titolo, Order = 1, Depth = 0, SectionKey = "custom",
            RowVersion = Guid.NewGuid().ToByteArray(),
        });
        doc.CurrentVersionId = ver.Id;
        await _db.SaveChangesAsync();
        return doc;
    }

    private EfDocumentUnionRepository Unioni() => new(_db);

    private ReleaseService Servizio(IDocumentUnionRepository? unioni)
    {
        var registry = TestReleaseTargets.Registry(_db);
        var media = new EfMediaMaintenance(_db);
        var airac = new Vipi.Domain.Services.AiracService();
        return new ReleaseService(
            new EfReleaseRepository(_db, registry, media), new AllowAuthz(), airac,
            new FrozenSectionRegistry(Array.Empty<IFrozenSectionProvider>()),
            new EfDocumentAdminRepository(_db, registry, new EfReleaseRepository(_db, registry, media), media),
            new EfEditingRepository(_db, airac, media), registry,
            Microsoft.Extensions.Options.Options.Create(new Vipi.Application.ReleaseRetentionOptions()),
            new EfUnitOfWork(_db), unioni);
    }

    private Task<List<DocRelease>> ReleaseAsync() =>
        _db.DocReleases.AsNoTracking().OrderBy(r => r.TargetType).ToListAsync();

    [Fact]
    public async Task Senza_unione_pubblica_UN_documento_solo()
    {
        // La porta dell'unione è la stessa che usano i pannelli SEMPRE: su un documento non unito deve essere
        // esattamente PublishAsync, o passare di lì cambierebbe il comportamento di ogni editor del sito.
        await Servizio(Unioni()).PublishUnionAsync(ReleaseTargetType.Airport, "LIMN", "2610", null);

        var rel = await ReleaseAsync();
        Assert.Single(rel);
        Assert.Equal(ReleaseTargetType.Airport, rel[0].TargetType);
    }

    [Fact]
    public async Task Uniti_un_clic_pubblica_TUTTI_allo_stesso_ciclo_e_alla_stessa_data()
    {
        await Unioni().CreateAsync(_militareId, _civileId, createdByUserId: 42);

        await Servizio(Unioni()).PublishUnionAsync(ReleaseTargetType.AirportMil, "LIMN", "2610", "insieme");

        var rel = await ReleaseAsync();
        Assert.Equal(2, rel.Count);
        // ⚠️ Stessa chiave (l'ICAO), TIPI diversi: è il fatto su cui poggiano le due edizioni dello stesso
        // scalo, e la coppia (TargetType, TargetKey) è ciò che le tiene distinte.
        Assert.Equal(new[] { "LIMN", "LIMN" }, rel.Select(r => r.TargetKey));
        Assert.Equal(2, rel.Select(r => r.TargetType).Distinct().Count());
        // Il ciclo è lo stesso per costruzione, e la data efficace pure: la calcola AiracService dal ciclo.
        Assert.Single(rel.Select(r => r.ReleaseAiracCycle).Distinct());
        Assert.Single(rel.Select(r => r.ReleaseEffectiveUtc).Distinct());
    }

    [Fact]
    public async Task Uniti_si_pubblica_partendo_da_QUALUNQUE_membro()
    {
        await Unioni().CreateAsync(_militareId, _civileId, 0);

        // Chi preme sta guardando la pagina unita, ma il pannello è keyed sul documento che ha in mano —
        // e quale dei due sia non deve cambiare l'esito.
        await Servizio(Unioni()).PublishUnionNowAsync(ReleaseTargetType.Airport, "LIMN", null);

        Assert.Equal(2, (await ReleaseAsync()).Count);
    }

    [Fact]
    public async Task Pubblica_ORA_promuove_la_bozza_di_OGNI_membro()
    {
        await Unioni().CreateAsync(_militareId, _civileId, 0);
        await BozzaAsync(_civileId);
        await BozzaAsync(_militareId);

        await Servizio(Unioni()).PublishUnionNowAsync(ReleaseTargetType.AirportMil, "LIMN", null);

        // ⚠️ Le due semantiche restano diverse anche unite: la «pubblica ora» promuove, la pianificata no.
        Assert.Empty(await _db.DocumentVersions.Where(v => v.Status == DocumentStatus.Draft).ToListAsync());
    }

    [Fact]
    public async Task La_PIANIFICATA_non_promuove_nessuna_bozza()
    {
        await Unioni().CreateAsync(_militareId, _civileId, 0);
        await BozzaAsync(_civileId);
        await BozzaAsync(_militareId);

        await Servizio(Unioni()).PublishUnionAsync(ReleaseTargetType.AirportMil, "LIMN", "2612", null);

        Assert.Equal(2, await _db.DocumentVersions.CountAsync(v => v.Status == DocumentStatus.Draft));
        Assert.Equal(2, (await ReleaseAsync()).Count);
    }

    [Fact]
    public async Task Un_lock_ALTRUI_su_un_solo_membro_ferma_TUTTA_la_pubblicazione()
    {
        await Unioni().CreateAsync(_militareId, _civileId, 0);
        var civile = await _db.Documents.FirstAsync(d => d.Id == _civileId);
        civile.LockedByUserId = 999;
        civile.LockedByName = "un altro editor";
        civile.LockedAtUtc = DateTime.UtcNow;
        civile.LockExpiresUtc = DateTime.UtcNow.AddMinutes(30);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => Servizio(Unioni()).PublishUnionAsync(ReleaseTargetType.AirportMil, "LIMN", "2610", null));

        // ⚠️ E NIENTE è stato scritto: mezza unione pubblicata è peggio di nessuna, e i cancelli girano tutti
        // PRIMA di qualunque scrittura proprio per questo.
        Assert.Empty(await ReleaseAsync());
    }

    [Fact]
    public async Task Annullare_una_release_annulla_le_SORELLE_dello_stesso_ciclo()
    {
        await Unioni().CreateAsync(_militareId, _civileId, 0);
        await Servizio(Unioni()).PublishUnionAsync(ReleaseTargetType.AirportMil, "LIMN", "2610", null);
        var militare = (await ReleaseAsync()).First(r => r.TargetType == ReleaseTargetType.AirportMil);

        await Servizio(Unioni()).CancelReleaseAsync(militare.Id);

        // Simmetrico alla pubblicazione: annullarne una sola lascerebbe metà unione in vigore a quel ciclo e
        // metà no — la desincronizzazione che l'accoppiamento doveva togliere.
        Assert.Empty(await ReleaseAsync());
    }

    [Fact]
    public async Task Annullare_NON_tocca_i_cicli_diversi()
    {
        await Unioni().CreateAsync(_militareId, _civileId, 0);
        var svc = Servizio(Unioni());
        await svc.PublishUnionAsync(ReleaseTargetType.AirportMil, "LIMN", "2610", null);
        await svc.PublishUnionAsync(ReleaseTargetType.AirportMil, "LIMN", "2611", null);
        var da2610 = (await ReleaseAsync()).First(r => r.ReleaseAiracCycle == "2610");

        await svc.CancelReleaseAsync(da2610.Id);

        // Restano le due del 2611: annullare un ciclo non è annullare la storia.
        var rimaste = await ReleaseAsync();
        Assert.Equal(2, rimaste.Count);
        Assert.All(rimaste, r => Assert.Equal("2611", r.ReleaseAiracCycle));
    }

    [Fact]
    public async Task I_bersagli_si_dicono_PRIMA_col_titolo_e_col_lock()
    {
        await Unioni().CreateAsync(_militareId, _civileId, 0);
        var civile = await _db.Documents.FirstAsync(d => d.Id == _civileId);
        civile.LockedByUserId = 7;
        civile.LockedByName = "Chi Sta Scrivendo";
        civile.LockExpiresUtc = DateTime.UtcNow.AddMinutes(30);
        await _db.SaveChangesAsync();

        var bersagli = await Servizio(Unioni()).BersagliUnitiAsync(ReleaseTargetType.AirportMil, "LIMN");

        // Un esito che tace metà del lavoro è peggio di nessun esito: qui la metà taciuta sarebbe un ALTRO
        // documento pubblicato, e chi lo tiene fermo.
        Assert.Equal(2, bersagli.Count);
        Assert.Equal(ReleaseTargetType.AirportMil, bersagli[0].Type);   // l'ospite per primo
        var bloccato = Assert.Single(bersagli, b => b.LockedByUserId is not null);
        Assert.Equal("Chi Sta Scrivendo", bloccato.LockedByName);
        Assert.Contains("Cameri", bloccato.Titolo);
    }

    private async Task BozzaAsync(int documentId)
    {
        var doc = await _db.Documents.Include(d => d.Versions).FirstAsync(d => d.Id == documentId);
        var ver = new DocumentVersion
        {
            DocumentId = documentId, VersionNumber = doc.Versions.Max(v => v.VersionNumber) + 1,
            Status = DocumentStatus.Draft, AiracCycle = "2609", CreatedUtc = DateTime.UtcNow,
        };
        _db.DocumentVersions.Add(ver);
        await _db.SaveChangesAsync();
        _db.DocumentSections.Add(new DocumentSection
        {
            DocumentVersionId = ver.Id, Title = "bozza", Order = 1, Depth = 0, SectionKey = "custom",
            RowVersion = Guid.NewGuid().ToByteArray(),
        });
        await _db.SaveChangesAsync();
    }

    private sealed class AllowAuthz : IEditAuthorizationService
    {
        public VipiRole Role => VipiRole.Admin;
        public bool IsAdmin => true;
        public int? CurrentUserId => 1;
        public string? CurrentName => "test";
        public void EnsureAdmin() { }
    }
}
