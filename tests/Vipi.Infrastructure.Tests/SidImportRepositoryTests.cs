using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>Merge SID importate: preserva le manuali e ri-applica priorità/forzatura per StableKey tra import.</summary>
public class SidImportRepositoryTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfAirportRepository _repo = default!;

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
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private static ImportedSid Imp(string name, string fix, string key, string? rwy = "07") =>
        new(Runway: rwy, Fix: fix, Name: name, Transition: null, Type: "RNAV", StableKey: key, NeedsFixReview: false);

    [Fact]
    public async Task Import_Preserves_Manual_And_Reapplies_Priority()
    {
        // Una SID manuale.
        await _repo.SaveSidsAsync("LIRF", new[] { new SidRow(0, "07", "OSTIA", "OST7A", null, "5000ft", "CONV", null, null, null) });

        // Primo import: due righe.
        await _repo.ReplaceImportedSidsAsync("LIRF", new[]
        {
            Imp("ALAX7G", "ALAXI", "LIRF|ALAXI|G|"),
            Imp("ALAX7J", "ALAXI", "LIRF|ALAXI|J|"),
        }, "2606");

        var afterFirst = (await _repo.LoadAsync("LIRF"))!.Sids;
        Assert.Equal(3, afterFirst.Count);                                   // 1 manuale + 2 importate
        Assert.Single(afterFirst.Where(s => !s.IsImported));                 // la manuale c'è
        var g = afterFirst.Single(s => s.Name == "ALAX7G");

        // Priorità + forzatura su una importata.
        await _repo.UpdateImportedSidAsync(g.Id, priority: 1, forcePublished: true, resolvedFix: null);

        // Secondo import: il codice cambia revisione (7G→8G) ma la StableKey resta → priorità/forzatura preservate.
        await _repo.ReplaceImportedSidsAsync("LIRF", new[]
        {
            Imp("ALAX8G", "ALAXI", "LIRF|ALAXI|G|"),
            Imp("ALAX7J", "ALAXI", "LIRF|ALAXI|J|"),
        }, "2607");

        var afterSecond = (await _repo.LoadAsync("LIRF"))!.Sids;
        Assert.Equal(3, afterSecond.Count);
        Assert.Single(afterSecond.Where(s => !s.IsImported && s.Name == "OST7A"));   // manuale intatta
        var g2 = afterSecond.Single(s => s.Name == "ALAX8G");
        Assert.Equal(1, g2.Priority);                                        // priorità mantenuta
        Assert.True(g2.ForcePublished);                                      // forzatura mantenuta
        Assert.Equal("2607", g2.SourceAiracCycle);
        var j2 = afterSecond.Single(s => s.Name == "ALAX7J");
        Assert.Null(j2.Priority);                                            // l'altra resta senza priorità
    }

    [Fact]
    public async Task SaveManualSids_Does_Not_Touch_Imported()
    {
        await _repo.ReplaceImportedSidsAsync("LIRF", new[] { Imp("ALAX7G", "ALAXI", "LIRF|ALAXI|G|") }, "2606");
        await _repo.SaveSidsAsync("LIRF", new[] { new SidRow(0, "07", "OSTIA", "OST7A", null, null, null, null, null, null) });

        var sids = (await _repo.LoadAsync("LIRF"))!.Sids;
        Assert.Single(sids.Where(s => s.IsImported));       // importata ancora presente
        Assert.Single(sids.Where(s => !s.IsImported));      // manuale presente
    }
}
