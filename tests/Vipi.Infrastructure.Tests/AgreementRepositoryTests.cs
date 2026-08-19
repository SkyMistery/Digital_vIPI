using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// CRUD di accordi e sezioni e, soprattutto, l'**outline delle varianti dentro una sezione** — che è la sola
/// parte davvero delicata: muovere una capofila senza le sue eccezioni le riassegna a un'altra alternativa
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

    // ---- l'accordo: due capi, e uno solo per coppia --------------------------------------------------

    [Fact]
    public async Task Un_accordo_con_una_sezione_a_due_aeroporti_va_e_torna()
    {
        var id = await _repo.AddAgreementAsync("LIRR", Pair(_neId, _ftwrId, "prova"));
        await _repo.AddSectionAsync("LIRR", id, new AgreementSectionInput
        {
            Kind = TransferFlowKind.Arrival,
            Direction = AgreementDirection.AtoB,
            Description = "via BIRSU",
            Airports = new[] { new AgreementAirportInput("LIRF"), new AgreementAirportInput("LFPG", "Paris CDG") },
        });
        var sec = Assert.Single((await _repo.ListByAccAsync("LIRR")).Single().Sections);
        await _repo.AddClauseAsync("LIRR", sec.Id, Clause("VALMA", 130));

        var a = Assert.Single(await _repo.ListByAccAsync("LIRR"));
        Assert.Equal("prova", a.Note);
        Assert.Equal(new[] { "LIRR_NE_CTR", "LIRF_TWR" }, new[] { a.SideA.Callsign, a.SideB.Callsign });
        var s = Assert.Single(a.Sections);
        Assert.Equal(new[] { "LIRF", "LFPG" }, s.Airports.Select(x => x.Icao));
        // Il nome si tiene solo per lo scalo fuori catalogo: per gli altri viene dal catalogo, e una copia
        // qui divergerebbe alla prima rinomina.
        Assert.Equal(new string?[] { null, "Paris CDG" }, s.Airports.Select(x => x.Name));
        Assert.Equal("VALMA", Assert.Single(s.Clauses).Cops);
    }

    [Fact]
    public async Task I_due_lati_si_salvano_in_forma_canonica_in_qualunque_ordine_arrivino()
    {
        // ⚠️ La forma canonica (id minore = A) è la CHIAVE dell'unicità, non una scelta editoriale: in SQL non
        // esiste «insieme di due». Non perde niente perché il verso vive sulla sezione.
        var id = await _repo.AddAgreementAsync("LIRR", Pair(Math.Max(_neId, _ftwrId), Math.Min(_neId, _ftwrId)));

        var a = Assert.Single(await _repo.ListByAccAsync("LIRR"));
        Assert.True(a.SideA.SectorId < a.SideB.SectorId);
        Assert.Equal(id, await _repo.FindByPairAsync("LIRR", _ftwrId, _neId));
        Assert.Equal(id, await _repo.FindByPairAsync("LIRR", _neId, _ftwrId));
    }

    [Fact]
    public async Task Fra_due_enti_esiste_un_accordo_solo()
    {
        await _repo.AddAgreementAsync("LIRR", Pair(_neId, _ftwrId));

        // ⚠️ Con l'ordine invertito è la STESSA coppia: se non lo fosse, l'archivio tornerebbe ad avere la
        // stessa relazione in due schede — che è ciò che questo giro ha appena chiuso.
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => _repo.AddAgreementAsync("LIRR", Pair(_ftwrId, _neId)));
    }

    [Fact]
    public async Task Cambiare_un_capo_ribalta_i_versi_delle_sezioni_se_i_lati_si_scambiano()
    {
        // ⚠️ È l'unico posto dove la canonizzazione si vede: sostituire un ente può spostare l'altro dall'altra
        // parte, e lasciare i versi com'erano farebbe dire a ogni sezione il CONTRARIO di ciò che c'era scritto.
        var alto = Math.Max(_neId, _ftwrId);
        var basso = Math.Min(_neId, _ftwrId);
        var id = await _repo.AddAgreementAsync("LIRR", Pair(basso, alto));
        var sec = await _repo.AddSectionAsync("LIRR", id, Section(AgreementDirection.AtoB));

        // _tsId prende il posto del lato B: se il suo id è minore del lato A, i due si scambiano.
        await _repo.UpdateAgreementAsync("LIRR", id, Pair(basso, _tsId));

        var a = Assert.Single(await _repo.ListByAccAsync("LIRR"));
        var atteso = a.SideA.SectorId == basso ? AgreementDirection.AtoB : AgreementDirection.BtoA;
        Assert.Equal(atteso, a.Sections.Single(s => s.Id == sec).Direction);
    }

    [Fact]
    public async Task Un_accordo_si_vede_anche_dal_capo_che_non_ne_e_responsabile()
    {
        // La duplicazione per ACC chiusa: prima il flusso viveva nel «secchio» di una sola ACC, quindi un
        // accordo poteva essere invisibile a uno dei suoi due capi, e un centro estero confinante con due ACC
        // italiane andava riscritto due volte.
        var estero = await ForeignSectorAsync();
        var id = await _repo.AddAgreementAsync("LIRR", Pair(_neId, estero));

        Assert.Equal(id, Assert.Single(await _repo.ListByAccAsync("LIRR")).Id);
        Assert.Equal(id, Assert.Single(await _repo.ListByAccAsync("LFFF")).Id);
    }

    // ---- clausole e outline --------------------------------------------------------------------------

    [Fact]
    public async Task Un_alternativa_nasce_pari_grado_e_senza_condizione()
    {
        var (_, first) = await WithClauseAsync();
        await _repo.UpdateClauseAsync("LIRR", first, Clause("VALMA", 130) with { ConditionLabel = "16R" });

        var second = await _repo.AddAlternativeAsync("LIRR", first);
        var clauses = await ClausesAsync();

        Assert.Equal(2, clauses.Count);
        Assert.All(clauses, c => Assert.Equal(0, c.VariantDepth));
        Assert.All(clauses, c => Assert.NotNull(c.VariantGroup));
        // ⚠️ La condizione NON si copia: e' esattamente cio' che l'alternativa deve dire di diverso, e
        // copiarla darebbe due clausole identiche.
        Assert.Equal("16R", clauses.Single(c => c.Id == first).ConditionLabel);
        Assert.Null(clauses.Single(c => c.Id == second).ConditionLabel);
    }

    [Fact]
    public async Task Un_eccezione_nasce_un_livello_piu_dentro_subito_sotto()
    {
        var (_, first) = await WithClauseAsync();
        var alt = await _repo.AddAlternativeAsync("LIRR", first);
        var exc = await _repo.AddExceptionAsync("LIRR", first);

        var clauses = await ClausesAsync();
        // L'eccezione va SUBITO SOTTO la sua clausola, non in fondo al gruppo: l'ordine e' la struttura.
        Assert.Equal(new[] { first, exc, alt }, clauses.OrderBy(c => c.Order).Select(c => c.Id));
        Assert.Equal(1, clauses.Single(c => c.Id == exc).VariantDepth);
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

        var clauses = await ClausesAsync();
        Assert.Equal(new[] { alt, first, exc }, clauses.OrderBy(c => c.Order).Select(c => c.Id));
        Assert.Equal(1, clauses.Single(c => c.Id == exc).VariantDepth);
    }

    [Fact]
    public async Task Sfilare_una_clausola_porta_via_il_sottoalbero_e_lo_riporta_a_zero()
    {
        var (_, first) = await WithClauseAsync();
        var alt = await _repo.AddAlternativeAsync("LIRR", first);
        var exc = await _repo.AddExceptionAsync("LIRR", alt);

        await _repo.DetachVariantAsync("LIRR", alt);

        var clauses = await ClausesAsync();
        var sfilata = clauses.Single(c => c.Id == alt);
        var eccezione = clauses.Single(c => c.Id == exc);
        // Il pezzo staccato riparte da zero e resta un gruppo, perche' ha ancora due clausole da tenere insieme.
        Assert.Equal(0, sfilata.VariantDepth);
        Assert.Equal(sfilata.VariantGroup, eccezione.VariantGroup);
        Assert.Equal(1, eccezione.VariantDepth);
        // Cio' che resta e' una clausola sola: un gruppo di uno non e' un gruppo.
        Assert.Null(clauses.Single(c => c.Id == first).VariantGroup);
    }

    [Fact]
    public async Task I_punti_si_propagano_alle_sorelle_del_gruppo()
    {
        // I punti sono l'identita' dell'accordo dentro un gruppo: le varianti sono lo stesso accordo detto a
        // condizioni diverse. Il RICEVENTE, che prima viaggiava con loro e poteva divergere, qui non c'e' —
        // e' dell'accordo, e il verso lo dice la sezione.
        var (_, first) = await WithClauseAsync();
        var alt = await _repo.AddAlternativeAsync("LIRR", first);

        await _repo.UpdateClauseAsync("LIRR", first, Clause("BIRSU, TOPNO", 150));

        var clauses = await ClausesAsync();
        Assert.All(clauses, c => Assert.Equal("BIRSU, TOPNO", c.Cops));
        // Il livello invece resta della singola clausola: e' proprio cio' che due varianti dicono diverso.
        Assert.Equal(150, clauses.Single(c => c.Id == first).LevelValue);
        Assert.Equal(130, clauses.Single(c => c.Id == alt).LevelValue);
    }

    [Fact]
    public async Task I_punti_si_normalizzano_scrivendo()
    {
        var (_, first) = await WithClauseAsync();
        await _repo.UpdateClauseAsync("LIRR", first, Clause("  BIRSU ,, TOPNO  ,  ", 130));

        Assert.Equal("BIRSU, TOPNO", Assert.Single(await ClausesAsync()).Cops);
    }

    [Fact]
    public async Task Eliminare_una_variante_scioglie_il_gruppo_rimasto_di_una()
    {
        var (_, first) = await WithClauseAsync();
        var alt = await _repo.AddAlternativeAsync("LIRR", first);

        await _repo.DeleteClauseAsync("LIRR", alt);

        Assert.Null(Assert.Single(await ClausesAsync()).VariantGroup);
    }

    // ---- le sezioni ----------------------------------------------------------------------------------

    [Fact]
    public async Task Le_clausole_di_due_sezioni_non_si_mescolano()
    {
        var id = await _repo.AddAgreementAsync("LIRR", Pair(_neId, _ftwrId));
        var andata = await _repo.AddSectionAsync("LIRR", id, Section(AgreementDirection.AtoB));
        var ritorno = await _repo.AddSectionAsync("LIRR", id, Section(AgreementDirection.BtoA));
        var prima = await _repo.AddClauseAsync("LIRR", andata, Clause("VALMA", 130));
        var seconda = await _repo.AddClauseAsync("LIRR", ritorno, Clause("VALMA", 90));

        var a = Assert.Single(await _repo.ListByAccAsync("LIRR"));
        // L'ordine riparte da uno in ogni sezione: sono due tabelle, non una sola con due meta'.
        Assert.Equal(1, a.AllClauses.Single(c => c.Id == prima).Order);
        Assert.Equal(1, a.AllClauses.Single(c => c.Id == seconda).Order);

        // E il trascinamento non attraversa la sezione: cambiare tabella a una clausola e' dire un'altra cosa,
        // non spostarla.
        await _repo.MoveClauseToAsync("LIRR", prima, seconda);
        a = Assert.Single(await _repo.ListByAccAsync("LIRR"));
        Assert.Equal(andata, a.AllClauses.Single(c => c.Id == prima).SectionId);
    }

    [Fact]
    public async Task Copiare_una_sezione_nel_verso_opposto_rinumera_i_gruppi_e_non_sovrascrive()
    {
        var (sec, first) = await WithClauseAsync();
        await _repo.AddAlternativeAsync("LIRR", first);

        var reverse = await _repo.CopySectionToReverseAsync("LIRR", sec);
        Assert.NotNull(reverse);

        var a = Assert.Single(await _repo.ListByAccAsync("LIRR"));
        var andata = a.Sections.Single(s => s.Id == sec);
        var ritorno = a.Sections.Single(s => s.Id == reverse);
        Assert.Equal(2, ritorno.Clauses.Count);
        Assert.Equal(AgreementDirection.BtoA, ritorno.Direction);
        // Gruppo RINUMERATO: i numeri sono progressivi per accordo, e riusarli farebbe sembrare le clausole
        // del verso opposto varianti delle prime.
        Assert.NotEqual(andata.Clauses[0].VariantGroup, ritorno.Clauses[0].VariantGroup);
        Assert.Equal(ritorno.Clauses[0].VariantGroup, ritorno.Clauses[1].VariantGroup);

        // Una seconda copia non fa niente: sovrascrivere butterebbe via cio' che qualcuno ha scritto, e
        // accodare produrrebbe un doppione di ogni clausola.
        Assert.Null(await _repo.CopySectionToReverseAsync("LIRR", sec));
    }

    [Fact]
    public async Task Unire_due_gemelle_accoda_le_clausole_e_rinumera_i_gruppi()
    {
        // Le gemelle sono ciò che il travaso ha ereditato (#26/#27 in archivio): stesso traffico, stesso verso,
        // stessi scali.
        var id = await _repo.AddAgreementAsync("LIRR", Pair(_neId, _ftwrId));
        var keep = await _repo.AddSectionAsync("LIRR", id, Section(AgreementDirection.AtoB));
        var absorb = await _repo.AddSectionAsync("LIRR", id, Section(AgreementDirection.AtoB));
        var capofila = await _repo.AddClauseAsync("LIRR", keep, Clause("VALMA", 130));
        await _repo.AddAlternativeAsync("LIRR", capofila);
        var altra = await _repo.AddClauseAsync("LIRR", absorb, Clause("OLGAT", 110));
        await _repo.AddAlternativeAsync("LIRR", altra);

        Assert.Equal(2, await _repo.MergeSectionsAsync("LIRR", keep, absorb));

        var a = Assert.Single(await _repo.ListByAccAsync("LIRR"));
        var sola = Assert.Single(a.Sections);
        Assert.Equal(keep, sola.Id);
        Assert.Equal(new[] { 1, 2, 3, 4 }, sola.Clauses.Select(c => c.Order));
        // I due gruppi «1» venivano da sezioni diverse: uniti nella stessa tabella devono restare DUE.
        Assert.Equal(2, sola.Clauses.Select(c => c.VariantGroup).Distinct().Count());
    }

    [Fact]
    public async Task Unire_si_rifiuta_su_due_sezioni_che_non_dicono_la_stessa_cosa()
    {
        // ⚠️ La condizione si rivalida al momento del tasto: fra la segnalazione e il clic l'archivio può essere
        // cambiato, e mescolare due tabelle diverse non è più separabile.
        var id = await _repo.AddAgreementAsync("LIRR", Pair(_neId, _ftwrId));
        var keep = await _repo.AddSectionAsync("LIRR", id, Section(AgreementDirection.AtoB));
        var altra = await _repo.AddSectionAsync("LIRR", id, Section(AgreementDirection.BtoA));

        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => _repo.MergeSectionsAsync("LIRR", keep, altra));
    }

    // ---- ripristino ----------------------------------------------------------------------------------

    [Fact]
    public async Task Un_accordo_ripristinato_rimette_le_sezioni_e_l_outline_non_righe_appiattite()
    {
        var snapshot = new AgreementSnapshot(
            Pair(_neId, _ftwrId),
            new[]
            {
                new AgreementSectionSnapshot(
                    new AgreementSectionInput
                    {
                        Kind = TransferFlowKind.Arrival,
                        Direction = AgreementDirection.AtoB,
                        Airports = new[] { new AgreementAirportInput("LIRF") },
                    },
                    1,
                    new[]
                    {
                        new AgreementClauseSnapshot(Clause("VALMA", 130), 1, 7, 0),
                        new AgreementClauseSnapshot(Clause("VALMA", 110), 2, 7, 1),
                    }),
            });

        await _repo.RestoreAgreementAsync("LIRR", snapshot);

        var a = Assert.Single(await _repo.ListByAccAsync("LIRR"));
        var s = Assert.Single(a.Sections);
        Assert.Equal(new[] { "LIRF" }, s.Airports.Select(x => x.Icao));
        Assert.Equal(new[] { 0, 1 }, s.Clauses.OrderBy(c => c.Order).Select(c => c.VariantDepth));
        Assert.Equal(s.Clauses[0].VariantGroup, s.Clauses[1].VariantGroup);
    }

    [Fact]
    public async Task Un_outline_rotto_non_rientra_da_un_ripristino()
    {
        // Una fotografia puo' essere vecchia di un archivio che nel frattempo e' cambiato: un'eccezione senza
        // la clausola che la ospita descriverebbe quella sbagliata, e nessun errore lo direbbe.
        var snapshot = new AgreementSnapshot(
            Pair(_neId, _ftwrId),
            new[]
            {
                new AgreementSectionSnapshot(
                    new AgreementSectionInput
                    {
                        Kind = TransferFlowKind.Overflight,
                        Direction = AgreementDirection.AtoB,
                    },
                    1,
                    new[] { new AgreementClauseSnapshot(Clause("VALMA", 110), 1, 7, 2) }),
            });

        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => _repo.RestoreAgreementAsync("LIRR", snapshot));
    }

    [Fact]
    public async Task Il_ripristino_di_un_accordo_non_passa_dalle_regole_di_creazione()
    {
        // ⚠️ Fuori dalle regole DI PROPOSITO, come per il modello di ferragosto: un annulla che rifiutasse di
        // rimettere ciò che ha appena cancellato sarebbe peggio della regola. Qui la forma canonica si applica
        // lo stesso, perché quella non è una regola editoriale ma la chiave dell'archivio.
        var snapshot = new AgreementSnapshot(
            Pair(Math.Max(_neId, _ftwrId), Math.Min(_neId, _ftwrId)),
            Array.Empty<AgreementSectionSnapshot>());

        await _repo.RestoreAgreementAsync("LIRR", snapshot);

        var a = Assert.Single(await _repo.ListByAccAsync("LIRR"));
        Assert.True(a.SideA.SectorId < a.SideB.SectorId);
    }

    // ---- attrezzi ------------------------------------------------------------------------------------

    private static AgreementInput Pair(int sideA, int sideB, string? note = null) => new()
    {
        SideASectorId = sideA,
        SideBSectorId = sideB,
        Note = note,
    };

    private static AgreementSectionInput Section(AgreementDirection direction) => new()
    {
        Kind = TransferFlowKind.Arrival,
        Direction = direction,
        Airports = new[] { new AgreementAirportInput("LIRF") },
    };

    private static AgreementClauseInput Clause(string cops, int level) => new()
    {
        Cops = cops, LevelValue = level, LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.AtOrBelow,
    };

    /// <summary>Un accordo con una sezione e una clausola: il punto di partenza di tutti i test sull'outline.</summary>
    private async Task<(int Section, int Clause)> WithClauseAsync()
    {
        var id = await _repo.AddAgreementAsync("LIRR", Pair(_neId, _ftwrId));
        var sec = await _repo.AddSectionAsync("LIRR", id, Section(AgreementDirection.AtoB));
        var clause = await _repo.AddClauseAsync("LIRR", sec, Clause("VALMA", 130));
        return (sec, clause);
    }

    private async Task<IReadOnlyList<AgreementClauseRow>> ClausesAsync() =>
        Assert.Single(await _repo.ListByAccAsync("LIRR")).AllClauses.ToList();

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
