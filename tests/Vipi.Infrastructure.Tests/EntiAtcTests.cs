using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.ReleaseTargets;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Gli ENTI ATC (S49, committente, 29 settembre 2026): la vIPI APP è dell'ente, non di un nominativo IVAO.
///
/// <para>Il caso vero è Pratica di Mare: la posizione giusta è <c>LIRE_TWR</c> — una torre che fa anche
/// l'avvicinamento — ma su IVAO c'è <c>LIRE_APP</c> «per far funzionare tutto», perché la vIPI APP poteva stare
/// solo su un settore APP. E un APP che cambiava stato perdeva il documento: spuntato «remotizzato», il
/// descrittore delle pubblicazioni non lo riconosceva più.</para>
/// </summary>
public class EntiAtcTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private Acc _acc = default!;
    private int _docId;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _acc = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        _db.Accs.Add(_acc);
        var lire = new Airport { Icao = "LIRE", Name = "Pratica di Mare", Acc = _acc };
        _db.Airports.Add(lire);
        var doc = new Document
        {
            Type = DocumentType.Vipi, Title = "Pratica Tower", Language = Language.It,
            Status = DocumentStatus.Published, LastUpdatedAiracCycle = "2610",
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        _docId = doc.Id;

        // La forma di PRIMA del 29 settembre 2026: la vIPI APP sta sul settore APP.
        _db.Sectors.Add(new Sector
        {
            Acc = _acc, Callsign = "LIRE_APP", Name = "LIRE Approach", Type = SectorType.App, Kind = SectorKind.Airport,
            ApproachKind = ApproachKind.Standalone, AirportId = lire.Id, AirportIcao = "LIRE", IsActive = true,
            DocumentId = doc.Id, IsPrimary = true,
        });
        _db.Sectors.Add(new Sector
        {
            Acc = _acc, Callsign = "LIRE_TWR", Name = "LIRE Tower", Type = SectorType.Twr, Kind = SectorKind.Airport,
            AirportId = lire.Id, AirportIcao = "LIRE", IsActive = true,
        });
        _db.AirportSectors.Add(new AirportSector
        {
            ComposePosition = "LIRE_APP", AirportIcao = "LIRE", AccCode = "LIRR", Position = "APP",
            AtcCallsign = "Pratica Tower", Frequency = "118.050", ImportedAtUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private Task<int> PonteAsync() => new EfDocumentMaintenance(_db).LinkAppUnitsAsync();

    private async Task<Document> DocumentoConEnteAsync() =>
        await _db.Documents.AsNoTracking().Include(d => d.AtcUnit).ThenInclude(u => u!.Acc)
            .Include(d => d.Sectors).FirstAsync(d => d.Id == _docId);

    [Fact]
    public async Task Il_ponte_porta_la_vipi_app_sul_suo_ente_e_sgancia_il_settore()
    {
        Assert.Equal(1, await PonteAsync());

        var ente = await new EfAtcUnitRepository(_db).FindAsync("LIRE_APP");
        Assert.NotNull(ente);
        // Il codice è il nominativo che portava il documento: è la chiave sotto cui è già pubblicato.
        Assert.Equal("LIRE_APP", ente!.Code);
        Assert.Equal("Pratica Tower", ente.Name);   // il nome IVAO dal catalogo
        Assert.Equal("LIRR", ente.AccCode);
        Assert.Equal(_docId, ente.DocumentId);
        Assert.Equal(new[] { "LIRE_APP" }, ente.Positions);

        var settore = await _db.Sectors.AsNoTracking().SingleAsync(s => s.Callsign == "LIRE_APP");
        Assert.Null(settore.DocumentId);
        Assert.False(settore.IsPrimary);

        // Idempotente: gira a ogni avvio.
        Assert.Equal(0, await PonteAsync());
        Assert.Equal(1, await _db.AtcUnits.CountAsync());
    }

    [Fact]
    public async Task Il_ponte_non_tocca_la_vipi_di_uno_scalo_ne_quella_di_un_acc()
    {
        var scalo = new Document { Type = DocumentType.Vipi, Title = "vIPI LIRA", Language = Language.It, LastUpdatedAiracCycle = "2610" };
        var acc = new Document { Type = DocumentType.Vipi, Title = "vIPI Roma", Language = Language.It, LastUpdatedAiracCycle = "2610" };
        _db.Documents.AddRange(scalo, acc);
        await _db.SaveChangesAsync();
        _db.Airports.Add(new Airport { Icao = "LIRA", Name = "Ciampino", Acc = _acc, DocumentId = scalo.Id });
        // Due legami storici che NON sono vIPI APP: un APP legato alla vIPI dello scalo, uno alla vIPI ACC.
        _db.Sectors.Add(new Sector { Acc = _acc, Callsign = "LIRA_APP", Name = "x", Type = SectorType.App, Kind = SectorKind.Airport, ApproachKind = ApproachKind.Standalone, IsActive = true, DocumentId = scalo.Id });
        _db.Sectors.Add(new Sector { Acc = _acc, Callsign = "LIRR_CTR", Name = "x", Type = SectorType.Ctr, Kind = SectorKind.Acc, IsActive = true, DocumentId = acc.Id, IsPrimary = true });
        _db.Sectors.Add(new Sector { Acc = _acc, Callsign = "LIRR_W_APP", Name = "x", Type = SectorType.App, Kind = SectorKind.Acc, ApproachKind = ApproachKind.Remotized, IsActive = true, DocumentId = acc.Id });
        await _db.SaveChangesAsync();

        Assert.Equal(1, await PonteAsync());   // solo Pratica

        Assert.False(await _db.AtcUnits.AnyAsync(u => u.DocumentId == scalo.Id || u.DocumentId == acc.Id));
        Assert.Equal(acc.Id, (await _db.Sectors.AsNoTracking().SingleAsync(s => s.Callsign == "LIRR_W_APP")).DocumentId);
    }

    [Fact]
    public async Task Pratica_passa_su_LIRE_TWR_e_documento_e_pubblicazioni_restano()
    {
        await PonteAsync();
        var enti = new AtcUnitService(new EfAtcUnitRepository(_db), new Authz(VipiRole.Editor), LockConcesso.Instance);
        var ente = (await enti.FindAsync("LIRE_APP"))!;

        await enti.AddPositionAsync(ente.Id, "lire_twr");
        await enti.MakePrimaryAsync(ente.Id, "LIRE_TWR");
        await enti.RemovePositionAsync(ente.Id, "LIRE_APP");

        // La torre trova l'ente, e da lei parte la derivazione.
        var perTorre = await new EfAppDerivationRepository(_db).ResolveForDocumentAsync("LIRE_TWR");
        Assert.NotNull(perTorre);
        Assert.Equal("LIRE_APP", perTorre!.Code);
        Assert.Equal("LIRE_TWR", perTorre.Seme);
        Assert.Equal(_docId, perTorre.DocumentId);

        // La chiave di pubblicazione è il codice: quella di ieri vale ancora, anche col nominativo andato via.
        var target = new AppReleaseTarget(_db);
        Assert.Equal(_docId, await target.ResolveDocumentIdAsync("LIRE_APP"));
        Assert.Equal("LIRR", await target.AuthAccCodeAsync("LIRE_TWR"));
        Assert.True(target.TryDescribe(await DocumentoConEnteAsync(), false, out var descritto));
        Assert.Equal("LIRE_APP", descritto.ReleaseKey);

        // E la sparizione di una posizione arriva alla vIPI dell'ente.
        var avvisati = await new EfDocumentImpactRepository(_db).FindDocumentsForSectorAsync("LIRE_TWR", "LIRR");
        Assert.Contains(avvisati, d => d.Id == _docId);
    }

    [Fact]
    public async Task Un_app_spuntato_remotizzato_non_perde_piu_la_sua_vipi()
    {
        // 🔴 Il guasto di prima: col descrittore che guardava il settore primario, un APP diventato «remotizzato»
        // rendeva la sua vIPI APP irraggiungibile — finiva nel descrittore d'aeroporto con l'ICAO vuoto.
        await PonteAsync();
        var settore = await _db.Sectors.SingleAsync(s => s.Callsign == "LIRE_APP");
        settore.ApproachKind = ApproachKind.Remotized;
        await _db.SaveChangesAsync();

        var doc = await DocumentoConEnteAsync();
        Assert.True(new AppReleaseTarget(_db).TryDescribe(doc, false, out var app));
        Assert.Equal("LIRE_APP", app.ReleaseKey);
        Assert.False(new AirportReleaseTarget(_db).TryDescribe(doc, false, out _));
        Assert.Equal(_docId, await new AppReleaseTarget(_db).ResolveDocumentIdAsync("LIRE_APP"));
    }

    [Fact]
    public async Task Una_posizione_appartiene_a_un_ente_solo_e_un_nominativo_deve_esserlo()
    {
        await PonteAsync();
        var repo = new EfAtcUnitRepository(_db);
        var altro = await repo.EnsureDocumentAsync("LIRF_APP", "Roma Approach", "LIRR", SectionProfile.App, 0);
        var enti = new AtcUnitService(repo, new Authz(VipiRole.Editor), LockConcesso.Instance);
        var pratica = (await enti.FindAsync("LIRE_APP"))!;
        var roma = (await enti.FindAsync("LIRF_APP"))!;
        Assert.Equal(altro, roma.DocumentId);

        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => enti.AddPositionAsync(roma.Id, "LIRE_APP"));
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => enti.AddPositionAsync(pratica.Id, "pratica"));
        await Assert.ThrowsAsync<EditNotAllowedException>(
            () => new AtcUnitService(repo, new Authz(VipiRole.User), LockConcesso.Instance).AddPositionAsync(pratica.Id, "LIRE_TWR"));
    }

    /// <summary>
    /// Revisione (S52): un APP spuntato «remotizzato» PRIMA del carico, con la sua vIPI ancora sul settore, e uno
    /// scalo senza vIPI propria. Il ponte degli scali girava per primo e prendeva quel documento come vIPI dello
    /// scalo: la vIPI APP di Pratica diventava la vIPI d'aeroporto di LIRE, e l'ente non nasceva più.
    /// </summary>
    [Fact]
    public async Task Un_app_remotizzato_con_la_sua_vipi_non_la_cede_allo_scalo()
    {
        var settore = await _db.Sectors.SingleAsync(s => s.Callsign == "LIRE_APP");
        settore.ApproachKind = ApproachKind.Remotized;
        await _db.SaveChangesAsync();

        var manutenzione = new EfDocumentMaintenance(_db);
        await manutenzione.LinkAirportDocumentsAsync();   // l'ordine vecchio dell'avvio: prima gli scali
        await manutenzione.LinkAppUnitsAsync();

        Assert.Null((await _db.Airports.AsNoTracking().SingleAsync(a => a.Icao == "LIRE")).DocumentId);
        Assert.Equal(_docId, (await new EfAtcUnitRepository(_db).FindAsync("LIRE_APP"))!.DocumentId);
    }

    /// <summary>
    /// Revisione (S52): il codice di un ente non può diventare la posizione di un altro. Il codice vince sulla
    /// posizione nella ricerca, quindi a Pratica — tolta la posizione LIRE_APP e data a un altro ente — indirizzo,
    /// vista live ed editor di LIRE_APP portavano ancora a Pratica, e la posizione dell'altro non valeva niente.
    /// </summary>
    [Fact]
    public async Task Il_codice_di_un_ente_non_diventa_la_posizione_di_un_altro()
    {
        await PonteAsync();
        var repo = new EfAtcUnitRepository(_db);
        var enti = new AtcUnitService(repo, new Authz(VipiRole.Editor), LockConcesso.Instance);
        var pratica = (await enti.FindAsync("LIRE_APP"))!;
        await enti.AddPositionAsync(pratica.Id, "LIRE_TWR");
        await enti.MakePrimaryAsync(pratica.Id, "LIRE_TWR");
        await enti.RemovePositionAsync(pratica.Id, "LIRE_APP");
        await repo.EnsureDocumentAsync("LIRF_APP", "Roma Approach", "LIRR", SectionProfile.App, 0);
        var roma = (await enti.FindAsync("LIRF_APP"))!;

        var ex = await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(
            () => enti.AddPositionAsync(roma.Id, "LIRE_APP"));
        Assert.Contains("Pratica", ex.Message);
        // Il proprio codice invece si può riprendere come posizione.
        await enti.AddPositionAsync(pratica.Id, "LIRE_APP");
    }

    /// <summary>
    /// Revisione (S52): un ente spostato nella vIPI dell'ACC. Le segnalazioni sulle sue posizioni andavano alla
    /// vIPI APP nascosta — che non ha più né contenuto né pagina — e non alla vIPI dell'ACC che ora lo descrive.
    /// </summary>
    [Fact]
    public async Task Le_segnalazioni_di_un_ente_spostato_vanno_alla_vipi_dell_acc()
    {
        await PonteAsync();
        var vipiAcc = new Document { Type = DocumentType.Vipi, Title = "vIPI Roma", Language = Language.It, LastUpdatedAiracCycle = "2610" };
        _db.Documents.Add(vipiAcc);
        await _db.SaveChangesAsync();
        _db.Sectors.Add(new Sector { Acc = _acc, Callsign = "LIRR_CTR", Name = "Roma", Type = SectorType.Ctr, Kind = SectorKind.Acc, IsActive = true, DocumentId = vipiAcc.Id, IsPrimary = true });
        (await _db.AtcUnits.SingleAsync()).Mode = AtcUnitMode.InAccVipi;
        await _db.SaveChangesAsync();

        var avvisati = await new EfDocumentImpactRepository(_db).FindDocumentsForSectorAsync("LIRE_APP", "LIRR");

        Assert.Contains(avvisati, d => d.Id == vipiAcc.Id);
        Assert.DoesNotContain(avvisati, d => d.Id == _docId);
    }

    /// <summary>
    /// Revisione (S52): i gesti del pannello «Ente» pretendono il lock della vIPI APP, come ogni altra scrittura del
    /// documento. Prima bastava il ruolo: da una seconda scheda, o col lock scaduto e preso da un altro, si
    /// cambiavano le posizioni — e con loro frequenze, AoR e coordinamenti — sotto chi stava scrivendo.
    /// </summary>
    [Fact]
    public async Task I_gesti_sull_ente_pretendono_il_lock_del_documento()
    {
        await PonteAsync();
        var enti = new AtcUnitService(new EfAtcUnitRepository(_db), new Authz(VipiRole.Editor), new LockNegato());
        var pratica = (await enti.FindAsync("LIRE_APP"))!;

        await Assert.ThrowsAsync<EditConflictException>(() => enti.AddPositionAsync(pratica.Id, "LIRE_TWR"));
        await Assert.ThrowsAsync<EditConflictException>(() => enti.MakePrimaryAsync(pratica.Id, "LIRE_APP"));
        await Assert.ThrowsAsync<EditConflictException>(() => enti.RemovePositionAsync(pratica.Id, "LIRE_APP"));
        Assert.Equal(new[] { "LIRE_APP" }, (await enti.FindAsync("LIRE_APP"))!.Positions);
    }

    /// <summary>Revisione (S52): la vIPI APP di un ente spostato nella vIPI ACC è nascosta, ma resta in Gestione
    /// documenti — prima nessun descrittore la riconosceva, e non la si poteva più né mostrare né eliminare.</summary>
    [Fact]
    public async Task La_vipi_app_di_un_ente_spostato_resta_in_gestione_documenti()
    {
        await PonteAsync();
        (await _db.AtcUnits.SingleAsync()).Mode = AtcUnitMode.InAccVipi;
        (await _db.Documents.SingleAsync(d => d.Id == _docId)).IsHidden = true;
        await _db.SaveChangesAsync();

        Assert.True(new AppReleaseTarget(_db).TryDescribe(await DocumentoConEnteAsync(), false, out var app));
        Assert.Equal("LIRE_APP", app.ReleaseKey);
        Assert.True(app.IsHidden);
        Assert.Null(await new AppReleaseTarget(_db).ResolveDocumentIdAsync("LIRE_APP"));   // la porta pubblica resta chiusa
    }

    /// <summary>
    /// La pagina degli enti (S53): per ogni ente le posizioni con quella che IVAO non manda più segnata, dove vive il
    /// contenuto (spostamento in corso compreso) e il documento com'è in «Bozze &amp; versioni». Solo per gli editor.
    /// </summary>
    [Fact]
    public async Task La_pagina_degli_enti_dice_posizioni_contenuto_e_documento()
    {
        await PonteAsync();
        var repo = new EfAtcUnitRepository(_db);
        var pratica = (await repo.FindAsync("LIRE_APP"))!;
        await repo.AddPositionAsync(pratica.Id, "LIRE_TWR");
        (await _db.Sectors.SingleAsync(s => s.Callsign == "LIRE_TWR")).IsActive = false;   // IVAO non la manda più
        await _db.SaveChangesAsync();
        var admin = TestReleaseTargets.AdminRepo(_db);

        var righe = await new AtcUnitOverviewService(repo, admin, new SpostamentiFinti(), new Authz(VipiRole.Editor)).ListAsync();

        var riga = Assert.Single(righe);
        Assert.Equal("Pratica Tower", riga.Ente.Name);
        Assert.Equal(new[] { ("LIRE_APP", true), ("LIRE_TWR", false) }, riga.Posizioni.Select(p => (p.Callsign, p.SuIvao)));
        Assert.Equal(AtcUnitStato.VipiPropria, riga.Stato);
        Assert.Equal(_docId, riga.Documento!.DocumentId);

        var inCorso = await new AtcUnitOverviewService(repo, admin, new SpostamentiFinti(pratica.Id), new Authz(VipiRole.Editor)).ListAsync();
        Assert.Equal(AtcUnitStato.InSpostamento, inCorso.Single().Stato);
        Assert.Equal("LIRR", inCorso.Single().SpostamentoVerso);

        await Assert.ThrowsAsync<EditNotAllowedException>(
            () => new AtcUnitOverviewService(repo, admin, new SpostamentiFinti(), new Authz(VipiRole.User)).ListAsync());
    }

    private sealed class SpostamentiFinti(params int[] inCorso) : IRemotizzazioneService
    {
        public Task<RemotizzazioneEsito> RemotizzaAsync(int unitId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string?> SpostamentoInCorsoAsync(int unitId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> ConcludiSpostamentiAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<int, string>> SpostamentiInCorsoAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<int, string>>(inCorso.ToDictionary(i => i, _ => "LIRR"));
    }

    private sealed class LockNegato : IDocumentLockGuard
    {
        public Task EnsureMineAsync(int documentId, CancellationToken ct = default) =>
            throw new EditConflictException("in modifica da un altro");
    }

    private sealed class Authz : IEditAuthorizationService
    {
        public Authz(VipiRole livello) => Role = livello;
        public VipiRole Role { get; }
        public bool IsAdmin => Role >= VipiRole.Admin;
        public int? CurrentUserId => 704798;
        public string? CurrentName => "test";
        public void EnsureAdmin() { }
    }
}
