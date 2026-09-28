using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Shape TWR reali da GitHub: applica il poligono vero alle TWR senza shape IVAO (match per callsign), marca REALE
/// (IsShapeSynthetic=false), mai sovrascrive una shape IVAO reale; dopo, il cerchio sintetico non le tocca più.
/// </summary>
public class GithubTowerShapeServiceTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfAirportSectorRepository _repo = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        var acc = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        _db.Accs.Add(acc);
        _db.Airports.Add(new Airport { Icao = "LIRN", Name = "Napoli", Acc = acc });
        _db.Airports.Add(new Airport { Icao = "LIRP", Name = "Pisa", Acc = acc });
        await _db.SaveChangesAsync();
        _repo = new EfAirportSectorRepository(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private sealed class FakeSource : ITowerShapeSource
    {
        private readonly IReadOnlyDictionary<string, string> _map;
        public FakeSource(IReadOnlyDictionary<string, string> map) => _map = map;
        public Task<IReadOnlyDictionary<string, string>> GetTowerPolygonsAsync(CancellationToken ct = default) =>
            Task.FromResult(_map);
    }

    [Fact]
    public async Task Applies_Github_Polygon_Only_To_Empty_Twr_As_Real_Shape()
    {
        // LIRN_TWR "[]" (senza shape IVAO); LIRP_TWR con shape IVAO reale.
        await _repo.ImportForAirportAsync("LIRN", new[]
        {
            new SourceAtcPosition("LIRN_TWR", "118.300", "TWR", null, "[]", null, null, 40.886, 14.291),
        });
        await _repo.ImportForAirportAsync("LIRP", new[]
        {
            new SourceAtcPosition("LIRP_TWR", "118.300", "TWR", null, "[[10.0,43.0],[10.1,43.0],[10.1,43.1]]", null, null, 43.68, 10.39),
        });

        var source = new FakeSource(new Dictionary<string, string>
        {
            ["LIRN_TWR"] = "[[14.2,40.8],[14.4,40.8],[14.4,41.0],[14.2,40.8]]",
            ["LIRP_TWR"] = "[[9.0,42.0],[9.1,42.0],[9.1,42.1],[9.0,42.0]]",   // presente ma LIRP ha già shape IVAO → ignorato
        });

        var svc = new GithubTowerShapeService(_repo, source);
        Assert.Equal(1, await svc.ApplyAsync());

        var lirn = await _db.AirportSectors.AsNoTracking().SingleAsync(s => s.ComposePosition == "LIRN_TWR");
        Assert.False(lirn.IsShapeSynthetic);   // poligono reale, non cerchio
        Assert.Equal("[[14.2,40.8],[14.4,40.8],[14.4,41.0],[14.2,40.8]]", lirn.RegionMapPolygon);

        // La shape IVAO reale non è toccata.
        var lirp = await _db.AirportSectors.AsNoTracking().SingleAsync(s => s.ComposePosition == "LIRP_TWR");
        Assert.Equal("[[10.0,43.0],[10.1,43.0],[10.1,43.1]]", lirp.RegionMapPolygon);

        // Dopo GitHub, il cerchio sintetico non ha più nulla da fare su LIRN.
        var circle = new TowerShapeFallbackService(_repo);
        Assert.Equal(0, await circle.ApplyAsync());
    }

    [Fact]
    public async Task Replaces_Synthetic_Circle_With_Github_Real_Shape()
    {
        // Regime stazionario reale: la TWR ha già un CERCHIO sintetico da un run precedente (proietta, ma è ripiego).
        await _repo.ImportForAirportAsync("LIRN", new[]
        {
            new SourceAtcPosition("LIRN_TWR", "118.300", "TWR", null, "[]", null, null, 40.886, 14.291),
        });
        var twr = await _db.AirportSectors.AsNoTracking().SingleAsync(s => s.ComposePosition == "LIRN_TWR");
        await _repo.SetSyntheticShapeAsync(twr.Id, "[[14.2,40.8],[14.25,40.85],[14.3,40.8],[14.2,40.8]]");

        var source = new FakeSource(new Dictionary<string, string>
        {
            ["LIRN_TWR"] = "[[14.1,40.7],[14.4,40.7],[14.4,41.0],[14.1,40.7]]",
        });
        var svc = new GithubTowerShapeService(_repo, source);

        // Il cerchio sintetico viene rimpiazzato dal poligono reale GitHub.
        Assert.Equal(1, await svc.ApplyAsync());
        var after = await _db.AirportSectors.AsNoTracking().SingleAsync(s => s.ComposePosition == "LIRN_TWR");
        Assert.False(after.IsShapeSynthetic);
        Assert.Equal("[[14.1,40.7],[14.4,40.7],[14.4,41.0],[14.1,40.7]]", after.RegionMapPolygon);

        // Idempotente: ora è reale non-sintetica → non più bersaglio.
        Assert.Equal(0, await svc.ApplyAsync());
    }

    /// <summary>
    /// ⚠️ Decisione del committente (26 agosto 2026): i ripieghi valgono solo per gli enti della divisione.
    /// La TWR di un campo estero prende l'area da IVAO o resta senza — né GitHub né il cerchio la toccano.
    /// </summary>
    [Fact]
    public async Task Una_torre_estera_non_prende_ne_la_shape_github_ne_il_cerchio()
    {
        var acc = await _db.Accs.SingleAsync(a => a.Code == "LIRR");
        _db.Airports.Add(new Airport { Icao = "LOWW", Name = "Vienna", Acc = acc });
        await _db.SaveChangesAsync();
        await _repo.ImportForAirportAsync("LOWW", new[]
        {
            new SourceAtcPosition("LOWW_TWR", "119.400", "TWR", null, "[]", null, null, 48.11, 16.57),
        });

        var source = new FakeSource(new Dictionary<string, string>
        {
            ["LOWW_TWR"] = "[[16.5,48.0],[16.6,48.0],[16.6,48.2],[16.5,48.0]]",
        });

        Assert.Equal(0, await new GithubTowerShapeService(_repo, source).ApplyAsync());
        Assert.Equal(0, await new TowerShapeFallbackService(_repo).ApplyAsync());

        var loww = await _db.AirportSectors.AsNoTracking().SingleAsync(s => s.ComposePosition == "LOWW_TWR");
        Assert.Null(loww.RegionMapPolygon);   // l'import non scrive il vuoto della sorgente: resta senza area
        Assert.False(loww.IsShapeSynthetic);
    }

    [Fact]
    public async Task Icao_Filter_Applies_Only_To_That_Airport()
    {
        // Due aeroporti con TWR vuota, entrambi su GitHub; il bottone manuale (icao) tocca solo il suo.
        await _repo.ImportForAirportAsync("LIRN", new[]
        {
            new SourceAtcPosition("LIRN_TWR", "118.300", "TWR", null, "[]", null, null, 40.886, 14.291),
        });
        await _repo.ImportForAirportAsync("LIRP", new[]
        {
            new SourceAtcPosition("LIRP_TWR", "118.300", "TWR", null, "[]", null, null, 43.68, 10.39),
        });

        var source = new FakeSource(new Dictionary<string, string>
        {
            ["LIRN_TWR"] = "[[14.2,40.8],[14.4,40.8],[14.4,41.0],[14.2,40.8]]",
            ["LIRP_TWR"] = "[[9.0,42.0],[9.1,42.0],[9.1,42.1],[9.0,42.0]]",
        });

        var svc = new GithubTowerShapeService(_repo, source);
        Assert.Equal(1, await svc.ApplyAsync("LIRN"));

        // Altro aeroporto: intatto, cioè ancora senza shape. ⚠️ Fino al 26 agosto 2026 qui si leggeva
        // `Assert.Equal("[]", …)`: si presidiava il modo in cui l'assenza era scritta in colonna, non il fatto
        // che GitHub non l'avesse toccato. Ora una shape vuota dalla sorgente non entra proprio in archivio
        // (PolygonGeometry.IsEmptyShape), e l'asserzione dice quel che ha sempre voluto dire.
        var lirp = await _db.AirportSectors.AsNoTracking().SingleAsync(s => s.ComposePosition == "LIRP_TWR");
        Assert.Null(lirp.RegionMapPolygon);
    }

    [Fact]
    public async Task Leaves_Circle_For_Twr_Not_In_Github()
    {
        await _repo.ImportForAirportAsync("LIRN", new[]
        {
            new SourceAtcPosition("LIRN_TWR", "118.300", "TWR", null, "[]", null, null, 40.886, 14.291),
        });

        var svc = new GithubTowerShapeService(_repo, new FakeSource(new Dictionary<string, string>()));
        Assert.Equal(0, await svc.ApplyAsync());   // GitHub non ha LIRN_TWR

        // Il cerchio sintetico interviene come prima.
        var circle = new TowerShapeFallbackService(_repo);
        Assert.Equal(1, await circle.ApplyAsync());
        var lirn = await _db.AirportSectors.AsNoTracking().SingleAsync(s => s.ComposePosition == "LIRN_TWR");
        Assert.True(lirn.IsShapeSynthetic);
    }

    // ---- U-037: la provenienza e il ciclo -----------------------------------------------------------------

    /// <summary>Un orologio fermo al 28 settembre 2026: il ciclo corrente è il 2609, il successivo il 2610 (1° ottobre).</summary>
    private sealed class Orologio : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
    }

    private GithubTowerShapeService Servizio(ITowerShapeSource sorgente) =>
        new(_repo, sorgente, airac: new Vipi.Domain.Services.AiracService(), clock: new Orologio());

    private const string Prima = "[[14.2,40.8],[14.4,40.8],[14.4,41.0],[14.2,40.8]]";
    private const string Dopo = "[[14.1,40.7],[14.5,40.7],[14.5,41.1],[14.1,40.7]]";

    private async Task<AirportSector> Lirn() =>
        await _db.AirportSectors.AsNoTracking().SingleAsync(s => s.ComposePosition == "LIRN_TWR");

    private Task TorreSenzaArea() => _repo.ImportForAirportAsync("LIRN", new[]
    {
        new SourceAtcPosition("LIRN_TWR", "118.300", "TWR", null, "[]", null, null, 40.886, 14.291),
    });

    /// <summary>
    /// 🔴 U-037 (revisione totale 3): l'area presa da <c>twrs.tfl</c> restava marcata come dell'anagrafica
    /// (<c>ShapeSource.Source</c>, quella di IVAO), e da lì non era più un bersaglio: 66 torri su 70 avevano
    /// l'anello di <c>twrs.tfl</c> e nessuna si sarebbe mai aggiornata. Ora porta la provenienza del sectorfile.
    /// </summary>
    [Fact]
    public async Task L_area_presa_da_twrs_porta_la_provenienza_del_sectorfile()
    {
        await TorreSenzaArea();

        Assert.Equal(1, await Servizio(new FakeSource(new Dictionary<string, string> { ["LIRN_TWR"] = Prima })).ApplyAsync());

        var t = await Lirn();
        Assert.Equal(Vipi.Domain.ShapeSource.Sectorfile, t.ShapeSource);
        Assert.Null(t.ShapeAiracCycle);        // la prima area entra subito: nessuna area è peggio di una in anticipo
    }

    /// <summary>
    /// 🔴 U-037: la divisione ridisegna in <c>twrs.tfl</c> l'area di una torre. Entra, come quella dei settori, dal
    /// ciclo SUCCESSIVO — il sectorfile lo scriviamo in anticipo — e quella di adesso resta in vigore per chi
    /// pubblica nel frattempo.
    /// </summary>
    [Fact]
    public async Task Un_area_ridisegnata_entra_dal_ciclo_successivo()
    {
        await TorreSenzaArea();
        await Servizio(new FakeSource(new Dictionary<string, string> { ["LIRN_TWR"] = Prima })).ApplyAsync();

        Assert.Equal(1, await Servizio(new FakeSource(new Dictionary<string, string> { ["LIRN_TWR"] = Dopo })).ApplyAsync());

        var t = await Lirn();
        Assert.Equal(Dopo, t.RegionMapPolygon);
        Assert.Equal(Prima, t.RegionMapPolygonInForce);
        Assert.Equal("2610", t.ShapeAiracCycle);
        // Idempotente: identica alla corrente, niente da fare.
        Assert.Equal(0, await Servizio(new FakeSource(new Dictionary<string, string> { ["LIRN_TWR"] = Dopo })).ApplyAsync());
    }

    /// <summary>
    /// 🔴 U-037: le torri che hanno GIÀ l'anello di <c>twrs.tfl</c> (scritte prima, senza provenienza) si
    /// riconoscono: stessa geometria, e si segnano come del sectorfile senza toccare niente. Una shape diversa da
    /// <c>twrs.tfl</c> con la provenienza dell'anagrafica è di IVAO, e resta sua.
    /// </summary>
    [Fact]
    public async Task Le_torri_scritte_prima_si_riconoscono_e_poi_si_aggiornano()
    {
        await TorreSenzaArea();
        var id = (await Lirn()).Id;
        var riga = await _db.AirportSectors.SingleAsync(s => s.Id == id);
        riga.RegionMapPolygon = Prima;                       // com'era scritta fino a oggi
        riga.ShapeSource = Vipi.Domain.ShapeSource.Source;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        Assert.Equal(0, await Servizio(new FakeSource(new Dictionary<string, string> { ["LIRN_TWR"] = Prima })).ApplyAsync());
        var t = await Lirn();
        Assert.Equal(Vipi.Domain.ShapeSource.Sectorfile, t.ShapeSource);
        Assert.Equal(Prima, t.RegionMapPolygon);

        Assert.Equal(1, await Servizio(new FakeSource(new Dictionary<string, string> { ["LIRN_TWR"] = Dopo })).ApplyAsync());
        Assert.Equal("2610", (await Lirn()).ShapeAiracCycle);
    }
}
