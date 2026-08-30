using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Airspace;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// <b>La porta unica</b> per la forma di un settore (carta
/// <c>docs/refactor/15-shape-del-settore-una-porta-sola.md</c> §3c): anello <b>e</b> quote, sempre della
/// stessa fonte, con una precedenza che vive in un posto solo.
///
/// <para>⚠️ Il test che conta più di tutti è quello dei <b>gradini</b>: un aggancio che non si risolve, o
/// una fonte muta, devono far <b>scendere</b> al gradino sotto — mai lasciare il settore senza area. È la
/// lezione del 26 agosto 2026, e qui è una proprietà del risolutore, non un'attenzione di chi lo chiama.</para>
/// </summary>
public class SectorShapeResolverTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfSectorAirspaceBindings _agganci = default!;
    private EfSectorShapeParts _pezzi = default!;
    private EfAirspaceCatalog _catalogo = default!;
    private EfSectorShapeResolver _risolutore = default!;
    private int _idApp;

    private const string App = "LICC_APP";
    private const string MonobloccoIvao = "[[14.5,36.5],[16.0,36.5],[16.0,38.0],[14.5,38.0]]";

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();

        var acc = new Acc { Code = "LICC", Name = "Catania" };
        _db.Accs.Add(acc);
        _db.Airports.Add(new Airport { Icao = "LICC", Name = "Catania Fontanarossa", Acc = acc });
        _db.AirportSectors.Add(new AirportSector
        {
            ComposePosition = App, AirportIcao = "LICC", AccCode = "LICC", Position = "APP",
            RegionMapPolygon = MonobloccoIvao, LowerLimit = 0, UpperLimit = 19500,
        });
        await _db.SaveChangesAsync();
        _idApp = (await _db.AirportSectors.FirstAsync(x => x.ComposePosition == App)).Id;

        _agganci = new EfSectorAirspaceBindings(_db);
        _pezzi = new EfSectorShapeParts(_db);
        _catalogo = new EfAirspaceCatalog(_db);
        _risolutore = new EfSectorShapeResolver(_db, _agganci, _pezzi);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private static string Kml() => """
        <?xml version="1.0" encoding="UTF-8"?>
        <kml xmlns="http://www.opengis.net/kml/2.2"><Document>
          <Placemark><ExtendedData><SchemaData>
            <SimpleData name="Name">CATANIA CTR Z1</SimpleData>
            <SimpleData name="Category">Control Traffic Region</SimpleData>
            <SimpleData name="Base">GND</SimpleData><SimpleData name="Top">FL105</SimpleData>
          </SchemaData></ExtendedData>
          <Polygon><outerBoundaryIs><LinearRing><coordinates>
            15.0,37.0,0 15.4,37.0,0 15.4,37.4,0 15.0,37.4,0 15.0,37.0,0
          </coordinates></LinearRing></outerBoundaryIs></Polygon></Placemark>
          <Placemark><ExtendedData><SchemaData>
            <SimpleData name="Name">CATANIA CTR Z2</SimpleData>
            <SimpleData name="Category">Control Traffic Region</SimpleData>
            <SimpleData name="Base">7000 FT AMSL</SimpleData><SimpleData name="Top">FL195</SimpleData>
          </SchemaData></ExtendedData>
          <Polygon><outerBoundaryIs><LinearRing><coordinates>
            15.5,37.0,2134 15.9,37.0,2134 15.9,37.4,2134 15.5,37.4,2134 15.5,37.0,2134
          </coordinates></LinearRing></outerBoundaryIs></Polygon></Placemark>
        </Document></kml>
        """;

    private async Task<IReadOnlyList<AirspaceVolumeRow>> CaricaAsync()
    {
        var kml = Kml();
        await _catalogo.SaveAsync(
            new NewAirspaceImport("it.kmz", System.Text.Encoding.UTF8.GetBytes(kml), "2609", 1, "Chi carica"),
            AirspaceKmlReader.LeggiKml(kml), DateTime.UtcNow);
        return await _catalogo.ListVolumesAsync(new AirspaceVolumeQuery());
    }

    private async Task<SectorShape> RisolviAsync() => (await _risolutore.ResolveAsync(new[] { App }))[App];

    /// <summary>Senza niente addosso, la forma è quella del catalogo: un anello e le due quote di IVAO.</summary>
    [Fact]
    public async Task Senza_aggancio_e_senza_pezzi_vale_il_catalogo_di_ivao()
    {
        var forma = await RisolviAsync();

        Assert.Equal(ShapeSource.Source, forma.Source);
        var pezzo = Assert.Single(forma.Parts);
        Assert.Equal(MonobloccoIvao, pezzo.PolygonJson);
        Assert.Equal(0, pezzo.BaseFeet);
        Assert.Equal(19_500, pezzo.TopFeet);
    }

    /// <summary>
    /// Agganciato: due pezzi, ognuno con la <b>sua</b> banda e il testo del file accanto — e non l'inviluppo,
    /// che su questo caso vero coinciderebbe col monoblocco di IVAO.
    /// </summary>
    [Fact]
    public async Task Con_l_aggancio_vince_l_aip_e_ogni_pezzo_porta_la_sua_banda()
    {
        var volumi = await CaricaAsync();
        await _agganci.SetAsync(SourceCatalog.AirportPosition, _idApp, App,
            volumi.Select(v => new AirspaceVolumeKey(v.NaturalKey, v.Ordinal)).ToList(), 1, "Chi sceglie");

        var forma = await RisolviAsync();

        Assert.Equal(ShapeSource.Aip, forma.Source);
        Assert.True(forma.FromAip);
        Assert.Equal(2, forma.Parts.Count);
        // ⚠️ Il lettore del KMZ scrive GND come 0 piedi col datum `Gnd`, non come null: il datum e il
        // testo grezzo sono quel che dice «è il suolo», non il numero.
        Assert.Equal(new int?[] { 0, 7_000 }, forma.Parts.Select(p => p.BaseFeet).ToArray());
        Assert.Equal(AirspaceDatum.Gnd, forma.Parts[0].BaseDatum);
        Assert.Equal(new int?[] { 10_500, 19_500 }, forma.Parts.Select(p => p.TopFeet).ToArray());
        Assert.Equal("GND", forma.Parts[0].BaseRaw);
        Assert.Equal("FL195", forma.Parts[1].TopRaw);
        // Il pezzo sa da quale volume viene: è la chiave naturale, la stessa che cita l'aggancio.
        Assert.All(forma.Parts, p => Assert.False(string.IsNullOrWhiteSpace(p.SourceRef)));
    }

    /// <summary>
    /// ⚠️ Il gradino: un aggancio <b>scoperto</b> (il file nuovo non porta più quel volume) non lascia il
    /// settore senza area — si scende a IVAO, e la pagina può dire quale aggancio è rimasto senza volume.
    /// </summary>
    [Fact]
    public async Task Un_aggancio_scoperto_scende_al_gradino_sotto_e_lo_dichiara()
    {
        var volumi = await CaricaAsync();
        await _agganci.SetAsync(SourceCatalog.AirportPosition, _idApp, App,
            volumi.Select(v => new AirspaceVolumeKey(v.NaturalKey, v.Ordinal)).ToList(), 1, "Chi sceglie");

        // Il caricamento in vigore perde i volumi: l'aggancio resta, e resta scoperto.
        var inVigore = await _db.AirspaceImports.FirstAsync(i => i.IsCurrent);
        _db.AirspaceVolumes.RemoveRange(_db.AirspaceVolumes.Where(v => v.ImportId == inVigore.Id));
        await _db.SaveChangesAsync();

        var forma = await RisolviAsync();

        Assert.Equal(ShapeSource.Source, forma.Source);
        Assert.Single(forma.Parts);
        Assert.Equal(2, forma.UncoveredKeys.Count);   // e si sa QUALI
    }

    /// <summary>
    /// Sganciare riporta a IVAO <b>al primo giro</b>, senza ri-importare niente: la forma di IVAO non è mai
    /// stata toccata. È la reversibilità, provata invece che promessa.
    /// </summary>
    [Fact]
    public async Task Sganciare_riporta_a_ivao_senza_reimportare_niente()
    {
        var volumi = await CaricaAsync();
        await _agganci.SetAsync(SourceCatalog.AirportPosition, _idApp, App,
            volumi.Select(v => new AirspaceVolumeKey(v.NaturalKey, v.Ordinal)).ToList(), 1, "Chi sceglie");
        Assert.Equal(ShapeSource.Aip, (await RisolviAsync()).Source);

        await _agganci.SetAsync(SourceCatalog.AirportPosition, _idApp, App,
            Array.Empty<AirspaceVolumeKey>(), 1, "Chi sgancia");

        var forma = await RisolviAsync();
        Assert.Equal(ShapeSource.Source, forma.Source);
        Assert.Equal(MonobloccoIvao, forma.Parts[0].PolygonJson);
        Assert.Empty(forma.UncoveredKeys);
    }

    /// <summary>I pezzi in archivio stanno in mezzo: battono il catalogo, perdono contro l'aggancio.</summary>
    [Fact]
    public async Task I_pezzi_in_archivio_battono_il_catalogo_e_perdono_contro_l_aggancio()
    {
        var atz = new ShapePart("[[15.05,37.45],[15.15,37.45],[15.15,37.55],[15.05,37.55]]",
            null, 3_000, AirspaceDatum.Gnd, AirspaceDatum.Amsl, "GND", "3000 FT AMSL", "ATZ|LICC|GND|3000 FT AMSL");
        await _pezzi.ReplacePartsAsync(SourceCatalog.AirportPosition, _idApp, App, ShapeSource.Aip,
            ShapePartState.InForce, new[] { atz });

        var conArchivio = await RisolviAsync();
        Assert.Equal(ShapeSource.Aip, conArchivio.Source);
        Assert.Equal("ATZ|LICC|GND|3000 FT AMSL", Assert.Single(conArchivio.Parts).SourceRef);

        var volumi = await CaricaAsync();
        await _agganci.SetAsync(SourceCatalog.AirportPosition, _idApp, App,
            volumi.Select(v => new AirspaceVolumeKey(v.NaturalKey, v.Ordinal)).ToList(), 1, "Chi sceglie");

        var conAggancio = await RisolviAsync();
        Assert.Equal(2, conAggancio.Parts.Count);   // vince la scelta umana, fatta adesso
    }

    /// <summary>
    /// I pezzi <b>in attesa</b> del ciclo AIRAC non li legge nessuno: la forma resta quella di prima finché
    /// il ciclo non arriva.
    /// </summary>
    [Fact]
    public async Task I_pezzi_in_attesa_del_ciclo_non_si_leggono()
    {
        await _pezzi.ReplacePartsAsync(SourceCatalog.AirportPosition, _idApp, App, ShapeSource.Sectorfile,
            ShapePartState.Pending,
            new[] { new ShapePart("[[15.0,37.0],[15.4,37.0],[15.4,37.4],[15.0,37.4]]", 0, 10_000,
                AirspaceDatum.Gnd, AirspaceDatum.Amsl, "GND", "FL100") },
            airacCycle: "2610");

        var forma = await RisolviAsync();

        Assert.Equal(ShapeSource.Source, forma.Source);      // il catalogo, non la geometria in attesa
        Assert.Equal(MonobloccoIvao, forma.Parts[0].PolygonJson);
    }

    /// <summary>Un settore senza nessuna forma — DEL e GND non ne hanno — non compare affatto.</summary>
    [Fact]
    public async Task Un_settore_senza_nessuna_forma_non_compare()
    {
        var forme = await _risolutore.ResolveAsync(new[] { "LICC_GND", App });

        Assert.False(forme.ContainsKey("LICC_GND"));
        Assert.True(forme.ContainsKey(App));
    }

    /// <summary>Il callsign si cerca senza badare a maiuscole e spazi: è come lo scrivono le pagine.</summary>
    [Fact]
    public async Task Il_callsign_si_cerca_case_insensitive()
    {
        var forme = await _risolutore.ResolveAsync(new[] { " licc_app " });

        Assert.True(forme.ContainsKey(App));
    }
}
