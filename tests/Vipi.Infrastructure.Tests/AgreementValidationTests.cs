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
        _svc = new AgreementService(_repo, new AllowAuthz(), new TopologyBuilder(_db));

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
