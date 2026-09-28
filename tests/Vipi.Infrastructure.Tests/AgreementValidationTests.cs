using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Infrastructure.Aor;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Le regole della **porta di scrittura** degli accordi: quelle che dicono cosa non si può salvare.
///
/// <para>Stanno nel service e non nel repository, quindi si provano da lì — col repository vero sotto, che è il
/// solo modo di sapere che una regola non blocca anche i casi legittimi.</para>
/// </summary>
public class AgreementValidationTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfAgreementRepository _repo = default!;
    private AgreementService _svc = default!;
    private int _neId, _ftwrId;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        await RomaStructureSeed.SeedAsync(_db);
        _repo = new EfAgreementRepository(_db);
        _svc = new AgreementService(_repo, new AllowAuthz(), new TopologyBuilder(_db), LockDiRisorsaConcesso.Instance);

        var sectors = await _db.Sectors.ToListAsync();
        _neId = sectors.First(s => s.Callsign == "LIRR_NE_CTR").Id;
        _ftwrId = sectors.First(s => s.Callsign == "LIRF_TWR").Id;
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    [Fact]
    public async Task Un_accordo_senza_uno_dei_due_capi_non_si_salva()
    {
        // Non produce niente: la derivazione scarta la riga. E «a UNICOM» non è un capo che si scrive — lo calcola
        // la vista operativa quando il ricevente è offline. Dal 18 agosto 2026 è anche di schema (NOT NULL), e
        // questa regola resta perché l'errore arrivi come una frase e non come una violazione di vincolo.
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => _svc.AddAgreementAsync("LIRR", Pair(sideB: 0)));
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => _svc.AddAgreementAsync("LIRR", Pair(sideA: 0)));
    }

    [Fact]
    public async Task Lo_stesso_ente_sui_due_lati_non_e_una_relazione()
    {
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => _svc.AddAgreementAsync("LIRR", Pair(sideB: _neId)));
    }

    /// <summary>
    /// T-053 (13 settembre 2026): le etichette libere hanno un tetto, uguale alla colonna, e il servizio lo dice
    /// con una frase. Su MariaDB fuori da strict un testo più lungo della colonna veniva troncato in silenzio;
    /// qui SQLite non conosce le lunghezze, quindi senza la regola il test salverebbe tutto.
    /// </summary>
    [Fact]
    public async Task Un_etichetta_oltre_il_tetto_si_rifiuta_con_una_frase_e_al_tetto_si_salva()
    {
        var id = await _svc.AddAgreementAsync("LIRR", Pair());
        var sezione = await _svc.AddSectionAsync("LIRR", id, Section(TransferFlowKind.Overflight));
        var tetto = Vipi.Domain.Entities.AgreementClauseLimits.Etichetta;

        var ex = await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => _svc.AddClauseAsync("LIRR", sezione,
            new AgreementClauseInput { LevelUnit = Vipi.Domain.LevelUnit.Fl, LevelConstraint = Vipi.Domain.LevelConstraint.AtOrAbove, LevelValue = 240, Cops = "VALMA", ConditionCustomLabel = new string('x', tetto + 1) }));
        Assert.Contains(tetto.ToString(), ex.Message);

        // 86 caratteri, il caso della revisione, e il tetto esatto: si salvano.
        Assert.True(await _svc.AddClauseAsync("LIRR", sezione,
            new AgreementClauseInput { LevelUnit = Vipi.Domain.LevelUnit.Fl, LevelConstraint = Vipi.Domain.LevelConstraint.AtOrAbove, LevelValue = 240, Cops = "VALMA", ConditionCustomLabel = new string('y', 86) }) > 0);
        Assert.True(await _svc.AddClauseAsync("LIRR", sezione,
            new AgreementClauseInput { LevelUnit = Vipi.Domain.LevelUnit.Fl, LevelConstraint = Vipi.Domain.LevelConstraint.AtOrAbove, LevelValue = 240, Cops = "ELKAP", ConditionCustomLabel = new string('z', tetto) }) > 0);
    }

    /// <summary>
    /// 🔴 U-178 (revisione totale 3): «Incolla tabella» scriveva le clausole una per una. Una riga rifiutata a metà
    /// lasciava salvate le precedenti, che la pagina non mostrava (niente ricarico dopo un errore): al nuovo invio,
    /// dopo aver corretto la riga, entravano due volte. Ora si validano tutte prima e si scrivono insieme.
    /// </summary>
    [Fact]
    public async Task Incollare_con_una_riga_rifiutata_non_scrive_niente()
    {
        var id = await _svc.AddAgreementAsync("LIRR", Pair());
        var sezione = await _svc.AddSectionAsync("LIRR", id, Section(TransferFlowKind.Overflight));
        var tetto = Vipi.Domain.Entities.AgreementClauseLimits.Etichetta;
        var righe = new[]
        {
            Clause("GISAM"),
            new AgreementClauseInput { LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.AtOrBelow, LevelValue = 130,
                Cops = "VALMA", ConditionCustomLabel = new string('x', tetto + 1) },
            Clause("ELKAP"),
        };

        var ex = await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => _svc.AddClausesAsync("LIRR", sezione, righe));
        Assert.Contains("2", ex.Message);   // quale riga: chi corregge deve sapere dove
        Assert.Equal(0, await _db.AgreementClauses.CountAsync(c => c.SectionId == sezione));

        // Corretta la riga, il nuovo invio scrive le tre righe una volta sola.
        righe[1] = Clause("VALMA");
        Assert.Equal(3, await _svc.AddClausesAsync("LIRR", sezione, righe));
        Assert.Equal(new[] { "GISAM", "VALMA", "ELKAP" },
            await _db.AgreementClauses.Where(c => c.SectionId == sezione).OrderBy(c => c.Order).Select(c => c.Cops).ToListAsync());
    }

    /// <summary>
    /// 🔴 U-154 (revisione totale 3): la barra «in blocco» scriveva la condizione senza il tetto delle colonne (su
    /// MariaDB fuori da strict il testo si troncava in silenzio, in strict l'errore grezzo del database).
    /// </summary>
    [Fact]
    public async Task La_condizione_in_blocco_oltre_il_tetto_si_rifiuta_con_una_frase()
    {
        var id = await _svc.AddAgreementAsync("LIRR", Pair());
        var sezione = await _svc.AddSectionAsync("LIRR", id, Section(TransferFlowKind.Overflight));
        var clausola = await _svc.AddClauseAsync("LIRR", sezione, Clause("GISAM"));
        var etichetta = Vipi.Domain.Entities.AgreementClauseLimits.Etichetta;
        var elenco = Vipi.Domain.Entities.AgreementClauseLimits.Elenco;

        var ex = await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => _svc.SetConditionAsync(
            "LIRR", [clausola], null, false, false, new string('x', etichetta + 1)));
        Assert.Contains(etichetta.ToString(), ex.Message);
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => _svc.SetConditionAsync(
            "LIRR", [clausola], new string('A', elenco + 1), false, false, null));

        Assert.Null(await _db.AgreementClauses.AsNoTracking().Where(c => c.Id == clausola)
            .Select(c => c.ConditionCustomLabel).SingleAsync());
        Assert.Equal(1, await _svc.SetConditionAsync("LIRR", [clausola], null, false, false, new string('y', etichetta)));
    }

    /// <summary>
    /// 🔴 U-154: la clausola «in ogni caso» deve dire a quali condizioni vale (<c>ValidateClause</c>), ma la barra
    /// in blocco poteva svuotarle la condizione e salvarla così: una riga che il pannello non riscriverebbe più.
    /// </summary>
    [Fact]
    public async Task La_barra_in_blocco_non_svuota_la_condizione_di_una_clausola_in_ogni_caso()
    {
        var id = await _svc.AddAgreementAsync("LIRR", Pair());
        var sezione = await _svc.AddSectionAsync("LIRR", id, Section(TransferFlowKind.Overflight));
        var notte = Clause("GISAM") with { ConditionCustomLabel = "di notte" };
        var clausola = await _svc.AddClauseAsync("LIRR", sezione, notte);
        var altra = await _svc.AddAlternativeAsync("LIRR", clausola);
        var riga = await _db.AgreementClauses.SingleAsync(c => c.Id == clausola);
        riga.IsGroupWide = true;
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => _svc.SetConditionAsync(
            "LIRR", [clausola, altra], null, false, false, null));

        _db.ChangeTracker.Clear();
        Assert.Equal("di notte", await _db.AgreementClauses.Where(c => c.Id == clausola)
            .Select(c => c.ConditionCustomLabel).SingleAsync());

        // L'alternativa da sola si svuota: non scavalca niente, e la regola non la riguarda.
        Assert.Equal(1, await _svc.SetConditionAsync("LIRR", [altra], null, false, false, null));
    }

    [Fact]
    public async Task Con_i_due_capi_si_salva()
    {
        Assert.True(await _svc.AddAgreementAsync("LIRR", Pair()) > 0);
    }

    [Fact]
    public async Task L_annulla_rimette_anche_un_accordo_che_non_si_potrebbe_piu_scrivere()
    {
        // ⚠️ Il ripristino è FUORI dalle regole di proposito: un annulla che rifiutasse di rimettere ciò che ha
        // appena cancellato sarebbe peggio della regola. Qui la sezione è un sorvolo con un aeroporto — che la
        // creazione rifiuta — e deve rientrare lo stesso.
        var snapshot = new AgreementSnapshot(
            Pair(),
            new[]
            {
                new AgreementSectionSnapshot(
                    new AgreementSectionInput
                    {
                        Kind = TransferFlowKind.Overflight,
                        Direction = AgreementDirection.AtoB,
                        Airports = new[] { new AgreementAirportInput("LIRF") },
                    },
                    1,
                    new[] { new AgreementClauseSnapshot(Clause("GISAM"), 1, null, 0) }),
            });

        var id = await _svc.RestoreAgreementAsync("LIRR", snapshot);

        var a = Assert.Single(await _svc.ListByAccAsync("LIRR"));
        Assert.Equal(id, a.Id);
        Assert.Equal(new[] { "LIRF" }, Assert.Single(a.Sections).Airports.Select(x => x.Icao));
    }

    // ---- le regole della SEZIONE ---------------------------------------------------------------------

    [Fact]
    public async Task Gli_arrivi_continuano_a_pretendere_un_aeroporto()
    {
        // La regola dura resta: il committente ha scelto di tenerla, e adesso vive sulla sezione — che è dove il
        // tipo di traffico è finito.
        var id = await _svc.AddAgreementAsync("LIRR", Pair());

        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => _svc.AddSectionAsync("LIRR", id, Section(TransferFlowKind.Arrival)));
    }

    [Fact]
    public async Task Un_sorvolo_non_vuole_aeroporti()
    {
        // Il traffico che sorvola non ha relazione con lo scalo: la frase userebbe comunque la forma neutra,
        // e lo scalo scritto lì sarebbe una contraddizione muta.
        var id = await _svc.AddAgreementAsync("LIRR", Pair());

        Assert.True(await _svc.AddSectionAsync("LIRR", id, Section(TransferFlowKind.Overflight)) > 0);
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => _svc.AddSectionAsync("LIRR", id, Section(TransferFlowKind.Overflight, "LIRF")));
    }

    [Fact]
    public async Task Il_VFR_puo_avere_aeroporti_ma_non_li_pretende()
    {
        // ⚠️ È la regola «dove non sono esclusi» — decisione del committente, 18 agosto 2026 — e non «dove
        // servono»: restringere il campo ai soli arrivi e partenze è ciò che a ferragosto aveva creato un
        // catch-22.
        var id = await _svc.AddAgreementAsync("LIRR", Pair());

        Assert.True(await _svc.AddSectionAsync("LIRR", id, Section(TransferFlowKind.Vfr)) > 0);
        Assert.True(await _svc.AddSectionAsync("LIRR", id, Section(TransferFlowKind.Vfr, "LIRF")) > 0);
    }

    [Fact]
    public async Task Lo_stesso_scalo_due_volte_nella_stessa_sezione_e_un_errore_di_battitura()
    {
        var id = await _svc.AddAgreementAsync("LIRR", Pair());

        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => _svc.AddSectionAsync("LIRR", id, Section(TransferFlowKind.Arrival, "LIRF", "LIRF")));
    }

    // ---- attrezzi ------------------------------------------------------------------------------------

    /// <summary>I due capi. <c>0</c> = capo mancante, che è ciò che le regole devono rifiutare.</summary>
    private AgreementInput Pair(int? sideA = null, int? sideB = null) => new()
    {
        SideASectorId = sideA ?? _neId,
        SideBSectorId = sideB ?? _ftwrId,
    };

    private static AgreementSectionInput Section(TransferFlowKind kind, params string[] icaos) => new()
    {
        Kind = kind,
        Direction = AgreementDirection.AtoB,
        Airports = icaos.Select(x => new AgreementAirportInput(x)).ToList(),
    };

    private static AgreementClauseInput Clause(string cops) => new()
    {
        Cops = cops, LevelValue = 130, LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.AtOrBelow,
    };

    private sealed class AllowAuthz : IEditAuthorizationService
    {
        public bool IsAdmin => true;
        public VipiRole Role => IsAdmin ? VipiRole.Admin : VipiRole.User;
        public int? CurrentUserId => 1;
        public string? CurrentName => "test";
        public void EnsureAdmin() { }
    }
}
