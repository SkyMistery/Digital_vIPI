using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Derivazione a view-time della sezione SID (doc 10 §3e): merge editoriali+importate, importate in attesa
/// finché il ciclo non raggiunge quello DA CUI valgono (o forzate), ordine per FIX + priorità. Fu la PRIMA
/// sezione d'aeroporto a smettere di essere cotta nel documento; dalla carta 2026-08-26 lo sono tutte.
/// <para>⚠️ Dalla carta 2026-09-02 §AW2 il ciclo passato a <c>ReplaceImportedSidsAsync</c> è «il ciclo DAL
/// QUALE la riga vale», non «il ciclo in cui l'ho presa»: il buffer di uno non si somma più qui — lo decide
/// <c>SidStampCycle</c>, e solo dove la sorgente il ciclo non lo dichiara.</para>
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
        // Manuale (sempre pubblica) + importata che entra a un ciclo FUTURO (in attesa: non ancora pubblica).
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
    /// Il ciclo a cui si guarda è un PARAMETRO, e serve all'anteprima di una release programmata: una SID che
    /// entra al ciclo prossimo non è pubblica adesso, ma nell'anteprima della release che esce a quel ciclo
    /// ci deve essere, perché è quello che il lettore vedrà.
    /// <para>⚠️ Senza, l'anteprima del 2609 mostrava la tabella di oggi: le SID del ciclo entrante mancavano
    /// dall'anteprima e poi comparivano da sole in pubblico al rollover.</para>
    /// </summary>
    [Fact]
    public async Task Imported_For_The_Next_Cycle_Shows_In_Its_Preview()
    {
        var airac = new AiracService();
        var cicli = airac.NextCycles(DateTime.UtcNow, 3);
        var oggi = cicli[0].Cycle;
        var prossimo = cicli[1].Cycle;

        // In vigore DAL ciclo prossimo.
        await _repo.ReplaceImportedSidsAsync("LIRF", new[] { Imp("ALAX7G", "ALAXI", "LIRF|ALAXI|G|") }, prossimo);

        Assert.Empty((await _sut.DeriveAsync("LIRF")).Rows);                       // «adesso»: non ancora
        Assert.Empty((await _sut.DeriveAsync("LIRF", oggi)).Rows);                 // idem, chiedendolo per nome
        Assert.Equal("ALAXI", Assert.Single((await _sut.DeriveAsync("LIRF", prossimo)).Rows).Fix);
    }

    /// <summary>
    /// ⚠️ <b>Il ciclo d'entrata è quello, e non «quello dopo»</b> — è il cuore della carta §AW2. Una release
    /// programmata al ciclo entrante deve contenere le SID che entrano <b>a quel ciclo</b>: prima
    /// l'anteprima le nascondeva ancora (il buffer si sommava una seconda volta qui) e uscivano al ciclo
    /// successivo, cioè con un mese di ritardo su quanto scritto nel changelog della sorgente.
    /// </summary>
    [Fact]
    public async Task Imported_For_A_Cycle_Is_In_The_Release_Of_That_Cycle()
    {
        var cicli = new AiracService().NextCycles(DateTime.UtcNow, 3);
        var oggi = cicli[0].Cycle;
        var prossimo = cicli[1].Cycle;

        await _repo.ReplaceImportedSidsAsync("LIRF", new[] { Imp("ALAX7G", "ALAXI", "LIRF|ALAXI|G|") }, prossimo);

        Assert.Empty((await _sut.DeriveAsync("LIRF", oggi)).Rows);                 // al ciclo di oggi ancora no
        Assert.Single((await _sut.DeriveAsync("LIRF", prossimo)).Rows);            // al SUO ciclo, sì
        Assert.Single((await _sut.DeriveAsync("LIRF", cicli[2].Cycle)).Rows);      // e ci resta
    }
}
