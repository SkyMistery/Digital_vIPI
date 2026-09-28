using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Airspace;
using Vipi.Application;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Aor;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// L'ordine delle frequenze di una vLOA è del DOCUMENTO.
///
/// <para>Fino all'8 settembre 2026 la tabella della vLOA usciva nell'ordine in cui il catalogo restituiva i
/// settori confinanti: l'editor poteva solo accendere e spegnere le righe. ACC e APP si riordinavano da un
/// pezzo, con lo stesso componente e lo stesso override per callsign — la vLOA no, e non perché fosse una
/// scelta: mancavano i cinque agganci.</para>
///
/// <para>⚠️ L'override si applica DENTRO ciascun lato. I due lati sono due tabelle con la loro intestazione
/// (home italiano, poi estero): se l'ordine li attraversasse, una riga tunisina finirebbe sotto il titolo
/// «IT - LIRR».</para>
/// </summary>
public class VloaOrdineFrequenzeTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private IVloaDerivationService _service = default!;
    private EfDocumentProfileRepository _profili = default!;
    private int _docId;

    /// <summary>Il servizio è <c>internal</c> e si prende dal contenitore, come in produzione: la prova non
    /// allarga la superficie del modulo per potersi scrivere.</summary>
    private IVloaDerivationService Servizio(IEditAuthorizationService authz, bool lockMio = true)
    {
        var servizi = new ServiceCollection().AddVipiApplication();
        servizi.AddSingleton<IDocumentLockGuard>(new LockFinto(lockMio));
        servizi.AddSingleton(_db);
        servizi.AddSingleton<IVloaDerivationRepository>(new EfVloaDerivationRepository(_db));
        servizi.AddSingleton<IAccDerivationRepository>(new EfAccDerivationRepository(_db));
        servizi.AddSingleton<IDocumentProfileRepository>(_profili);
        servizi.AddSingleton<IAgreementService>(
            new AgreementService(new EfAgreementRepository(_db), authz, new TopologyBuilder(_db), LockDiRisorsaConcesso.Instance));
        servizi.AddSingleton<ICoordinationSentenceTemplate, StubCoordinationSentenceTemplate>();
        servizi.AddSingleton(authz);
        servizi.AddSingleton<ISectorShapeResolver>(
            new EfSectorShapeResolver(_db, new EfSectorAirspaceBindings(_db, LivelloFisso.Editor), new EfSectorShapeParts(_db)));
        servizi.AddSingleton(new ReadingLanguageContext());
        servizi.AddSingleton<IOptions<NeighboursOptions>>(Options.Create(new NeighboursOptions()));
        return servizi.BuildServiceProvider().GetRequiredService<IVloaDerivationService>();
    }

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        await RomaStructureSeed.SeedAsync(_db);
        await RomaVloaSeed.SeedAsync(_db);   // vLOA LIRR ↔ DTTC, con le due parti

        // I poligoni di confine: i due CTR di Roma toccano il CTR di Tunisi lungo il parallelo 40, quindi
        // confinano tutti e tre e la derivazione ha due righe per lato da mettere in fila.
        _db.AccSectors.AddRange(
            Cat("LIRR_NE_CTR", "LIRR", "[[10.0,40.0],[11.0,40.0],[11.0,41.0],[10.0,41.0]]"),
            Cat("LIRR_EW_CTR", "LIRR", "[[11.0,40.0],[12.0,40.0],[12.0,41.0],[11.0,41.0]]"),
            Cat("DTTC_CTR", "DTTC", "[[10.0,39.0],[12.0,39.0],[12.0,40.0],[10.0,40.0]]"));
        await _db.SaveChangesAsync();

        _docId = await _db.Documents.Where(d => d.Type == DocumentType.Vloa).Select(d => d.Id).FirstAsync();

        _profili = new EfDocumentProfileRepository(_db);
        _service = Servizio(new PermettiTutto());
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    /// <summary>Senza override l'ordine resta quello del catalogo: la vLOA di sempre.</summary>
    [Fact]
    public async Task Senza_ordine_salvato_le_righe_restano_come_le_da_il_catalogo()
    {
        var righe = await _service.DeriveFrequenciesAsync(_docId);

        Assert.Equal(new[] { "LIRR_EW_CTR", "LIRR_NE_CTR" }, Lato(righe, foreign: false));
        Assert.Equal(new[] { "DTTC_CTR" }, Lato(righe, foreign: true));
    }

    [Fact]
    public async Task L_ordine_salvato_torna_indietro_dalla_derivazione()
    {
        // ⚠️ L'ordine chiesto è l'OPPOSTO di quello che dà il catalogo (EW, poi NE): con lo stesso non si
        // distinguerebbe una derivazione che legge l'override da una che lo ignora.
        await _service.SaveFrequencyOrderAsync(_docId, new[]
        {
            new AppFreqOrderOverride("LIRR_NE_CTR", 0),
            new AppFreqOrderOverride("LIRR_EW_CTR", 1),
        });

        var righe = await _service.DeriveFrequenciesAsync(_docId);

        Assert.Equal(new[] { "LIRR_NE_CTR", "LIRR_EW_CTR" }, Lato(righe, foreign: false));
    }

    /// <summary>
    /// ⚠️ L'override di un lato non tira righe dall'altro. L'indice è globale (home in fila, poi estero) proprio
    /// perché due callsign di lati diversi non si contendano lo stesso posto, ma l'applicazione è per lato.
    /// </summary>
    [Fact]
    public async Task Il_lato_estero_resta_dov_e()
    {
        await _service.SaveFrequencyOrderAsync(_docId, new[]
        {
            new AppFreqOrderOverride("DTTC_CTR", 0),          // il primo posto in assoluto…
            new AppFreqOrderOverride("LIRR_NE_CTR", 1),
            new AppFreqOrderOverride("LIRR_EW_CTR", 2),
        });

        var righe = await _service.DeriveFrequenciesAsync(_docId);

        Assert.Equal(new[] { "LIRR_NE_CTR", "LIRR_EW_CTR" }, Lato(righe, foreign: false));
        Assert.Equal(new[] { "DTTC_CTR" }, Lato(righe, foreign: true));   // …resta comunque sotto la SUA intestazione
    }

    /// <summary>Il riordino è una scrittura sul documento: dietro lo stesso cancello del resto dell'editoriale.</summary>
    [Fact]
    public async Task Senza_i_permessi_l_ordine_non_si_scrive()
    {
        var servizio = Servizio(new SoloLettura());

        await Assert.ThrowsAsync<EditNotAllowedException>(() =>
            servizio.SaveFrequencyOrderAsync(_docId, new[] { new AppFreqOrderOverride("LIRR_EW_CTR", 0) }));

        Assert.Empty((await _profili.GetAsync(_docId)).FreqOrder);
    }

    /// <summary>
    /// U-051 (revisione totale 3): ordine e «nascondi» della vLOA scrivevano il profilo del documento senza
    /// chiedere il lock. Riprodotto: la pagina che l'aveva perso ha riscritto HiddenAorSectorsJson (vLOA 65)
    /// sopra il lavoro di chi l'aveva preso dopo. Gemello di T-004 (APP, ACC, vSOP militare).
    /// </summary>
    [Fact]
    public async Task Senza_il_lock_ordine_e_nascondi_non_si_scrivono()
    {
        var servizio = Servizio(new PermettiTutto(), lockMio: false);

        await Assert.ThrowsAsync<EditConflictException>(() =>
            servizio.SaveFrequencyOrderAsync(_docId, new[] { new AppFreqOrderOverride("LIRR_EW_CTR", 0) }));
        await Assert.ThrowsAsync<EditConflictException>(() => servizio.ToggleAorSectorAsync(_docId, "LIRR_EW_CTR"));
        await Assert.ThrowsAsync<EditConflictException>(() => servizio.ToggleFrequencyAsync(_docId, "LIRR_EW_CTR"));

        _db.ChangeTracker.Clear();
        Assert.Empty((await _profili.GetAsync(_docId)).FreqOrder);
        var stato = await new EfVloaDerivationRepository(_db).LoadEditorialAsync(_docId);
        Assert.Empty(stato.HiddenAorSectors);
        Assert.Empty(stato.HiddenFrequencies);
    }

    /// <summary>
    /// 🔴 U-155 (revisione totale 3): un accordo di confine riguarda tutte e due le ACC, e la vLOA leggeva gli
    /// accordi di ciascuna: lo stesso accordo entrava due volte, nascosto solo dal collasso della tabella.
    /// </summary>
    [Fact]
    public async Task Un_accordo_di_confine_entra_una_volta_sola()
    {
        var repo = new EfAgreementRepository(_db);
        var ne = await _db.Sectors.Where(s => s.Callsign == "LIRR_NE_CTR").Select(s => s.Id).SingleAsync();
        var tunisi = await _db.Sectors.Where(s => s.Callsign == "DTTC_CTR").Select(s => s.Id).SingleAsync();
        Assert.True(ne < tunisi);   // A è il minore: il verso AtoB è Roma → Tunisi
        var accordo = await repo.AddAgreementAsync("LIRR", new AgreementInput { SideASectorId = ne, SideBSectorId = tunisi });
        var sezione = await repo.AddSectionAsync("LIRR", accordo, new AgreementSectionInput
        {
            Kind = TransferFlowKind.Overflight, Direction = AgreementDirection.AtoB,
        });
        await repo.AddClauseAsync("LIRR", sezione, new AgreementClauseInput
        {
            Cops = "VALMA", LevelValue = 240, LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.Exact,
        });

        var coord = await _service.DeriveCoordinationAsync(_docId);

        var righe = coord.HomeToForeign.Sectors.SelectMany(s => s.Accs)
            .SelectMany(a => a.Airports.SelectMany(x => x.Arrivals.Concat(x.Departures))
                .Concat(a.Extras.SelectMany(x => x.Rows)))
            .ToList();
        Assert.Single(righe, r => r.Cop == "VALMA");
    }

    /// <summary>
    /// 🔴 U-060 (revisione totale 3), scelta del committente: con la controparte sparita (settore disattivato) la
    /// vIPI continua a stampare l'accordo, e la vLOA lo toglieva — due documenti che raccontano la stessa coppia
    /// in due modi. Ora lo stampa anche la vLOA, e la segnalazione «da rivedere» chiede all'editor di decidere.
    /// </summary>
    [Fact]
    public async Task Con_la_controparte_disattivata_la_vLOA_continua_a_stampare_l_accordo()
    {
        var repo = new EfAgreementRepository(_db);
        var ne = await _db.Sectors.Where(s => s.Callsign == "LIRR_NE_CTR").Select(s => s.Id).SingleAsync();
        var tunisi = await _db.Sectors.SingleAsync(s => s.Callsign == "DTTC_CTR");
        var accordo = await repo.AddAgreementAsync("LIRR", new AgreementInput { SideASectorId = ne, SideBSectorId = tunisi.Id });
        var sezione = await repo.AddSectionAsync("LIRR", accordo, new AgreementSectionInput
        {
            Kind = TransferFlowKind.Overflight, Direction = AgreementDirection.AtoB,
        });
        await repo.AddClauseAsync("LIRR", sezione, new AgreementClauseInput
        {
            Cops = "VALMA", LevelValue = 240, LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.Exact,
        });
        tunisi.IsActive = false;
        await _db.SaveChangesAsync();

        var coord = await _service.DeriveCoordinationAsync(_docId);

        var righe = coord.HomeToForeign.Sectors.SelectMany(s => s.Accs)
            .SelectMany(a => a.Airports.SelectMany(x => x.Arrivals.Concat(x.Departures))
                .Concat(a.Extras.SelectMany(x => x.Rows)))
            .ToList();
        Assert.Single(righe, r => r.Cop == "VALMA");
    }

    private sealed class LockFinto(bool mio) : IDocumentLockGuard
    {
        public Task EnsureMineAsync(int documentId, CancellationToken ct = default) =>
            mio ? Task.CompletedTask : throw new EditConflictException("lock di un altro");
    }

    private static string[] Lato(VloaFreqData dati, bool foreign) =>
        dati.Rows.Where(r => r.IsForeign == foreign).Select(r => r.Row.Callsign).ToArray();

    private static AccSector Cat(string compose, string acc, string poly) => new()
    {
        ComposePosition = compose, CenterId = acc, Position = "CTR", RegionMapPolygon = poly,
    };

    private sealed class PermettiTutto : IEditAuthorizationService
    {
        public bool IsAdmin => true;
        public VipiRole Role => VipiRole.Admin;
        public int? CurrentUserId => 1;
        public string? CurrentName => "test";
    }

    private sealed class SoloLettura : IEditAuthorizationService
    {
        public bool IsAdmin => false;
        public VipiRole Role => VipiRole.User;
        public int? CurrentUserId => 2;
        public string? CurrentName => "lettore";
    }
}
