using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain;
using Vipi.Application.Content;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Occultamento settore in /services/vsop/admin/acc: contesto gerarchico per la Regola 1 (blocco radice con
/// figli visibili) e <b>reverse-lookup</b> settore → documenti per la Regola 3.
///
/// <para>⚠️ Il lookup è stato riscritto il 25 agosto 2026 (carta «documenti da rivedere», slice 0): prima
/// prendeva ogni documento primario e ogni APP dell'ACC — nascondere <c>LIRF_GND</c> segnalava mezza Italia —
/// e non guardava affatto <c>Airport.DocumentId</c>. I casi qui sotto fissano la regola nuova: si segnala per
/// <b>legame dimostrabile</b>, e i due difetti hanno un test ciascuno perché non tornino.</para>
/// </summary>
public class DocumentImpactLookupTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    // Documenti della scena, per Id (riempiti in InitializeAsync).
    private int _accDoc, _appPisaDoc, _scaloPisaDoc, _appGrottaglieDoc, _scaloGrottaglieDoc, _scaloMilanoDoc, _vloaDoc;

    // Le due edizioni militari della scena: Pisa è un campo MISTO (civile + militare, ed è fra i quindici
    // SOP veri), Rivolto è SOLO militare — nessun documento civile a cui appoggiarsi.
    private int _milPisaDoc, _milRivoltoDoc;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        var lirr = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        var limm = new Acc { Code = "LIMM", Name = "Milano", CountryPrefix = "LI" };
        _db.Accs.AddRange(lirr, limm);

        // Catalogo ACC: NE radice, TS sotto NE.
        _db.AccSectors.AddRange(
            new AccSector { ComposePosition = "LIRR_NE_CTR", CenterId = "LIRR", Position = "CTR" },
            new AccSector { ComposePosition = "LIRR_TS_CTR", CenterId = "LIRR", Position = "CTR", ParentCallsign = "LIRR_NE_CTR" });

        // Pisa: APP (sotto NE) + TWR + GND. Grottaglie: SOLO un APP non remotizzato (il caso LIBG).
        // Milano Linate: una torre, in un ALTRO ACC — serve a dimostrare che non viene segnalata per sbaglio.
        var lirp = new Airport { Icao = "LIRP", Name = "Pisa", Acc = lirr };
        var lipi = new Airport { Icao = "LIPI", Name = "Rivolto", Acc = lirr };
        var libg = new Airport { Icao = "LIBG", Name = "Grottaglie", Acc = lirr };
        var liml = new Airport { Icao = "LIML", Name = "Linate", Acc = limm };
        _db.Airports.AddRange(lirp, libg, liml, lipi);
        _db.AirportSectors.AddRange(
            new AirportSector { ComposePosition = "LIRP_APP", AirportIcao = "LIRP", AccCode = "LIRR", Position = "APP", ParentCallsign = "LIRR_NE_CTR" },
            new AirportSector { ComposePosition = "LIRP_TWR", AirportIcao = "LIRP", AccCode = "LIRR", Position = "TWR" },
            new AirportSector { ComposePosition = "LIRP_GND", AirportIcao = "LIRP", AccCode = "LIRR", Position = "GND" },
            new AirportSector { ComposePosition = "LIBG_APP", AirportIcao = "LIBG", AccCode = "LIRR", Position = "APP" },
            new AirportSector { ComposePosition = "LIML_TWR", AirportIcao = "LIML", AccCode = "LIMM", Position = "TWR" },
            new AirportSector { ComposePosition = "LIPI_TWR", AirportIcao = "LIPI", AccCode = "LIRR", Position = "TWR" });
        await _db.SaveChangesAsync();

        var accDoc = Doc("vIPI Roma ACC");
        var appPisa = Doc("vIPI APP Pisa");
        var scaloPisa = Doc("vIPI Aeroporto Pisa");
        var appGrottaglie = Doc("vIPI APP Grottaglie");
        var scaloGrottaglie = Doc("vIPI Aeroporto Grottaglie");
        var scaloMilano = Doc("vIPI Aeroporto Linate");
        var milPisa = Doc("vSOP MIL — LIRP");
        var milRivolto = Doc("vSOP MIL — LIPI");
        var vloa = new Document { Type = DocumentType.Vloa, Title = "vLOA LIRR ↔ LFMM", LastUpdatedAiracCycle = "2608" };
        _db.Documents.Add(vloa);
        await _db.SaveChangesAsync();

        _accDoc = accDoc.Id; _appPisaDoc = appPisa.Id; _scaloPisaDoc = scaloPisa.Id;
        _appGrottaglieDoc = appGrottaglie.Id; _scaloGrottaglieDoc = scaloGrottaglie.Id;
        _scaloMilanoDoc = scaloMilano.Id; _vloaDoc = vloa.Id;
        _milPisaDoc = milPisa.Id; _milRivoltoDoc = milRivolto.Id;

        // Proiezione dei settori. Nota bene chi porta quale documento:
        //  - LIRR_NE_CTR: radice CTR primaria → documento ACC-wide;
        //  - LIRP_APP: APP con documento proprio;
        //  - LIRP_TWR/GND: legati al documento dello SCALO (proiezione del legame vero, che sta sull'aeroporto);
        //  - LIBG_APP: APP con documento proprio, e lo scalo NON ha nessun settore legato (è il caso LIBG).
        _db.Sectors.AddRange(
            Sec(lirr, "LIRR_NE_CTR", SectorType.Ctr, SectorKind.Acc, accDoc.Id, primary: true),
            Sec(lirr, "LIRR_TS_CTR", SectorType.Ctr, SectorKind.Acc, null),
            Sec(lirr, "LIRP_APP", SectorType.App, SectorKind.Airport, appPisa.Id, primary: true, icao: "LIRP"),
            Sec(lirr, "LIRP_TWR", SectorType.Twr, SectorKind.Airport, scaloPisa.Id, primary: true, icao: "LIRP"),
            Sec(lirr, "LIRP_GND", SectorType.Gnd, SectorKind.Airport, scaloPisa.Id, icao: "LIRP"),
            Sec(lirr, "LIBG_APP", SectorType.App, SectorKind.Airport, appGrottaglie.Id, primary: true, icao: "LIBG"),
            Sec(limm, "LIML_TWR", SectorType.Twr, SectorKind.Airport, scaloMilano.Id, primary: true, icao: "LIML"));

        // Il legame AUTOREVOLE del documento d'aeroporto sta sull'aeroporto (dal 25 agosto 2026).
        lirp.DocumentId = scaloPisa.Id;
        libg.DocumentId = scaloGrottaglie.Id;   // ⚠️ nessun Sector porta questo documento: è tutto il punto
        liml.DocumentId = scaloMilano.Id;

        // E il legame GEMELLO dell'edizione militare. ⚠️ Su Rivolto c'è SOLO questo: `DocumentId` resta
        // nullo, e un lookup che guardasse la sola colonna civile non troverebbe niente da segnalare.
        lirp.MilDocumentId = milPisa.Id;
        lipi.MilDocumentId = milRivolto.Id;

        _db.NeighbourCandidates.Add(new NeighbourCandidate
        {
            HomeAccCode = "LIRR", ForeignAccCode = "LFMM", ForeignAccName = "Marseille", CountryId = "FR",
            ForeignRootCallsign = "LFMM_CTR", Status = NeighbourCandidateStatus.Confirmed,
            VloaDocumentId = vloa.Id, AdjacentHomeCallsigns = JsonSerializer.Serialize(new[] { "LIRR_TS_CTR" }),
            CreatedUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        Document Doc(string title)
        {
            var d = new Document { Type = DocumentType.Vipi, Title = title, LastUpdatedAiracCycle = "2608" };
            _db.Documents.Add(d);
            return d;
        }

        Sector Sec(Acc acc, string cs, SectorType type, SectorKind kind, int? docId,
            bool primary = false, string? icao = null) =>
            new()
            {
                Acc = acc, Callsign = cs, Name = cs, Type = type, Kind = kind, DocumentId = docId,
                IsPrimary = primary, AirportIcao = icao, IsProjected = true, IsActive = true,
            };
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private EfDocumentImpactRepository Repo => new(_db);

    private async Task<HashSet<int>> LookupAsync(string callsign, string acc = "LIRR") =>
        (await Repo.FindDocumentsForSectorAsync(callsign, acc)).Select(d => d.Id).ToHashSet();

    // ---- Regola 1: contesto radice/figli visibili ----

    [Fact]
    public async Task HideContext_Root_With_Visible_Children_Is_Flagged()
    {
        var repo = new EfAccAdminRepository(_db);
        var ne = await _db.AccSectors.FirstAsync(s => s.ComposePosition == "LIRR_NE_CTR");

        var ctx = await repo.GetSubcenterHideContextAsync(ne.Id);
        Assert.NotNull(ctx);
        Assert.True(ctx!.IsRoot);
        Assert.True(ctx.HasVisibleChildren);   // TS + LIRP_APP visibili
    }

    [Fact]
    public async Task HideContext_NonRoot_Is_Not_Root()
    {
        var repo = new EfAccAdminRepository(_db);
        var ts = await _db.AccSectors.FirstAsync(s => s.ComposePosition == "LIRR_TS_CTR");

        var ctx = await repo.GetSubcenterHideContextAsync(ts.Id);
        Assert.False(ctx!.IsRoot);   // ha padre NE → l'occultamento è consentito (i figli risalgono)
    }

    [Fact]
    public async Task HideContext_Root_With_All_Children_Hidden_Has_No_Visible_Children()
    {
        (await _db.AccSectors.FirstAsync(s => s.ComposePosition == "LIRR_TS_CTR")).IsHidden = true;
        (await _db.AirportSectors.FirstAsync(s => s.ComposePosition == "LIRP_APP")).IsHidden = true;
        await _db.SaveChangesAsync();

        var repo = new EfAccAdminRepository(_db);
        var ne = await _db.AccSectors.FirstAsync(s => s.ComposePosition == "LIRR_NE_CTR");
        var ctx = await repo.GetSubcenterHideContextAsync(ne.Id);
        Assert.False(ctx!.HasVisibleChildren);
    }

    // ---- Regola 3: reverse-lookup preciso ----

    // ---- L'edizione MILITARE dello scalo (correzione del 29 agosto 2026) -----------------------------
    //
    // ⚠️ La carta dei vSOP militari (§2) promette che una frequenza cambiata nel catalogo «cambia in
    // ENTRAMBI i documenti — alla vista se Live, alla ripubblicazione se Frozen». Il motore la mantiene:
    // le due edizioni derivano dalla stessa anagrafica. Ma il meccanismo che dice all'editore «ripubblica»
    // è QUESTO, e fino al 29 agosto 2026 guardava soltanto `Airport.DocumentId`.

    [Fact]
    public async Task Una_posizione_di_PISA_segnala_ANCHE_il_vSOP_militare()
    {
        // Pisa è un campo MISTO — scalo civile con sedime militare, ed è fra i quindici SOP veri. I due
        // documenti derivano la STESSA tabella frequenze: se cambia una posizione, vanno riletti tutti e due.
        var ids = await LookupAsync("LIRP_TWR");

        Assert.Contains(_scaloPisaDoc, ids);   // il civile: c'era già
        Assert.Contains(_milPisaDoc, ids);     // il militare: non c'era
    }

    [Fact]
    public async Task Un_campo_SOLO_militare_non_e_invisibile_agli_impatti()
    {
        // ⚠️ Rivolto non ha un documento civile: `Airport.DocumentId` è NULLO. Un lookup che guardi la sola
        // colonna civile su questo campo non torna NIENTE — non una riga sbagliata: zero — e l'unico
        // documento che quel campo ha non viene avvisato mai.
        var ids = await LookupAsync("LIPI_TWR");

        Assert.Contains(_milRivoltoDoc, ids);
    }

    [Fact]
    public async Task Il_militare_di_UN_campo_non_segnala_quello_di_un_ALTRO()
    {
        // La regola resta «per legame dimostrabile»: allargare il lookup non deve allargare il rumore, che
        // era il difetto da cui è nata la riscrittura del 25 agosto.
        var ids = await LookupAsync("LIPI_TWR");

        Assert.DoesNotContain(_milPisaDoc, ids);
        Assert.DoesNotContain(_scaloPisaDoc, ids);
    }

    [Fact]
    public async Task Un_Settore_Acc_Segnala_La_vIPI_Acc_Il_Padre_E_La_vLOA_Confinante()
    {
        var ids = await LookupAsync("LIRR_TS_CTR");

        Assert.Contains(_accDoc, ids);    // CTR: pesa sulla sezionazione dell'ACC, ed è anche il documento del padre NE
        Assert.Contains(_vloaDoc, ids);   // il callsign è fra i confinanti domestici della coppia
    }

    /// <summary>⚠️ Il difetto che ha imposto la riscrittura: prima <b>ogni</b> documento primario e <b>ogni</b>
    /// APP dell'ACC finivano nell'elenco. Pisa e Grottaglie non c'entrano nulla con un CTR di Roma.</summary>
    [Fact]
    public async Task Non_Segnala_I_Documenti_Estranei_Dello_Stesso_Acc()
    {
        var ids = await LookupAsync("LIRR_TS_CTR");

        Assert.DoesNotContain(_appPisaDoc, ids);
        Assert.DoesNotContain(_scaloPisaDoc, ids);
        Assert.DoesNotContain(_appGrottaglieDoc, ids);
        Assert.DoesNotContain(_scaloGrottaglieDoc, ids);
        Assert.DoesNotContain(_scaloMilanoDoc, ids);   // altro ACC
    }

    /// <summary>⚠️ L'altra metà del difetto: il documento dello scalo è legato all'AEROPORTO. A Grottaglie nessun
    /// <c>Sector</c> lo porta — su IVAO c'è solo un APP non remotizzato — quindi passando dai settori non si
    /// trovava mai.</summary>
    [Fact]
    public async Task Trova_Il_Documento_Dello_Scalo_Anche_Quando_Nessun_Settore_Lo_Porta()
    {
        var ids = await LookupAsync("LIBG_APP");

        Assert.Contains(_scaloGrottaglieDoc, ids);   // via Airport.DocumentId
        Assert.Contains(_appGrottaglieDoc, ids);     // il documento del settore stesso
        Assert.Contains(_accDoc, ids);               // un APP pesa sui gruppi APP della vIPI ACC
    }

    [Fact]
    public async Task Una_Posizione_Di_Terra_Non_Riapre_La_vIPI_Acc()
    {
        var ids = await LookupAsync("LIRP_GND");

        Assert.Contains(_scaloPisaDoc, ids);      // lo scalo sì: la sua sezione frequenze elenca il GND
        Assert.DoesNotContain(_accDoc, ids);      // la vIPI di Roma no: un ground non è sezionazione d'area
    }

    /// <summary>Il legame di copertura si racconta da entrambi i lati: sparito il figlio, il documento del padre
    /// ha una consegna che non esiste più.</summary>
    [Fact]
    public async Task Segnala_Il_Padre_Quando_Cambia_Il_Figlio()
    {
        var ids = await LookupAsync("LIRP_APP");

        Assert.Contains(_appPisaDoc, ids);    // sé stesso
        Assert.Contains(_scaloPisaDoc, ids);  // lo scalo di cui è una posizione
        Assert.Contains(_accDoc, ids);        // il padre LIRR_NE_CTR porta il documento ACC-wide
    }

    /// <summary>Un settore <b>disattivato</b> resta il soggetto della segnalazione: dal 25 agosto la proiezione
    /// non recide più il legame quando il callsign sparisce dai cataloghi.</summary>
    [Fact]
    public async Task Trova_Il_Documento_Anche_Se_Il_Settore_E_Disattivato()
    {
        var s = await _db.Sectors.FirstAsync(x => x.Callsign == "LIBG_APP");
        s.IsActive = false;
        await _db.SaveChangesAsync();

        var ids = await LookupAsync("LIBG_APP");
        Assert.Contains(_appGrottaglieDoc, ids);
    }

    /// <summary>Le citazioni dirette attraversano gli ACC: una frequenza di Linate che punta a un CTR di Roma
    /// lega quel documento a quel settore, e nessuna gerarchia lo direbbe.</summary>
    [Fact]
    public async Task Segnala_Chi_Cita_Il_Settore_In_Una_Frequenza_Linkata()
    {
        var liml = await _db.Airports.FirstAsync(a => a.Icao == "LIML");
        var ne = await _db.Sectors.FirstAsync(s => s.Callsign == "LIRR_NE_CTR");
        _db.AirportFrequencyLinks.Add(new AirportFrequencyLink
        {
            AirportId = liml.Id, SourceSectorId = ne.Id, Order = 0, LabelOverride = "Roma Radar",
        });
        await _db.SaveChangesAsync();

        var ids = await LookupAsync("LIRR_NE_CTR");
        Assert.Contains(_scaloMilanoDoc, ids);
    }

    [Fact]
    public async Task Segnala_La_vLOA_Che_Ha_Il_Settore_Fra_Le_Parti()
    {
        var ts = await _db.Sectors.FirstAsync(s => s.Callsign == "LIRR_TS_CTR");
        _db.DocumentParties.Add(new DocumentParty { DocumentId = _vloaDoc, SectorId = ts.Id, Role = PartyRole.Home });
        await _db.SaveChangesAsync();

        var ids = await LookupAsync("LIRR_TS_CTR");
        Assert.Contains(_vloaDoc, ids);
    }

    // ---- L'ACC del documento, per l'autorizzazione ----

    [Fact]
    public async Task Acc_Del_Documento_Risolto_Per_Settore_Aeroporto_E_vLOA()
    {
        var repo = Repo;

        Assert.Equal("LIRR", await repo.GetDocAccCodeAsync(_accDoc));            // dal settore primario
        Assert.Equal("LIRR", await repo.GetDocAccCodeAsync(_scaloGrottaglieDoc)); // dall'AEROPORTO (nessun settore)

        // ⚠️ La vLOA: prima del 25 agosto qui tornava null, e il servizio saltava l'autorizzazione.
        var ne = await _db.Sectors.FirstAsync(s => s.Callsign == "LIRR_NE_CTR");
        _db.DocumentParties.Add(new DocumentParty { DocumentId = _vloaDoc, SectorId = ne.Id, Role = PartyRole.Home });
        await _db.SaveChangesAsync();
        Assert.Equal("LIRR", await repo.GetDocAccCodeAsync(_vloaDoc));
    }

    // ---- La casella: apertura, deduplicazione, chiusura ----

    [Fact]
    public async Task Aprire_Due_Volte_Lo_Stesso_Fatto_Lascia_Una_Riga_Sola()
    {
        var repo = Repo;
        var input = new RaiseImpactInput(_accDoc, ImpactKind.SectorGone, "LIRR_TS_CTR",
            DocumentImpactService.Reasons.SectorGone, new[] { "LIRR_TS_CTR" });

        var primo = await repo.RaiseAsync(input);
        var secondo = await repo.RaiseAsync(input);

        Assert.Equal(primo, secondo);
        Assert.Single(await repo.ListOpenAsync(_accDoc));
    }

    [Fact]
    public async Task Chiusa_Sparisce_Dagli_Aperti_E_Si_Puo_Riaprire()
    {
        var repo = Repo;
        var input = new RaiseImpactInput(_accDoc, ImpactKind.SectorGone, "LIRR_TS_CTR",
            DocumentImpactService.Reasons.SectorGone, new[] { "LIRR_TS_CTR" });
        var id = await repo.RaiseAsync(input);

        await repo.ClearAsync(id, byUserId: 704798, DateTime.UtcNow);
        Assert.Empty(await repo.ListOpenAsync(_accDoc));

        // Il fatto si ripresenta: deve poter riaprire, e con una riga NUOVA (la chiusa resta storia).
        var riaperto = await repo.RaiseAsync(input);
        Assert.NotEqual(id, riaperto);
        Assert.Single(await repo.ListOpenAsync(_accDoc));
    }

    [Fact]
    public async Task La_Frase_Si_Ricompone_Da_Chiave_E_Argomenti()
    {
        var repo = Repo;
        await repo.RaiseAsync(new RaiseImpactInput(_accDoc, ImpactKind.SectorGone, "LIRR_TS_CTR",
            DocumentImpactService.Reasons.SectorGone, new[] { "LIRR_TS_CTR" }));

        var riga = Assert.Single(await repo.ListOpenAsync(_accDoc));
        Assert.Equal(DocumentImpactService.Reasons.SectorGone, riga.ReasonKey);
        Assert.Equal(new[] { "LIRR_TS_CTR" }, riga.ReasonArgs);
        Assert.Equal("vIPI Roma ACC", riga.DocumentTitle);
    }
}
