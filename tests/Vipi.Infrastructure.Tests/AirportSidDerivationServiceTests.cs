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
/// <para>⚠️ Dalla carta 2026-09-02 §AW2 il ciclo passato a <c>ReplaceImportedProceduresAsync</c> è «il ciclo DAL
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

    private static ImportedProcedure Imp(string name, string fix, string key) =>
        new(Runway: "07", Fix: fix, Name: name, Transition: null, Type: "RNAV", StableKey: key, NeedsFixReview: false);

    // --- I due versi: stessa derivazione, tabelle separate ---------------------------------------------

    [Fact]
    public async Task La_Derivazione_Da_Il_Verso_Che_Le_Si_Chiede()
    {
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid,
            new[] { Imp("ALAX7G", "ALAXI", "LIRF|ALAXI|G|") }, "2001");
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Star,
            new[] { Imp("GILI3A", "GILIO", "STAR|LIRF|GILIO|A|") }, "2001");

        var partenze = await _sut.DeriveAsync("LIRF", ProcedureKind.Sid);
        var arrivi = await _sut.DeriveAsync("LIRF", ProcedureKind.Star);

        Assert.Equal("ALAX7G", Assert.Single(partenze.Rows).Name);
        Assert.Equal("GILI3A", Assert.Single(arrivi.Rows).Name);
    }

    [Fact]
    public async Task Gli_Arrivi_Nascosti_E_In_Attesa_Si_Comportano_Come_Le_Partenze()
    {
        var cicli = new AiracService();
        var oggi = cicli.GetCycle(DateTime.UtcNow);
        var prossimo = cicli.NextCycles(DateTime.UtcNow, 2)[1].Cycle;

        // Una STAR che entra al ciclo PROSSIMO: oggi non si vede, al suo ciclo sì.
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Star,
            new[] { Imp("GILI3A", "GILIO", "STAR|LIRF|GILIO|A|") }, prossimo);

        Assert.Empty((await _sut.DeriveAsync("LIRF", ProcedureKind.Star, oggi)).Rows);
        var riga = Assert.Single((await _sut.DeriveAsync("LIRF", ProcedureKind.Star, prossimo)).Rows);
        Assert.Equal("GILIO", riga.Fix);

        // Nascosta dallo staff: fuori a qualunque ciclo, come una SID.
        var sezione = await _db.AirportProcedures.SingleAsync(x => x.Kind == ProcedureKind.Star);
        sezione.IsHidden = true;
        await _db.SaveChangesAsync();
        Assert.Empty((await _sut.DeriveAsync("LIRF", ProcedureKind.Star, prossimo)).Rows);
    }

    [Fact]
    public async Task Manual_Public_Imported_Deferred_Then_Forced()
    {
        // Manuale (sempre pubblica) + importata che entra a un ciclo FUTURO (in attesa: non ancora pubblica).
        await _repo.SaveSidsAsync("LIRF", ProcedureKind.Sid, new[] { new SidRow(0, "07", "OSTIA", "OST7A", null, "5000ft", "CONV", null, null, null) });
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[] { Imp("ALAX7G", "ALAXI", "LIRF|ALAXI|G|") }, "3512");

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
    public async Task Nascosta_Esce_Anche_Se_Forzata_E_La_Correzione_Si_Pubblica()
    {
        await _repo.SaveSidsAsync("LIRF", ProcedureKind.Sid, new[]
        {
            new SidRow(0, "07", "OSTIA", "OST7A", null, null, null, null, null, null, IsHidden: true),
        });
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[]
        {
            Imp("SIV5A", "SIVIL", "LIRF|SIVIL|A|"),
            Imp("ALAX7G", "ALAXI", "LIRF|ALAXI|G|"),
        }, "2606");
        var sids = (await _repo.LoadAsync("LIRF"))!.Sids;
        var siv = sids.Single(s => s.Name == "SIV5A");
        var alax = sids.Single(s => s.Name == "ALAX7G");

        await _repo.UpdateImportedSidAsync(alax.Id, priority: null, forcePublished: true, resolvedFix: null,
            initialClimb: null, initialClimbByApp: false, cat: null, wtc: null, condition: null);
        await _repo.SetImportedSidsHiddenAsync("LIRF", new[] { alax.Id }, hidden: true);
        await _repo.SetImportedSidOverridesAsync("LIRF", siv.Id, "SOSIV", "ESINO");

        var riga = Assert.Single((await _sut.DeriveAsync("LIRF")).Rows);
        Assert.Equal("SOSIV", riga.Fix);
        Assert.Equal("ESINO", riga.Transition);

        // Rimostrata: torna, senza bisogno di altro.
        await _repo.SetImportedSidsHiddenAsync("LIRF", new[] { alax.Id }, hidden: false);
        Assert.Equal(new[] { "ALAXI", "SOSIV" }, (await _sut.DeriveAsync("LIRF")).Rows.Select(r => r.Fix).ToArray());
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
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[] { Imp("ALAX7G", "ALAXI", "LIRF|ALAXI|G|") }, prossimo);

        Assert.Empty((await _sut.DeriveAsync("LIRF")).Rows);                       // «adesso»: non ancora
        Assert.Empty((await _sut.DeriveAsync("LIRF", ProcedureKind.Sid, oggi)).Rows);                 // idem, chiedendolo per nome
        Assert.Equal("ALAXI", Assert.Single((await _sut.DeriveAsync("LIRF", ProcedureKind.Sid, prossimo)).Rows).Fix);
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

        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[] { Imp("ALAX7G", "ALAXI", "LIRF|ALAXI|G|") }, prossimo);

        Assert.Empty((await _sut.DeriveAsync("LIRF", ProcedureKind.Sid, oggi)).Rows);                 // al ciclo di oggi ancora no
        Assert.Single((await _sut.DeriveAsync("LIRF", ProcedureKind.Sid, prossimo)).Rows);            // al SUO ciclo, sì
        Assert.Single((await _sut.DeriveAsync("LIRF", ProcedureKind.Sid, cicli[2].Cycle)).Rows);      // e ci resta
    }
}
