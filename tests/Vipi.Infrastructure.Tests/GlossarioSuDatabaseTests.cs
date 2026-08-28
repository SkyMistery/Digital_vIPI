using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Translation;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il glossario di fraseologia dove vive davvero (<c>lavori-aperti §Q3</c>).
///
/// <para>
/// ⚠️ <b>Quel che si prova qui non è «i dati si salvano».</b> Sono due promesse che, se cadessero, cadrebbero
/// in silenzio: che una voce <b>tolta</b> da chi cura il glossario non torni al riavvio dopo, e che una voce
/// nuova possa far <b>rifare</b> le traduzioni automatiche che la contengono — senza toccare quelle che una
/// persona ha già riletto.
/// </para>
/// </summary>
public class GlossarioSuDatabaseTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private EfGlossaryStore Deposito() => new(_db);
    private EfTranslationMemory Memoria() => new(_db);

    // ---- Il seme -------------------------------------------------------------------------------------

    [Fact]
    public async Task Il_seme_scrive_le_voci_di_partenza_e_non_le_attribuisce_a_nessuno()
    {
        var quante = await GlossarioFraseologia.SeminaAsync(Deposito());

        Assert.Equal(GlossarioFraseologia.Semi.Count, quante);
        var voci = await Deposito().ListAsync("it", "en");
        Assert.All(voci, v => Assert.Null(v.UpdatedByUserId));
    }

    [Fact]
    public async Task Il_seme_e_idempotente()
    {
        await GlossarioFraseologia.SeminaAsync(Deposito());
        Assert.Equal(0, await GlossarioFraseologia.SeminaAsync(Deposito()));
    }

    [Fact]
    public async Task Una_voce_TOLTA_non_torna_al_giro_dopo()
    {
        // ⚠️ È la ragione per cui la condizione del seme è «il glossario è vuoto» e non «questa voce manca».
        // Con la seconda, una formula che un controllore ha tolto — perché su quel campo si dice altro —
        // tornerebbe a ogni riavvio, per sempre, senza che nessuno capisca da dove.
        await GlossarioFraseologia.SeminaAsync(Deposito());
        var tolta = (await Deposito().ListAsync("it", "en")).First(v => v.SourceText == "riporta sottovento");
        Assert.True(await Deposito().DeleteAsync(tolta.Id));

        Assert.Equal(0, await GlossarioFraseologia.SeminaAsync(Deposito()));
        Assert.DoesNotContain(await Deposito().ListAsync("it", "en"), v => v.SourceText == "riporta sottovento");
    }

    // ---- L'unicità della formula ---------------------------------------------------------------------

    [Fact]
    public async Task Riscrivere_la_stessa_formula_con_le_maiuscole_CORREGGE_invece_di_duplicare()
    {
        // ⚠️ Su SQLite il confronto fra testi distingue le maiuscole e su MySQL, con la collazione di
        // questo schema, pure: senza la colonna già in minuscolo, «Riporta sottovento» sarebbe una SECONDA
        // riga — due rese della stessa formula, e a scegliere sarebbe l'ordine della query.
        Assert.True(await Deposito().UpsertAsync("it", "en", "riporta sottovento", "report downwind", 111));
        Assert.False(await Deposito().UpsertAsync("it", "en", "Riporta Sottovento", "report downwind now", 222));

        var voce = Assert.Single(await Deposito().ListAsync("it", "en"));
        Assert.Equal("report downwind now", voce.TargetText);
        Assert.Equal(222, voce.UpdatedByUserId);
    }

    [Fact]
    public async Task I_due_VERSI_sono_glossari_diversi()
    {
        await Deposito().UpsertAsync("it", "en", "riporta sottovento", "report downwind", 1);

        Assert.Single(await Deposito().ListAsync("it", "en"));
        Assert.Empty(await Deposito().ListAsync("en", "it"));
    }

    // ---- Far rifare quel che è già in memoria --------------------------------------------------------

    [Fact]
    public async Task Dimenticare_butta_le_AUTOMATICHE_e_lascia_stare_le_riviste()
    {
        var memoria = Memoria();
        await memoria.SaveMachineAsync("it", "en", "azure", new[]
        {
            ("Poi riporta sottovento.", "Then bring it back downwind."),
            ("Contatta la torre.", "Contact the tower."),
        });
        await memoria.SaveHumanAsync("it", "en", "Riporta sottovento e attendi.", "Report downwind and wait.", 704798);

        // Il conto guarda le sole automatiche, e non distingue le maiuscole: la riletta non si conta
        // nemmeno, o il tasto prometterebbe di rifare una cosa che poi giustamente non tocca.
        Assert.Equal(1, await memoria.ContaConLaFormulaAsync("it", "en", "riporta sottovento"));

        Assert.Equal(1, await memoria.DimenticaAutomaticheConLaFormulaAsync("it", "en", "riporta sottovento"));

        var rimaste = await _db.TranslationUnits.AsNoTracking().Select(u => u.SourceText).ToListAsync();
        Assert.Contains("Contatta la torre.", rimaste);
        Assert.Contains("Riporta sottovento e attendi.", rimaste);          // la lettura di una persona vince
        Assert.DoesNotContain("Poi riporta sottovento.", rimaste);
    }

    [Fact]
    public async Task Dimenticare_trova_la_formula_anche_a_INIZIO_frase()
    {
        // Senza il confronto in minuscolo su entrambi i lati, questa riga resterebbe in memoria com'era e
        // il documento non cambierebbe: il curatore vedrebbe la formula scritta e la carta invariata.
        var memoria = Memoria();
        await memoria.SaveMachineAsync("it", "en", "azure", new[]
        {
            ("Riporta sottovento, poi chiama.", "Bring it back downwind, then call."),
        });

        Assert.Equal(1, await memoria.DimenticaAutomaticheConLaFormulaAsync("it", "en", "riporta sottovento"));
    }

    [Fact]
    public async Task Dimenticare_non_tratta_le_percentuali_come_jolly()
    {
        // ⚠️ Se la ricerca fosse un LIKE con le percentuali intorno, una formula che ne contenesse una
        // diventerebbe un carattere jolly e cancellerebbe molto piu' di quel che il curatore ha chiesto.
        var memoria = Memoria();
        await memoria.SaveMachineAsync("it", "en", "azure", new[]
        {
            ("Contatta la torre.", "Contact the tower."),
        });

        Assert.Equal(0, await memoria.DimenticaAutomaticheConLaFormulaAsync("it", "en", "%torre%"));
        Assert.Single(await _db.TranslationUnits.AsNoTracking().ToListAsync());
    }
}
