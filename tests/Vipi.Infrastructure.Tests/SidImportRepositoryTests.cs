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
        _repo = new EfAirportRepository(_db, new EfMediaMaintenance(_db));
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
        Assert.Single(afterFirst, s => !s.IsImported);                       // la manuale c'è
        var g = afterFirst.Single(s => s.Name == "ALAX7G");

        // Priorità + forzatura su una importata.
        await _repo.UpdateImportedSidAsync(g.Id, priority: 1, forcePublished: true, resolvedFix: null,
            initialClimb: null, initialClimbByApp: false, cat: null, wtc: null, condition: null);

        // Secondo import: il codice cambia revisione (7G→8G) ma la StableKey resta → priorità/forzatura preservate.
        await _repo.ReplaceImportedSidsAsync("LIRF", new[]
        {
            Imp("ALAX8G", "ALAXI", "LIRF|ALAXI|G|"),
            Imp("ALAX7J", "ALAXI", "LIRF|ALAXI|J|"),
        }, "2607");

        var afterSecond = (await _repo.LoadAsync("LIRF"))!.Sids;
        Assert.Equal(3, afterSecond.Count);
        Assert.Single(afterSecond, s => !s.IsImported && s.Name == "OST7A");         // manuale intatta
        var g2 = afterSecond.Single(s => s.Name == "ALAX8G");
        Assert.Equal(1, g2.Priority);                                        // priorità mantenuta
        Assert.True(g2.ForcePublished);                                      // forzatura mantenuta
        Assert.Equal("2607", g2.SourceAiracCycle);
        var j2 = afterSecond.Single(s => s.Name == "ALAX7J");
        Assert.Null(j2.Priority);                                            // l'altra resta senza priorità
    }

    [Fact]
    public async Task Reimport_Sopravvive_A_Due_Revisioni_Con_La_Stessa_StableKey()
    {
        // La StableKey esclude di proposito la cifra della revisione, quindi un file .sid che contiene DUE revisioni
        // della stessa SID produce due righe con la stessa chiave. È il caso reale: sul DB di sviluppo ci sono 20
        // coppie così (LIRF, LIMC, LIME, LIBG…). Il primo import passa; il secondo indicizzava le righe precedenti
        // con un dizionario a chiave unica e lanciava, quindi l'import di quegli aeroporti era rotto per sempre.
        var due = new[]
        {
            Imp("ROBO1H", "ROBOT", "LIRF|ROBOT|H||07"),
            Imp("ROBO2H", "ROBOT", "LIRF|ROBOT|H||07"),
        };

        await _repo.ReplaceImportedSidsAsync("LIRF", due, "2606");
        Assert.Equal(2, (await _repo.LoadAsync("LIRF"))!.Sids.Count(s => s.IsImported));

        await _repo.ReplaceImportedSidsAsync("LIRF", due, "2607");   // prima lanciava ArgumentException

        var sids = (await _repo.LoadAsync("LIRF"))!.Sids.Where(s => s.IsImported).ToList();
        Assert.Equal(2, sids.Count);                                          // nessuna riga persa né duplicata
        Assert.Equal(new[] { "ROBO1H", "ROBO2H" }, sids.Select(s => s.Name).OrderBy(n => n));
    }

    [Fact]
    public async Task Con_Chiave_Duplicata_Gli_Arricchimenti_Si_Riapplicano_In_Modo_Deterministico()
    {
        var due = new[]
        {
            Imp("ROBO1H", "ROBOT", "LIRF|ROBOT|H||07"),
            Imp("ROBO2H", "ROBOT", "LIRF|ROBOT|H||07"),
        };
        await _repo.ReplaceImportedSidsAsync("LIRF", due, "2606");

        // Arricchimento editoriale sulla prima riga della coppia.
        var first = (await _repo.LoadAsync("LIRF"))!.Sids.Where(s => s.IsImported).OrderBy(s => s.Id).First();
        await _repo.UpdateImportedSidAsync(first.Id, priority: 3, forcePublished: true, resolvedFix: null,
            initialClimb: "5000ft", initialClimbByApp: false, cat: null, wtc: null, condition: null);

        await _repo.ReplaceImportedSidsAsync("LIRF", due, "2607");

        // Regola first-wins: l'arricchimento associato alla chiave torna su TUTTE le righe che la condividono.
        // Non è ambiguo per l'utente — la chiave È l'identità editoriale, la revisione no.
        var sids = (await _repo.LoadAsync("LIRF"))!.Sids.Where(s => s.IsImported).ToList();
        Assert.Equal(2, sids.Count);
        Assert.All(sids, s => Assert.Equal(3, s.Priority));
        Assert.All(sids, s => Assert.Equal("5000ft", s.InitialClimb));
        Assert.All(sids, s => Assert.True(s.ForcePublished));
    }

    [Fact]
    public async Task Reimport_Unchanged_Keeps_First_SourceCycle()
    {
        // Stesso contenuto SID prelevato a due cicli diversi: conserva il ciclo di PRIMO prelievo, così passato
        // quel ciclo la SID diventa pubblica (IsPublicAt) e ci resta — il re-timbro non la ri-nasconde.
        await _repo.ReplaceImportedSidsAsync("LIRF", new[] { Imp("ALAX7G", "ALAXI", "LIRF|ALAXI|G|") }, "2606");
        await _repo.ReplaceImportedSidsAsync("LIRF", new[] { Imp("ALAX7G", "ALAXI", "LIRF|ALAXI|G|") }, "2607");

        var s = (await _repo.LoadAsync("LIRF"))!.Sids.Single(x => x.IsImported);
        Assert.Equal("2606", s.SourceAiracCycle);
    }

    [Fact]
    public async Task Reimport_Preserves_Manually_Resolved_Fix()
    {
        // Import con fix non risolto (prefisso grezzo, da verificare).
        await _repo.ReplaceImportedSidsAsync("LIRF", new[]
        {
            new ImportedSid("07", "ZZZ", "ZZZ5A", null, "RNAV", "LIRF|ZZZ|A||07", NeedsFixReview: true),
        }, "2606");
        var imp = (await _repo.LoadAsync("LIRF"))!.Sids.Single(s => s.IsImported);
        Assert.True(imp.NeedsFixReview);

        // L'operatore risolve il fix a mano.
        await _repo.UpdateImportedSidAsync(imp.Id, priority: null, forcePublished: false, resolvedFix: "ZAGRE",
            initialClimb: null, initialClimbByApp: false, cat: null, wtc: null, condition: null);

        // Reimport: la sorgente ripropone ancora il prefisso grezzo → la risoluzione manuale va conservata.
        await _repo.ReplaceImportedSidsAsync("LIRF", new[]
        {
            new ImportedSid("07", "ZZZ", "ZZZ5A", null, "RNAV", "LIRF|ZZZ|A||07", NeedsFixReview: true),
        }, "2607");

        var after = (await _repo.LoadAsync("LIRF"))!.Sids.Single(s => s.IsImported);
        Assert.Equal("ZAGRE", after.Fix);
        Assert.False(after.NeedsFixReview);
    }

    [Fact]
    public async Task Editorial_Enrichments_On_Imported_Persist_And_Survive_Reimport()
    {
        await _repo.ReplaceImportedSidsAsync("LIRF", new[] { Imp("ALAX7G", "ALAXI", "LIRF|ALAXI|G|") }, "2606");
        var imp = (await _repo.LoadAsync("LIRF"))!.Sids.Single(s => s.IsImported);

        // L'operatore aggiunge gli arricchimenti editoriali che la sorgente non fornisce.
        await _repo.UpdateImportedSidAsync(imp.Id, priority: null, forcePublished: false, resolvedFix: null,
            initialClimb: "5000", initialClimbByApp: true, cat: "C, D", wtc: "M, H", condition: "solo notte");

        var saved = (await _repo.LoadAsync("LIRF"))!.Sids.Single(s => s.IsImported);
        Assert.Equal("5000", saved.InitialClimb);
        Assert.True(saved.InitialClimbByApp);
        Assert.Equal("C, D", saved.Cat);
        Assert.Equal("M, H", saved.Wtc);
        Assert.Equal("solo notte", saved.Condition);

        // Reimport della stessa riga (StableKey invariata): gli arricchimenti a mano non vanno persi.
        await _repo.ReplaceImportedSidsAsync("LIRF", new[] { Imp("ALAX7G", "ALAXI", "LIRF|ALAXI|G|") }, "2607");
        var after = (await _repo.LoadAsync("LIRF"))!.Sids.Single(s => s.IsImported);
        Assert.Equal("5000", after.InitialClimb);
        Assert.True(after.InitialClimbByApp);
        Assert.Equal("C, D", after.Cat);
        Assert.Equal("M, H", after.Wtc);
        Assert.Equal("solo notte", after.Condition);
    }

    [Fact]
    public async Task SaveManualSids_Does_Not_Touch_Imported()
    {
        await _repo.ReplaceImportedSidsAsync("LIRF", new[] { Imp("ALAX7G", "ALAXI", "LIRF|ALAXI|G|") }, "2606");
        await _repo.SaveSidsAsync("LIRF", new[] { new SidRow(0, "07", "OSTIA", "OST7A", null, null, null, null, null, null) });

        var sids = (await _repo.LoadAsync("LIRF"))!.Sids;
        Assert.Single(sids, s => s.IsImported);             // importata ancora presente
        Assert.Single(sids, s => !s.IsImported);            // manuale presente
    }
}
