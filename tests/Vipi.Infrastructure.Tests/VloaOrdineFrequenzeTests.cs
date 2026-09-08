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
    private IVloaDerivationService Servizio(IEditAuthorizationService authz)
    {
        var servizi = new ServiceCollection().AddVipiApplication();
        servizi.AddSingleton(_db);
        servizi.AddSingleton<IVloaDerivationRepository>(new EfVloaDerivationRepository(_db));
        servizi.AddSingleton<IAccDerivationRepository>(new EfAccDerivationRepository(_db));
        servizi.AddSingleton<IDocumentProfileRepository>(_profili);
        servizi.AddSingleton<IAgreementService>(
            new AgreementService(new EfAgreementRepository(_db), authz, new TopologyBuilder(_db)));
        servizi.AddSingleton<ICoordinationSentenceTemplate, StubCoordinationSentenceTemplate>();
        servizi.AddSingleton(authz);
        servizi.AddSingleton<ISectorShapeResolver>(
            new EfSectorShapeResolver(_db, new EfSectorAirspaceBindings(_db), new EfSectorShapeParts(_db)));
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
