using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Import settori ATC d'aeroporto dalla sorgente: upsert per ComposePosition (incl. APP), idempotente,
/// con default limiti GND/19500, ereditando l'ACC dall'aeroporto e preservando IsHidden + limiti admin.
/// </summary>
public class AirportSectorImportTests : IAsyncLifetime
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

        // Un ACC + un aeroporto di competenza (l'import eredita l'ACC dall'aeroporto).
        var acc = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        _db.Accs.Add(acc);
        _db.Airports.Add(new Airport { Icao = "LIRN", Name = "Napoli", Acc = acc });
        await _db.SaveChangesAsync();

        _repo = new EfAirportSectorRepository(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    // Postazioni di Napoli, incluso un APP (che il vecchio percorso scartava) e ATIS/GND (senza limiti).
    private static IReadOnlyList<SourceAtcPosition> Positions() => new[]
    {
        new SourceAtcPosition("LIRN_ATIS", "127.000", "ATIS", null, null, null, null),
        new SourceAtcPosition("LIRN_GND", "121.900", "GND", null, "{\"poly\":9}", null, null),  // shape ignorata per GND
        new SourceAtcPosition("LIRN_TWR", "118.300", "TWR", null, "{\"poly\":1}", null, null),
        new SourceAtcPosition("LIRN_US0_APP", "124.350", "APP", "US0", null, null, null),
    };

    [Fact]
    public async Task Import_Creates_All_Sectors_Including_App_With_Per_Type_Limits()
    {
        var r = await _repo.ImportForAirportAsync("LIRN", Positions());

        Assert.Equal(4, r.Created);
        var sectors = await _repo.ListByAirportAsync("LIRN");
        Assert.Equal(4, sectors.Count);

        // APP incluso
        var app = sectors.Single(s => s.ComposePosition == "LIRN_US0_APP");
        Assert.Equal("APP", app.Position);
        Assert.Equal("US0", app.MiddleIdentifier);

        // ACC ereditato dall'aeroporto
        Assert.All(sectors, s => Assert.Equal("LIRR", s.AccCode));

        // TWR: GND(0)→3000 ft (le torri arrivano a 3000, non a FL195) + shape; APP: GND(0)→19500
        var twr = sectors.Single(s => s.ComposePosition == "LIRN_TWR");
        Assert.Equal(0, twr.LowerLimit);
        Assert.Equal(3000, twr.UpperLimit);
        Assert.True(twr.HasPolygon);
        Assert.Equal(0, app.LowerLimit);
        Assert.Equal(19500, app.UpperLimit);

        // GND e ATIS: niente limiti, niente shape (anche se la sorgente dà un poligono per la GND)
        foreach (var compose in new[] { "LIRN_GND", "LIRN_ATIS" })
        {
            var s = sectors.Single(x => x.ComposePosition == compose);
            Assert.Null(s.LowerLimit);
            Assert.Null(s.UpperLimit);
            Assert.False(s.HasPolygon);
        }
    }

    [Fact]
    public async Task Reimport_Is_Idempotent_And_Preserves_Hidden_And_Limits()
    {
        await _repo.ImportForAirportAsync("LIRN", Positions());
        var twr = (await _repo.ListByAirportAsync("LIRN")).Single(s => s.ComposePosition == "LIRN_TWR");

        await _repo.SetLimitsAsync(twr.Id, 0, 24500);
        await _repo.SetHiddenAsync(twr.Id, true);

        var r = await _repo.ImportForAirportAsync("LIRN", Positions());

        Assert.Equal(0, r.Created);
        Assert.Equal(4, r.Updated);

        var after = (await _repo.ListByAirportAsync("LIRN")).Single(s => s.ComposePosition == "LIRN_TWR");
        Assert.Equal(0, after.LowerLimit);
        Assert.Equal(24500, after.UpperLimit);
        Assert.True(after.IsHidden);
    }

    [Fact]
    public async Task Import_Sets_Default_Primary_Per_Type_And_SetPrimary_Is_Exclusive_Within_Type()
    {
        // Due TWR per verificare l'esclusiva per tipo.
        var positions = new[]
        {
            new SourceAtcPosition("LIRN_ATIS", "127.000", "ATIS", null, null, null, null),
            new SourceAtcPosition("LIRN_GND", "121.900", "GND", null, null, null, null),
            new SourceAtcPosition("LIRN_TWR", "118.300", "TWR", null, "{\"poly\":1}", null, null),
            new SourceAtcPosition("LIRN_N_TWR", "118.800", "TWR", "N", "{\"poly\":2}", null, null),
            new SourceAtcPosition("LIRN_US0_APP", "124.350", "APP", "US0", null, null, null),
        };
        await _repo.ImportForAirportAsync("LIRN", positions);
        var sectors = await _repo.ListByAirportAsync("LIRN");

        // default: una principale PER TIPO presente (GND, TWR, APP) = 3; ATIS mai principale
        Assert.Equal(3, sectors.Count(s => s.IsPrimary));
        Assert.False(sectors.Single(s => s.ComposePosition == "LIRN_ATIS").IsPrimary);
        Assert.Single(sectors, s => s.Position == "TWR" && s.IsPrimary);

        // scelta esplicita dell'altra TWR: esclusiva SOLO tra le TWR (GND/APP intatte)
        var otherTwr = sectors.Single(s => s.Position == "TWR" && !s.IsPrimary);
        await _repo.SetPrimaryAsync(otherTwr.Id);
        var after = await _repo.ListByAirportAsync("LIRN");
        Assert.Single(after, s => s.Position == "TWR" && s.IsPrimary);
        Assert.True(after.Single(s => s.Id == otherTwr.Id).IsPrimary);
        Assert.True(after.Single(s => s.ComposePosition == "LIRN_GND").IsPrimary);
        Assert.True(after.Single(s => s.ComposePosition == "LIRN_US0_APP").IsPrimary);

        // re-import preserva la scelta
        await _repo.ImportForAirportAsync("LIRN", positions);
        var reimported = await _repo.ListByAirportAsync("LIRN");
        Assert.True(reimported.Single(s => s.Id == otherTwr.Id).IsPrimary);
    }

    [Fact]
    public async Task Import_Unknown_Airport_Does_Nothing()
    {
        var r = await _repo.ImportForAirportAsync("ZZZZ", Positions());
        Assert.Equal(0, r.Created);
        Assert.Equal(0, r.Updated);
        Assert.Empty(await _repo.ListByAirportAsync("ZZZZ"));
    }

    [Fact]
    public async Task GetAccCode_Resolvers_Work()
    {
        await _repo.ImportForAirportAsync("LIRN", Positions());
        var s = (await _repo.ListByAirportAsync("LIRN")).First();

        Assert.Equal("LIRR", await _repo.GetAccCodeByIcaoAsync("LIRN"));
        Assert.Equal("LIRR", await _repo.GetAccCodeBySectorIdAsync(s.Id));
        Assert.Null(await _repo.GetAccCodeByIcaoAsync("ZZZZ"));
        Assert.Contains("LIRN", await _repo.ListAirportIcaosAsync());
    }

    // ---- La shape: l'assenza non cancella la presenza ------------------------------------------------

    private const string PoligonoVero = "[[14.29,40.88],[14.31,40.88],[14.31,40.90],[14.29,40.90]]";

    /// <summary>
    /// ⚠️ Il difetto misurato il 26 agosto 2026: IVAO ha smesso di mandare le shape e risponde
    /// <c>regionMapPolygon: []</c> su <b>tutte</b> le posizioni. L'upsert assegnava senza guardare, e su una
    /// copia del database vero un solo giro d'import portava <b>83 poligoni a zero</b> — 66 reali presi da
    /// GitHub e 17 cerchi di ripiego. Il giro notturno li rimetteva subito dopo, ma gli altri tre chiamanti no.
    /// </summary>
    [Fact]
    public async Task Una_shape_vuota_dalla_sorgente_non_cancella_quella_che_abbiamo()
    {
        await _repo.ImportForAirportAsync("LIRN", new[] { new SourceAtcPosition("LIRN_TWR", "118.300", "TWR") });
        var id = (await _db.AirportSectors.SingleAsync(x => x.ComposePosition == "LIRN_TWR")).Id;
        await _repo.SetRealShapeAsync(id, PoligonoVero);     // com'è arrivata da GitHub
        _db.ChangeTracker.Clear();

        await _repo.ImportForAirportAsync("LIRN", new[]
        {
            new SourceAtcPosition("LIRN_TWR", "118.300", "TWR", RegionMapPolygon: "[]"),
        });

        var dopo = await _db.AirportSectors.AsNoTracking().SingleAsync(x => x.ComposePosition == "LIRN_TWR");
        Assert.Equal(PoligonoVero, dopo.RegionMapPolygon);
        Assert.False(dopo.IsShapeSynthetic);
    }

    /// <summary>Nemmeno il cerchio di ripiego si perde: senza di lui la TWR resta senza area fino al prossimo giro.</summary>
    [Fact]
    public async Task Nemmeno_un_cerchio_sintetico_si_perde()
    {
        await _repo.ImportForAirportAsync("LIRN", new[] { new SourceAtcPosition("LIRN_TWR", "118.300", "TWR") });
        var id = (await _db.AirportSectors.SingleAsync(x => x.ComposePosition == "LIRN_TWR")).Id;
        await _repo.SetSyntheticShapeAsync(id, PoligonoVero);
        _db.ChangeTracker.Clear();

        await _repo.ImportForAirportAsync("LIRN", new[]
        {
            new SourceAtcPosition("LIRN_TWR", "118.300", "TWR", RegionMapPolygon: "[]"),
        });

        var dopo = await _db.AirportSectors.AsNoTracking().SingleAsync(x => x.ComposePosition == "LIRN_TWR");
        Assert.Equal(PoligonoVero, dopo.RegionMapPolygon);
        Assert.True(dopo.IsShapeSynthetic);   // resta un ripiego, così GitHub può ancora rimpiazzarlo
    }

    /// <summary>Il verso opposto: quando la sorgente manda una shape VERA, quella comanda — anche su un ripiego.</summary>
    [Fact]
    public async Task Una_shape_vera_dalla_sorgente_sovrascrive_il_ripiego()
    {
        await _repo.ImportForAirportAsync("LIRN", new[] { new SourceAtcPosition("LIRN_TWR", "118.300", "TWR") });
        var id = (await _db.AirportSectors.SingleAsync(x => x.ComposePosition == "LIRN_TWR")).Id;
        await _repo.SetSyntheticShapeAsync(id, "[[1,1],[2,2],[3,3]]");
        _db.ChangeTracker.Clear();

        await _repo.ImportForAirportAsync("LIRN", new[]
        {
            new SourceAtcPosition("LIRN_TWR", "118.300", "TWR", RegionMapPolygon: PoligonoVero),
        });

        var dopo = await _db.AirportSectors.AsNoTracking().SingleAsync(x => x.ComposePosition == "LIRN_TWR");
        Assert.Equal(PoligonoVero, dopo.RegionMapPolygon);
        Assert.False(dopo.IsShapeSynthetic);   // non è più un ripiego
    }

    /// <summary>Una riga NUOVA con shape vuota nasce senza shape, non con un `"[]"` che si spaccia per una forma:
    /// i ripieghi cercano proprio chi non ne ha.</summary>
    [Fact]
    public async Task Una_riga_nuova_con_shape_vuota_nasce_senza_shape()
    {
        await _repo.ImportForAirportAsync("LIRN", new[]
        {
            new SourceAtcPosition("LIRN_TWR", "118.300", "TWR", RegionMapPolygon: "[]"),
        });

        Assert.Null((await _db.AirportSectors.AsNoTracking()
            .SingleAsync(x => x.ComposePosition == "LIRN_TWR")).RegionMapPolygon);
    }

    // ---- Il giorno in cui IVAO torna a mandare le shape ----------------------------------------------

    /// <summary>
    /// ⚠️ IVAO ha confermato (26 agosto 2026) che l'assenza dei poligoni è un guasto loro e che lo
    /// sistemeranno. Quindi questo non è un caso ipotetico ma il prossimo che succederà: una riga riempita
    /// dal <b>ripiego</b> riceve finalmente la shape dell'<b>anagrafica</b>.
    ///
    /// <para>Quando accade, l'anagrafica deve <b>riprendere il comando per intero</b>: la provenienza torna
    /// <c>Source</c> e un eventuale differimento si chiude. Senza, la riga resterebbe marcata
    /// <c>Sectorfile</c> e il gate AIRAC continuerebbe ad applicarsi a una geometria che non ne ha bisogno —
    /// peggio, con un differimento aperto la release pubblicherebbe la <b>vecchia shape del sectorfile</b>
    /// al posto di quella vera, per settimane.</para>
    /// </summary>
    [Fact]
    public async Task Quando_l_anagrafica_torna_a_mandare_la_shape_riprende_il_comando()
    {
        await _repo.ImportForAirportAsync("LIRN", new[] { new SourceAtcPosition("LIRN_TWR", "118.300", "TWR") });
        var riga = await _db.AirportSectors.SingleAsync(x => x.ComposePosition == "LIRN_TWR");

        // Lo stato lasciato dal ripiego, con un differimento aperto.
        riga.RegionMapPolygon = "[[9.0,45.0],[9.5,45.0],[9.5,45.5]]";
        riga.RegionMapPolygonInForce = "[[8.0,44.0],[8.5,44.0],[8.5,44.5]]";
        riga.ShapeAiracCycle = "2610";
        riga.ShapeSource = ShapeSource.Sectorfile;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        await _repo.ImportForAirportAsync("LIRN", new[]
        {
            new SourceAtcPosition("LIRN_TWR", "118.300", "TWR", RegionMapPolygon: PoligonoVero),
        });

        var dopo = await _db.AirportSectors.AsNoTracking().SingleAsync(x => x.ComposePosition == "LIRN_TWR");
        Assert.Equal(PoligonoVero, dopo.RegionMapPolygon);
        Assert.Equal(ShapeSource.Source, dopo.ShapeSource);       // il comando torna all'anagrafica
        Assert.Null(dopo.ShapeAiracCycle);                        // e il differimento si chiude
        Assert.Null(dopo.RegionMapPolygonInForce);
    }

    /// <summary>Una shape VUOTA non riprende un bel niente: è l'assenza, e l'assenza non comanda.</summary>
    [Fact]
    public async Task Una_shape_vuota_non_toglie_il_comando_al_ripiego()
    {
        await _repo.ImportForAirportAsync("LIRN", new[] { new SourceAtcPosition("LIRN_TWR", "118.300", "TWR") });
        var riga = await _db.AirportSectors.SingleAsync(x => x.ComposePosition == "LIRN_TWR");
        riga.RegionMapPolygon = PoligonoVero;
        riga.ShapeAiracCycle = "2610";
        riga.ShapeSource = ShapeSource.Sectorfile;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        await _repo.ImportForAirportAsync("LIRN", new[]
        {
            new SourceAtcPosition("LIRN_TWR", "118.300", "TWR", RegionMapPolygon: "[]"),
        });

        var dopo = await _db.AirportSectors.AsNoTracking().SingleAsync(x => x.ComposePosition == "LIRN_TWR");
        Assert.Equal(ShapeSource.Sectorfile, dopo.ShapeSource);
        Assert.Equal("2610", dopo.ShapeAiracCycle);
    }
}
