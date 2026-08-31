using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Derivazione a view-time della sezione SID (doc 10 §3e): merge editoriali+importate, importate differite al ciclo
/// successivo al prelievo (o forzate), ordine per FIX + priorità. Fu la PRIMA sezione d'aeroporto a
/// smettere di essere cotta nel documento; dalla carta 2026-08-26 lo sono tutte.
/// </summary>
public class AirportSidDerivationServiceTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfAirportRepository _repo = default!;
    private AirportSidDerivationService _sut = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        var acc = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        _db.Accs.Add(acc);
        _db.Airports.Add(new Airport { Icao = "LIRF", Name = "Fiumicino", Acc = acc });
        await _db.SaveChangesAsync();
        _repo = new EfAirportRepository(_db, new EfMediaMaintenance(_db));
        _sut = new AirportSidDerivationService(_repo, new AiracService());
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private static ImportedSid Imp(string name, string fix, string key) =>
        new(Runway: "07", Fix: fix, Name: name, Transition: null, Type: "RNAV", StableKey: key, NeedsFixReview: false);

    [Fact]
    public async Task Manual_Public_Imported_Deferred_Then_Forced()
    {
        // Manuale (sempre pubblica) + importata prelevata a un ciclo FUTURO (differita: non ancora pubblica).
        await _repo.SaveSidsAsync("LIRF", new[] { new SidRow(0, "07", "OSTIA", "OST7A", null, "5000ft", "CONV", null, null, null) });
        await _repo.ReplaceImportedSidsAsync("LIRF", new[] { Imp("ALAX7G", "ALAXI", "LIRF|ALAXI|G|") }, "3512");

        var v = await _sut.DeriveAsync("LIRF");
        var row = Assert.Single(v.Rows);
        Assert.Equal("OSTIA", row.Fix);
        Assert.Equal("OST7A", row.Name);
        Assert.Equal("—", row.Transition);   // campo vuoto → trattino (render-ready)

        // Forzata → compare; ordine per FIX: ALAXI prima di OSTIA.
        var g = (await _repo.LoadAsync("LIRF"))!.Sids.Single(s => s.Name == "ALAX7G");
        await _repo.UpdateImportedSidAsync(g.Id, priority: null, forcePublished: true, resolvedFix: null,
            initialClimb: null, initialClimbByApp: false, cat: null, wtc: null, condition: null);

        var v2 = await _sut.DeriveAsync("LIRF");
        Assert.Equal(new[] { "ALAXI", "OSTIA" }, v2.Rows.Select(r => r.Fix).ToArray());
    }

    [Fact]
    public async Task Unknown_Airport_Is_Empty()
    {
        Assert.Empty((await _sut.DeriveAsync("ZZZZ")).Rows);
    }

    /// <summary>
    /// Il ciclo a cui si guarda è un PARAMETRO, e serve all'anteprima di una release programmata: una SID
    /// prelevata nel ciclo corrente non è pubblica adesso — compare dal ciclo dopo — ma nell'anteprima della
    /// release che esce a quel ciclo dopo ci deve essere, perché è quello che il lettore vedrà.
    /// <para>⚠️ Senza, l'anteprima del 2608 mostrava la tabella di oggi: le SID prelevate nel ciclo in corso
    /// mancavano dall'anteprima e poi comparivano da sole in pubblico al rollover.</para>
    /// </summary>
    [Fact]
    public async Task Imported_This_Cycle_Shows_In_The_Preview_Of_The_Next_One()
    {
        var airac = new AiracService();
        var cicli = airac.NextCycles(DateTime.UtcNow, 3);
        var oggi = cicli[0].Cycle;
        var prossimo = cicli[1].Cycle;

        // Prelevata ORA: differita al ciclo successivo.
        await _repo.ReplaceImportedSidsAsync("LIRF", new[] { Imp("ALAX7G", "ALAXI", "LIRF|ALAXI|G|") }, oggi);

        Assert.Empty((await _sut.DeriveAsync("LIRF")).Rows);                       // «adesso»: non ancora
        Assert.Empty((await _sut.DeriveAsync("LIRF", oggi)).Rows);                 // idem, chiedendolo per nome
        Assert.Equal("ALAXI", Assert.Single((await _sut.DeriveAsync("LIRF", prossimo)).Rows).Fix);
    }

    /// <summary>
    /// ⚠️ Il ciclo sposta la domanda nel tempo, non abolisce il differimento: una SID prelevata DENTRO il
    /// ciclo di rilascio non compare nemmeno nella sua anteprima — uscirà a quello dopo, ed è giusto che
    /// l'anteprima lo dica invece di promettere una riga che al rilascio non ci sarà.
    /// </summary>
    [Fact]
    public async Task Imported_In_The_Release_Cycle_Stays_Deferred_Even_In_Its_Preview()
    {
        var cicli = new AiracService().NextCycles(DateTime.UtcNow, 3);
        var prossimo = cicli[1].Cycle;

        await _repo.ReplaceImportedSidsAsync("LIRF", new[] { Imp("ALAX7G", "ALAXI", "LIRF|ALAXI|G|") }, prossimo);

        Assert.Empty((await _sut.DeriveAsync("LIRF", prossimo)).Rows);             // il buffer di un ciclo resta
        Assert.Single((await _sut.DeriveAsync("LIRF", cicli[2].Cycle)).Rows);      // e cade a quello dopo
    }
}
