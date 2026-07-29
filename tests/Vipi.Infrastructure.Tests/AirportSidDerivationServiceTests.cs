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
/// successivo al prelievo (o forzate), ordine per FIX + priorità. Sostituisce la cottura in RebuildDocumentAsync.
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
        _repo = new EfAirportRepository(_db);
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
}
