using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Airspace;
using Vipi.Domain;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Gli agganci settore → volumi dell'AIP: la richiesta che ha fatto nascere tutta la carta. Il caso vero è
/// Catania, dove l'avvicinamento controlla <b>sette</b> zone di CTR e l'anagrafica ne dà una sola, generosa.
/// </summary>
public class AgganciSpaziAereiTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfAirspaceCatalog _catalogo = default!;
    private EfSectorAirspaceBindings _agganci = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _catalogo = new EfAirspaceCatalog(_db);
        _agganci = new EfSectorAirspaceBindings(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    /// <summary>Un CTR a più zone, come Catania: ogni zona è un volume suo, con la sua banda di quote.</summary>
    private static string Kml(params (string Nome, string Base, string Top, double Lon)[] zone)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8"?><kml xmlns="http://www.opengis.net/kml/2.2"><Document>""");
        foreach (var z in zone)
            sb.Append($"""
                <Placemark><ExtendedData><SchemaData>
                  <SimpleData name="Name">{z.Nome}</SimpleData>
                  <SimpleData name="Category">Control Traffic Region</SimpleData>
                  <SimpleData name="Base">{z.Base}</SimpleData><SimpleData name="Top">{z.Top}</SimpleData>
                </SchemaData></ExtendedData>
                <Polygon><outerBoundaryIs><LinearRing><coordinates>
                  {z.Lon},37.0,762 {z.Lon + 0.5},37.0,762 {z.Lon + 0.25},37.5,762 {z.Lon},37.0,762
                </coordinates></LinearRing></outerBoundaryIs></Polygon></Placemark>
                """);
        sb.Append("</Document></kml>");
        return sb.ToString();
    }

    private static readonly (string, string, string, double)[] Catania =
    [
        ("CATANIA CTR Z1", "GND", "3500 FT AMSL", 15.0),
        ("CATANIA CTR Z2", "GND", "3500 FT AMSL", 15.6),
        ("CATANIA CTR Z3", "3500 FT AMSL", "FL195", 16.2),
    ];

    private async Task<IReadOnlyList<AirspaceVolumeRow>> CaricaAsync(string kml)
    {
        await _catalogo.SaveAsync(
            new NewAirspaceImport("it.kmz", System.Text.Encoding.UTF8.GetBytes(kml), "2609", 7, "Chi carica"),
            AirspaceKmlReader.LeggiKml(kml), DateTime.UtcNow);
        return await _catalogo.ListVolumesAsync(new AirspaceVolumeQuery());
    }

    private static IReadOnlyList<AirspaceVolumeKey> Chiavi(IEnumerable<AirspaceVolumeRow> v) =>
        v.Select(x => new AirspaceVolumeKey(x.NaturalKey, x.Ordinal)).ToList();

    [Fact]
    public async Task Un_Avvicinamento_Aggancia_Tutte_Le_Zone_Del_Suo_Ctr()
    {
        var volumi = await CaricaAsync(Kml(Catania));

        await _agganci.SetAsync(SourceCatalog.AirportPosition, 42, "LICC_APP", Chiavi(volumi), 7, "Chi sceglie");

        var risolto = await _agganci.ResolveAsync(["LICC_APP"]);
        var riga = Assert.Contains("LICC_APP", risolto);
        Assert.Equal(3, riga.Volumes.Count);
        Assert.Empty(riga.Missing);
        Assert.Equal("Chi sceglie", riga.ChosenByName);
        Assert.True(riga.HasShape);
    }

    [Fact]
    public async Task La_Forma_Agganciata_E_Una_Lista_Di_Poligoni_Non_Uno()
    {
        // ⚠️ È il perno della correzione alla carta (§6-bis): la colonna della shape di un settore tiene UN
        // anello, e Catania sono tre zone. Ridotte alla prima sarebbero un confine sbagliato, disegnato bene.
        var volumi = await CaricaAsync(Kml(Catania));
        await _agganci.SetAsync(SourceCatalog.AirportPosition, 42, "LICC_APP", Chiavi(volumi), 7, "Chi sceglie");

        var forma = AirspaceAor.Shape((await _agganci.ResolveAsync(["LICC_APP"]))["LICC_APP"]);

        Assert.NotNull(forma);
        Assert.Equal(3, forma.Polygons.Count);
    }

    [Fact]
    public async Task La_Banda_Del_Tre_D_E_Linviluppo_Delle_Zone()
    {
        // Z1/Z2 vanno da terra a 3500 ft, Z3 da 3500 a FL195: l'inviluppo è GND → FL195.
        var volumi = await CaricaAsync(Kml(Catania));
        await _agganci.SetAsync(SourceCatalog.AirportPosition, 42, "LICC_APP", Chiavi(volumi), 7, "Chi sceglie");

        var forma = AirspaceAor.Shape((await _agganci.ResolveAsync(["LICC_APP"]))["LICC_APP"])!;

        Assert.Equal(0, forma.LowerFl);      // GND
        Assert.Equal(195, forma.UpperFl);    // FL195
    }

    [Fact]
    public async Task Un_Tetto_Illimitato_Vince_Sullinviluppo()
    {
        var volumi = await CaricaAsync(Kml(
            ("PROVA Z1", "GND", "3500 FT AMSL", 15.0),
            ("PROVA Z2", "GND", "FL999", 15.6)));
        await _agganci.SetAsync(SourceCatalog.AirportPosition, 1, "LIXX_APP", Chiavi(volumi), 7, "Tizio");

        var forma = AirspaceAor.Shape((await _agganci.ResolveAsync(["LIXX_APP"]))["LIXX_APP"])!;

        Assert.Equal(Vipi.Application.Aor.AorFlBand.Unlimited, forma.UpperFl);
    }

    [Fact]
    public async Task Riagganciare_Sostituisce_Non_Accumula()
    {
        var volumi = await CaricaAsync(Kml(Catania));
        await _agganci.SetAsync(SourceCatalog.AirportPosition, 42, "LICC_APP", Chiavi(volumi), 7, "Tizio");

        await _agganci.SetAsync(SourceCatalog.AirportPosition, 42, "LICC_APP", Chiavi(volumi.Take(1)), 7, "Tizio");

        Assert.Single((await _agganci.ResolveAsync(["LICC_APP"]))["LICC_APP"].Volumes);
    }

    [Fact]
    public async Task Sganciare_E_Un_Elenco_Vuoto_E_Il_Settore_Sparisce_Dagli_Agganci()
    {
        var volumi = await CaricaAsync(Kml(Catania));
        await _agganci.SetAsync(SourceCatalog.AirportPosition, 42, "LICC_APP", Chiavi(volumi), 7, "Tizio");

        await _agganci.SetAsync(SourceCatalog.AirportPosition, 42, "LICC_APP", Array.Empty<AirspaceVolumeKey>(), 7, "Tizio");

        Assert.Empty(await _agganci.ResolveAsync(["LICC_APP"]));
        Assert.Empty(await _agganci.ListAsync());
    }

    [Fact]
    public async Task Laggancio_Sopravvive_Al_Ricaricamento_Del_File()
    {
        // ⚠️ È la ragione per cui l'aggancio cita la CHIAVE e non l'id della riga: un file nuovo rifà tutte
        // le righe, e con l'id ogni aggancio si romperebbe a ogni ciclo AIRAC.
        var volumi = await CaricaAsync(Kml(Catania));
        await _agganci.SetAsync(SourceCatalog.AirportPosition, 42, "LICC_APP", Chiavi(volumi), 7, "Tizio");
        var idPrima = volumi.Select(v => v.Id).ToList();

        var dopo = await CaricaAsync(Kml(Catania));   // stesso contenuto, righe nuove

        Assert.Empty(idPrima.Intersect(dopo.Select(v => v.Id)));   // sono davvero righe nuove
        var riga = (await _agganci.ResolveAsync(["LICC_APP"]))["LICC_APP"];
        Assert.Equal(3, riga.Volumes.Count);
        Assert.Empty(riga.Missing);
    }

    [Fact]
    public async Task Un_Volume_Sparito_Dal_File_Lascia_Laggancio_Scoperto_Senza_Cancellarlo()
    {
        var volumi = await CaricaAsync(Kml(Catania));
        await _agganci.SetAsync(SourceCatalog.AirportPosition, 42, "LICC_APP", Chiavi(volumi), 7, "Tizio");

        // Il file nuovo non ha più la Z3.
        await CaricaAsync(Kml(Catania.Take(2).ToArray()));

        var riga = (await _agganci.ResolveAsync(["LICC_APP"]))["LICC_APP"];
        Assert.Equal(2, riga.Volumes.Count);
        var mancante = Assert.Single(riga.Missing);
        Assert.Contains("CATANIA CTR Z3", mancante.Key);
        Assert.Equal(3, riga.Total);          // l'aggancio c'è ancora, e si vede che è scoperto
        Assert.True(riga.HasShape);           // le due zone rimaste si disegnano lo stesso
    }

    [Fact]
    public async Task Senza_Nessun_Volume_Risolto_Non_Ce_Forma_E_Il_Settore_Resta_Comera()
    {
        // ⚠️ Null vuol dire «lascia il settore com'era»: un aggancio che non si risolve NON deve cancellare
        // l'area che il settore già mostrava.
        var volumi = await CaricaAsync(Kml(Catania));
        await _agganci.SetAsync(SourceCatalog.AirportPosition, 42, "LICC_APP", Chiavi(volumi), 7, "Tizio");

        await CaricaAsync(Kml(("ALTRO CTR", "GND", "FL100", 9.0)));   // niente Catania nel file nuovo

        var riga = (await _agganci.ResolveAsync(["LICC_APP"]))["LICC_APP"];
        Assert.Empty(riga.Volumes);
        Assert.Equal(3, riga.Missing.Count);
        Assert.False(riga.HasShape);
        Assert.Null(AirspaceAor.Shape(riga));
    }

    [Fact]
    public async Task Un_Callsign_Senza_Aggancio_Non_Compare_Affatto()
    {
        await CaricaAsync(Kml(Catania));

        Assert.Empty(await _agganci.ResolveAsync(["LIRF_APP", "LIMC_APP"]));
    }

    [Fact]
    public async Task Il_Callsign_Si_Cerca_Senza_Badare_Alle_Maiuscole()
    {
        var volumi = await CaricaAsync(Kml(Catania));
        await _agganci.SetAsync(SourceCatalog.AirportPosition, 42, "  licc_app  ", Chiavi(volumi), 7, "Tizio");

        Assert.Contains("LICC_APP", await _agganci.ResolveAsync(["licc_app"]));
    }
}
