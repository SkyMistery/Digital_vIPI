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

    private static ImportedProcedure Imp(string name, string fix, string key, string? rwy = "07") =>
        new(Runway: rwy, Fix: fix, Name: name, Transition: null, Type: "RNAV", StableKey: key, NeedsFixReview: false);

    [Fact]
    public async Task Import_Preserves_Manual_And_Reapplies_Priority()
    {
        // Una SID manuale.
        await _repo.SaveSidsAsync("LIRF", ProcedureKind.Sid, new[] { new SidRow(0, "07", "OSTIA", "OST7A", null, "5000ft", "CONV", null, null, null) });

        // Primo import: due righe.
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[]
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
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[]
        {
            Imp("ALAX8G", "ALAXI", "LIRF|ALAXI|G|"),
            Imp("ALAX7J", "ALAXI", "LIRF|ALAXI|J|"),
        }, "2607");

        // La 7G resta come versione sostituita dal 2607 (U-003): le righe vive sono ancora tre.
        var afterSecond = (await _repo.LoadAsync("LIRF"))!.Sids.Where(s => !s.IsSuperseded).ToList();
        Assert.Equal(3, afterSecond.Count);
        Assert.Equal("ALAX7G", Assert.Single((await _repo.LoadAsync("LIRF"))!.Sids, s => s.IsSuperseded).Name);
        Assert.Single(afterSecond, s => !s.IsImported && s.Name == "OST7A");         // manuale intatta
        var g2 = afterSecond.Single(s => s.Name == "ALAX8G");
        Assert.Equal(1, g2.Priority);                                        // priorità mantenuta
        // La forzatura no: la 7G resta in vigore fino al 2607 come sostituita, e la 8G forzata uscirebbe insieme a
        // lei (U-003). La forzatura passa solo quando la vecchia non resta — vedi il test qui sotto.
        Assert.False(g2.ForcePublished);
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

        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, due, "2606");
        Assert.Equal(2, (await _repo.LoadAsync("LIRF"))!.Sids.Count(s => s.IsImported));

        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, due, "2607");   // prima lanciava ArgumentException

        var sids = (await _repo.LoadAsync("LIRF"))!.Sids.Where(s => s.IsImported).ToList();
        Assert.Equal(2, sids.Count);                                          // nessuna riga persa né duplicata
        Assert.Equal(new[] { "ROBO1H", "ROBO2H" }, sids.Select(s => s.Name).OrderBy(n => n));
    }

    /// <summary>
    /// 🔴 U-004 (revisione totale 3): con la chiave condivisa vinceva la PRIMA riga, e le sue decisioni tornavano su
    /// tutte. Ma le coppie vere non sono revisioni della stessa SID: sono procedure diverse che convivono
    /// (ROBO1H/ROBO5H a LIBG, XIB5A-OKU5R/OKU6A a LIRF, VOG1K/VOG1S a LIME). Ognuna tiene le sue.
    /// </summary>
    [Fact]
    public async Task Con_Chiave_Condivisa_Ogni_Riga_Tiene_Le_Sue_Decisioni()
    {
        var due = new[]
        {
            Imp("ROBO1H", "ROBOT", "LIRF|ROBOT|H||07"),
            Imp("ROBO2H", "ROBOT", "LIRF|ROBOT|H||07"),
        };
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, due, "2606");

        // Arricchimento editoriale sulla seconda riga della coppia: è quella che il first-wins perdeva.
        var seconda = (await _repo.LoadAsync("LIRF"))!.Sids.Single(s => s.Name == "ROBO2H");
        await _repo.UpdateImportedSidAsync(seconda.Id, priority: 3, forcePublished: true, resolvedFix: null,
            initialClimb: "5000ft", initialClimbByApp: false, cat: null, wtc: null, condition: null);

        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, due, "2607");

        var sids = (await _repo.LoadAsync("LIRF"))!.Sids.Where(s => s.IsImported).ToList();
        Assert.Equal(2, sids.Count);
        var r2 = sids.Single(s => s.Name == "ROBO2H");
        Assert.Equal(3, r2.Priority);
        Assert.Equal("5000ft", r2.InitialClimb);
        Assert.True(r2.ForcePublished);
        var r1 = sids.Single(s => s.Name == "ROBO1H");
        Assert.Null(r1.Priority);
        Assert.Null(r1.InitialClimb);
        Assert.False(r1.ForcePublished);
    }

    /// <summary>
    /// 🔴 U-004: la seconda riga della coppia si confrontava col NOME della prima, sempre diverso, quindi a ogni
    /// giro prendeva il ciclo appena calcolato — col ciclo dichiarato avanti restava fuori dalla pagina pubblica
    /// per dieci-dodici giorni a ogni ciclo (a LIRF mancava XIB5A-OKU6A).
    /// </summary>
    [Fact]
    public async Task Con_Chiave_Condivisa_Nessuna_Riga_Si_Ritimbra()
    {
        var due = new[]
        {
            new ImportedProcedure("16L", "XIBIL", "XIB5A-OKU5R", "OKUDA", "RNAV", "LIRF|XIBIL|A|OKUDA|16L", false),
            new ImportedProcedure("16L", "XIBIL", "XIB5A-OKU6A", "OKUDA", "RNAV", "LIRF|XIBIL|A|OKUDA|16L", false),
        };
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, due, "2606");
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, due, "2607");

        var sids = (await _repo.LoadAsync("LIRF"))!.Sids.Where(s => s.IsImported).ToList();
        Assert.All(sids, s => Assert.Equal("2606", s.SourceAiracCycle));
    }

    /// <summary>
    /// 🔴 U-005: il punto risolto in un altro modo (alias creato, catalogo cambiato) cambiava la chiave, e la riga
    /// rinasceva nuda. Qui la stessa SID arriva prima «da verificare» col prefisso grezzo, poi risolta: priorità,
    /// arricchimenti e ciclo d'entrata restano.
    /// </summary>
    [Fact]
    public async Task Il_Punto_Risolto_In_Un_Altro_Modo_Non_Fa_Perdere_Le_Decisioni()
    {
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[]
        {
            new ImportedProcedure("25", "SOSA", "SOSA5A", null, "RNAV", "LIRF|SOSA|A||25", NeedsFixReview: true),
        }, "2606");
        var imp = (await _repo.LoadAsync("LIRF"))!.Sids.Single(s => s.IsImported);
        await _repo.UpdateImportedSidAsync(imp.Id, priority: 1, forcePublished: false, resolvedFix: null,
            initialClimb: "4000", initialClimbByApp: false, cat: null, wtc: "M, H", condition: null);

        // La notte dopo qualcuno ha creato l'alias SOSA → SOSAK: il parser risolve, e la chiave vecchia era col fix.
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[]
        {
            new ImportedProcedure("25", "SOSAK", "SOSA5A", null, "RNAV", "LIRF|SOSAK|A||25", NeedsFixReview: false),
        }, "2607");

        var dopo = (await _repo.LoadAsync("LIRF"))!.Sids.Single(s => s.IsImported);
        Assert.Equal("SOSAK", dopo.Fix);
        Assert.Equal(1, dopo.Priority);
        Assert.Equal("4000", dopo.InitialClimb);
        Assert.Equal("M, H", dopo.Wtc);
        Assert.Equal("2606", dopo.SourceAiracCycle);
    }

    private static readonly Vipi.Domain.Services.AiracService Airac = new();

    private async Task<List<SidRow>> Importate() =>
        (await _repo.LoadAsync("LIRF"))!.Sids.Where(s => s.IsImported).ToList();

    /// <summary>
    /// 🔴 U-003 (revisione totale 3): fra il changelog del ciclo nuovo e la sua entrata in vigore, una procedura
    /// rivista SPARIVA: la vecchia cancellata, la nuova in attesa del suo ciclo. Successo il 25 settembre 2026 a
    /// LIMF con le TOP1B: fino al 1° ottobre il vSOP pubblico non le aveva. Ora la versione in vigore resta,
    /// «sostituita dal ciclo» nuovo, e ognuna delle due si vede nel suo tratto.
    /// </summary>
    [Fact]
    public async Task La_versione_in_vigore_resta_finche_non_entra_la_nuova()
    {
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[]
        {
            new ImportedProcedure("36", "TOPIS", "TOP1B-AST8L", "ASTIG", "RNAV", "LIRF|TOP|B|ASTIG|36", false),
        }, "2609");

        // Il changelog del 2610 rivede la SID: stessa chiave, contenuto nuovo, ciclo d'entrata futuro.
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[]
        {
            new ImportedProcedure("36", "TOPIS", "TOP2B-AST8L", "ASTIG", "RNAV", "LIRF|TOP|B|ASTIG|36", false),
        }, "2610");

        var righe = await Importate();
        Assert.Equal(2, righe.Count);
        var al2609 = Assert.Single(righe, s => s.IsPublicAt("2609", Airac));
        Assert.Equal("TOP1B-AST8L", al2609.Name);
        Assert.Equal("2610", al2609.SupersededFromCycle);
        var al2610 = Assert.Single(righe, s => s.IsPublicAt("2610", Airac));
        Assert.Equal("TOP2B-AST8L", al2610.Name);
    }

    /// <summary>
    /// La forzatura passa alla revisione nuova solo quando la vecchia non resta: una correzione dentro lo stesso
    /// ciclo sostituisce e basta, e la decisione dello staff vale per la riga che c'è.
    /// </summary>
    [Fact]
    public async Task Correzione_dentro_il_ciclo_tiene_la_forzatura_e_non_lascia_la_vecchia()
    {
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[] { Imp("ALAX7G", "ALAXI", "LIRF|ALAX|G||07") }, "2610");
        var g = Assert.Single(await Importate());
        await _repo.UpdateImportedSidAsync(g.Id, priority: null, forcePublished: true, resolvedFix: null,
            initialClimb: null, initialClimbByApp: false, cat: null, wtc: null, condition: null);

        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[] { Imp("ALAX8G", "ALAXI", "LIRF|ALAX|G||07") }, "2610");

        var dopo = Assert.Single(await Importate());
        Assert.Equal("ALAX8G", dopo.Name);
        Assert.True(dopo.ForcePublished);
    }

    /// <summary>Una procedura che la sorgente del ciclo nuovo non ha più vale ancora fino a quel ciclo.</summary>
    [Fact]
    public async Task Una_procedura_tolta_dalla_sorgente_vale_fino_al_ciclo_nuovo()
    {
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[]
        {
            Imp("ALAX7G", "ALAXI", "LIRF|ALAX|G||07"), Imp("ALAX7J", "ALAXI", "LIRF|ALAX|J||07"),
        }, "2609");
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[]
        {
            Imp("ALAX7G", "ALAXI", "LIRF|ALAX|G||07"),
        }, "2610");

        var j = Assert.Single(await Importate(), s => s.Name == "ALAX7J");
        Assert.Equal("2610", j.SupersededFromCycle);
        Assert.True(j.IsPublicAt("2609", Airac));
        Assert.False(j.IsPublicAt("2610", Airac));
    }

    /// <summary>
    /// Se la riga era entrata nello STESSO ciclo che la sorgente dichiara adesso, era una correzione dentro il
    /// ciclo: si toglie subito, come prima. E le sostituite si tolgono quando la sorgente dichiara un ciclo DOPO
    /// quello da cui erano sostituite: a quel punto non servono più a nessun ciclo.
    /// </summary>
    [Fact]
    public async Task Le_sostituite_scadute_si_tolgono()
    {
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[] { Imp("ALAX7J", "ALAXI", "LIRF|ALAX|J||07") }, "2609");
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[] { Imp("ALAX7G", "ALAXI", "LIRF|ALAX|G||07") }, "2610");
        Assert.Equal(2, (await Importate()).Count);

        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[] { Imp("ALAX7G", "ALAXI", "LIRF|ALAX|G||07") }, "2610");
        Assert.Equal(2, (await Importate()).Count);   // stesso ciclo: la sostituita serve ancora

        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[] { Imp("ALAX7G", "ALAXI", "LIRF|ALAX|G||07") }, "2611");
        Assert.Equal("ALAX7G", Assert.Single(await Importate()).Name);

        // Correzione dentro il ciclo: una riga entrata al 2611 e tolta mentre la sorgente dichiara ancora il 2611
        // non resta.
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[]
        {
            Imp("ALAX7G", "ALAXI", "LIRF|ALAX|G||07"), Imp("OST1E", "OST", "LIRF|OST|E||07"),
        }, "2611");
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[] { Imp("ALAX7G", "ALAXI", "LIRF|ALAX|G||07") }, "2611");
        Assert.Equal("ALAX7G", Assert.Single(await Importate()).Name);
    }

    /// <summary>
    /// 🔴 Anche U-005: una riga malformata per un giro (TOP1B LAG2L, 25 settembre) spariva con tutti i suoi
    /// arricchimenti. Ora resta come sostituita e, se la sorgente la rimanda, si riprende le sue decisioni.
    /// </summary>
    [Fact]
    public async Task Una_riga_che_torna_si_riprende_le_sue_decisioni()
    {
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[] { Imp("ALAX7J", "ALAXI", "LIRF|ALAX|J||07") }, "2609");
        var j = Assert.Single(await Importate());
        await _repo.UpdateImportedSidAsync(j.Id, priority: 2, forcePublished: false, resolvedFix: null,
            initialClimb: "FL70", initialClimbByApp: false, cat: null, wtc: "L, M", condition: null);

        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[] { Imp("ALAX7G", "ALAXI", "LIRF|ALAX|G||07") }, "2610");
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[]
        {
            Imp("ALAX7G", "ALAXI", "LIRF|ALAX|G||07"), Imp("ALAX7J", "ALAXI", "LIRF|ALAX|J||07"),
        }, "2610");

        var tornata = Assert.Single(await Importate(), s => s.Name == "ALAX7J");
        Assert.Null(tornata.SupersededFromCycle);
        Assert.Equal(2, tornata.Priority);
        Assert.Equal("FL70", tornata.InitialClimb);
        Assert.Equal("2609", tornata.SourceAiracCycle);
    }

    [Fact]
    public async Task Reimport_Unchanged_Keeps_First_SourceCycle()
    {
        // Stesso contenuto SID visto a due cicli d'entrata diversi: conserva il PRIMO, così raggiunto quel
        // ciclo la SID diventa pubblica (IsPublicAt) e ci RESTA — il re-timbro non la ri-nasconde. Vale anche
        // dopo la carta §AW2, che ha cambiato che cosa significa il valore ma non questa regola: solo un
        // contenuto CAMBIATO riparte dal ciclo d'entrata nuovo.
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[] { Imp("ALAX7G", "ALAXI", "LIRF|ALAXI|G|") }, "2606");
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[] { Imp("ALAX7G", "ALAXI", "LIRF|ALAXI|G|") }, "2607");

        var s = (await _repo.LoadAsync("LIRF"))!.Sids.Single(x => x.IsImported);
        Assert.Equal("2606", s.SourceAiracCycle);
    }

    [Fact]
    public async Task Reimport_Preserves_Manually_Resolved_Fix()
    {
        // Import con fix non risolto (prefisso grezzo, da verificare).
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[]
        {
            new ImportedProcedure("07", "ZZZ", "ZZZ5A", null, "RNAV", "LIRF|ZZZ|A||07", NeedsFixReview: true),
        }, "2606");
        var imp = (await _repo.LoadAsync("LIRF"))!.Sids.Single(s => s.IsImported);
        Assert.True(imp.NeedsFixReview);

        // L'operatore risolve il fix a mano.
        await _repo.UpdateImportedSidAsync(imp.Id, priority: null, forcePublished: false, resolvedFix: "ZAGRE",
            initialClimb: null, initialClimbByApp: false, cat: null, wtc: null, condition: null);

        // Reimport: la sorgente ripropone ancora il prefisso grezzo → la risoluzione manuale va conservata.
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[]
        {
            new ImportedProcedure("07", "ZZZ", "ZZZ5A", null, "RNAV", "LIRF|ZZZ|A||07", NeedsFixReview: true),
        }, "2607");

        var after = (await _repo.LoadAsync("LIRF"))!.Sids.Single(s => s.IsImported);
        Assert.Equal("ZAGRE", after.Fix);
        Assert.False(after.NeedsFixReview);
    }

    [Fact]
    public async Task Editorial_Enrichments_On_Imported_Persist_And_Survive_Reimport()
    {
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[] { Imp("ALAX7G", "ALAXI", "LIRF|ALAXI|G|") }, "2606");
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
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[] { Imp("ALAX7G", "ALAXI", "LIRF|ALAXI|G|") }, "2607");
        var after = (await _repo.LoadAsync("LIRF"))!.Sids.Single(s => s.IsImported);
        Assert.Equal("5000", after.InitialClimb);
        Assert.True(after.InitialClimbByApp);
        Assert.Equal("C, D", after.Cat);
        Assert.Equal("M, H", after.Wtc);
        Assert.Equal("solo notte", after.Condition);
    }

    [Fact]
    public async Task Nascosta_E_Corretta_A_Mano_Sopravvivono_Al_Reimport()
    {
        // LIRF, segnalato dal campo: il parser risolve «SIV» in SIVIL, ed è SOSIV.
        var riga = new ImportedProcedure("25", "SIVIL", "SIV5A", "ESINO", "RNAV", "LIRF|SIVIL|A|ESINO|25", NeedsFixReview: false);
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[] { riga }, "2606");
        var imp = (await _repo.LoadAsync("LIRF"))!.Sids.Single(s => s.IsImported);

        await _repo.SetImportedSidOverridesAsync("LIRF", imp.Id, "sosiv", "ELKAP");
        Assert.Equal(1, await _repo.SetImportedSidsHiddenAsync("LIRF", new[] { imp.Id }, hidden: true));

        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[] { riga }, "2607");

        var dopo = (await _repo.LoadAsync("LIRF"))!.Sids.Single(s => s.IsImported);
        Assert.True(dopo.IsHidden);
        Assert.Equal("SIVIL", dopo.Fix);                 // la sorgente resta quella
        Assert.Equal("SOSIV", dopo.EffectiveFix);        // si pubblica la correzione, in maiuscolo
        Assert.Equal("ELKAP", dopo.EffectiveTransition);
        // ⚠️ La transition corretta NON fa sembrare la SID una revisione nuova: il ciclo d'entrata resta il primo.
        Assert.Equal("2606", dopo.SourceAiracCycle);
    }

    [Fact]
    public async Task Correzione_Uguale_Alla_Sorgente_Torna_A_Seguire_La_Sorgente()
    {
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[] { Imp("ALAX7G", "ALAXI", "LIRF|ALAXI|G|") }, "2606");
        var imp = (await _repo.LoadAsync("LIRF"))!.Sids.Single(s => s.IsImported);

        await _repo.SetImportedSidOverridesAsync("LIRF", imp.Id, "SOSIV", null);
        await _repo.SetImportedSidOverridesAsync("LIRF", imp.Id, "alaxi", " ");

        var dopo = (await _repo.LoadAsync("LIRF"))!.Sids.Single(s => s.IsImported);
        Assert.Null(dopo.FixOverride);
        Assert.Null(dopo.TransitionOverride);
    }

    [Fact]
    public async Task Nascondere_Non_Tocca_Le_Righe_Di_Un_Altro_Scalo()
    {
        var acc = _db.Accs.Single();
        _db.Airports.Add(new Airport { Icao = "LIRA", Name = "Ciampino", Acc = acc });
        await _db.SaveChangesAsync();
        await _repo.ReplaceImportedProceduresAsync("LIRA", ProcedureKind.Sid, new[] { Imp("ALAX7G", "ALAXI", "LIRA|ALAXI|G|") }, "2606");
        var altra = (await _repo.LoadAsync("LIRA"))!.Sids.Single();

        Assert.Equal(0, await _repo.SetImportedSidsHiddenAsync("LIRF", new[] { altra.Id }, hidden: true));
        Assert.False((await _repo.LoadAsync("LIRA"))!.Sids.Single().IsHidden);
    }

    [Fact]
    public async Task Manuale_Nascosta_Resta_Nascosta_Dopo_Il_Salvataggio()
    {
        await _repo.SaveSidsAsync("LIRF", ProcedureKind.Sid, new[] { new SidRow(0, "07", "OSTIA", "OST7A", null, null, null, null, null, null, IsHidden: true) });
        Assert.True((await _repo.LoadAsync("LIRF"))!.Sids.Single().IsHidden);
    }

    [Fact]
    public async Task SaveManualSids_Does_Not_Touch_Imported()
    {
        await _repo.ReplaceImportedProceduresAsync("LIRF", ProcedureKind.Sid, new[] { Imp("ALAX7G", "ALAXI", "LIRF|ALAXI|G|") }, "2606");
        await _repo.SaveSidsAsync("LIRF", ProcedureKind.Sid, new[] { new SidRow(0, "07", "OSTIA", "OST7A", null, null, null, null, null, null) });

        var sids = (await _repo.LoadAsync("LIRF"))!.Sids;
        Assert.Single(sids, s => s.IsImported);             // importata ancora presente
        Assert.Single(sids, s => !s.IsImported);            // manuale presente
    }
}
