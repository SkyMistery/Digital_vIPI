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
/// il comportamento di <b>oggi</b> dei tre motori che <b>non</b> conoscono l'aggancio agli spazi aerei
/// dell'AIP — attribuzione del traffico, confinanti, mappa della vLOA.
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
    /// ⚠️ <b>OGGI</b>: l'attribuzione del traffico rivendica il monoblocco di IVAO anche su un settore
    /// agganciato — e con esso un punto che nell'AIP <b>non è suo</b>, e il cielo fino a FL195 anche dove il
    /// CTR si ferma a FL105.
    ///
    /// <para>Si ribalta in <b>S9</b>: il volume diventa l'insieme dei pezzi, e questo punto non si rivendica più.</para>
    /// </summary>
    [Fact]
    public async Task Oggi_Il_Traffico_Rivendica_Il_Monoblocco_Di_Ivao_Anche_Se_Il_Settore_E_Agganciato()
    {
        var volumi = await AgganciaAsync(SourceCatalog.AirportPosition, App);
        Assert.Equal(2, volumi.Count);   // le due zone ci sono, e l'aggancio le cita

        var righe = await new EfSectorVolumeCatalog(_db).GetAllAsync();
        var riga = righe.Single(r => r.Callsign == App);
        Assert.Equal(MonobloccoIvao, riga.RegionMapPolygon);   // ⚠️ il catalogo non sa niente dell'aggancio
        Assert.Equal(0, riga.LowerLimit);
        Assert.Equal(19500, riga.UpperLimit);

        var claims = SectorVolumeMap.BuildClaims(righe, new HashSet<string>(new[] { App }, StringComparer.OrdinalIgnoreCase));
        var volume = Assert.Single(claims).Volume;

        // Un punto dentro il monoblocco e fuori da tutte e due le zone dell'AIP: oggi si rivendica.
        Assert.True(volume.Contains(FuoriLat, FuoriLon, 5_000));
        // E il cielo sopra la Z1, che il CTR non ha: pure.
        Assert.True(volume.Contains(37.2, 15.2, 15_000));
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
    /// ⚠️ <b>OGGI</b>: la mappa della vLOA legge le shape da <c>GetSectorPolygonsRawByCallsignAsync</c>, che
    /// dà il poligono di IVAO — è la cucitura che <b>S7</b> sposta sul risolutore.
    /// </summary>
    [Fact]
    public async Task Oggi_La_Cucitura_Della_vLOA_Da_Il_Poligono_Di_Ivao()
    {
        await AgganciaAsync(SourceCatalog.AirportPosition, App);

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
