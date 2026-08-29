using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vipi.Application.Aor;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Una coppia, una vLOA.
///
/// <para>Il contratto di <c>FindVloaIdByPairAsync</c> lo dichiarava dal primo giorno — «una sola vLOA per
/// coppia ACC↔ACC» — e nessuno lo imponeva. La generazione da <c>/services/vsop/admin/neighbours</c> è idempotente
/// per parti; <c>/services/vsop/editor/new-document</c>, che crea la stessa cosa, non lo era: due porte con due
/// politiche.</para>
///
/// <para>⚠️ E il resto dell'applicazione non sa gestirne due: <c>FindVloaIdByPairAsync</c> fa
/// <c>FirstOrDefault</c>, quindi con due documenti sulla stessa coppia l'editor ne apre uno <b>senza un
/// criterio</b> e l'altro resta invisibile — pur potendo avere release pubblicate.</para>
/// </summary>
public class VloaUnaPerCoppiaTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfEditingRepository _repo = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        await RomaStructureSeed.SeedAsync(_db);
        await RomaContentSeed.SeedAsync(_db);
        await RomaVloaSeed.SeedAsync(_db);   // porta una vLOA LIRR ↔ DTTC
        _repo = new EfEditingRepository(_db, new AiracService(), new EfMediaMaintenance(_db));
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private EditingService Servizio() => new(_repo, new PermettiTutto(),
        Options.Create(new Vipi.Application.ReleaseRetentionOptions()));

    private async Task<int> SettoreAsync(string callsign) =>
        await _db.Sectors.Where(s => s.Callsign == callsign).Select(s => s.Id).FirstAsync();

    [Fact]
    public async Task Una_seconda_vLOA_sulla_stessa_coppia_non_si_crea()
    {
        var home = await SettoreAsync("LIRR_NE_CTR");
        var estero = await SettoreAsync("DTTC_CTR");
        var quante = await _db.Documents.CountAsync(d => d.Type == DocumentType.Vloa);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Servizio().CreateDocumentAsync(DocumentType.Vloa, "vLOA Roma–Tunisi (bis)",
                scopeSectorIds: null, primarySectorId: null, homeSectorId: home, neighbourSectorId: estero));

        // Il messaggio deve NOMINARE quello che c'è già: chi ha appena scritto un titolo deve capire perché
        // non è stato usato, e dove andare.
        Assert.Contains("LIRR", ex.Message);
        Assert.Contains("DTTC", ex.Message);
        Assert.Equal(quante, await _db.Documents.CountAsync(d => d.Type == DocumentType.Vloa));
    }

    /// <summary>
    /// ⚠️ La <b>direzione conta</b>: LIRR→DTTC e DTTC→LIRR sono due vLOA legittime, una per lato. La guardia
    /// non deve confondere le due — sarebbe passare da «se ne creano infinite» a «la seconda non si crea
    /// mai», che è un difetto peggiore perché toglie un documento vero.
    /// </summary>
    [Fact]
    public async Task La_coppia_inversa_resta_una_vLOA_diversa()
    {
        var home = await SettoreAsync("LIRR_NE_CTR");
        var estero = await SettoreAsync("DTTC_CTR");

        var id = await Servizio().CreateDocumentAsync(DocumentType.Vloa, "vLOA Tunisi–Roma",
            scopeSectorIds: null, primarySectorId: null, homeSectorId: estero, neighbourSectorId: home);

        Assert.True(id > 0);
        Assert.Equal(2, await _db.Documents.CountAsync(d => d.Type == DocumentType.Vloa));
    }

    [Fact]
    public async Task Una_coppia_nuova_si_crea_normalmente()
    {
        var home = await SettoreAsync("LIRR_NE_CTR");
        var altroEstero = await NuovoSettoreEsteroAsync("LFFF", "Paris ACC", "LFFF_CTR");

        var id = await Servizio().CreateDocumentAsync(DocumentType.Vloa, "vLOA Roma–Parigi",
            scopeSectorIds: null, primarySectorId: null, homeSectorId: home, neighbourSectorId: altroEstero);

        Assert.True(id > 0);
        Assert.Equal(2, await _db.Documents.CountAsync(d => d.Type == DocumentType.Vloa));
    }

    /// <summary>
    /// ⚠️ Una vLOA nasce con la struttura del <b>catalogo</b>, da qualunque porta la si crei.
    ///
    /// <para>Prima da <c>/services/vsop/editor/new-document</c> nasceva con una sezione sola — «Scopo e validità», per
    /// giunta con una chiave <i>libera</i> che non è nessuna delle sette del profilo — mentre la stessa vLOA
    /// generata da «ACC confinanti» nasceva con le canoniche. Due porte, due risultati, e da questa usciva
    /// un documento fuori catalogo.</para>
    /// </summary>
    [Fact]
    public async Task La_vLOA_nasce_con_le_sezioni_del_catalogo()
    {
        var home = await SettoreAsync("LIRR_NE_CTR");
        var estero = await NuovoSettoreEsteroAsync("LFFF", "Paris ACC", "LFFF_CTR");

        var id = await Servizio().CreateDocumentAsync(DocumentType.Vloa, "vLOA Roma–Parigi",
            scopeSectorIds: null, primarySectorId: null, homeSectorId: home, neighbourSectorId: estero);

        var versione = await _db.DocumentVersions.Where(v => v.DocumentId == id).Select(v => v.Id).FirstAsync();
        var chiavi = await _db.DocumentSections
            .Where(s => s.DocumentVersionId == versione && s.ParentSectionId == null)
            .OrderBy(s => s.Order).Select(s => s.SectionKey).ToListAsync();

        var attese = SectionCatalog.For(SectionProfile.Vloa).OrderBy(d => d.Order).Select(d => d.Key).ToList();
        Assert.Equal(attese, chiavi);

        // E nessuna chiave libera: era il segno che questa porta non conosceva il catalogo.
        Assert.DoesNotContain(chiavi, k => k.StartsWith("custom", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Le vIPI di questo percorso non cambiano: la loro struttura la fa l'editor.</summary>
    [Fact]
    public async Task La_vIPI_di_questo_percorso_resta_com_era()
    {
        var settore = await SettoreAsync("LIRR_NE_CTR");
        var libero = new Sector
        {
            AccId = await _db.Sectors.Where(s => s.Id == settore).Select(s => s.AccId).FirstAsync(),
            Callsign = "LIRR_ZZ_CTR", Name = "Prova", Type = SectorType.Ctr, Kind = SectorKind.Acc,
        };
        _db.Sectors.Add(libero);
        await _db.SaveChangesAsync();

        var id = await Servizio().CreateDocumentAsync(DocumentType.Vipi, "vIPI di prova",
            scopeSectorIds: new[] { libero.Id }, primarySectorId: libero.Id, homeSectorId: null, neighbourSectorId: null);

        var versione = await _db.DocumentVersions.Where(v => v.DocumentId == id).Select(v => v.Id).FirstAsync();
        var sezione = await _db.DocumentSections.SingleAsync(s => s.DocumentVersionId == versione);
        Assert.Equal("Scopo e validità", sezione.Title);
    }

    /// <summary>Un settore Neighbour inesistente si ferma prima, e con un messaggio suo: la guardia della
    /// coppia non deve mangiarsi la validazione che c'era già.</summary>
    [Fact]
    public async Task Un_settore_neighbour_inesistente_lo_dice()
    {
        var home = await SettoreAsync("LIRR_NE_CTR");

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Servizio().CreateDocumentAsync(DocumentType.Vloa, "vLOA fantasma",
                scopeSectorIds: null, primarySectorId: null, homeSectorId: home, neighbourSectorId: 99001));

        Assert.Contains("Neighbour", ex.Message);
    }

    /// <summary>
    /// ⚠️ <b>Le due porte devono fare la STESSA domanda.</b> «Nuovo documento» lascia scegliere QUALUNQUE
    /// settore d'area dell'ACC come Home; la generazione da «ACC confinanti» sceglie da sé la radice. Finché
    /// quest'ultima confrontava i <b>SectorId</b>, bastava che la prima vLOA fosse nata sull'altro settore
    /// perché non la trovasse: nasceva la SECONDA vLOA sulla stessa coppia di ACC.
    ///
    /// <para>Da lì in poi le due non si vedono più: <c>FindVloaIdByPairAsync</c> — come l'editor e il
    /// pubblico trovano la vLOA di una coppia — fa <c>FirstOrDefault</c> per codice ACC e ne apre una senza
    /// un criterio. L'altra resta invisibile pur potendo avere release pubblicate.</para>
    /// </summary>
    [Fact]
    public async Task La_generazione_da_confinanti_RIUSA_la_vLOA_creata_su_un_ALTRO_settore_dello_stesso_ACC()
    {
        // ⚠️ NON la radice: `LIRR_TS_CTR` è un settore d'area figlio, e «Nuovo documento» lo offre come Home
        // esattamente come gli altri. È il caso che la generazione in blocco non vedeva.
        var homeScelto = await SettoreAsync("LIRR_TS_CTR");
        var estero = await NuovoSettoreEsteroAsync("LFFF", "Paris ACC", "LFFF_CTR");

        var daNuovoDocumento = await Servizio().CreateDocumentAsync(DocumentType.Vloa, "vLOA Roma–Parigi",
            scopeSectorIds: null, primarySectorId: null, homeSectorId: homeScelto, neighbourSectorId: estero);

        // La radice che sceglierebbe la generazione in blocco: se fosse lo STESSO settore, questo test non
        // proverebbe niente — quindi lo si verifica invece di sperarci.
        var radice = await _db.Sectors
            .Where(x => x.Acc!.Code == "LIRR" && x.Kind == SectorKind.Acc)
            .OrderBy(x => x.ParentSectorId == null ? 0 : 1).ThenBy(x => x.CoverageOrder)
            .Select(x => x.Id).FirstAsync();
        Assert.NotEqual(homeScelto, radice);

        _db.NeighbourCandidates.Add(new NeighbourCandidate
        {
            HomeAccCode = "LIRR", ForeignAccCode = "LFFF", ForeignAccName = "Paris ACC",
            CountryId = "FR", ForeignRootCallsign = "LFFF_CTR",
        });
        await _db.SaveChangesAsync();
        var candidato = await _db.NeighbourCandidates.Where(c => c.ForeignAccCode == "LFFF")
            .Select(c => c.Id).FirstAsync();

        var quante = await _db.Documents.CountAsync(d => d.Type == DocumentType.Vloa);
        var generata = await new EfNeighbourRepository(_db, new AiracService())
            .MaterializeAndCreateVloaAsync(candidato);

        Assert.Equal(daNuovoDocumento, generata);
        Assert.Equal(quante, await _db.Documents.CountAsync(d => d.Type == DocumentType.Vloa));
    }

    private async Task<int> NuovoSettoreEsteroAsync(string accCode, string accName, string callsign)
    {
        var acc = new Acc { Code = accCode, Name = accName, CountryPrefix = accCode[..2], IsForeign = true };
        _db.Accs.Add(acc);
        await _db.SaveChangesAsync();
        var s = new Sector { AccId = acc.Id, Callsign = callsign, Name = accName, Type = SectorType.Ctr, Kind = SectorKind.Acc };
        _db.Sectors.Add(s);
        await _db.SaveChangesAsync();
        return s.Id;
    }

    private sealed class PermettiTutto : Vipi.Application.Auth.IEditAuthorizationService
    {
        public bool IsAdmin => true;
        public VipiRole Role => IsAdmin ? VipiRole.Admin : VipiRole.User;
        public int? CurrentUserId => 111;
        public string? CurrentName => "test";
        public void EnsureAdmin() { }
    }
}
