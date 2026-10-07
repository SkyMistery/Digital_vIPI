using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Spostare una sezione o delle clausole nell'accordo di un'<b>altra coppia</b>. Un accordo è una coppia di enti:
/// una clausola scritta sotto la coppia sbagliata si poteva solo riscrivere. Qui si dice «chi cede → chi riceve», e
/// il lavoro va nell'accordo di quella coppia. Carta <c>docs/feature/2026-10-04-copertura-unica.md</c> §8.
///
/// <para>Le tre cose che non devono succedere in silenzio: il <b>verso</b> che si capovolge perché i lati
/// dell'accordo di arrivo sono canonici in un altro ordine; i <b>gruppi di varianti</b> che si fondono con quelli
/// dell'accordo di arrivo perché hanno lo stesso numero; un <b>annulla</b> che rimette le righe in coda invece che
/// dov'erano.</para>
/// </summary>
public class AgreementMoveTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfAgreementRepository _repo = default!;
    private int _ne, _ts, _ew, _su;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        await RomaStructureSeed.SeedAsync(_db);
        _repo = new EfAgreementRepository(_db);

        var settori = await _db.Sectors.ToListAsync();
        int Id(string cs) => settori.First(s => s.Callsign == cs).Id;
        (_ne, _ts, _ew, _su) = (Id("LIRR_NE_CTR"), Id("LIRR_TS_CTR"), Id("LIRR_EW_CTR"), Id("LIRR_SU_CTR"));
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    // ---- la sezione ---------------------------------------------------------------------------------

    [Fact]
    public async Task Una_sezione_passa_nell_accordo_dell_altra_coppia_con_le_sue_clausole()
    {
        var (sezione, _) = await SezioneAsync(_ne, _ew, "VALMA", "BIRSU");

        var esito = await _repo.MoveSectionAsync("LIRR", sezione, _ts, _ew);

        Assert.True(esito.AgreementCreated);
        Assert.Equal(2, esito.Clauses);
        var arrivo = (await _repo.ListByAccAsync("LIRR")).Single(a => a.Id == esito.AgreementId);
        var spostata = Assert.Single(arrivo.Sections);
        Assert.Equal(sezione, spostata.Id);
        Assert.Equal(new[] { "VALMA", "BIRSU" }, spostata.Clauses.OrderBy(c => c.Order).Select(c => c.Cops));
        // L'accordo di partenza resta, vuoto: toglierlo è una scelta di chi scrive, non di uno spostamento.
        Assert.Empty((await _repo.ListByAccAsync("LIRR")).Single(a => a.Id != esito.AgreementId).Sections);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Chi_cede_resta_chi_cede_qualunque_sia_l_ordine_canonico_dei_lati(bool cedeIlNuovo)
    {
        // ⚠️ I lati dell'accordo di arrivo sono in forma canonica (id minore = A): il verso della sezione va
        // ricalcolato su di lui, o la tabella direbbe il contrario di quel che si è chiesto — senza un errore.
        var (sezione, _) = await SezioneAsync(_ne, _ew, "VALMA");
        var (cede, riceve) = cedeIlNuovo ? (_su, _ew) : (_ew, _su);

        var esito = await _repo.MoveSectionAsync("LIRR", sezione, cede, riceve);

        var arrivo = (await _repo.ListByAccAsync("LIRR")).Single(a => a.Id == esito.AgreementId);
        var s = Assert.Single(arrivo.Sections);
        Assert.Equal(cede, arrivo.Sender(s.Direction).SectorId);
        Assert.Equal(riceve, arrivo.Receiver(s.Direction).SectorId);
    }

    [Fact]
    public async Task Se_l_accordo_della_coppia_esiste_la_sezione_ci_entra_in_coda_e_i_gruppi_non_si_fondono()
    {
        // Due accordi, ognuno con un gruppo di varianti che porta lo STESSO numero (i gruppi sono progressivi per
        // accordo): spostando la sezione, i due gruppi devono restare due.
        var (sezA, clausolaA) = await SezioneAsync(_ne, _ew, "VALMA");
        await _repo.AddAlternativeAsync("LIRR", clausolaA);
        var (sezB, clausolaB) = await SezioneAsync(_ts, _ew, "BIRSU");
        await _repo.AddAlternativeAsync("LIRR", clausolaB);

        var esito = await _repo.MoveSectionAsync("LIRR", sezA, _ts, _ew);

        Assert.False(esito.AgreementCreated);
        var arrivo = (await _repo.ListByAccAsync("LIRR")).Single(a => a.Id == esito.AgreementId);
        Assert.Equal(new[] { sezB, sezA }, arrivo.Sections.OrderBy(s => s.Order).Select(s => s.Id));
        var gruppi = arrivo.Sections.Select(s => s.Clauses.Select(c => c.VariantGroup).Distinct().Single()).ToList();
        Assert.All(gruppi, g => Assert.NotNull(g));
        Assert.Equal(2, gruppi.Distinct().Count());
    }

    [Fact]
    public async Task Stessa_coppia_e_stesso_verso_non_c_e_niente_da_spostare()
    {
        var (sezione, _) = await SezioneAsync(_ne, _ew, "VALMA");

        var esito = await _repo.MoveSectionAsync("LIRR", sezione, _ne, _ew);

        Assert.Null(esito.SectionId);
        Assert.Equal(0, esito.Clauses);
        Assert.Single(await _repo.ListByAccAsync("LIRR"));
    }

    [Fact]
    public async Task Chi_cede_e_chi_riceve_non_possono_essere_lo_stesso_ente()
    {
        var (sezione, _) = await SezioneAsync(_ne, _ew, "VALMA");

        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => _repo.MoveSectionAsync("LIRR", sezione, _ts, _ts));
    }

    [Fact]
    public async Task Annullare_lo_spostamento_di_una_sezione_la_rimette_dov_era_e_toglie_l_accordo_nato_per_lei()
    {
        var (sezione, clausola) = await SezioneAsync(_ne, _ew, "VALMA");
        await _repo.AddAlternativeAsync("LIRR", clausola);
        var prima = await FotoAsync();

        var esito = await _repo.MoveSectionAsync("LIRR", sezione, _ts, _su);
        await _repo.UndoMoveAsync("LIRR", esito.Undo);

        Assert.Equal(prima, await FotoAsync());
        Assert.Single(await _repo.ListByAccAsync("LIRR"));
    }

    // ---- le clausole --------------------------------------------------------------------------------

    [Fact]
    public async Task Le_clausole_scelte_vanno_nella_sezione_gemella_dell_altra_coppia_che_nasce_se_manca()
    {
        var (sezione, prima) = await SezioneAsync(_ne, _ew, "VALMA", "BIRSU", "GINEL");
        var righe = await ClausoleDiAsync(sezione);

        var esito = await _repo.MoveClausesAsync("LIRR", new[] { righe[1].Id }, _ts, _ew);

        Assert.True(esito.AgreementCreated);
        Assert.Equal(1, esito.Clauses);
        var accordi = await _repo.ListByAccAsync("LIRR");
        var arrivo = accordi.Single(a => a.Id == esito.AgreementId);
        var gemella = Assert.Single(arrivo.Sections);
        // La gemella dice la stessa cosa: stesso traffico, stessi scali, e cede chi è stato indicato.
        Assert.Equal(TransferFlowKind.Arrival, gemella.Kind);
        Assert.Equal(new[] { "LIRF" }, gemella.Airports.Select(a => a.Icao));
        Assert.Equal(_ts, arrivo.Sender(gemella.Direction).SectorId);
        Assert.Equal("BIRSU", Assert.Single(gemella.Clauses).Cops);
        // Di qua restano le altre due, nell'ordine di prima.
        Assert.Equal(new[] { "VALMA", "GINEL" }, (await ClausoleDiAsync(sezione)).Select(c => c.Cops));
        Assert.Equal(prima, righe[0].Id);
    }

    [Fact]
    public async Task Un_gruppo_di_varianti_si_sposta_intero_anche_se_ne_e_scelta_una_riga_sola()
    {
        // Portarne via una lascerebbe di qua un'alternativa senza sorelle e di là una riga che non è più
        // l'eccezione di nessuno.
        var (sezione, capofila) = await SezioneAsync(_ne, _ew, "VALMA", "BIRSU");
        var alternativa = await _repo.AddAlternativeAsync("LIRR", capofila);
        await _repo.AddExceptionAsync("LIRR", alternativa);

        var esito = await _repo.MoveClausesAsync("LIRR", new[] { alternativa }, _ts, _ew);

        Assert.Equal(3, esito.Clauses);
        var arrivate = await ClausoleDiAsync(esito.SectionId!.Value);
        Assert.Equal(3, arrivate.Count);
        Assert.Single(arrivate.Select(c => c.VariantGroup).Distinct());
        Assert.Equal(new[] { 0, 0, 1 }, arrivate.Select(c => c.VariantDepth));
        Assert.Equal("BIRSU", Assert.Single(await ClausoleDiAsync(sezione)).Cops);
    }

    [Fact]
    public async Task Se_la_sezione_gemella_esiste_le_clausole_ci_entrano_in_coda()
    {
        var (sezA, _) = await SezioneAsync(_ne, _ew, "VALMA", "BIRSU");
        var (sezB, _) = await SezioneAsync(_ts, _ew, "GINEL");
        var daSpostare = (await ClausoleDiAsync(sezA))[0].Id;

        var esito = await _repo.MoveClausesAsync("LIRR", new[] { daSpostare }, _ts, _ew);

        Assert.Equal(sezB, esito.SectionId);
        Assert.Empty(esito.Undo.CreatedSectionIds);
        Assert.Equal(new[] { "GINEL", "VALMA" }, (await ClausoleDiAsync(sezB)).Select(c => c.Cops));
    }

    [Fact]
    public async Task Annullare_lo_spostamento_di_clausole_le_rimette_nell_ordine_di_prima_e_toglie_quel_che_era_nato()
    {
        // ⚠️ Non «rispostale indietro»: finirebbero in coda, e l'ordine è quello in cui il documento le stampa.
        var (sezione, _) = await SezioneAsync(_ne, _ew, "VALMA", "BIRSU", "GINEL");
        var prima = await FotoAsync();
        var inMezzo = (await ClausoleDiAsync(sezione))[1].Id;

        var esito = await _repo.MoveClausesAsync("LIRR", new[] { inMezzo }, _ts, _su);
        await _repo.UndoMoveAsync("LIRR", esito.Undo);

        Assert.Equal(prima, await FotoAsync());
        Assert.Single(await _repo.ListByAccAsync("LIRR"));
    }

    [Fact]
    public async Task L_annulla_non_cancella_la_sezione_nata_se_nel_frattempo_qualcuno_ci_ha_scritto()
    {
        var (sezione, _) = await SezioneAsync(_ne, _ew, "VALMA", "BIRSU");
        var esito = await _repo.MoveClausesAsync("LIRR", new[] { (await ClausoleDiAsync(sezione))[0].Id }, _ts, _ew);
        await _repo.AddClauseAsync("LIRR", esito.SectionId!.Value, Clausola("NUOVA"));

        await _repo.UndoMoveAsync("LIRR", esito.Undo);

        Assert.Equal("NUOVA", Assert.Single(await ClausoleDiAsync(esito.SectionId!.Value)).Cops);
        Assert.Equal(new[] { "VALMA", "BIRSU" }, (await ClausoleDiAsync(sezione)).Select(c => c.Cops));
    }

    // ---- attrezzi -----------------------------------------------------------------------------------

    private static AgreementClauseInput Clausola(string cops) => new()
    {
        Cops = cops, LevelValue = 130, LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.AtOrBelow,
    };

    /// <summary>Un accordo fra i due enti (se non c'è) e una sezione «arrivi a LIRF», cede il primo, con quelle clausole.</summary>
    private async Task<(int Sezione, int PrimaClausola)> SezioneAsync(int cede, int riceve, params string[] cops)
    {
        var accordo = await _repo.FindByPairAsync("LIRR", cede, riceve)
                      ?? await _repo.AddAgreementAsync("LIRR", new AgreementInput { SideASectorId = cede, SideBSectorId = riceve });
        var sezione = await _repo.AddSectionAsync("LIRR", accordo, new AgreementSectionInput
        {
            Kind = TransferFlowKind.Arrival,
            Direction = cede < riceve ? AgreementDirection.AtoB : AgreementDirection.BtoA,
            Airports = new[] { new AgreementAirportInput("LIRF") },
        });
        var prima = 0;
        foreach (var c in cops)
        {
            var id = await _repo.AddClauseAsync("LIRR", sezione, Clausola(c));
            if (prima == 0) prima = id;
        }
        return (sezione, prima);
    }

    private async Task<IReadOnlyList<AgreementClauseRow>> ClausoleDiAsync(int sezione) =>
        (await _repo.ListByAccAsync("LIRR")).SelectMany(a => a.Sections).Single(s => s.Id == sezione)
        .Clauses.OrderBy(c => c.Order).ToList();

    /// <summary>Tutto l'archivio degli accordi in una stringa: quel che un annulla deve restituire uguale.</summary>
    private async Task<string> FotoAsync() => string.Join("\n",
        (await _repo.ListByAccAsync("LIRR")).OrderBy(a => a.Id).SelectMany(a => a.Sections.OrderBy(s => s.Id).Select(s =>
            $"accordo {a.Id} sezione {s.Id} verso {s.Direction} ordine {s.Order}: "
            + string.Join(" · ", s.Clauses.OrderBy(c => c.Order)
                .Select(c => $"{c.Id}@{c.Order} {c.Cops} g{c.VariantGroup} d{c.VariantDepth}")))));
}
