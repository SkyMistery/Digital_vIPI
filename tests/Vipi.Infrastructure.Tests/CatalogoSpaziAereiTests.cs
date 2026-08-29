using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Airspace;
using Vipi.Application.Aor;
using Vipi.Domain;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il catalogo degli spazi aerei su database: caricamento, messa in vigore, e la forma con cui i volumi
/// entrano in archivio — che è quella di IVAO, ed è la ragione per cui tutto il resto li disegna senza
/// sapere da dove vengono.
/// </summary>
public class CatalogoSpaziAereiTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfAirspaceCatalog _catalogo = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _catalogo = new EfAirspaceCatalog(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    // Due volumi: un CTR (utilizzabile) e un'area regolamentata (che si legge ma non si usa).
    private const string Kml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <!-- This file was created on: Wed 15 July 2026 at 18:30:49 UTC -->
        <kml xmlns="http://www.opengis.net/kml/2.2"><Document>
          <Placemark><ExtendedData><SchemaData>
            <SimpleData name="Name">PROVA CTR</SimpleData>
            <SimpleData name="Category">Control Traffic Region</SimpleData>
            <SimpleData name="Base">GND</SimpleData><SimpleData name="Top">2500 FT AMSL</SimpleData>
          </SchemaData></ExtendedData>
          <Polygon><outerBoundaryIs><LinearRing><coordinates>
            9.0,45.0,762 9.5,45.0,762 9.25,45.5,762 9.0,45.0,762
          </coordinates></LinearRing></outerBoundaryIs></Polygon></Placemark>
          <Placemark><ExtendedData><SchemaData>
            <SimpleData name="Name">LI-R99-PROVA</SimpleData>
            <SimpleData name="Category">Restricted area</SimpleData>
            <SimpleData name="Base">FL125</SimpleData><SimpleData name="Top">FL240</SimpleData>
          </SchemaData></ExtendedData>
          <Polygon><outerBoundaryIs><LinearRing><coordinates>
            10.0,44.0,3810 10.5,44.0,3810 10.25,44.5,3810 10.0,44.0,3810
          </coordinates></LinearRing></outerBoundaryIs></Polygon></Placemark>
        </Document></kml>
        """;

    private Task<AirspaceImportRow> CaricaAsync(string? ciclo = "2609", string nome = "it (2).kmz") =>
        _catalogo.SaveAsync(
            new NewAirspaceImport(nome, System.Text.Encoding.UTF8.GetBytes(Kml), ciclo, 42, "Mario Rossi"),
            AirspaceKmlReader.LeggiKml(Kml),
            new DateTime(2026, 8, 29, 20, 0, 0, DateTimeKind.Utc));

    [Fact]
    public async Task Il_Caricamento_Conta_Quel_Che_Ha_Letto()
    {
        var riga = await CaricaAsync();

        Assert.Equal(2, riga.VolumesRead);
        Assert.Equal(1, riga.VolumesUsable);      // l'area regolamentata si legge ma non si usa
        Assert.Equal(6, riga.PointCount);
        Assert.Equal("2609", riga.AiracCycle);
        Assert.True(riga.IsCurrent);
        Assert.Equal(64, riga.Sha256.Length);
    }

    [Fact]
    public async Task Il_File_Si_Conserva_Intero()
    {
        // ⚠️ Decisione del committente: e' l'unico modo di rispondere fra sei mesi a «da dove viene questo
        // confine», e di rifare la lettura se la regola cambia senza richiedere il file a qualcuno.
        var riga = await CaricaAsync();

        var file = await _catalogo.GetFileAsync(riga.Id);
        Assert.NotNull(file);
        Assert.Equal("it (2).kmz", file.Value.FileName);
        Assert.Equal(Kml, System.Text.Encoding.UTF8.GetString(file.Value.Content));
    }

    [Fact]
    public async Task La_Data_Di_Generazione_La_Dice_Il_File()
    {
        var riga = await CaricaAsync();

        Assert.Equal(new DateTime(2026, 7, 15, 18, 30, 49, DateTimeKind.Utc), riga.GeneratedUtc);
    }

    [Fact]
    public async Task Il_Volume_Entra_Nella_Forma_Di_Ivao_E_Il_Proiettore_Lo_Disegna()
    {
        // È il perno della carta: il poligono e' nella stessa forma del `regionMapPolygon`, quindi la mappa,
        // il 3D e la stampa lo disegnano senza sapere che viene dall'AIP.
        await CaricaAsync();

        var ctr = Assert.Single(await _catalogo.ListVolumesAsync(new AirspaceVolumeQuery(UsableOnly: true)));
        Assert.Equal("PROVA CTR", ctr.Name);
        Assert.Equal(3, ctr.PointCount);

        var proiettato = AorPolygonProjector.Project(ctr.PolygonJson);
        Assert.NotNull(proiettato);
        Assert.Equal(3, proiettato.Points.Count);
        Assert.Equal(45.0, proiettato.MinLat, 6);
        Assert.Equal(9.0, proiettato.MinLon, 6);
    }

    [Fact]
    public async Task Il_Riquadro_Si_Archivia_Per_Filtrare_Senza_Rileggere_I_Punti()
    {
        await CaricaAsync();

        var ctr = Assert.Single(await _catalogo.ListVolumesAsync(new AirspaceVolumeQuery(UsableOnly: true)));
        Assert.Equal(45.0, ctr.MinLat, 6);
        Assert.Equal(45.5, ctr.MaxLat, 6);
        Assert.Equal(9.0, ctr.MinLon, 6);
        Assert.Equal(9.5, ctr.MaxLon, 6);
    }

    [Fact]
    public async Task Un_Caricamento_Nuovo_Spegne_Il_Precedente()
    {
        var primo = await CaricaAsync(nome: "primo.kmz");
        var secondo = await CaricaAsync(nome: "secondo.kmz");

        var corrente = await _catalogo.GetCurrentAsync();
        Assert.NotNull(corrente);
        Assert.Equal(secondo.Id, corrente.Id);
        Assert.Equal(2, (await _catalogo.ListImportsAsync()).Count);
        Assert.Single(await _catalogo.ListImportsAsync(), i => i.IsCurrent);
        Assert.NotEqual(primo.Id, corrente.Id);
    }

    [Fact]
    public async Task Si_Puo_Tornare_A_Un_Caricamento_Di_Prima()
    {
        var primo = await CaricaAsync(nome: "primo.kmz");
        await CaricaAsync(nome: "secondo.kmz");

        await _catalogo.SetCurrentAsync(primo.Id);

        var corrente = await _catalogo.GetCurrentAsync();
        Assert.Equal(primo.Id, corrente!.Id);
        Assert.Single(await _catalogo.ListImportsAsync(), i => i.IsCurrent);
    }

    [Fact]
    public async Task Quello_In_Vigore_Non_Si_Elimina()
    {
        // ⚠️ I settori che ne hanno preso la shape resterebbero a citare un volume che non c'e' piu'.
        var riga = await CaricaAsync();

        await Assert.ThrowsAsync<ValidationException>(() => _catalogo.DeleteAsync(riga.Id));
        Assert.NotNull(await _catalogo.GetCurrentAsync());
    }

    [Fact]
    public async Task Eliminare_Un_Caricamento_Porta_Via_I_Suoi_Volumi()
    {
        var primo = await CaricaAsync(nome: "primo.kmz");
        await CaricaAsync(nome: "secondo.kmz");

        await _catalogo.DeleteAsync(primo.Id);

        Assert.Single(await _catalogo.ListImportsAsync());
        Assert.Empty(await _db.AirspaceVolumes.Where(v => v.ImportId == primo.Id).ToListAsync());
    }

    [Fact]
    public async Task I_Volumi_Si_Chiedono_Per_Famiglia_E_Per_Nome()
    {
        await CaricaAsync();

        Assert.Equal(2, (await _catalogo.ListVolumesAsync(new AirspaceVolumeQuery())).Count);
        Assert.Single(await _catalogo.ListVolumesAsync(new AirspaceVolumeQuery(Families: [AirspaceFamily.Ctr])));
        Assert.Single(await _catalogo.ListVolumesAsync(new AirspaceVolumeQuery(Search: "R99")));
        Assert.Empty(await _catalogo.ListVolumesAsync(new AirspaceVolumeQuery(Search: "non c'e'")));

        var per = await _catalogo.CountByFamilyAsync();
        Assert.Equal(1, per[AirspaceFamily.Ctr]);
        Assert.Equal(1, per[AirspaceFamily.Restricted]);
    }

    [Fact]
    public async Task Senza_Nessun_Caricamento_Il_Catalogo_E_Vuoto_Non_Rotto()
    {
        Assert.Null(await _catalogo.GetCurrentAsync());
        Assert.Empty(await _catalogo.ListVolumesAsync(new AirspaceVolumeQuery()));
        Assert.Empty(await _catalogo.CountByFamilyAsync());
        Assert.Empty(await _catalogo.ListImportsAsync());
    }

    [Fact]
    public async Task Le_Segnalazioni_Del_Lettore_Restano_Leggibili_Dopo()
    {
        // Il caricamento le archivia: la pagina le rimostra senza rileggere il file.
        var kml = Kml.Replace("""<SimpleData name="Base">GND</SimpleData>""",
                              """<SimpleData name="Base">quota da confermare</SimpleData>""");
        var riga = await _catalogo.SaveAsync(
            new NewAirspaceImport("x.kmz", System.Text.Encoding.UTF8.GetBytes(kml), null, 1, "Tizio"),
            AirspaceKmlReader.LeggiKml(kml), DateTime.UtcNow);

        var segnalazioni = await _catalogo.GetIssuesAsync(riga.Id);
        Assert.Contains(segnalazioni, i => i.Kind == AirspaceIssueKind.QuotaNonLetta && i.Volume == "PROVA CTR");
    }
}
