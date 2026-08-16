using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// CRUD degli accordi di coordinamento e, soprattutto, l'**outline delle varianti dentro un verso** — che è la
/// sola parte davvero delicata: muovere una capofila senza le sue eccezioni le riassegna a un'altra alternativa
/// senza nessun errore, ed è il difetto più pericoloso di quest'area.
/// </summary>
public class AgreementRepositoryTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfAgreementRepository _repo = default!;
    private int _neId, _tsId, _ftwrId;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        await RomaStructureSeed.SeedAsync(_db);
        _repo = new EfAgreementRepository(_db);

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

    // ---- intestazione --------------------------------------------------------------------------------

    [Fact]
    public async Task Un_accordo_con_due_lati_e_due_aeroporti_va_e_torna()
    {
        var id = await _repo.AddAgreementAsync("LIRR", new AgreementInput
        {
            TrafficKind = TransferFlowKind.Arrival,
            Description = "prova",
            SideA = new[] { _neId, _tsId },
            SideB = new[] { _ftwrId },
            Airports = new[] { new AgreementAirportInput("LIRF"), new AgreementAirportInput("LFPG", "Paris CDG") },
        });
        await _repo.AddClauseAsync("LIRR", id, AgreementDirection.AtoB, Clause("VALMA", 130));

        var a = Assert.Single(await _repo.ListByAccAsync("LIRR"));
        Assert.Equal(new[] { "LIRR_NE_CTR", "LIRR_TS_CTR" }, Side(a, AgreementSide.A));
        Assert.Equal(new[] { "LIRF_TWR" }, Side(a, AgreementSide.B));
        Assert.Equal(new[] { "LIRF", "LFPG" }, a.Airports.Select(x => x.Icao));
        // Il nome si tiene solo per lo scalo fuori catalogo: per gli altri viene dal catalogo, e una copia
        // qui divergerebbe alla prima rinomina.
        Assert.Equal(new string?[] { null, "Paris CDG" }, a.Airports.Select(x => x.Name));
        Assert.Equal("VALMA", Assert.Single(a.Clauses).Cops);
    }

    [Fact]
    public async Task Riscrivere_l_intestazione_sostituisce_gli_elenchi_invece_di_accodarli()
    {
        var id = await _repo.AddAgreementAsync("LIRR", Header(sideB: new[] { _ftwrId }));
        await _repo.UpdateAgreementAsync("LIRR", id, new AgreementInput
        {
            TrafficKind = TransferFlowKind.Departure,
            SideA = new[] { _tsId },
            SideB = Array.Empty<int>(),
            Airports = new[] { new AgreementAirportInput("LIRA") },
        });

        var a = Assert.Single(await _repo.ListByAccAsync("LIRR"));
        Assert.Equal(TransferFlowKind.Departure, a.TrafficKind);
        Assert.Equal(new[] { "LIRR_TS_CTR" }, Side(a, AgreementSide.A));
        // Lato B svuotato: il traffico va rilasciato a UNICOM. E' un accordo incompleto, non un errore — e
        // dev'essere SALVABILE, o l'editore lo terrebbe fuori dall'archivio finche' non lo completa.
        Assert.Empty(Side(a, AgreementSide.B));
        Assert.Equal(new[] { "LIRA" }, a.Airports.Select(x => x.Icao));
    }

    [Fact]
    public async Task Un_accordo_si_vede_anche_dal_capo_che_non_ne_e_responsabile()
    {
        // La duplicazione per ACC chiusa: prima il flusso viveva nel «secchio» di una sola ACC, quindi un
        // accordo poteva essere invisibile a uno dei suoi due capi, e un centro estero confinante con due ACC
        // italiane andava riscritto due volte.
        var estero = await ForeignSectorAsync();
        var id = await _repo.AddAgreementAsync("LIRR", Header(sideB: new[] { estero }));

        Assert.Equal(id, Assert.Single(await _repo.ListByAccAsync("LIRR")).Id);
        Assert.Equal(id, Assert.Single(await _repo.ListByAccAsync("LFFF")).Id);
    }

    // ---- clausole e outline --------------------------------------------------------------------------

    [Fact]
    public async Task Un_alternativa_nasce_pari_grado_e_senza_condizione()
    {
        var (id, first) = await WithClauseAsync();
        await _repo.UpdateClauseAsync("LIRR", first, Clause("VALMA", 130) with { ConditionLabel = "16R" });

        var second = await _repo.AddAlternativeAsync("LIRR", first);
        var a = Assert.Single(await _repo.ListByAccAsync("LIRR"));

        Assert.Equal(2, a.Clauses.Count);
        Assert.All(a.Clauses, c => Assert.Equal(0, c.VariantDepth));
        Assert.All(a.Clauses, c => Assert.NotNull(c.VariantGroup));
        // ⚠️ La condizione NON si copia: e' esattamente cio' che l'alternativa deve dire di diverso, e
        // copiarla darebbe due clausole identiche.
        Assert.Equal("16R", a.Clauses.Single(c => c.Id == first).ConditionLabel);
        Assert.Null(a.Clauses.Single(c => c.Id == second).ConditionLabel);
    }

    [Fact]
    public async Task Un_eccezione_nasce_un_livello_piu_dentro_subito_sotto()
    {
        var (_, first) = await WithClauseAsync();
        var alt = await _repo.AddAlternativeAsync("LIRR", first);
        var exc = await _repo.AddExceptionAsync("LIRR", first);

        var a = Assert.Single(await _repo.ListByAccAsync("LIRR"));
        // L'eccezione va SUBITO SOTTO la sua clausola, non in fondo al gruppo: l'ordine e' la struttura.
        Assert.Equal(new[] { first, exc, alt }, a.Clauses.OrderBy(c => c.Order).Select(c => c.Id));
        Assert.Equal(1, a.Clauses.Single(c => c.Id == exc).VariantDepth);
    }

    [Fact]
    public async Task Spostare_una_capofila_porta_via_il_suo_sottoalbero()
    {
        // Il difetto piu' pericoloso di quest'area: lasciare indietro le eccezioni le riassegna all'alternativa
        // di sopra, e quelle continuano a dire quello che dicevano di un'altra. Nessun errore, significato
        // cambiato.
        var (_, first) = await WithClauseAsync();
        var alt = await _repo.AddAlternativeAsync("LIRR", first);
        var exc = await _repo.AddExceptionAsync("LIRR", first);

        await _repo.MoveClauseAsync("LIRR", first, up: false);

        var a = Assert.Single(await _repo.ListByAccAsync("LIRR"));
        Assert.Equal(new[] { alt, first, exc }, a.Clauses.OrderBy(c => c.Order).Select(c => c.Id));
        Assert.Equal(1, a.Clauses.Single(c => c.Id == exc).VariantDepth);
    }

    [Fact]
    public async Task Sfilare_una_clausola_porta_via_il_sottoalbero_e_lo_riporta_a_zero()
    {
        var (_, first) = await WithClauseAsync();
        var alt = await _repo.AddAlternativeAsync("LIRR", first);
        var exc = await _repo.AddExceptionAsync("LIRR", alt);

        await _repo.DetachVariantAsync("LIRR", alt);

        var a = Assert.Single(await _repo.ListByAccAsync("LIRR"));
        var sfilata = a.Clauses.Single(c => c.Id == alt);
        var eccezione = a.Clauses.Single(c => c.Id == exc);
        // Il pezzo staccato riparte da zero e resta un gruppo, perche' ha ancora due clausole da tenere insieme.
        Assert.Equal(0, sfilata.VariantDepth);
        Assert.Equal(sfilata.VariantGroup, eccezione.VariantGroup);
        Assert.Equal(1, eccezione.VariantDepth);
        // Cio' che resta e' una clausola sola: un gruppo di uno non e' un gruppo.
        Assert.Null(a.Clauses.Single(c => c.Id == first).VariantGroup);
    }

    [Fact]
    public async Task I_punti_si_propagano_alle_sorelle_del_gruppo()
    {
        // I punti sono l'identita' dell'accordo dentro un gruppo: le varianti sono lo stesso accordo detto a
        // condizioni diverse. Il RICEVENTE, che prima viaggiava con loro e poteva divergere, qui non c'e' —
        // e' dell'accordo.
        var (_, first) = await WithClauseAsync();
        var alt = await _repo.AddAlternativeAsync("LIRR", first);

        await _repo.UpdateClauseAsync("LIRR", first, Clause("BIRSU, TOPNO", 150));

        var a = Assert.Single(await _repo.ListByAccAsync("LIRR"));
        Assert.All(a.Clauses, c => Assert.Equal("BIRSU, TOPNO", c.Cops));
        // Il livello invece resta della singola clausola: e' proprio cio' che due varianti dicono diverso.
        Assert.Equal(150, a.Clauses.Single(c => c.Id == first).LevelValue);
        Assert.Equal(130, a.Clauses.Single(c => c.Id == alt).LevelValue);
    }

    [Fact]
    public async Task I_punti_si_normalizzano_scrivendo()
    {
        var (_, first) = await WithClauseAsync();
        await _repo.UpdateClauseAsync("LIRR", first, Clause("  BIRSU ,, TOPNO  ,  ", 130));

        var a = Assert.Single(await _repo.ListByAccAsync("LIRR"));
        Assert.Equal("BIRSU, TOPNO", Assert.Single(a.Clauses).Cops);
    }

    [Fact]
    public async Task Eliminare_una_variante_scioglie_il_gruppo_rimasto_di_una()
    {
        var (_, first) = await WithClauseAsync();
        var alt = await _repo.AddAlternativeAsync("LIRR", first);

        await _repo.DeleteClauseAsync("LIRR", alt);

        var a = Assert.Single(await _repo.ListByAccAsync("LIRR"));
        Assert.Null(Assert.Single(a.Clauses).VariantGroup);
    }

    // ---- i due versi ---------------------------------------------------------------------------------

    [Fact]
    public async Task Le_clausole_dei_due_versi_non_si_mescolano()
    {
        var id = await _repo.AddAgreementAsync("LIRR", Header(sideB: new[] { _ftwrId }));
        var andata = await _repo.AddClauseAsync("LIRR", id, AgreementDirection.AtoB, Clause("VALMA", 130));
        var ritorno = await _repo.AddClauseAsync("LIRR", id, AgreementDirection.BtoA, Clause("VALMA", 90));

        var a = Assert.Single(await _repo.ListByAccAsync("LIRR"));
        // L'ordine riparte da uno in ogni verso: sono due tabelle, non una sola con due meta'.
        Assert.Equal(1, a.Clauses.Single(c => c.Id == andata).Order);
        Assert.Equal(1, a.Clauses.Single(c => c.Id == ritorno).Order);

        // E il trascinamento non attraversa il verso: cambiare verso a una clausola e' dire un'altra cosa,
        // non spostarla.
        await _repo.MoveClauseToAsync("LIRR", andata, ritorno);
        a = Assert.Single(await _repo.ListByAccAsync("LIRR"));
        Assert.Equal(AgreementDirection.AtoB, a.Clauses.Single(c => c.Id == andata).Direction);
    }

    [Fact]
    public async Task Copiare_un_verso_nell_altro_rinumera_i_gruppi_e_non_sovrascrive()
    {
        var (id, first) = await WithClauseAsync();
        await _repo.AddAlternativeAsync("LIRR", first);

        Assert.Equal(2, await _repo.CopyDirectionAsync("LIRR", id, AgreementDirection.AtoB));

        var a = Assert.Single(await _repo.ListByAccAsync("LIRR"));
        var andata = a.Clauses.Where(c => c.Direction == AgreementDirection.AtoB).ToList();
        var ritorno = a.Clauses.Where(c => c.Direction == AgreementDirection.BtoA).ToList();
        Assert.Equal(2, ritorno.Count);
        // Gruppo RINUMERATO: i numeri sono progressivi per accordo, e riusarli farebbe sembrare le clausole
        // del verso opposto varianti delle prime.
        Assert.NotEqual(andata[0].VariantGroup, ritorno[0].VariantGroup);
        Assert.Equal(ritorno[0].VariantGroup, ritorno[1].VariantGroup);

        // Una seconda copia non fa niente: sovrascrivere butterebbe via cio' che qualcuno ha scritto, e
        // accodare produrrebbe un doppione di ogni clausola.
        Assert.Equal(0, await _repo.CopyDirectionAsync("LIRR", id, AgreementDirection.AtoB));
    }

    // ---- ripristino ----------------------------------------------------------------------------------

    [Fact]
    public async Task Un_accordo_ripristinato_rimette_l_outline_non_righe_appiattite()
    {
        var snapshot = new AgreementSnapshot(
            Header(sideB: new[] { _ftwrId }),
            new[]
            {
                new AgreementClauseSnapshot(Clause("VALMA", 130), AgreementDirection.AtoB, 1, 7, 0),
                new AgreementClauseSnapshot(Clause("VALMA", 110), AgreementDirection.AtoB, 2, 7, 1),
            });

        await _repo.RestoreAgreementAsync("LIRR", snapshot);

        var a = Assert.Single(await _repo.ListByAccAsync("LIRR"));
        Assert.Equal(new[] { 0, 1 }, a.Clauses.OrderBy(c => c.Order).Select(c => c.VariantDepth));
        Assert.Equal(a.Clauses[0].VariantGroup, a.Clauses[1].VariantGroup);
    }

    [Fact]
    public async Task Un_outline_rotto_non_rientra_da_un_ripristino()
    {
        // Una fotografia puo' essere vecchia di un archivio che nel frattempo e' cambiato: un'eccezione senza
        // la clausola che la ospita descriverebbe quella sbagliata, e nessun errore lo direbbe.
        var snapshot = new AgreementSnapshot(
            Header(sideB: new[] { _ftwrId }),
            new[] { new AgreementClauseSnapshot(Clause("VALMA", 110), AgreementDirection.AtoB, 1, 7, 2) });

        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => _repo.RestoreAgreementAsync("LIRR", snapshot));
    }

    // ---- attrezzi ------------------------------------------------------------------------------------

    private static string[] Side(AgreementRow a, AgreementSide side) =>
        a.Parties.Where(p => p.Side == side).OrderBy(p => p.Order).Select(p => p.Callsign).ToArray();

    private AgreementInput Header(IReadOnlyList<int>? sideB = null) => new()
    {
        TrafficKind = TransferFlowKind.Arrival,
        SideA = new[] { _neId },
        SideB = sideB ?? Array.Empty<int>(),
        Airports = new[] { new AgreementAirportInput("LIRF") },
    };

    private static AgreementClauseInput Clause(string cops, int level) => new()
    {
        Cops = cops, LevelValue = level, LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.AtOrBelow,
    };

    private async Task<(int Agreement, int Clause)> WithClauseAsync()
    {
        var id = await _repo.AddAgreementAsync("LIRR", Header(sideB: new[] { _ftwrId }));
        var clause = await _repo.AddClauseAsync("LIRR", id, AgreementDirection.AtoB, Clause("VALMA", 130));
        return (id, clause);
    }

    /// <summary>Un settore di un'altra ACC, per provare che un accordo si vede da entrambi i capi.</summary>
    private async Task<int> ForeignSectorAsync()
    {
        var acc = new Vipi.Domain.Entities.Acc { Code = "LFFF", Name = "Paris", IsForeign = true };
        _db.Accs.Add(acc);
        await _db.SaveChangesAsync();

        var s = new Vipi.Domain.Entities.Sector
        {
            AccId = acc.Id, Callsign = "LFFF_CTR", Name = "Paris Control",
            Type = SectorType.Ctr, Kind = SectorKind.Acc, IsActive = true,
        };
        _db.Sectors.Add(s);
        await _db.SaveChangesAsync();
        return s.Id;
    }
}
