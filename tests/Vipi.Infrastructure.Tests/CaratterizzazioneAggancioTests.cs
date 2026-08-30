using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Airspace;
using Vipi.Application.Stats;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// <b>Caratterizzazione</b> (carta <c>docs/refactor/15-shape-del-settore-una-porta-sola.md</c>, S1): fotografa
/// il comportamento dei motori davanti all'aggancio agli spazi aerei dell'AIP. ✅ <b>Tre su tre ribaltati</b>:
/// la mappa della vLOA da <b>S7</b>, l'attribuzione del traffico da <b>S9</b>, i confinanti da <b>S10</b> —
/// e ogni test porta accanto la ragione del cambio, che è il motivo per cui questa classe esiste.
///
/// <para>⚠️ <b>Questi test asseriscono un difetto, non un contratto.</b> Servono da rete: quando S9, S10 e S7
/// porteranno quei motori sul risolutore, sono i test che devono <b>cambiare</b>, e il cambio si vede nel
/// diff invece di passare inosservato. Chi li trova rossi senza aver toccato la carta 15 ha rotto qualcos'altro.</para>
///
/// <para>Il caso è quello vero, misurato sul <c>vipi.db</c> del 30 agosto 2026: un avvicinamento agganciato a
/// <b>due</b> zone di CTR con bande diverse (<c>GND → FL105</c> e <c>7000 FT AMSL → FL195</c>), mentre
/// l'anagrafica IVAO ne dà <b>una</b>, generosa (<c>0 → 19500</c>).</para>
/// </summary>
public class CaratterizzazioneAggancioTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfAirspaceCatalog _catalogo = default!;
    private EfSectorAirspaceBindings _agganci = default!;

    private const string App = "LICC_APP";
    private const string Ctr = "LICC_CTR";

    /// <summary>Il monoblocco dell'anagrafica: lon,lat come vuole <c>regionMapPolygon</c>.</summary>
    private const string MonobloccoIvao = "[[14.5,36.5],[16.0,36.5],[16.0,38.0],[14.5,38.0]]";

    /// <summary>Un punto dentro il monoblocco e <b>fuori</b> da tutte le zone dell'AIP.</summary>
    private const double FuoriLat = 37.9, FuoriLon = 15.9;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _catalogo = new EfAirspaceCatalog(_db);
        _agganci = new EfSectorAirspaceBindings(_db);

        var acc = new Acc { Code = "LICC", Name = "Catania" };
        _db.Accs.Add(acc);
        _db.Airports.Add(new Airport { Icao = "LICC", Name = "Catania Fontanarossa", Acc = acc });
        _db.AccSectors.Add(new AccSector
        {
            ComposePosition = Ctr, CenterId = "LICC", Position = "CTR",
            RegionMapPolygon = MonobloccoIvao, LowerLimit = 0, UpperLimit = 19500,
        });
        _db.AirportSectors.Add(new AirportSector
        {
            ComposePosition = App, AirportIcao = "LICC", AccCode = "LICC", Position = "APP",
            RegionMapPolygon = MonobloccoIvao, LowerLimit = 0, UpperLimit = 19500,
        });
        // La proiezione: è da qui che l'attribuzione del traffico prende l'albero.
        _db.Sectors.Add(new Sector
        {
            Callsign = App, Name = "Catania Avvicinamento", Acc = acc,
            Type = SectorType.App, Kind = SectorKind.Airport, AirportIcao = "LICC", IsActive = true,
        });
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    /// <summary>Due zone di CTR, ognuna con la sua banda: è la forma che il file dell'AIP porta davvero.</summary>
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

    private async Task<IReadOnlyList<AirspaceVolumeRow>> AgganciaAsync(SourceCatalog catalogo, string callsign)
    {
        var kml = Kml();
        await _catalogo.SaveAsync(
            new NewAirspaceImport("it.kmz", System.Text.Encoding.UTF8.GetBytes(kml), "2609", 1, "Chi carica"),
            AirspaceKmlReader.LeggiKml(kml), DateTime.UtcNow);

        var volumi = await _catalogo.ListVolumesAsync(new AirspaceVolumeQuery());
        var id = catalogo == SourceCatalog.Subcenter
            ? (await _db.AccSectors.FirstAsync(x => x.ComposePosition == callsign)).Id
            : (await _db.AirportSectors.FirstAsync(x => x.ComposePosition == callsign)).Id;

        await _agganci.SetAsync(catalogo, id, callsign,
            volumi.Select(v => new AirspaceVolumeKey(v.NaturalKey, v.Ordinal)).ToList(), 1, "Chi sceglie");
        return volumi;
    }

    /// <summary>
    /// ✅ <b>RIBALTATO da S9</b>, ed è l'assert che vale tutta la carta: l'attribuzione del traffico non
    /// rivendica più il monoblocco di IVAO su un settore agganciato. Il volume è l'insieme dei <b>pezzi</b>,
    /// ognuno con la sua banda, quindi:
    /// <list type="bullet">
    ///   <item>un punto dentro il monoblocco e <b>fuori</b> dalle due zone dell'AIP <b>non è più suo</b>;</item>
    ///   <item>il cielo <b>sopra la Z1</b> (che finisce a FL105) nemmeno, anche se l'inviluppo arriva a FL195;</item>
    ///   <item>quel che è davvero dentro una zona, alla sua quota, resta suo.</item>
    /// </list>
    ///
    /// <para>⚠️ Le tratte già scritte in archivio non si toccano: l'attribuzione si decide al giro del poller
    /// e si conserva. La non-retroattività è nella macchina, non nell'attenzione di chi aggancia.</para>
    /// </summary>
    [Fact]
    public async Task Il_Traffico_Rivendica_I_Pezzi_Agganciati_E_Non_Il_Monoblocco()
    {
        var volumi = await AgganciaAsync(SourceCatalog.AirportPosition, App);
        Assert.Equal(2, volumi.Count);

        var righe = await new EfSectorVolumeCatalog(_db, new EfSectorShapeResolver(_db, new EfSectorAirspaceBindings(_db), new EfSectorShapeParts(_db))).GetAllAsync();
        var riga = righe.Single(r => r.Callsign == App);
        Assert.Equal(2, riga.Parts.Count);
        Assert.Equal(ShapeSource.Aip, riga.Source);   // e in archivio finirà scritto da dove veniva

        var claims = SectorVolumeMap.BuildClaims(righe, new HashSet<string>(new[] { App }, StringComparer.OrdinalIgnoreCase));
        var volume = Assert.Single(claims).Volume;

        // Un punto dentro il monoblocco e fuori da tutte e due le zone: NON è suo.
        Assert.False(volume.Contains(FuoriLat, FuoriLon, 5_000));
        // Il cielo sopra la Z1, che il CTR non ha: nemmeno.
        Assert.False(volume.Contains(37.2, 15.2, 15_000));
        // Ma dentro la Z1, alla sua quota, sì.
        Assert.True(volume.Contains(37.2, 15.2, 5_000));
        // E dentro la Z2, che comincia a 7000 ft.
        Assert.True(volume.Contains(37.2, 15.7, 15_000));
        Assert.False(volume.Contains(37.2, 15.7, 3_000));
    }

    /// <summary>
    /// ⚠️ <b>OGGI</b>: i confinanti leggono il poligono di IVAO dal catalogo, aggancio o no.
    /// Si ribalta in <b>S10</b>, dove l'adiacenza è vera se lo è <b>un pezzo qualunque</b>.
    /// </summary>
    [Fact]
    public async Task Oggi_I_Confinanti_Leggono_Il_Poligono_Di_Ivao()
    {
        await AgganciaAsync(SourceCatalog.Subcenter, Ctr);

        var domestici = await new EfNeighbourRepository(_db, new AiracService()).ListDomesticSectorPolygonsAsync();
        var riga = domestici.Single(d => d.ComposePosition == Ctr);

        Assert.Equal(MonobloccoIvao, riga.RegionMapPolygon);
    }

    /// <summary>
    /// ✅ <b>RIBALTATO da S7.</b> La mappa della vLOA leggeva le shape da
    /// <c>GetSectorPolygonsRawByCallsignAsync</c> — il poligono di IVAO — e la stessa area appariva in due
    /// forme diverse in due documenti dello stesso pacchetto. Ora passa dalla porta unica e vede l'AIP.
    ///
    /// <para>La cucitura vecchia resta com'era, ed è giusto: la usa ancora il congelamento di release, che
    /// ha bisogno del grezzo del catalogo. Quel che è cambiato è <b>chi la chiama</b>.</para>
    /// </summary>
    [Fact]
    public async Task La_vLOA_Ora_Legge_Dalla_Porta_Unica_E_Vede_L_Aip()
    {
        await AgganciaAsync(SourceCatalog.AirportPosition, App);

        var risolutore = new EfSectorShapeResolver(_db, _agganci, new EfSectorShapeParts(_db));
        var forma = (await risolutore.ResolveAsync(new[] { App }))[App];

        Assert.Equal(ShapeSource.Aip, forma.Source);
        Assert.Equal(2, forma.Parts.Count);

        // La cucitura di prima esiste ancora, e dà ancora IVAO: non è morta, è solo un'altra domanda.
        var raw = await new EfAccDerivationRepository(_db).GetSectorPolygonsRawByCallsignAsync(new[] { App });
        Assert.Equal(MonobloccoIvao, raw[App]);
    }

    /// <summary>
    /// La rete sotto la rete: l'aggancio <b>c'è</b> e si risolve in due volumi con bande <b>diverse</b>. Se
    /// questo test diventasse rosso, i tre di sopra non direbbero più quel che credono di dire.
    /// </summary>
    [Fact]
    public async Task L_Aggancio_Risolve_Due_Volumi_Con_Bande_Diverse()
    {
        await AgganciaAsync(SourceCatalog.AirportPosition, App);

        var risolti = await _agganci.ResolveAsync(new[] { App });
        var riga = risolti[App];

        Assert.Equal(2, riga.Volumes.Count);
        Assert.Empty(riga.Missing);
        Assert.Equal(new int?[] { 0, 7_000 }, riga.Volumes.Select(v => v.BaseFeet).ToArray());
        Assert.Equal(new int?[] { 10_500, 19_500 }, riga.Volumes.Select(v => v.TopFeet).ToArray());
    }
}
