using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Airspace;
using Vipi.Application.Aor;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// L'ATZ dell'AIP al posto del cerchio da 5 NM, per le torri senza area (decisione 2 del committente).
/// ⚠️ È un <b>ripiego</b>: il sectorfile e l'anagrafica se lo riprendono quando hanno qualcosa di meglio.
/// </summary>
public class AtzTorriTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfAirportSectorRepository _repo = default!;
    private EfAirspaceCatalog _catalogo = default!;
    private AtzTowerShapeService _atz = default!;
    private EfSectorAirspaceBindings _agganci = default!;
    private EfSectorShapeParts _pezzi = default!;
    private EfSectorShapeResolver _forme = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _repo = new EfAirportSectorRepository(_db);
        _catalogo = new EfAirspaceCatalog(_db);
        _agganci = new EfSectorAirspaceBindings(_db);
        _pezzi = new EfSectorShapeParts(_db);
        _forme = new EfSectorShapeResolver(_db, _agganci, _pezzi);
        _atz = new AtzTowerShapeService(_repo, _catalogo, _pezzi);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private int _accId;

    /// <summary>Un aeroporto con la sua torre, senza area: il bersaglio del ripiego.</summary>
    private async Task<int> TorreSenzaAreaAsync(string icao, string? poligono = null, bool sintetica = false)
    {
        if (_accId == 0)
        {
            var acc = new Acc { Code = "LIRR", Name = "Roma" };
            _db.Accs.Add(acc);
            await _db.SaveChangesAsync();
            _accId = acc.Id;
        }
        _db.Airports.Add(new Airport { Icao = icao, Name = icao, AccId = _accId, Latitude = 42.0, Longitude = 12.0 });
        var s = new AirportSector
        {
            AccCode = "LIRR",
            AirportIcao = icao,
            ComposePosition = icao + "_TWR",
            Position = "TWR",
            RegionMapPolygon = poligono,
            IsShapeSynthetic = sintetica,
        };
        _db.AirportSectors.Add(s);
        await _db.SaveChangesAsync();
        return s.Id;
    }

    private static string Kml(params (string Nome, string Categoria)[] volumi)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8"?><kml xmlns="http://www.opengis.net/kml/2.2"><Document>""");
        var lon = 12.0;
        foreach (var v in volumi)
        {
            sb.Append($"""
                <Placemark><ExtendedData><SchemaData>
                  <SimpleData name="Name">{v.Nome}</SimpleData>
                  <SimpleData name="Category">{v.Categoria}</SimpleData>
                  <SimpleData name="Base">GND</SimpleData><SimpleData name="Top">2000 FT AGL</SimpleData>
                </SchemaData></ExtendedData>
                <Polygon><outerBoundaryIs><LinearRing><coordinates>
                  {lon},42.0,0 {lon + 0.1},42.0,0 {lon + 0.05},42.1,0 {lon},42.0,0
                </coordinates></LinearRing></outerBoundaryIs></Polygon></Placemark>
                """);
            lon += 0.5;
        }
        sb.Append("</Document></kml>");
        return sb.ToString();
    }

    private async Task CaricaAsync(string kml) =>
        await _catalogo.SaveAsync(
            new NewAirspaceImport("it.kmz", System.Text.Encoding.UTF8.GetBytes(kml), "2609", 1, "Tizio"),
            AirspaceKmlReader.LeggiKml(kml), DateTime.UtcNow);

    private async Task<AirportSector> RicaricaAsync(int id) =>
        await _db.AirportSectors.AsNoTracking().FirstAsync(s => s.Id == id);

    [Fact]
    public async Task Una_Torre_Senza_Area_Prende_La_Sua_Atz()
    {
        var id = await TorreSenzaAreaAsync("LIBC");
        await CaricaAsync(Kml(("ATZ CROTONE LIBC", "Airspace class G")));

        var esito = await _atz.ApplyAsync();

        Assert.Equal(1, esito.Applied);

        // ⚠️ Carta refactor 15: l'ATZ NON abita più la colonna della shape — è un aggancio, e si legge
        // dalla porta unica. È quel che la rende reversibile.
        var forma = (await _forme.ResolveAsync(["LIBC_TWR"]))["LIBC_TWR"];
        Assert.Equal(ShapeSource.Aip, forma.Source);
        Assert.NotNull(AorPolygonProjector.Project(Assert.Single(forma.Parts).PolygonJson));

        var torre = await RicaricaAsync(id);
        Assert.Null(torre.RegionMapPolygon);                  // la colonna resta libera: la riempirà il cerchio
        Assert.Equal(ShapeSource.Source, torre.ShapeSource);
    }

    /// <summary>
    /// Il cerchio cede il posto al confine vero — ma <b>resta sotto</b>, ed è la differenza che conta: dalla
    /// carta refactor 15 sganciare l'ATZ riporta la torre al suo cerchio, mentre prima la shape scritta in
    /// colonna se l'era portato via per sempre.
    /// </summary>
    [Fact]
    public async Task Il_Cerchio_Cede_Il_Posto_Al_Confine_Vero_E_Torna_Se_Si_Sgancia()
    {
        var cerchio = CircleShapeBuilder.Build(42.0, 12.0);
        var id = await TorreSenzaAreaAsync("LIBC", cerchio, sintetica: true);
        await CaricaAsync(Kml(("ATZ CROTONE LIBC", "Airspace class G")));

        Assert.Equal(1, (await _atz.ApplyAsync()).Applied);

        var conAtz = (await _forme.ResolveAsync(["LIBC_TWR"]))["LIBC_TWR"];
        Assert.Equal(ShapeSource.Aip, conAtz.Source);
        Assert.NotEqual(cerchio, conAtz.Parts[0].PolygonJson);

        // Il cerchio non è mai stato toccato: sta ancora in colonna, e lo sgancio lo rimette in vista.
        var torre = await RicaricaAsync(id);
        Assert.Equal(cerchio, torre.RegionMapPolygon);
        Assert.True(torre.IsShapeSynthetic);

        await _pezzi.ClearPartsAsync(SourceCatalog.AirportPosition, id, ShapeSource.Aip);

        var dopoSgancio = (await _forme.ResolveAsync(["LIBC_TWR"]))["LIBC_TWR"];
        Assert.Equal(ShapeSource.Synthetic, dopoSgancio.Source);   // ed è un cerchio, e lo dice
        Assert.Equal(cerchio, dopoSgancio.Parts[0].PolygonJson);
    }

    [Fact]
    public async Task Una_Shape_Vera_Di_Ivao_Non_Si_Tocca()
    {
        // ⚠️ Il ripiego riempie i vuoti: una shape della sorgente è verità primaria.
        var ivao = "[[12.0,42.0],[12.2,42.0],[12.2,42.2],[12.0,42.2]]";
        var id = await TorreSenzaAreaAsync("LIBC", ivao);
        await CaricaAsync(Kml(("ATZ CROTONE LIBC", "Airspace class G")));

        Assert.Equal(0, (await _atz.ApplyAsync()).Applied);
        Assert.Equal(ivao, (await RicaricaAsync(id)).RegionMapPolygon);
    }

    /// <summary>
    /// ✅ <b>RIBALTATO dalla carta refactor 15.</b> Guidonia (<c>LIRG</c>) ha DUE zone e Torino Aeritalia
    /// (<c>LIMA</c>) TRE: prima si <b>saltavano</b>, perché la colonna della shape tiene un anello e mezza
    /// zona di traffico è peggio di nessuna. Un aggancio tiene una <b>lista</b>: adesso si prendono intere.
    /// </summary>
    [Fact]
    public async Task Un_Icao_Con_Piu_Zone_Le_Prende_Tutte()
    {
        var id = await TorreSenzaAreaAsync("LIRG");
        await CaricaAsync(Kml(
            ("ATZ ATZ 1 GUIDONIA LIRG", "Airspace class G"),
            ("ATZ ATZ 2 GUIDONIA LIRG", "Airspace class G")));

        var esito = await _atz.ApplyAsync();

        Assert.Equal(1, esito.Applied);
        Assert.Equal(["LIRG"], esito.MultiZone);   // si dicono lo stesso: sono i campi da guardare
        Assert.Equal(0, esito.StillWithout);

        var forma = (await _forme.ResolveAsync(["LIRG_TWR"]))["LIRG_TWR"];
        Assert.Equal(2, forma.Parts.Count);
        Assert.Null((await RicaricaAsync(id)).RegionMapPolygon);   // la colonna resta libera
    }

    [Fact]
    public async Task Un_Matz_Senza_Icao_Nel_Nome_Non_Si_Aggancia_Da_Solo()
    {
        // ⚠️ «MATZ» è un gruppo di quattro lettere: una regola che prendesse «la prima parola di quattro
        // lettere» ci vedrebbe un codice d'aeroporto. Sono 17 su 91, quasi tutte basi militari.
        var id = await TorreSenzaAreaAsync("LIBA");
        await CaricaAsync(Kml(("MATZ AMENDOLA-TWR", "Airspace class G")));

        var esito = await _atz.ApplyAsync();

        Assert.Equal(0, esito.Applied);
        Assert.Empty(esito.MultiZone);
        Assert.Equal(1, esito.StillWithout);
        Assert.Null((await RicaricaAsync(id)).RegionMapPolygon);
    }

    [Fact]
    public async Task Un_File_Nuovo_Aggiorna_Latz_Gia_Messa()
    {
        // Una shape che non si aggiorna mai è una shape che invecchia in silenzio.
        await TorreSenzaAreaAsync("LIBC");
        await CaricaAsync(Kml(("ATZ CROTONE LIBC", "Airspace class G")));
        await _atz.ApplyAsync();
        var prima = (await _forme.ResolveAsync(["LIBC_TWR"]))["LIBC_TWR"].Parts[0].PolygonJson;

        // Il file nuovo ha lo stesso ATZ ma spostato: il volume è preceduto da un altro, quindi cade su
        // un'altra longitudine.
        await CaricaAsync(Kml(("ATZ ALTRO LIRZ", "Airspace class G"), ("ATZ CROTONE LIBC", "Airspace class G")));
        Assert.Equal(1, (await _atz.ApplyAsync()).Applied);

        Assert.NotEqual(prima, (await _forme.ResolveAsync(["LIBC_TWR"]))["LIBC_TWR"].Parts[0].PolygonJson);
    }

    [Fact]
    public async Task Solo_Le_Atz_Non_Un_Ctr_Che_Nomina_Lo_Stesso_Campo()
    {
        var id = await TorreSenzaAreaAsync("LIBC");
        await CaricaAsync(Kml(("CROTONE CTR LIBC", "Control Traffic Region")));

        Assert.Equal(0, (await _atz.ApplyAsync()).Applied);
        Assert.Null((await RicaricaAsync(id)).RegionMapPolygon);
    }

    [Fact]
    public async Task Senza_Catalogo_Caricato_Non_Succede_Niente()
    {
        var id = await TorreSenzaAreaAsync("LIBC");

        Assert.Equal(AtzTowerShapeResult.Empty, await _atz.ApplyAsync());
        Assert.Null((await RicaricaAsync(id)).RegionMapPolygon);
    }

    [Fact]
    public async Task Il_Sectorfile_Si_Riprende_Una_Torre_Che_Laip_Aveva_Riempito()
    {
        // ⚠️ Fra i due il sectorfile è la fonte primaria: l'AIP è secondaria, «solo se non la trovi nel
        // sectorfile». Senza questa regola l'ATZ resterebbe anche il giorno che twrs.tfl impara quella torre.
        var id = await TorreSenzaAreaAsync("LIBC");
        await CaricaAsync(Kml(("ATZ CROTONE LIBC", "Airspace class G")));
        await _atz.ApplyAsync();

        var daSectorfile = "[[12.9,42.9],[13.0,42.9],[13.0,43.0],[12.9,43.0]]";
        var github = new GithubTowerShapeService(_repo, new SorgenteTorri(("LIBC_TWR", daSectorfile)));
        Assert.Equal(1, await github.ApplyAsync());

        var torre = await RicaricaAsync(id);
        Assert.Equal(daSectorfile, torre.RegionMapPolygon);

        // ⚠️ Carta refactor 15: la precedenza la dice il risolutore, e una shape VERA del catalogo sta
        // sopra il ripiego dell'AIP. I pezzi dell'ATZ restano in archivio — non si cancellano da sé, e
        // tornerebbero utili se il sectorfile si ritirasse — ma non si vedono più.
        var forma = (await _forme.ResolveAsync(["LIBC_TWR"]))["LIBC_TWR"];
        Assert.Equal(ShapeSource.Source, forma.Source);
        Assert.Equal(daSectorfile, Assert.Single(forma.Parts).PolygonJson);
    }

    /// <summary>Una sorgente di poligoni TWR finta: quel che <c>twrs.tfl</c> darebbe.</summary>
    private sealed class SorgenteTorri : Vipi.Application.Abstractions.ITowerShapeSource
    {
        private readonly Dictionary<string, string> _poligoni;

        public SorgenteTorri(params (string Callsign, string Json)[] righe) =>
            _poligoni = righe.ToDictionary(r => r.Callsign, r => r.Json, StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyDictionary<string, string>> GetTowerPolygonsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(_poligoni);
    }
}
