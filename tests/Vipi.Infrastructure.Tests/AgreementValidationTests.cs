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
    private int _neId, _tsId, _ftwrId;

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
        _tsId = sectors.First(s => s.Callsign == "LIRR_TS_CTR").Id;
        _ftwrId = sectors.First(s => s.Callsign == "LIRF_TWR").Id;
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    [Fact]
    public async Task Un_accordo_senza_ricevente_non_si_salva()
    {
        // Non produce niente: la derivazione scarta la riga. E «a UNICOM» non è un capo che si scrive — lo calcola
        // la vista operativa quando il ricevente è offline.
        var ex = await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => _svc.AddAgreementAsync("LIRR", Input(sideB: Array.Empty<int>())));
        Assert.Contains("riceve", ex.Message);
    }

    [Fact]
    public async Task Un_accordo_senza_chi_trasferisce_non_si_salva()
    {
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => _svc.AddAgreementAsync("LIRR", Input(sideA: Array.Empty<int>())));
    }

    [Fact]
    public async Task Con_i_due_capi_si_salva()
    {
        var id = await _svc.AddAgreementAsync("LIRR", Input());
        Assert.True(id > 0);
    }

    [Fact]
    public async Task Togliere_il_ricevente_a_un_accordo_esistente_non_passa()
    {
        // È il percorso con cui si sistemano le due righe ereditate: aprendole, il salvataggio chiede il
        // ricevente. Deve valere anche al contrario — non si può svuotare un accordo sano.
        var id = await _svc.AddAgreementAsync("LIRR", Input());

        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => _svc.UpdateAgreementAsync("LIRR", id, Input(sideB: Array.Empty<int>())));
    }

    [Fact]
    public async Task L_annulla_rimette_anche_un_accordo_che_non_si_potrebbe_piu_scrivere()
    {
        // ⚠️ Il ripristino è FUORI dalla regola di proposito: in archivio esistono due accordi senza ricevente, e
        // un annulla che rifiutasse di rimettere ciò che ha appena cancellato sarebbe peggio della regola.
        var snapshot = new AgreementSnapshot(
            new AgreementInput
            {
                TrafficKind = TransferFlowKind.Overflight,
                SideA = new[] { _neId },
                SideB = Array.Empty<int>(),
                Airports = Array.Empty<AgreementAirportInput>(),
            },
            new[] { new AgreementClauseSnapshot(Clause("GISAM"), AgreementDirection.AtoB, 1, null, 0) });

        var id = await _svc.RestoreAgreementAsync("LIRR", snapshot);

        var a = Assert.Single(await _svc.ListByAccAsync("LIRR"));
        Assert.Equal(id, a.Id);
        Assert.DoesNotContain(a.Parties, p => p.Side == AgreementSide.B);
    }

    [Fact]
    public async Task Gli_arrivi_continuano_a_pretendere_un_aeroporto()
    {
        // La regola dura resta: il committente ha scelto di tenerla, e il form chiede l'aeroporto dove serve.
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => _svc.AddAgreementAsync("LIRR", Input(kind: TransferFlowKind.Arrival,
                                                      airports: Array.Empty<AgreementAirportInput>())));
    }

    [Fact]
    public async Task Un_sorvolo_non_pretende_aeroporti()
    {
        var id = await _svc.AddAgreementAsync("LIRR", Input(kind: TransferFlowKind.Overflight,
                                                           airports: Array.Empty<AgreementAirportInput>()));
        Assert.True(id > 0);
    }

    // ---- attrezzi ------------------------------------------------------------------------------------

    private AgreementInput Input(TransferFlowKind kind = TransferFlowKind.Arrival,
        IReadOnlyList<int>? sideA = null, IReadOnlyList<int>? sideB = null,
        IReadOnlyList<AgreementAirportInput>? airports = null) => new()
        {
            TrafficKind = kind,
            SideA = sideA ?? new[] { _neId },
            SideB = sideB ?? new[] { _ftwrId },
            Airports = airports ?? new[] { new AgreementAirportInput("LIRF") },
        };

    private static AgreementClauseInput Clause(string cops) => new()
    {
        Cops = cops, LevelValue = 130, LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.AtOrBelow,
    };

    private sealed class AllowAuthz : IEditAuthorizationService
    {
        public bool IsAdmin => true;
        public int? CurrentUserId => 1;
        public string? CurrentName => "test";
        public void EnsureAdmin() { }
        public Task EnsureCanEditAccAsync(string accCode, CancellationToken ct = default) => Task.CompletedTask;
        public Task EnsureCanEditDocumentAsync(int documentId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> CanEditAccAsync(string accCode, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> CanEditDocumentAsync(int documentId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<GrantRow>> ListGrantsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GrantRow>>(Array.Empty<GrantRow>());
        public Task<int> AddGrantAsync(int UserId, string? displayName, string accCode, CancellationToken ct = default) => Task.FromResult(0);
        public Task RevokeGrantAsync(int grantId, CancellationToken ct = default) => Task.CompletedTask;
    }
}
