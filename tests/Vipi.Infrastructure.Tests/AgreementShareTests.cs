using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Le sezioni <b>condivise</b> fra più accordi: lo stesso contenuto vale per più coppie di enti e si scrive una
/// volta. Il caso è Trapani — gli stessi coordinamenti verso <c>LIRR_SU</c> (GAT) e verso <c>LIRR_MIL</c> (OAT).
/// Carta <c>docs/feature/2026-10-06-sezioni-condivise.md</c>.
///
/// <para>Qui i settori sono quelli del seed di Roma: <c>NE</c> fa la parte di chi cede sempre, <c>EW</c> e
/// <c>SU</c> quella dei due riceventi. La regola che ogni test tiene in piedi è una: <b>il contenuto si distrugge
/// solo quando se ne va l'ultima presenza</b>.</para>
/// </summary>
public class AgreementShareTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfAgreementRepository _repo = default!;
    private int _ne, _ts, _ew, _su;

    /// <summary>Gli stessi quattro settori in ordine di id. I lati di un accordo sono canonici (id minore = A): per
    /// provare che un verso segue l'accordo giusto serve una coppia in cui l'ente comune sta a sinistra e una in
    /// cui sta a destra — e questo lo decide l'ordine degli id, non il nome.</summary>
    private int _primo, _secondo, _terzo, _quarto;

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
        var perId = new[] { _ne, _ts, _ew, _su }.OrderBy(x => x).ToArray();
        (_primo, _secondo, _terzo, _quarto) = (perId[0], perId[1], perId[2], perId[3]);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    // ---- condividere --------------------------------------------------------------------------------

    [Fact]
    public async Task Una_sezione_condivisa_compare_in_tutti_e_due_gli_accordi_ed_e_la_stessa()
    {
        var (casa, sezione) = await SezioneAsync(_ne, _ew, "VALMA", "BIRSU");

        var esito = await _repo.ShareSectionAsync("LIRR", sezione, _ne, _su);

        Assert.True(esito.Added);
        Assert.True(esito.AgreementCreated);
        var (diCasa, ospite) = (await SezioneInAsync(casa, sezione), await SezioneInAsync(esito.AgreementId, sezione));
        // Stesse clausole, con gli STESSI id: non è una copia.
        Assert.Equal(diCasa.Clauses.Select(c => c.Id), ospite.Clauses.Select(c => c.Id));
        Assert.Equal(new[] { "VALMA", "BIRSU" }, ospite.Clauses.Select(c => c.Cops));
        // Ognuna dice dove ALTRO compare, e non nomina sé stessa.
        Assert.Equal(esito.AgreementId, Assert.Single(diCasa.SharedWith).AgreementId);
        Assert.Equal(casa, Assert.Single(ospite.SharedWith).AgreementId);
        Assert.True(diCasa.IsShared && ospite.IsShared);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Il_verso_e_della_presenza_chi_cede_resta_chi_cede_in_ogni_accordo(bool cedeIlComune)
    {
        // ⚠️ I lati di ogni accordo sono canonici (id minore = A) in un ordine suo. L'ente comune qui è il SECONDO
        // per id: nell'accordo col primo sta a destra, in quello col terzo sta a sinistra — quindi lo stesso «il
        // comune cede» è BtoA di qua e AtoB di là. Il verso va scritto sulla presenza, o la tabella ospite direbbe
        // il contrario. (Con un ente comune che sta dalla stessa parte in tutti e due, copiare il verso di casa
        // passerebbe per giusto: la prima stesura di questo test era così, e una mutazione l'ha smascherata.)
        var (comune, altroCasa, altroOspite) = (_secondo, _primo, _terzo);
        var (cede, riceve) = cedeIlComune ? (comune, altroCasa) : (altroCasa, comune);
        var (casa, sezione) = await SezioneAsync(cede, riceve, "VALMA");
        var (cedeLa, riceveLa) = cedeIlComune ? (comune, altroOspite) : (altroOspite, comune);

        var esito = await _repo.ShareSectionAsync("LIRR", sezione, cedeLa, riceveLa);

        var accordi = await _repo.ListByAccAsync("LIRR");
        var (a, b) = (accordi.Single(x => x.Id == casa), accordi.Single(x => x.Id == esito.AgreementId));
        var (versoCasa, versoOspite) = (a.Sections.Single(s => s.Id == sezione).Direction, b.Sections.Single(s => s.Id == sezione).Direction);
        Assert.NotEqual(versoCasa, versoOspite);   // è il punto: lo stesso senso, due versi scritti
        Assert.Equal(cede, a.Sender(versoCasa).SectorId);
        Assert.Equal(cedeLa, b.Sender(versoOspite).SectorId);
        Assert.Equal(riceveLa, b.Receiver(versoOspite).SectorId);
    }

    [Fact]
    public async Task Condividere_dove_compare_gia_non_fa_niente()
    {
        var (_, sezione) = await SezioneAsync(_ne, _ew, "VALMA");
        var prima = await _repo.ShareSectionAsync("LIRR", sezione, _ne, _su);

        var dinuovo = await _repo.ShareSectionAsync("LIRR", sezione, _ne, _su);
        var inCasa = await _repo.ShareSectionAsync("LIRR", sezione, _ne, _ew);

        Assert.False(dinuovo.Added);
        Assert.False(inCasa.Added);
        Assert.Single((await SezioneInAsync(prima.AgreementId, sezione)).SharedWith);
    }

    [Fact]
    public async Task Una_clausola_scritta_da_un_accordo_si_legge_anche_nell_altro()
    {
        var (casa, sezione) = await SezioneAsync(_ne, _ew, "VALMA");
        var ospite = (await _repo.ShareSectionAsync("LIRR", sezione, _ne, _su)).AgreementId;

        await _repo.AddClauseAsync("LIRR", sezione, Clausola("NUOVA"));

        Assert.Equal(new[] { "VALMA", "NUOVA" }, (await SezioneInAsync(casa, sezione)).Clauses.Select(c => c.Cops));
        Assert.Equal(new[] { "VALMA", "NUOVA" }, (await SezioneInAsync(ospite, sezione)).Clauses.Select(c => c.Cops));
    }

    // ---- togliere: il contenuto se ne va solo con l'ultima presenza ----------------------------------

    [Fact]
    public async Task Tolta_dall_accordo_ospite_resta_in_quello_di_casa_e_l_annulla_la_rimette()
    {
        var (casa, sezione) = await SezioneAsync(_ne, _ew, "VALMA");
        var ospite = (await _repo.ShareSectionAsync("LIRR", sezione, _ne, _su)).AgreementId;
        var prima = await FotoAsync();

        var disfa = await _repo.RemoveSectionAsync("LIRR", sezione, ospite);

        Assert.NotNull(disfa);
        Assert.False((await SezioneInAsync(casa, sezione)).IsShared);
        Assert.DoesNotContain((await AccordoAsync(ospite)).Sections, s => s.Id == sezione);

        await _repo.UndoPresenceAsync("LIRR", disfa!);
        Assert.Equal(prima, await FotoAsync());
    }

    [Fact]
    public async Task Tolta_dall_accordo_di_casa_la_casa_passa_all_ospite_e_il_contenuto_resta()
    {
        var (casa, sezione) = await SezioneAsync(_ne, _ew, "VALMA", "BIRSU");
        var ospite = (await _repo.ShareSectionAsync("LIRR", sezione, _su, _ne)).AgreementId;
        var prima = await FotoAsync();

        var disfa = await _repo.RemoveSectionAsync("LIRR", sezione, casa);

        Assert.NotNull(disfa);
        Assert.Empty((await AccordoAsync(casa)).Sections);
        var rimasta = await SezioneInAsync(ospite, sezione);
        Assert.Equal(new[] { "VALMA", "BIRSU" }, rimasta.Clauses.Select(c => c.Cops));
        Assert.False(rimasta.IsShared);
        // E nell'accordo che l'ha ereditata cede ancora chi cedeva lì.
        var b = await AccordoAsync(ospite);
        Assert.Equal(_su, b.Sender(rimasta.Direction).SectorId);

        // L'annulla la rimette di CASA dov'era, non in coda come un'ospite qualunque.
        await _repo.UndoPresenceAsync("LIRR", disfa!);
        Assert.Equal(prima, await FotoAsync());
        Assert.Equal(casa, await _db.AgreementSections.Where(s => s.Id == sezione).Select(s => s.AgreementId).SingleAsync());
    }

    [Fact]
    public async Task L_ultima_presenza_tolta_elimina_la_sezione()
    {
        var (casa, sezione) = await SezioneAsync(_ne, _ew, "VALMA");

        var disfa = await _repo.RemoveSectionAsync("LIRR", sezione, casa);

        Assert.Null(disfa);   // niente da rimettere come presenza: si annulla dalla fotografia, come sempre
        Assert.False(await _db.AgreementSections.AnyAsync(s => s.Id == sezione));
    }

    [Fact]
    public async Task L_eliminazione_di_sempre_non_distrugge_una_sezione_che_vive_anche_altrove()
    {
        var (_, sezione) = await SezioneAsync(_ne, _ew, "VALMA");
        var ospite = (await _repo.ShareSectionAsync("LIRR", sezione, _ne, _su)).AgreementId;

        await _repo.DeleteSectionAsync("LIRR", sezione);

        Assert.Equal("VALMA", Assert.Single((await SezioneInAsync(ospite, sezione)).Clauses).Cops);
    }

    [Fact]
    public async Task Eliminare_l_accordo_di_casa_non_porta_via_le_sezioni_che_altri_stanno_mostrando()
    {
        // 🔴 La cancellazione dell'accordo scende in cascata sulle sue sezioni: senza la promozione, il contenuto
        // sparirebbe anche dall'accordo che lo ospita.
        var (casa, condivisa) = await SezioneAsync(_ne, _ew, "VALMA");
        var (_, solaSua) = await SezioneAsync(_ne, _ew, "BIRSU", kind: TransferFlowKind.Overflight);
        var ospite = (await _repo.ShareSectionAsync("LIRR", condivisa, _ne, _su)).AgreementId;

        await _repo.DeleteAgreementAsync("LIRR", casa);

        Assert.Equal("VALMA", Assert.Single((await SezioneInAsync(ospite, condivisa)).Clauses).Cops);
        Assert.False(await _db.AgreementSections.AnyAsync(s => s.Id == solaSua));
        Assert.False(await _db.CoordinationAgreements.AnyAsync(a => a.Id == casa));
    }

    [Fact]
    public async Task Eliminare_l_accordo_ospite_lascia_la_sezione_a_casa_e_non_piu_condivisa()
    {
        var (casa, sezione) = await SezioneAsync(_ne, _ew, "VALMA");
        var ospite = (await _repo.ShareSectionAsync("LIRR", sezione, _ne, _su)).AgreementId;

        await _repo.DeleteAgreementAsync("LIRR", ospite);

        var rimasta = await SezioneInAsync(casa, sezione);
        Assert.False(rimasta.IsShared);
        Assert.Equal("VALMA", Assert.Single(rimasta.Clauses).Cops);
    }

    [Fact]
    public async Task L_accordo_eliminato_e_rimesso_riprende_la_sezione_come_presenza_non_come_copia()
    {
        var (casa, sezione) = await SezioneAsync(_ne, _ew, "VALMA");
        var ospite = (await _repo.ShareSectionAsync("LIRR", sezione, _ne, _su)).AgreementId;
        var prima = await AccordoAsync(ospite);
        var foto = new AgreementSnapshot(
            new AgreementInput { SideASectorId = prima.SideA.SectorId, SideBSectorId = prima.SideB.SectorId },
            prima.Sections.Select(s => new AgreementSectionSnapshot(
                new AgreementSectionInput { Kind = s.Kind, Direction = s.Direction, Airports = s.Airports.Select(a => new AgreementAirportInput(a.Icao)).ToList() },
                s.Order, Array.Empty<AgreementClauseSnapshot>(), SharedSectionId: s.IsShared ? s.Id : null)).ToList());
        await _repo.DeleteAgreementAsync("LIRR", ospite);

        var rimesso = await _repo.RestoreAgreementAsync("LIRR", foto);

        var tornata = await SezioneInAsync(rimesso, sezione);
        Assert.Equal((await SezioneInAsync(casa, sezione)).Clauses.Select(c => c.Id), tornata.Clauses.Select(c => c.Id));
        Assert.Equal(prima.Sender(prima.Sections.Single().Direction).SectorId,
            (await AccordoAsync(rimesso)).Sender(tornata.Direction).SectorId);
    }

    // ---- staccare -----------------------------------------------------------------------------------

    [Fact]
    public async Task Staccata_diventa_una_copia_indipendente_e_l_altra_resta_com_era()
    {
        var (casa, sezione) = await SezioneAsync(_ne, _ew, "VALMA", "BIRSU");
        var capofila = (await SezioneInAsync(casa, sezione)).Clauses[0].Id;
        await _repo.AddAlternativeAsync("LIRR", capofila);
        var ospite = (await _repo.ShareSectionAsync("LIRR", sezione, _ne, _su)).AgreementId;
        var versoOspite = (await SezioneInAsync(ospite, sezione)).Direction;

        var esito = await _repo.DetachSectionAsync("LIRR", sezione, ospite);

        var copia = await SezioneInAsync(ospite, esito.NewSectionId);
        var originale = await SezioneInAsync(casa, sezione);
        Assert.False(copia.IsShared);
        Assert.False(originale.IsShared);
        Assert.Equal(originale.Clauses.Select(c => c.Cops), copia.Clauses.Select(c => c.Cops));
        Assert.Empty(originale.Clauses.Select(c => c.Id).Intersect(copia.Clauses.Select(c => c.Id)));
        Assert.Equal(versoOspite, copia.Direction);
        // L'outline viaggia con la copia: stesse profondità, e un gruppo suo.
        Assert.Equal(originale.Clauses.Select(c => c.VariantDepth), copia.Clauses.Select(c => c.VariantDepth));
        Assert.Equal(originale.Clauses.Select(c => c.VariantGroup is null), copia.Clauses.Select(c => c.VariantGroup is null));

        // Da qui in poi sono due tabelle: scrivere nell'una non tocca l'altra.
        await _repo.AddClauseAsync("LIRR", esito.NewSectionId, Clausola("SOLOQUI"));
        Assert.DoesNotContain((await SezioneInAsync(casa, sezione)).Clauses, c => c.Cops == "SOLOQUI");
    }

    [Fact]
    public async Task Annullare_lo_stacco_toglie_la_copia_e_rimette_la_condivisione()
    {
        var (_, sezione) = await SezioneAsync(_ne, _ew, "VALMA");
        var ospite = (await _repo.ShareSectionAsync("LIRR", sezione, _ne, _su)).AgreementId;
        var prima = await FotoAsync();

        var esito = await _repo.DetachSectionAsync("LIRR", sezione, ospite);
        await _repo.UndoPresenceAsync("LIRR", esito.Undo);

        Assert.Equal(prima, await FotoAsync());
        Assert.False(await _db.AgreementSections.AnyAsync(s => s.Id == esito.NewSectionId));
    }

    [Fact]
    public async Task Staccare_dall_accordo_di_casa_lascia_il_contenuto_all_ospite()
    {
        var (casa, sezione) = await SezioneAsync(_ne, _ew, "VALMA");
        var ospite = (await _repo.ShareSectionAsync("LIRR", sezione, _ne, _su)).AgreementId;

        var esito = await _repo.DetachSectionAsync("LIRR", sezione, casa);

        Assert.Equal("VALMA", Assert.Single((await SezioneInAsync(casa, esito.NewSectionId)).Clauses).Cops);
        Assert.Equal("VALMA", Assert.Single((await SezioneInAsync(ospite, sezione)).Clauses).Cops);
        Assert.DoesNotContain((await AccordoAsync(casa)).Sections, s => s.Id == sezione);
    }

    [Fact]
    public async Task Una_sezione_che_non_e_condivisa_non_si_stacca()
    {
        var (casa, sezione) = await SezioneAsync(_ne, _ew, "VALMA");

        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => _repo.DetachSectionAsync("LIRR", sezione, casa));
    }

    // ---- modificare ---------------------------------------------------------------------------------

    [Fact]
    public async Task Girare_il_verso_da_un_accordo_non_lo_gira_nell_altro_ma_traffico_e_scali_valgono_per_tutti()
    {
        var (casa, sezione) = await SezioneAsync(_ne, _ew, "VALMA");
        var ospite = (await _repo.ShareSectionAsync("LIRR", sezione, _ne, _su)).AgreementId;
        var versoCasa = (await SezioneInAsync(casa, sezione)).Direction;
        var versoOspite = (await SezioneInAsync(ospite, sezione)).Direction;
        var girato = versoOspite == AgreementDirection.AtoB ? AgreementDirection.BtoA : AgreementDirection.AtoB;

        await _repo.UpdateSectionAsync("LIRR", sezione, new AgreementSectionInput
        {
            Kind = TransferFlowKind.Departure, Direction = girato,
            Airports = new[] { new AgreementAirportInput("LIRA") },
        }, agreementId: ospite);

        var (a, b) = (await SezioneInAsync(casa, sezione), await SezioneInAsync(ospite, sezione));
        Assert.Equal(versoCasa, a.Direction);
        Assert.Equal(girato, b.Direction);
        Assert.All(new[] { a, b }, s =>
        {
            Assert.Equal(TransferFlowKind.Departure, s.Kind);
            Assert.Equal(new[] { "LIRA" }, s.Airports.Select(x => x.Icao));
        });
    }

    [Fact]
    public async Task Se_i_lati_dell_accordo_ospite_si_scambiano_chi_cede_resta_chi_cede()
    {
        // Stessa trappola di UpdateAgreementAsync sulle sezioni di casa: cambiare un capo può spostare l'altro
        // dall'altra parte della forma canonica, e il verso della presenza va ribaltato con lui. L'ospite nasce
        // fra il secondo e il quarto (il comune è A); sostituendo il quarto col primo, il comune diventa B.
        var comune = _secondo;
        var (_, sezione) = await SezioneAsync(comune, _terzo, "VALMA");
        var ospite = (await _repo.ShareSectionAsync("LIRR", sezione, comune, _quarto)).AgreementId;
        var versoPrima = (await SezioneInAsync(ospite, sezione)).Direction;

        await _repo.UpdateAgreementAsync("LIRR", ospite, new AgreementInput { SideASectorId = comune, SideBSectorId = _primo });

        var accordo = await AccordoAsync(ospite);
        var versoDopo = accordo.Sections.Single(s => s.Id == sezione).Direction;
        Assert.NotEqual(versoPrima, versoDopo);
        Assert.Equal(comune, accordo.Sender(versoDopo).SectorId);
    }

    [Fact]
    public async Task Il_reciproco_copiato_da_un_accordo_ospite_nasce_in_quell_accordo_ed_e_suo()
    {
        var (casa, sezione) = await SezioneAsync(_ne, _ew, "VALMA", kind: TransferFlowKind.Overflight);
        var ospite = (await _repo.ShareSectionAsync("LIRR", sezione, _ne, _su)).AgreementId;

        var reciproco = await _repo.CopySectionToReverseAsync("LIRR", sezione, ospite);

        var accordo = await AccordoAsync(ospite);
        var nata = accordo.Sections.Single(s => s.Id == reciproco);
        Assert.False(nata.IsShared);
        Assert.Equal(_su, accordo.Sender(nata.Direction).SectorId);
        Assert.Single((await AccordoAsync(casa)).Sections);
    }

    [Fact]
    public async Task Una_sezione_ospite_si_scrive_anche_dalla_ACC_dell_accordo_che_la_ospita()
    {
        // La sezione sta di casa in un accordo tutto di Roma, ed è ospite in uno con un ente di Parigi. Da Parigi
        // la si legge — sta in un suo accordo — e quindi la si deve poter scrivere: «chi la vede la può scrivere».
        var parigi = await SettoreEsteroAsync();
        var (_, sezione) = await SezioneAsync(_ne, _ew, "VALMA");
        var ospite = (await _repo.ShareSectionAsync("LIRR", sezione, _ne, parigi)).AgreementId;

        var vista = (await _repo.ListByAccAsync("LFFF")).Single(a => a.Id == ospite).Sections.Single(s => s.Id == sezione);
        Assert.Equal("VALMA", Assert.Single(vista.Clauses).Cops);

        await _repo.AddClauseAsync("LFFF", sezione, Clausola("DAPARIGI"));
        await _repo.UpdateClauseAsync("LFFF", vista.Clauses[0].Id, Clausola("VALMA2"));

        Assert.Equal(new[] { "VALMA2", "DAPARIGI" },
            (await SezioneInAsync((await _repo.FindByPairAsync("LIRR", _ne, _ew))!.Value, sezione)).Clauses.Select(c => c.Cops));
    }

    // ---- i rifiuti ----------------------------------------------------------------------------------

    [Fact]
    public async Task Una_sezione_condivisa_non_si_sposta_e_non_si_unisce()
    {
        var (casa, sezione) = await SezioneAsync(_ne, _ew, "VALMA");
        var (_, gemella) = await SezioneAsync(_ne, _ew, "BIRSU");
        await _repo.ShareSectionAsync("LIRR", sezione, _ne, _su);

        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => _repo.MoveSectionAsync("LIRR", sezione, _ne, _ts));
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => _repo.MergeSectionsAsync("LIRR", gemella, sezione));
        Assert.Equal(2, (await AccordoAsync(casa)).Sections.Count);
    }

    // ---- attrezzi -----------------------------------------------------------------------------------

    private static AgreementClauseInput Clausola(string cops) => new()
    {
        Cops = cops, LevelValue = 130, LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.AtOrBelow,
    };

    /// <summary>L'accordo fra i due enti (se non c'è) e una sezione in cui cede il primo, con quelle clausole.</summary>
    private async Task<(int Accordo, int Sezione)> SezioneAsync(int cede, int riceve, params string[] cops) =>
        await SezioneAsync(cede, riceve, cops, TransferFlowKind.Arrival);

    private Task<(int Accordo, int Sezione)> SezioneAsync(int cede, int riceve, string cop, TransferFlowKind kind) =>
        SezioneAsync(cede, riceve, new[] { cop }, kind);

    private async Task<(int Accordo, int Sezione)> SezioneAsync(int cede, int riceve, string[] cops, TransferFlowKind kind)
    {
        var accordo = await _repo.FindByPairAsync("LIRR", cede, riceve)
                      ?? await _repo.AddAgreementAsync("LIRR", new AgreementInput { SideASectorId = cede, SideBSectorId = riceve });
        var sezione = await _repo.AddSectionAsync("LIRR", accordo, new AgreementSectionInput
        {
            Kind = kind,
            Direction = cede < riceve ? AgreementDirection.AtoB : AgreementDirection.BtoA,
            Airports = kind == TransferFlowKind.Overflight ? Array.Empty<AgreementAirportInput>() : new[] { new AgreementAirportInput("LIRF") },
        });
        foreach (var c in cops) await _repo.AddClauseAsync("LIRR", sezione, Clausola(c));
        return (accordo, sezione);
    }

    /// <summary>Un settore di un'altra ACC: un accordo con lui riguarda anche lei.</summary>
    private async Task<int> SettoreEsteroAsync()
    {
        var acc = new Vipi.Domain.Entities.Acc { Code = "LFFF", Name = "Paris", IsForeign = true };
        _db.Accs.Add(acc);
        await _db.SaveChangesAsync();
        var s = new Vipi.Domain.Entities.Sector
        {
            AccId = acc.Id, Callsign = "LFFF_CTR", Name = "Paris Control", Type = SectorType.Ctr, Kind = SectorKind.Acc, IsActive = true,
        };
        _db.Sectors.Add(s);
        await _db.SaveChangesAsync();
        return s.Id;
    }

    private async Task<AgreementRow> AccordoAsync(int id) => (await _repo.ListByAccAsync("LIRR")).Single(a => a.Id == id);

    private async Task<AgreementSectionRow> SezioneInAsync(int accordo, int sezione) =>
        (await AccordoAsync(accordo)).Sections.Single(s => s.Id == sezione);

    /// <summary>Tutto l'archivio degli accordi come lo legge chi lo usa: quel che un annulla deve restituire uguale.</summary>
    private async Task<string> FotoAsync() => string.Join("\n",
        (await _repo.ListByAccAsync("LIRR")).OrderBy(a => a.Id).Select(a => $"accordo {a.Id}: " + string.Join(" | ",
            a.Sections.OrderBy(s => s.Id).Select(s =>
                $"sezione {s.Id} verso {s.Direction} ordine {s.Order} con [{string.Join(",", s.SharedWith.Select(x => x.AgreementId))}] "
                + string.Join(" · ", s.Clauses.Select(c => $"{c.Id}@{c.Order} {c.Cops}"))))));
}
