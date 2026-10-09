using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Le clausole <b>condivise</b> fra più accordi: lo stesso coordinamento vale per più coppie di enti e si scrive una
/// volta. Il caso è Trapani — gli stessi coordinamenti verso <c>LIRR_SU</c> (GAT) e verso <c>LIRR_MIL</c> (OAT).
/// Carta <c>docs/feature/2026-10-06-sezioni-condivise.md</c> §10.
///
/// <para>Qui i settori sono quelli del seed di Roma: <c>NE</c> fa la parte di chi cede sempre, <c>EW</c> e
/// <c>SU</c> quella dei due riceventi. Le regole che ogni test tiene in piedi sono tre: <b>il contenuto si distrugge
/// solo quando se ne va l'ultima presenza</b>; <b>nell'accordo di arrivo la clausola va nella sezione che dice la
/// stessa cosa</b>, che nasce solo se non c'è; <b>un gruppo di varianti viaggia intero</b>.</para>
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
    private int _primo, _secondo, _terzo;

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
        (_primo, _secondo, _terzo) = (perId[0], perId[1], perId[2]);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    // ---- condividere --------------------------------------------------------------------------------

    [Fact]
    public async Task Una_clausola_condivisa_compare_in_tutti_e_due_gli_accordi_ed_e_la_stessa()
    {
        var (casa, sezione) = await SezioneAsync(_ne, _ew, "VALMA", "BIRSU");
        var valma = await IdAsync(casa, "VALMA");

        var esito = await _repo.ShareClausesAsync("LIRR", new[] { valma }, _ne, _su);

        Assert.Equal((1, true, true), (esito.Clauses, esito.AgreementCreated, esito.SectionCreated));
        var diCasa = (await SezioneInAsync(casa, sezione)).Clauses;
        var ospite = Assert.Single((await AccordoAsync(esito.AgreementId)).Sections);
        // La STESSA clausola, con lo stesso id: non è una copia. E solo quella: BIRSU non è stata chiesta.
        var riga = Assert.Single(ospite.Clauses);
        Assert.Equal(valma, riga.Id);
        Assert.Equal((true, true), (riga.IsGuest, riga.IsShared));
        Assert.Equal(casa, Assert.Single(riga.SharedWith).AgreementId);
        // Di casa lo sa anche lei, e solo lei.
        Assert.Equal(new[] { (true, false), (false, false) }, diCasa.Select(c => (c.IsShared, c.IsGuest)));
        Assert.Equal(esito.AgreementId, Assert.Single(diCasa[0].SharedWith).AgreementId);
        // La sezione nata per ospitarla dice la stessa cosa di quella di casa.
        var origine = await SezioneInAsync(casa, sezione);
        Assert.Equal((origine.Kind, origine.AirportsLabel), (ospite.Kind, ospite.AirportsLabel));
    }

    [Fact]
    public async Task Se_la_sezione_uguale_c_e_gia_la_clausola_entra_li_e_non_ne_nasce_un_altra()
    {
        // 🔴 La richiesta del 7 ottobre: l'accordo coi militari ha già la SUA tabella «arrivi LIRF». Il primo giro
        // condivideva la sezione intera, e gliene metteva accanto una seconda uguale.
        var (casa, _) = await SezioneAsync(_ne, _ew, "VALMA");
        var (altro, sua) = await SezioneAsync(_ne, _su, "GIKIN");

        var esito = await _repo.ShareClausesAsync("LIRR", new[] { await IdAsync(casa, "VALMA") }, _ne, _su);

        Assert.Equal((altro, sua, false, false), (esito.AgreementId, esito.SectionId, esito.AgreementCreated, esito.SectionCreated));
        var tabella = Assert.Single((await AccordoAsync(altro)).Sections);
        // Le sue prima, le ospiti in coda — e il posto in tabella lo dice.
        Assert.Equal(new[] { "GIKIN", "VALMA" }, tabella.Clauses.Select(c => c.Cops));
        Assert.Equal(new[] { false, true }, tabella.Clauses.Select(c => c.IsGuest));
        Assert.True(tabella.Clauses[0].Order < tabella.Clauses[1].Order);
    }

    [Fact]
    public async Task Una_sezione_con_altri_scali_non_e_la_stessa_tabella()
    {
        var (casa, _) = await SezioneAsync(_ne, _ew, "VALMA");
        var altro = await _repo.AddAgreementAsync("LIRR", new AgreementInput { SideASectorId = _ne, SideBSectorId = _su });
        var napoli = await _repo.AddSectionAsync("LIRR", altro, new AgreementSectionInput
        {
            Kind = TransferFlowKind.Arrival, Direction = Verso(_ne, _su), Airports = new[] { new AgreementAirportInput("LIRN") },
        });

        var esito = await _repo.ShareClausesAsync("LIRR", new[] { await IdAsync(casa, "VALMA") }, _ne, _su);

        Assert.True(esito.SectionCreated);
        Assert.NotEqual(napoli, esito.SectionId);
        Assert.Equal(2, (await AccordoAsync(altro)).Sections.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Chi_cede_resta_chi_cede_anche_nell_altro_accordo(bool cedeIlComune)
    {
        // L'ente comune sta in MEZZO per id: nell'accordo di casa è a sinistra, in quello di arrivo a destra. Il
        // verso della sezione di arrivo va calcolato sui lati di QUELL'accordo, o direbbe il contrario.
        var (cede, riceve) = cedeIlComune ? (_secondo, _terzo) : (_terzo, _secondo);
        var (casa, _) = await SezioneAsync(cede, riceve, "VALMA");
        var (cedeLi, riceveLi) = cedeIlComune ? (_secondo, _primo) : (_primo, _secondo);

        var esito = await _repo.ShareClausesAsync("LIRR", new[] { await IdAsync(casa, "VALMA") }, cedeLi, riceveLi);

        var accordo = await AccordoAsync(esito.AgreementId);
        var sezione = Assert.Single(accordo.Sections);
        Assert.Equal((cedeLi, riceveLi), (accordo.Sender(sezione.Direction).SectorId, accordo.Receiver(sezione.Direction).SectorId));
    }

    [Fact]
    public async Task Condividere_dove_compare_gia_non_fa_niente_e_non_lascia_un_accordo_vuoto()
    {
        var (casa, _) = await SezioneAsync(_ne, _ew, "VALMA");
        var valma = new[] { await IdAsync(casa, "VALMA") };
        await _repo.ShareClausesAsync("LIRR", valma, _ne, _su);
        var prima = await FotoAsync();

        var diNuovo = await _repo.ShareClausesAsync("LIRR", valma, _ne, _su);
        // La stessa coppia di casa: lì la clausola c'è già. E un accordo nato per niente non resta lì.
        var aCasa = await _repo.ShareClausesAsync("LIRR", valma, _ne, _ew);
        var nelVersoOpposto = await _repo.ShareClausesAsync("LIRR", valma, _su, _ne);

        Assert.All(new[] { diNuovo, aCasa, nelVersoOpposto }, e => Assert.Equal((false, 0, false), (e.Added, e.Clauses, e.AgreementCreated)));
        Assert.Equal(prima, await FotoAsync());
    }

    [Fact]
    public async Task Condividere_la_sezione_condivide_tutte_le_clausole_che_mostra()
    {
        var (casa, sezione) = await SezioneAsync(_ne, _ew, "VALMA", "BIRSU");

        var esito = await _repo.ShareSectionAsync("LIRR", sezione, _ne, _su);

        Assert.Equal(2, esito.Clauses);
        Assert.Equal((await SezioneInAsync(casa, sezione)).Clauses.Select(c => c.Id),
            Assert.Single((await AccordoAsync(esito.AgreementId)).Sections).Clauses.Select(c => c.Id));
    }

    [Fact]
    public async Task Una_clausola_corretta_da_un_accordo_si_legge_corretta_anche_nell_altro()
    {
        var (casa, sezione) = await SezioneAsync(_ne, _ew, "VALMA");
        var valma = await IdAsync(casa, "VALMA");
        var altro = (await _repo.ShareClausesAsync("LIRR", new[] { valma }, _ne, _su)).AgreementId;

        await _repo.UpdateClauseAsync("LIRR", valma, Clausola("VALMA2"));

        Assert.Equal("VALMA2", Assert.Single((await SezioneInAsync(casa, sezione)).Clauses).Cops);
        Assert.Equal("VALMA2", Assert.Single(Assert.Single((await AccordoAsync(altro)).Sections).Clauses).Cops);
    }

    // ---- i gruppi di varianti viaggiano interi ------------------------------------------------------

    [Fact]
    public async Task Un_gruppo_di_varianti_si_condivide_intero()
    {
        var (casa, _) = await SezioneAsync(_ne, _ew, "VALMA", "BIRSU");
        var alternativa = await _repo.AddAlternativeAsync("LIRR", await IdAsync(casa, "VALMA"));

        // Si sceglie UNA variante: arriva il gruppo.
        var esito = await _repo.ShareClausesAsync("LIRR", new[] { alternativa }, _ne, _su);

        Assert.Equal(2, esito.Clauses);
        var righe = Assert.Single((await AccordoAsync(esito.AgreementId)).Sections).Clauses;
        Assert.Equal(new[] { "VALMA", "VALMA" }, righe.Select(c => c.Cops));
        Assert.NotNull(righe[0].VariantGroup);
        Assert.Equal(righe[0].VariantGroup, righe[1].VariantGroup);
    }

    [Fact]
    public async Task Il_gruppo_condiviso_non_si_confonde_con_un_gruppo_dell_accordo_di_arrivo()
    {
        // I gruppi nati prima del 7 ottobre erano progressivi PER ACCORDO: due accordi hanno entrambi un gruppo «1».
        // Portato tale e quale nella tabella dell'altro, il gruppo ospite diventerebbe variante del suo.
        var (casa, _) = await SezioneAsync(_ne, _ew, "VALMA");
        await _repo.AddAlternativeAsync("LIRR", await IdAsync(casa, "VALMA"));
        var (altro, _) = await SezioneAsync(_ne, _su, "GIKIN");
        await _repo.AddAlternativeAsync("LIRR", await IdAsync(altro, "GIKIN"));
        foreach (var c in await _db.AgreementClauses.ToListAsync()) c.VariantGroup = 1;
        await _db.SaveChangesAsync();

        await _repo.ShareClausesAsync("LIRR", new[] { await IdAsync(casa, "VALMA") }, _ne, _su);

        var righe = Assert.Single((await AccordoAsync(altro)).Sections).Clauses;
        Assert.Equal(4, righe.Count);
        Assert.Equal(2, righe.Select(c => c.VariantGroup).Distinct().Count());
        Assert.All(righe.GroupBy(c => c.VariantGroup), g => Assert.Single(g.Select(c => c.Cops).Distinct()));
    }

    [Fact]
    public async Task Una_variante_aggiunta_a_una_clausola_condivisa_compare_anche_nell_altro_accordo()
    {
        var (casa, _) = await SezioneAsync(_ne, _ew, "VALMA");
        var valma = await IdAsync(casa, "VALMA");
        var altro = (await _repo.ShareClausesAsync("LIRR", new[] { valma }, _ne, _su)).AgreementId;

        var alternativa = await _repo.AddAlternativeAsync("LIRR", valma);
        var eccezione = await _repo.AddExceptionAsync("LIRR", valma);

        // Varianti ed eccezioni sono contenuto del gruppo: lasciate a casa, l'altro accordo ne mostrerebbe un pezzo.
        var righe = Assert.Single((await AccordoAsync(altro)).Sections).Clauses;
        Assert.Equal(new[] { valma, eccezione, alternativa }, righe.Select(c => c.Id));
        Assert.Equal(new[] { 0, 1, 0 }, righe.Select(c => c.VariantDepth));
        Assert.Single(righe.Select(c => c.VariantGroup).Distinct());
    }

    [Fact]
    public async Task Una_variante_sola_di_un_gruppo_condiviso_se_ne_va_per_tutti()
    {
        var (casa, sezione) = await SezioneAsync(_ne, _ew, "VALMA");
        var valma = await IdAsync(casa, "VALMA");
        var alternativa = await _repo.AddAlternativeAsync("LIRR", valma);
        var altro = (await _repo.ShareClausesAsync("LIRR", new[] { valma }, _ne, _su)).AgreementId;

        // Guardando dall'accordo ospite, e dicendolo: la struttura di un gruppo è contenuto, non una presenza.
        await _repo.DeleteClausesAsync("LIRR", new[] { alternativa }, altro);

        foreach (var righe in new[] { (await SezioneInAsync(casa, sezione)).Clauses, Assert.Single((await AccordoAsync(altro)).Sections).Clauses })
        {
            var rimasta = Assert.Single(righe);
            Assert.Equal((valma, (int?)null), (rimasta.Id, rimasta.VariantGroup));   // un gruppo di una non è un gruppo
        }
    }

    [Fact]
    public async Task Il_gruppo_intero_tolto_da_un_accordo_resta_nell_altro_e_arriva_intero()
    {
        var (casa, sezione) = await SezioneAsync(_ne, _ew, "VALMA", "BIRSU");
        var valma = await IdAsync(casa, "VALMA");
        var eccezione = await _repo.AddExceptionAsync("LIRR", valma);
        var altro = (await _repo.ShareClausesAsync("LIRR", new[] { valma }, _ne, _su)).AgreementId;

        // Dall'accordo di CASA: la casa del gruppo passa all'altro, tutta insieme.
        await _repo.DeleteClausesAsync("LIRR", new[] { valma, eccezione }, casa);

        Assert.Equal("BIRSU", Assert.Single((await SezioneInAsync(casa, sezione)).Clauses).Cops);
        var righe = Assert.Single((await AccordoAsync(altro)).Sections).Clauses;
        Assert.Equal(new[] { valma, eccezione }, righe.Select(c => c.Id));
        Assert.Equal(new[] { 0, 1 }, righe.Select(c => c.VariantDepth));
        Assert.NotNull(righe[0].VariantGroup);
        Assert.All(righe, c => Assert.Equal((false, false), (c.IsGuest, c.IsShared)));
    }

    // ---- togliere -----------------------------------------------------------------------------------

    [Fact]
    public async Task Tolta_dall_accordo_ospite_resta_in_quello_di_casa_e_l_annulla_la_rimette()
    {
        var (casa, sezione) = await SezioneAsync(_ne, _ew, "VALMA", "BIRSU");
        var valma = await IdAsync(casa, "VALMA");
        var altro = (await _repo.ShareClausesAsync("LIRR", new[] { valma }, _ne, _su)).AgreementId;
        var prima = await FotoAsync();
        var ospite = Assert.Single((await AccordoAsync(altro)).Sections);
        var foto = new[] { new AgreementClauseRestore(ospite.Id, Foto(ospite.Clauses[0])) };

        var tolte = await _repo.DeleteClausesAsync("LIRR", new[] { valma }, altro);

        Assert.Equal(1, tolte);
        Assert.Empty(Assert.Single((await AccordoAsync(altro)).Sections).Clauses);
        Assert.Equal(new[] { (valma, false) }, (await SezioneInAsync(casa, sezione)).Clauses.Take(1).Select(c => (c.Id, c.IsShared)));

        await _repo.RestoreClausesAsync("LIRR", foto);
        Assert.Equal(prima, await FotoAsync());
    }

    [Fact]
    public async Task Tolta_dall_accordo_di_casa_la_casa_passa_all_ospite_e_l_annulla_la_riporta_dov_era()
    {
        var (casa, sezione) = await SezioneAsync(_ne, _ew, "VALMA", "BIRSU");
        var valma = await IdAsync(casa, "VALMA");
        var altro = (await _repo.ShareClausesAsync("LIRR", new[] { valma }, _ne, _su)).AgreementId;
        var prima = await FotoAsync();
        var foto = new[] { new AgreementClauseRestore(sezione, Foto((await SezioneInAsync(casa, sezione)).Clauses[0])) };

        await _repo.DeleteClausesAsync("LIRR", new[] { valma }, casa);

        // Il contenuto non è andato da nessuna parte: stesso id, ora di casa nell'altro accordo e non più condiviso.
        Assert.Equal("BIRSU", Assert.Single((await SezioneInAsync(casa, sezione)).Clauses).Cops);
        var rimasta = Assert.Single(Assert.Single((await AccordoAsync(altro)).Sections).Clauses);
        Assert.Equal((valma, false, false), (rimasta.Id, rimasta.IsGuest, rimasta.IsShared));

        // ⚠️ L'annulla è uno stato, non «ricondividi»: torna di casa dov'era, PRIMA di BIRSU, e l'altro la ospita.
        await _repo.RestoreClausesAsync("LIRR", foto);
        Assert.Equal(prima, await FotoAsync());
    }

    [Fact]
    public async Task Senza_dire_da_dove_la_si_guarda_se_ne_va_ovunque_e_l_annulla_la_rimette_condivisa()
    {
        var (casa, sezione) = await SezioneAsync(_ne, _ew, "VALMA");
        var valma = await IdAsync(casa, "VALMA");
        var altro = (await _repo.ShareClausesAsync("LIRR", new[] { valma }, _ne, _su)).AgreementId;
        var foto = new[] { new AgreementClauseRestore(sezione, Foto((await SezioneInAsync(casa, sezione)).Clauses[0])) };

        await _repo.DeleteClausesAsync("LIRR", new[] { valma });

        Assert.Empty((await SezioneInAsync(casa, sezione)).Clauses);
        Assert.Empty(Assert.Single((await AccordoAsync(altro)).Sections).Clauses);

        // Il contenuto non c'è più da nessuna parte: torna dalla fotografia, di casa dov'era e ospite dov'era ospite.
        await _repo.RestoreClausesAsync("LIRR", foto);
        var diCasa = Assert.Single((await SezioneInAsync(casa, sezione)).Clauses);
        var ospite = Assert.Single(Assert.Single((await AccordoAsync(altro)).Sections).Clauses);
        Assert.Equal((diCasa.Id, "VALMA", true), (ospite.Id, ospite.Cops, ospite.IsGuest));
        Assert.False(diCasa.IsGuest);
    }

    [Fact]
    public async Task Eliminare_la_sezione_di_casa_non_porta_via_le_clausole_che_altri_mostrano()
    {
        var (casa, sezione) = await SezioneAsync(_ne, _ew, "VALMA", "BIRSU");
        var valma = await IdAsync(casa, "VALMA");
        var altro = (await _repo.ShareClausesAsync("LIRR", new[] { valma }, _ne, _su)).AgreementId;
        var prima = await SezioneInAsync(casa, sezione);
        var foto = new AgreementSectionRestore(casa, Foto(prima));

        await _repo.DeleteSectionAsync("LIRR", sezione);

        Assert.Empty((await AccordoAsync(casa)).Sections);
        var rimasta = Assert.Single(Assert.Single((await AccordoAsync(altro)).Sections).Clauses);
        Assert.Equal((valma, false, false), (rimasta.Id, rimasta.IsGuest, rimasta.IsShared));

        // L'annulla rimette la sezione: VALMA torna come la STESSA clausola (di casa qui, ospite là), BIRSU dal contenuto.
        var rimessa = await _repo.RestoreSectionAsync("LIRR", foto);
        var tornate = (await SezioneInAsync(casa, rimessa!.Value)).Clauses;
        Assert.Equal(new[] { "VALMA", "BIRSU" }, tornate.Select(c => c.Cops));
        Assert.Equal((valma, true, false), (tornate[0].Id, tornate[0].IsShared, tornate[0].IsGuest));
        Assert.True(Assert.Single(Assert.Single((await AccordoAsync(altro)).Sections).Clauses).IsGuest);
    }

    [Fact]
    public async Task Eliminare_la_sezione_che_la_ospita_la_lascia_a_casa_non_piu_condivisa()
    {
        var (casa, sezione) = await SezioneAsync(_ne, _ew, "VALMA");
        var esito = await _repo.ShareClausesAsync("LIRR", new[] { await IdAsync(casa, "VALMA") }, _ne, _su);

        await _repo.DeleteSectionAsync("LIRR", esito.SectionId!.Value);

        Assert.False(Assert.Single((await SezioneInAsync(casa, sezione)).Clauses).IsShared);
        Assert.Empty(await _db.AgreementClauseShares.ToListAsync());
    }

    [Fact]
    public async Task Eliminare_l_accordo_di_casa_non_porta_via_le_clausole_e_rimesso_le_riprende_come_presenze()
    {
        var (casa, _) = await SezioneAsync(_ne, _ew, "VALMA", "BIRSU");
        var valma = await IdAsync(casa, "VALMA");
        var altro = (await _repo.ShareClausesAsync("LIRR", new[] { valma }, _ne, _su)).AgreementId;
        var prima = await AccordoAsync(casa);
        var foto = new AgreementSnapshot(
            new AgreementInput { SideASectorId = prima.SideA.SectorId, SideBSectorId = prima.SideB.SectorId },
            prima.Sections.Select(Foto).ToList());

        await _repo.DeleteAgreementAsync("LIRR", casa);

        var rimasta = Assert.Single(Assert.Single((await AccordoAsync(altro)).Sections).Clauses);
        Assert.Equal((valma, false), (rimasta.Id, rimasta.IsShared));

        var rimesso = await _repo.RestoreAgreementAsync("LIRR", foto);
        var tornate = Assert.Single((await AccordoAsync(rimesso)).Sections).Clauses;
        Assert.Equal(new[] { "VALMA", "BIRSU" }, tornate.Select(c => c.Cops));
        // Non una copia: la stessa clausola, di nuovo in tutti e due.
        Assert.Equal(valma, tornate[0].Id);
        Assert.Equal(valma, Assert.Single(Assert.Single((await AccordoAsync(altro)).Sections).Clauses).Id);
    }

    [Fact]
    public async Task Annullare_la_condivisione_toglie_presenze_sezione_e_accordo_nati_per_ospitarla()
    {
        var (casa, _) = await SezioneAsync(_ne, _ew, "VALMA", "BIRSU");
        var prima = await FotoAsync();

        var esito = await _repo.ShareClausesAsync("LIRR", new[] { await IdAsync(casa, "VALMA") }, _ne, _su);
        await _repo.UndoShareAsync("LIRR", esito.Undo);

        Assert.Equal(prima, await FotoAsync());
        Assert.Null(await _repo.FindByPairAsync("LIRR", _ne, _su));
    }

    [Fact]
    public async Task Annullare_la_condivisione_non_tocca_la_sezione_che_c_era_gia_ne_quel_che_ci_e_stato_scritto()
    {
        var (casa, _) = await SezioneAsync(_ne, _ew, "VALMA");
        var (altro, sua) = await SezioneAsync(_ne, _su);
        var esito = await _repo.ShareClausesAsync("LIRR", new[] { await IdAsync(casa, "VALMA") }, _ne, _su);
        // Nel frattempo qualcuno ha condiviso ANCHE verso TS, e la sezione nata là ha ricevuto una clausola sua.
        var versoTs = await _repo.ShareClausesAsync("LIRR", new[] { await IdAsync(casa, "VALMA") }, _ne, _ts);
        await _repo.AddClauseAsync("LIRR", versoTs.SectionId!.Value, Clausola("SOLOTS"));

        await _repo.UndoShareAsync("LIRR", esito.Undo);
        await _repo.UndoShareAsync("LIRR", versoTs.Undo);

        // La sezione che c'era resta, vuota com'era; quella nata per ospitare resta perché non è più vuota.
        Assert.Empty(Assert.Single((await AccordoAsync(altro)).Sections, s => s.Id == sua).Clauses);
        Assert.Equal("SOLOTS", Assert.Single(Assert.Single((await AccordoAsync(versoTs.AgreementId)).Sections).Clauses).Cops);
    }

    // ---- staccare -----------------------------------------------------------------------------------

    [Fact]
    public async Task Staccata_diventa_una_copia_indipendente_e_l_altra_resta_com_era()
    {
        var (casa, sezione) = await SezioneAsync(_ne, _ew, "VALMA");
        var valma = await IdAsync(casa, "VALMA");
        var altro = (await _repo.ShareClausesAsync("LIRR", new[] { valma }, _ne, _su)).AgreementId;

        var esito = await _repo.DetachClausesAsync("LIRR", new[] { valma }, altro);

        Assert.Equal(1, esito.Clauses);
        var copia = Assert.Single(Assert.Single((await AccordoAsync(altro)).Sections).Clauses);
        Assert.NotEqual(valma, copia.Id);
        Assert.Equal(("VALMA", false, false), (copia.Cops, copia.IsShared, copia.IsGuest));
        // Da qui in poi dicono cose diverse senza toccarsi.
        await _repo.UpdateClauseAsync("LIRR", copia.Id, Clausola("SOLOMIL"));
        var originale = Assert.Single((await SezioneInAsync(casa, sezione)).Clauses);
        Assert.Equal((valma, "VALMA", false), (originale.Id, originale.Cops, originale.IsShared));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Annullare_lo_stacco_toglie_la_copia_e_rimette_la_condivisione_com_era(bool dallAccordoDiCasa)
    {
        var (casa, _) = await SezioneAsync(_ne, _ew, "VALMA", "BIRSU");
        var valma = await IdAsync(casa, "VALMA");
        var altro = (await _repo.ShareClausesAsync("LIRR", new[] { valma }, _ne, _su)).AgreementId;
        var prima = await FotoAsync();

        var esito = await _repo.DetachClausesAsync("LIRR", new[] { valma }, dallAccordoDiCasa ? casa : altro);
        Assert.NotEqual(prima, await FotoAsync());
        await _repo.UndoDetachAsync("LIRR", esito.Undo);

        Assert.Equal(prima, await FotoAsync());
    }

    [Fact]
    public async Task Staccare_dall_accordo_di_casa_lascia_l_originale_all_altro_e_la_copia_al_suo_posto()
    {
        var (casa, sezione) = await SezioneAsync(_ne, _ew, "VALMA", "BIRSU");
        var valma = await IdAsync(casa, "VALMA");
        var altro = (await _repo.ShareClausesAsync("LIRR", new[] { valma }, _ne, _su)).AgreementId;

        await _repo.DetachClausesAsync("LIRR", new[] { valma }, casa);

        var diCasa = (await SezioneInAsync(casa, sezione)).Clauses;
        Assert.Equal(new[] { "VALMA", "BIRSU" }, diCasa.Select(c => c.Cops));   // la copia sta dove stava l'originale
        Assert.NotEqual(valma, diCasa[0].Id);
        var rimasta = Assert.Single(Assert.Single((await AccordoAsync(altro)).Sections).Clauses);
        Assert.Equal((valma, false, false), (rimasta.Id, rimasta.IsGuest, rimasta.IsShared));
    }

    [Fact]
    public async Task Una_clausola_che_non_e_condivisa_non_si_stacca()
    {
        var (casa, _) = await SezioneAsync(_ne, _ew, "VALMA");
        var valma = await IdAsync(casa, "VALMA");

        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => _repo.DetachClausesAsync("LIRR", new[] { valma }, casa));
    }

    // ---- le altre porte -----------------------------------------------------------------------------

    [Fact]
    public async Task Una_clausola_ospite_si_scrive_anche_dalla_ACC_dell_accordo_che_la_ospita()
    {
        // La clausola sta di casa in un accordo tutto di Roma, ed è ospite in uno con un ente di Parigi. Da Parigi
        // la si legge — sta in un suo accordo — e quindi la si deve poter scrivere: «chi la vede la può scrivere».
        var parigi = await SettoreEsteroAsync();
        var (casa, sezione) = await SezioneAsync(_ne, _ew, "VALMA");
        var valma = await IdAsync(casa, "VALMA");
        var ospite = (await _repo.ShareClausesAsync("LIRR", new[] { valma }, _ne, parigi)).AgreementId;

        var vista = Assert.Single((await _repo.ListByAccAsync("LFFF")).Single(a => a.Id == ospite).Sections);
        Assert.Equal("VALMA", Assert.Single(vista.Clauses).Cops);

        await _repo.UpdateClauseAsync("LFFF", valma, Clausola("VALMA2"));

        Assert.Equal("VALMA2", Assert.Single((await SezioneInAsync(casa, sezione)).Clauses).Cops);
    }

    [Fact]
    public async Task Una_clausola_condivisa_non_si_sposta_e_la_sua_sezione_nemmeno()
    {
        var (casa, sezione) = await SezioneAsync(_ne, _ew, "VALMA");
        var valma = await IdAsync(casa, "VALMA");
        var esito = await _repo.ShareClausesAsync("LIRR", new[] { valma }, _ne, _su);
        var prima = await FotoAsync();

        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => _repo.MoveClausesAsync("LIRR", new[] { valma }, _ne, _ts));
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => _repo.MoveSectionAsync("LIRR", sezione, _ne, _ts));
        // Nemmeno la sezione che la OSPITA: porterebbe la presenza in un terzo accordo senza che nessuno l'abbia chiesto.
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => _repo.MoveSectionAsync("LIRR", esito.SectionId!.Value, _ne, _ts));

        // E il rifiuto non lascia in giro l'accordo di arrivo.
        Assert.Equal(prima, await FotoAsync());
        Assert.Null(await _repo.FindByPairAsync("LIRR", _ne, _ts));
    }

    [Fact]
    public async Task Unire_due_gemelle_porta_con_se_le_clausole_che_l_assorbita_ospitava()
    {
        var (casa, _) = await SezioneAsync(_ne, _ew, "VALMA");
        var valma = await IdAsync(casa, "VALMA");
        var (altro, prima) = await SezioneAsync(_ne, _su, "GIKIN");
        var (_, seconda) = await SezioneAsync(_ne, _su, "ELKAP");
        Assert.Equal(prima, (await _repo.ShareClausesAsync("LIRR", new[] { valma }, _ne, _su)).SectionId);

        await _repo.MergeSectionsAsync("LIRR", seconda, prima);

        var tabella = Assert.Single((await AccordoAsync(altro)).Sections);
        Assert.Equal(new[] { "ELKAP", "GIKIN", "VALMA" }, tabella.Clauses.Select(c => c.Cops));
        Assert.Equal((valma, true), (tabella.Clauses[2].Id, tabella.Clauses[2].IsGuest));
    }

    [Fact]
    public async Task Il_reciproco_copia_anche_le_clausole_ospiti_come_clausole_sue()
    {
        var (casa, _) = await SezioneAsync(_ne, _ew, "VALMA");
        var valma = await IdAsync(casa, "VALMA");
        var (altro, sua) = await SezioneAsync(_ne, _su, "GIKIN");
        await _repo.ShareClausesAsync("LIRR", new[] { valma }, _ne, _su);

        var reciproco = await _repo.CopySectionToReverseAsync("LIRR", sua);

        var copiate = (await SezioneInAsync(altro, reciproco!.Value)).Clauses;
        Assert.Equal(new[] { "GIKIN", "VALMA" }, copiate.Select(c => c.Cops));
        Assert.All(copiate, c => Assert.Equal((false, false), (c.IsShared, c.IsGuest)));
        Assert.DoesNotContain(valma, copiate.Select(c => c.Id));
    }

    // ---- l'ordine dichiarato della sezione ----------------------------------------------------------

    [Fact]
    public async Task Con_l_ordine_alfabetico_la_clausola_ospite_va_al_suo_posto_e_non_in_coda()
    {
        // La richiesta del 7 ottobre: le ospiti stavano in coda e non si potevano mettere fra le clausole di casa.
        var (casa, _) = await SezioneAsync(_ne, _ew, "MEDIO");
        var (altro, sua) = await SezioneAsync(_ne, _su, "ZETAS", "ALFAS");
        await _repo.ShareClausesAsync("LIRR", new[] { await IdAsync(casa, "MEDIO") }, _ne, _su);
        Assert.Equal(new[] { "ZETAS", "ALFAS", "MEDIO" }, (await SezioneInAsync(altro, sua)).Clauses.Select(c => c.Cops));

        await OrdineAsync(altro, sua, AgreementClauseOrder.Points);

        var ordinata = await SezioneInAsync(altro, sua);
        Assert.Equal(AgreementClauseOrder.Points, ordinata.ClauseOrder);
        Assert.Equal(new[] { "ALFAS", "MEDIO", "ZETAS" }, ordinata.Clauses.Select(c => c.Cops));
        Assert.Equal(new[] { false, true, false }, ordinata.Clauses.Select(c => c.IsGuest));
        // Così la leggono anche i documenti e la vista live: le righe piatte escono nello stesso ordine.
        var flusso = AgreementExpansion.Expand(await _repo.ListByAccAsync("LIRR")).Single(f => f.Points.Any(p => p.Cop == "ZETAS"));
        Assert.Equal(new[] { "ALFAS", "MEDIO", "ZETAS" }, flusso.Points.Select(p => p.Cop));
    }

    [Fact]
    public async Task Il_verso_opposto_si_salva_si_rilegge_e_arriva_a_documenti_e_vista_live()
    {
        // Committente, 9 ottobre 2026: ogni ordine si deve poter leggere al contrario. Il verso è SALVATO (per
        // nome, nella colonna di testo che c'era già: nessuna migrazione) e vale dove vale l'ordine.
        var (casa, sezione) = await SezioneAsync(_ne, _ew, "MEDIO", "ZETAS", "ALFAS");

        await OrdineAsync(casa, sezione, AgreementClauseOrder.PointsDescending);

        var ordinata = await SezioneInAsync(casa, sezione);
        Assert.Equal(AgreementClauseOrder.PointsDescending, ordinata.ClauseOrder);
        Assert.Equal(new[] { "ZETAS", "MEDIO", "ALFAS" }, ordinata.Clauses.Select(c => c.Cops));
        var flusso = AgreementExpansion.Expand(await _repo.ListByAccAsync("LIRR")).Single(f => f.Points.Any(p => p.Cop == "ZETAS"));
        Assert.Equal(new[] { "ZETAS", "MEDIO", "ALFAS" }, flusso.Points.Select(p => p.Cop));

        // …e tornando «a mano» il posto scritto c'è ancora.
        await OrdineAsync(casa, sezione, AgreementClauseOrder.Manual);
        Assert.Equal(new[] { "MEDIO", "ZETAS", "ALFAS" }, (await SezioneInAsync(casa, sezione)).Clauses.Select(c => c.Cops));
    }

    [Fact]
    public async Task Tornando_a_mano_le_clausole_ritrovano_il_posto_che_avevano()
    {
        var (casa, sezione) = await SezioneAsync(_ne, _ew, "ZETAS", "ALFAS", "MEDIO");

        await OrdineAsync(casa, sezione, AgreementClauseOrder.Points);
        await OrdineAsync(casa, sezione, AgreementClauseOrder.Manual);

        Assert.Equal(new[] { "ZETAS", "ALFAS", "MEDIO" }, (await SezioneInAsync(casa, sezione)).Clauses.Select(c => c.Cops));
    }

    [Fact]
    public async Task L_annulla_di_un_eliminazione_in_una_sezione_ordinata_rimette_la_clausola_al_posto_salvato()
    {
        // La fotografia porta il posto SALVATO, non quello a schermo: col secondo, tornando «a mano» la clausola
        // rimessa finirebbe dove non era.
        var (casa, sezione) = await SezioneAsync(_ne, _ew, "ZETAS", "ALFAS", "MEDIO");
        await OrdineAsync(casa, sezione, AgreementClauseOrder.Points);
        var zetas = (await SezioneInAsync(casa, sezione)).Clauses.Single(c => c.Cops == "ZETAS");
        Assert.Equal((3, 1), (zetas.Order, zetas.StoredOrder));

        await _repo.DeleteClausesAsync("LIRR", new[] { zetas.Id }, casa);
        await _repo.RestoreClausesAsync("LIRR", new[] { new AgreementClauseRestore(sezione, Foto(zetas)) });
        await OrdineAsync(casa, sezione, AgreementClauseOrder.Manual);

        Assert.Equal(new[] { "ZETAS", "ALFAS", "MEDIO" }, (await SezioneInAsync(casa, sezione)).Clauses.Select(c => c.Cops));
    }

    [Fact]
    public async Task La_sezione_nata_per_ospitare_si_legge_nello_stesso_ordine_di_quella_di_casa()
    {
        var (casa, sezione) = await SezioneAsync(_ne, _ew, "ZETAS", "ALFAS");
        await OrdineAsync(casa, sezione, AgreementClauseOrder.Points);

        var esito = await _repo.ShareSectionAsync("LIRR", sezione, _ne, _su);

        var nata = Assert.Single((await AccordoAsync(esito.AgreementId)).Sections);
        Assert.Equal((AgreementClauseOrder.Points, "ALFAS"), (nata.ClauseOrder, nata.Clauses[0].Cops));
    }

    // ---- l'accordo intero ---------------------------------------------------------------------------

    [Fact]
    public async Task Condividere_l_accordo_intero_porta_ogni_sezione_nell_altro_cambiando_un_ente_solo()
    {
        // Trapani: «tutto quel che vale con SU vale anche con MIL». Due sezioni, nei due versi.
        var (casa, arrivi) = await SezioneAsync(_ne, _ew, "VALMA", "BIRSU");
        var sorvoli = await _repo.AddSectionAsync("LIRR", casa, new AgreementSectionInput
        {
            Kind = TransferFlowKind.Overflight, Direction = Verso(_ew, _ne),   // qui cede l'ALTRO capo
        });
        await _repo.AddClauseAsync("LIRR", sorvoli, Clausola("GIKIN"));
        var prima = await FotoAsync();

        var esito = await _repo.ShareAgreementAsync("LIRR", casa, insteadOfSectorId: _ew, withSectorId: _su);

        Assert.Equal((3, true), (esito.Clauses, esito.AgreementCreated));
        var altro = await AccordoAsync(esito.AgreementId);
        Assert.Equal(2, altro.Sections.Count);
        // Chi cede resta chi cede, sezione per sezione: negli arrivi NE, nei sorvoli l'ente che ha preso il posto di EW.
        var arriviLi = altro.Sections.Single(s => s.Kind == TransferFlowKind.Arrival);
        var sorvoliLi = altro.Sections.Single(s => s.Kind == TransferFlowKind.Overflight);
        Assert.Equal((_ne, _su), (altro.Sender(arriviLi.Direction).SectorId, altro.Receiver(arriviLi.Direction).SectorId));
        Assert.Equal((_su, _ne), (altro.Sender(sorvoliLi.Direction).SectorId, altro.Receiver(sorvoliLi.Direction).SectorId));
        Assert.Equal((await AccordoAsync(casa)).AllClauses.Select(c => c.Id).OrderBy(x => x), altro.AllClauses.Select(c => c.Id).OrderBy(x => x));

        // Un annulla solo, per tutto il gesto.
        await _repo.UndoShareAsync("LIRR", esito.Undo);
        Assert.Equal(prima, await FotoAsync());
        Assert.Null(await _repo.FindByPairAsync("LIRR", _ne, _su));
        _ = arrivi;
    }

    [Fact]
    public async Task L_accordo_intero_si_condivide_solo_cambiando_uno_dei_suoi_due_enti_con_un_terzo()
    {
        var (casa, _) = await SezioneAsync(_ne, _ew, "VALMA");

        // L'ente da sostituire non è dell'accordo; il sostituto è già uno dei due.
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => _repo.ShareAgreementAsync("LIRR", casa, _ts, _su));
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => _repo.ShareAgreementAsync("LIRR", casa, _ew, _ne));
        Assert.Null(await _repo.FindByPairAsync("LIRR", _ne, _su));
    }

    // ---- attrezzi -----------------------------------------------------------------------------------

    /// <summary>Cambia il solo ordine delle clausole della sezione, lasciando il resto com'è — come fa la pagina.</summary>
    private async Task OrdineAsync(int accordo, int sezione, AgreementClauseOrder ordine)
    {
        var s = await SezioneInAsync(accordo, sezione);
        await _repo.UpdateSectionAsync("LIRR", sezione, new AgreementSectionInput
        {
            Kind = s.Kind, Direction = s.Direction, Description = s.Description, ClauseOrder = ordine,
            Airports = s.Airports.Select(a => new AgreementAirportInput(a.Icao, a.Name)).ToList(),
        });
    }


    private static AgreementClauseInput Clausola(string cops) => new()
    {
        Cops = cops, LevelValue = 130, LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.AtOrBelow,
    };

    private static AgreementDirection Verso(int cede, int riceve) =>
        cede < riceve ? AgreementDirection.AtoB : AgreementDirection.BtoA;

    /// <summary>L'accordo fra i due enti (se non c'è) e una sezione «arrivi LIRF» in cui cede il primo, con quelle clausole.</summary>
    private async Task<(int Accordo, int Sezione)> SezioneAsync(int cede, int riceve, params string[] cops)
    {
        var accordo = await _repo.FindByPairAsync("LIRR", cede, riceve)
                      ?? await _repo.AddAgreementAsync("LIRR", new AgreementInput { SideASectorId = cede, SideBSectorId = riceve });
        var sezione = await _repo.AddSectionAsync("LIRR", accordo, new AgreementSectionInput
        {
            Kind = TransferFlowKind.Arrival,
            Direction = Verso(cede, riceve),
            Airports = new[] { new AgreementAirportInput("LIRF") },
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

    /// <summary>L'id della prima clausola dell'accordo con quei punti.</summary>
    private async Task<int> IdAsync(int accordo, string cops) =>
        (await AccordoAsync(accordo)).AllClauses.First(c => c.Cops == cops).Id;

    /// <summary>La fotografia che la pagina scatta prima di eliminare: i dati, il posto, e — per una clausola
    /// condivisa — dov'era.</summary>
    private static AgreementClauseSnapshot Foto(AgreementClauseRow c) =>
        new AgreementClauseSnapshot(
            new AgreementClauseInput
            {
                Cops = c.Cops, LevelValue = c.LevelValue, LevelUnit = c.LevelUnit, LevelConstraint = c.LevelConstraint,
                IsGroupWide = c.IsGroupWide,
            },
            c.StoredOrder ?? c.Order, c.VariantGroup, c.VariantDepth).ConLePresenzeDi(c);

    private static AgreementSectionSnapshot Foto(AgreementSectionRow s) => new(
        new AgreementSectionInput
        {
            Kind = s.Kind, Direction = s.Direction, Description = s.Description, ClauseOrder = s.ClauseOrder,
            Airports = s.Airports.Select(a => new AgreementAirportInput(a.Icao, a.Name)).ToList(),
        },
        s.Order, s.Clauses.Select(Foto).ToList());

    /// <summary>Tutto l'archivio degli accordi come lo legge chi lo usa: quel che un annulla deve restituire uguale.</summary>
    private async Task<string> FotoAsync() => string.Join("\n",
        (await _repo.ListByAccAsync("LIRR")).OrderBy(a => a.Id).Select(a => $"accordo {a.Id}: " + string.Join(" | ",
            a.Sections.OrderBy(s => s.Id).Select(s =>
                $"sezione {s.Id} verso {s.Direction} ordine {s.Order}: "
                + string.Join(" · ", s.Clauses.Select(c =>
                    $"{c.Id}@{c.Order} {c.Cops} g{c.VariantGroup}/{c.VariantDepth}{(c.IsGuest ? " ospite" : "")}"
                    + $" con[{string.Join(",", c.SharedWith.Select(x => x.SectionId).OrderBy(x => x))}]"))))));
}
