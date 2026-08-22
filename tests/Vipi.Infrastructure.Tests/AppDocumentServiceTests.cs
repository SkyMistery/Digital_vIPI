using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Aor;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Derivazione delle sezioni live dell'APP standalone su Document (doc 08e): frequenze (sottoalbero+ATIS+genitore) e
/// coordinamenti (ACC vs torre). Sostituisce i vecchi test su AppProfileService (storage editoriale ora su Document).
/// </summary>
public class AppDocumentServiceTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private AppDocumentService _service = default!;
    private int _appId, _neId, _ptwrId;

    private const string App = "LIRP_APP";

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        await RomaStructureSeed.SeedAsync(_db);

        var sectors = await _db.Sectors.ToListAsync();
        _appId = sectors.First(s => s.Callsign == App).Id;
        _neId = sectors.First(s => s.Callsign == "LIRR_NE_CTR").Id;
        _ptwrId = sectors.First(s => s.Callsign == "LIRP_TWR").Id;

        // L'aeroporto LIRP è sotto l'APP nell'albero di copertura (ParentCallsign = callsign APP).
        var lirp = await _db.Airports.FirstAsync(a => a.Icao == "LIRP");
        lirp.ParentCallsign = App;

        // Catalogo posizioni dell'aeroporto (fonte delle frequenze): ATIS·GND·TWR·APP.
        _db.AirportSectors.AddRange(
            Pos("LIRP_ATIS", "ATIS", "125.000"),
            Pos("LIRP_GND", "GND", "121.700"),
            Pos("LIRP_TWR", "TWR", "118.300"),
            Pos("LIRP_APP", "APP", "126.080"));
        await _db.SaveChangesAsync();

        var repo = new EfAppDerivationRepository(_db);
        var topo = new TopologyBuilder(_db);
        var authz = new AllowAuthz();
        var transfers = new AgreementService(new EfAgreementRepository(_db), authz, topo);
        var editing = new EfEditingRepository(_db, new AiracService(), new EfMediaMaintenance(_db));
        var docProfiles = new EfDocumentProfileRepository(_db);
        _service = new AppDocumentService(repo, new EfSpecialAreaRepository(_db), editing, authz, topo, transfers,
            new StubCoordinationSentenceTemplate(), docProfiles, new Vipi.Application.Aor.AorService(),
            new NoMinimaSource());
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    [Fact]
    public async Task Derive_Frequencies_Orders_Atis_Twr_App_With_Primary_Star()
    {
        var freqs = await _service.DeriveFrequenciesAsync(App);

        // Figli (sottoalbero, dal catalogo AirportSector) ATIS·GND·TWR·APP(★) + genitore di copertura (CTR) in coda.
        Assert.Equal(new[] { "ATIS", "GND", "TWR", "APP", "CTR" }, freqs.Select(f => f.Position).ToArray());
        Assert.Equal("LIRP_ATIS", freqs[0].Callsign);
        Assert.Contains(freqs, f => f.Callsign == "LIRP_GND");
        Assert.Contains(freqs, f => f.Callsign == "LIRP_TWR");
        var app = freqs.Single(f => f.Position == "APP");
        Assert.True(app.IsPrimary);
        Assert.Equal(App, app.Callsign);
        Assert.Equal("LIRR_NE_CTR", freqs[^1].Callsign);
    }

    [Fact]
    public async Task Configurations_Roundtrip_And_Derive_Accorpamento_Table()
    {
        // Salva una configurazione col settore APP primario aperto + Center Point/Range manuali.
        var cfg = new AccConfiguration { Key = "cfg:1", Name = "APP unico" };
        cfg.Open.Add(new AccConfigOpen { Callsign = App, CenterPoint = "GINEL", Range = "140" });
        await _service.SaveConfigurationsAsync(App, new[] { cfg });

        // Round-trip storage (blocco keyed "configurations").
        var loaded = await _service.GetConfigurationsAsync(App);
        Assert.Single(loaded);
        Assert.Equal("APP unico", loaded[0].Name);
        Assert.Equal(App, loaded[0].Open[0].Callsign);

        // Accorpamento derivato: una tabella per config; il settore aperto è unificato, con CP/Range dell'input.
        var table = await _service.DeriveConfigTableAsync(App);
        Assert.Single(table);
        Assert.Equal("cfg:1", table[0].ConfigKey);
        var row = Assert.Single(table[0].Rows);
        Assert.Equal(App, row.UnifiedCallsign);
        Assert.Equal("GINEL", row.CenterPoint);
        Assert.Equal("140", row.Range);
    }

    [Fact]
    public async Task Freq_Order_Override_Moves_Row_To_Front()
    {
        // L'override d'ordine vive nel DocumentProfile: serve prima il Document dell'APP.
        await _service.EnsureAsync(App);
        await _service.SaveFrequencyOrderAsync(App, new[] { new AppFreqOrderOverride("LIRP_TWR", 0) });

        var freqs = await _service.DeriveFrequenciesAsync(App);
        Assert.Equal("LIRP_TWR", freqs[0].Callsign);   // override 0 vince su tutti i default
    }

    [Fact]
    public async Task Derive_Coordination_Classifies_Acc_Vs_Tower()
    {
        var tr = new EfAgreementRepository(_db);
        // Partenza verso ACC (NE): va in TowardAcc.
        await Agreement(tr, _appId, _neId, TransferFlowKind.Departure, null, "VALMA", 150);
        // Arrivo verso torre (LIRP_TWR): va in TowardTowers.
        await Agreement(tr, _appId, _ptwrId, TransferFlowKind.Arrival, null, "", 2000, feet: true);
        // Partenza verso torre: dall'11 agosto 2026 COMPARE. La sezione estesa porta tutto ciò che entra o esce
        // dall'ente; prima veniva scartata in silenzio, e il documento diceva metà dell'accordo con la torre.
        await Agreement(tr, _appId, _ptwrId, TransferFlowKind.Departure, null, "XYZ", 3000, feet: true);

        var coord = await _service.DeriveCoordinationAsync(App);

        var acc = Assert.Single(coord.TowardAcc);
        Assert.Equal("LIRR_NE_CTR", acc.TargetCallsign);
        Assert.Equal(TransferFlowKind.Departure, Assert.Single(acc.Rows).Kind);

        var twr = Assert.Single(coord.TowardTowers);
        Assert.Equal("LIRP_TWR", twr.TargetCallsign);
        Assert.Equal(2, twr.Rows.Count);
        Assert.Single(twr.Rows, r => r.Kind == TransferFlowKind.Arrival);
        Assert.Single(twr.Rows, r => r.Kind == TransferFlowKind.Departure);
    }

    [Fact]
    public async Task Derive_Coordination_Includes_Inbound_Arrival_From_Acc()
    {
        var tr = new EfAgreementRepository(_db);
        // Arrivo che l'ACC (NE) consegna all'APP: flusso di PROPRIETÀ del CTR, Next = APP.
        await Agreement(tr, _neId, _appId, TransferFlowKind.Arrival, null, "MAREL", 150);

        var coord = await _service.DeriveCoordinationAsync(App);

        var acc = Assert.Single(coord.TowardAcc);
        Assert.Equal("LIRR_NE_CTR", acc.TargetCallsign);     // referente = l'ACC che possiede il flusso
        var row = Assert.Single(acc.Rows);
        Assert.Equal(TransferFlowKind.Arrival, row.Kind);
        Assert.Equal("MAREL", row.Cop);
        Assert.Equal("LIRR_NE_CTR", row.Next);
        Assert.Empty(coord.TowardTowers);
    }

    [Fact]
    public async Task Owned_Arrival_To_Acc_Reads_App_As_Sender()
    {
        // Regressione invert: arrivo POSSEDUTO dall'APP verso l'ACC (NE). Direzione owner→next: l'APP è il mittente,
        // NE il destinatario ("… trasferisce a Roma Radar NE …"), non il contrario.
        var tr = new EfAgreementRepository(_db);
        await Agreement(tr, _appId, _neId, TransferFlowKind.Arrival, "LIRP", "MAREL", 150);

        var coord = await _service.DeriveCoordinationAsync(App);

        var acc = Assert.Single(coord.TowardAcc);
        Assert.Equal("LIRR_NE_CTR", acc.TargetCallsign);
        var row = Assert.Single(acc.Rows);
        Assert.Contains("trasferisce a Roma Radar NE", row.Sentence!);
        Assert.False(row.Sentence!.StartsWith("Roma Radar NE"), "l'invert è tornato: NE non deve essere il mittente");
    }

    [Fact]
    public async Task Overflight_without_airport_goes_to_overflights_group()
    {
        var tr = new EfAgreementRepository(_db);
        var sec = await SectionAsync(tr, _appId, _neId, TransferFlowKind.Overflight, null);
        await tr.AddClauseAsync("LIRR", sec, new AgreementClauseInput
        {
            Cops = "ELB", LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.Special, LevelSpecial = "per aerovia",
        });

        var coord = await _service.DeriveCoordinationAsync(App);

        var sorvoli = Assert.Single(coord.Overflights);
        Assert.Equal("Sorvoli", sorvoli.TargetCallsign);
        var row = Assert.Single(sorvoli.Rows);
        Assert.Equal(TransferFlowKind.Overflight, row.Kind);
        Assert.DoesNotContain("destinazione", row.Sentence!);
        Assert.Empty(coord.TowardAcc);       // il sorvolo non è un arrivo/partenza verso ACC
    }

    // ---- Copertura del dominio di gerarchia: il doc del padre (LIRP_APP) copre i figli APP (LIRP_E_APP) ----

    /// <summary>Aggiunge un APP figlio (ParentSectorId = primario) + il suo catalogo con poligono AoR; ritorna l'Id settore.</summary>
    private async Task<int> AddChildAppAsync(string callsign, string poly)
    {
        var primary = await _db.Sectors.FirstAsync(s => s.Id == _appId);
        var child = new Sector
        {
            Callsign = callsign, Name = callsign, AccId = primary.AccId, Type = SectorType.App, Kind = SectorKind.Airport,
            ApproachKind = ApproachKind.Standalone, ParentSectorId = _appId, AirportIcao = "LIRP", IsActive = true,
        };
        _db.Sectors.Add(child);
        _db.AirportSectors.Add(new AirportSector
        {
            ComposePosition = callsign, AirportIcao = "LIRP", AccCode = "LIRR", Position = "APP", Frequency = "127.000",
            RegionMapPolygon = poly,
        });
        await _db.SaveChangesAsync();
        return child.Id;
    }

    [Fact]
    public async Task GetAorView_Includes_Child_App_Polygons_But_Not_Towers_Automatically()
    {
        // Il primario ha un poligono; aggiungo un APP figlio (E) con poligono proprio. La TWR ha shape ma NON deve
        // più comparire in automatico (l'overlay torri è stato sostituito dalle shape extra scelte a mano).
        (await _db.AirportSectors.FirstAsync(s => s.ComposePosition == App)).RegionMapPolygon =
            "[[10.4,43.6],[10.5,43.6],[10.5,43.7],[10.4,43.7]]";
        (await _db.AirportSectors.FirstAsync(s => s.ComposePosition == "LIRP_TWR")).RegionMapPolygon =
            "[[10.45,43.62],[10.46,43.62],[10.46,43.63],[10.45,43.63]]";
        await _db.SaveChangesAsync();
        await AddChildAppAsync("LIRP_E_APP", "[[10.5,43.6],[10.6,43.6],[10.6,43.7],[10.5,43.7]]");

        var view = await _service.GetAorViewAsync(App);

        Assert.Contains(view.Sectors, s => s.Callsign == App);              // primario
        Assert.Contains(view.Sectors, s => s.Callsign == "LIRP_E_APP");     // figlio nel dominio
        Assert.DoesNotContain(view.Sectors, s => s.Callsign == "LIRP_TWR"); // torre NON automatica
    }

    [Fact]
    public async Task AorExtras_Roundtrip_And_Appended_As_Toggleable_Ring()
    {
        (await _db.AirportSectors.FirstAsync(s => s.ComposePosition == App)).RegionMapPolygon =
            "[[10.4,43.6],[10.5,43.6],[10.5,43.7],[10.4,43.7]]";
        (await _db.AirportSectors.FirstAsync(s => s.ComposePosition == "LIRP_TWR")).RegionMapPolygon =
            "[[10.45,43.62],[10.46,43.62],[10.46,43.63],[10.45,43.63]]";
        await _db.SaveChangesAsync();

        // La torre è nel catalogo delle shape selezionabili; aggiungila a mano come shape extra.
        var catalog = await _service.ListSelectableSectorShapesAsync();
        Assert.Contains(catalog, c => c.Callsign == "LIRP_TWR");

        await _service.SaveAorCustomizationAsync(App, new AorExtraShapes { Callsigns = { "LIRP_TWR", "LIRP_TWR" } });   // dedup atteso

        var custom = await _service.GetAorCustomizationAsync(App);
        Assert.Equal(new[] { "LIRP_TWR" }, custom.Callsigns.ToArray());

        var view = await _service.GetAorViewAsync(App);
        Assert.Contains(view.Sectors, s => s.Callsign == App);        // primario
        Assert.Contains(view.Sectors, s => s.Callsign == "LIRP_TWR"); // shape extra appesa come anello
    }

    [Fact]
    public async Task AorView_Colors_Default_By_Type_And_Honor_Override()
    {
        (await _db.AirportSectors.FirstAsync(s => s.ComposePosition == App)).RegionMapPolygon =
            "[[10.4,43.6],[10.5,43.6],[10.5,43.7],[10.4,43.7]]";
        (await _db.AirportSectors.FirstAsync(s => s.ComposePosition == "LIRP_TWR")).RegionMapPolygon =
            "[[10.45,43.62],[10.46,43.62],[10.46,43.63],[10.45,43.63]]";
        await _db.SaveChangesAsync();

        // La TWR come shape extra + un override colore sul primario (APP).
        await _service.SaveAorCustomizationAsync(App, new AorExtraShapes
        {
            Callsigns = { "LIRP_TWR" },
            Colors = { ["LIRP_APP"] = "#123456" },
        });

        var view = await _service.GetAorViewAsync(App);
        var app = view.Sectors.Single(s => s.Callsign == App);
        var twr = view.Sectors.Single(s => s.Callsign == "LIRP_TWR");

        Assert.Equal("#123456", app.Color);                                       // override manuale
        Assert.Equal(Vipi.Application.Aor.AorColorScheme.Defaults["TWR"], twr.Color);  // default per tipo (_TWR rosso)
    }

    [Fact]
    public async Task Derive_Coordination_Includes_Child_App_Flows()
    {
        var childId = await AddChildAppAsync("LIRP_E_APP", "[[10.5,43.6],[10.6,43.6],[10.6,43.7],[10.5,43.7]]");

        // Flusso di PROPRIETÀ del figlio (E) verso l'ACC NE: deve comparire nel doc del padre.
        var tr = new EfAgreementRepository(_db);
        await Agreement(tr, childId, _neId, TransferFlowKind.Departure, null, "VALMA", 150);

        var coord = await _service.DeriveCoordinationAsync(App);

        var acc = Assert.Single(coord.TowardAcc);
        Assert.Equal("LIRR_NE_CTR", acc.TargetCallsign);
        Assert.Contains(acc.Rows, r => r.OwnerCallsign == "LIRP_E_APP");   // riga del figlio, non solo del primario
    }

    // ---- aree regolamentate: come la vIPI ACC ma senza aree di default (nessun modo automatico) ----

    [Fact]
    public async Task Regulated_Is_Empty_By_Default()
    {
        _db.SpecialAreas.AddRange(Area("a1", "LIRR", "R14A"), Area("a2", "LIRR", "R99"));
        await _db.SaveChangesAsync();
        await _service.EnsureAsync(App);

        var sel = await _service.GetRegulatedAsync(App);

        Assert.False(sel.OwnAuto);          // l'APP non ha il modo automatico del blocco Aerovia
        Assert.Empty(sel.OwnIds);
        Assert.Empty(sel.ExtraIds);
        Assert.Empty(await _service.ResolveRegulatedAreasAsync(sel));   // nessuna area, benché l'ACC ne abbia
    }

    [Fact]
    public async Task Regulated_Roundtrips_Own_And_Extra_In_Order()
    {
        _db.Accs.Add(new Acc { Code = "LIMM", Name = "Milano" });
        _db.SpecialAreas.AddRange(
            Area("a1", "LIRR", "AAA"), Area("a2", "LIRR", "BBB"), Area("x1", "LIMM", "XXX"));
        await _db.SaveChangesAsync();
        await _service.EnsureAsync(App);

        await _service.SaveRegulatedAsync(App, new RegulatedSelection
        {
            OwnAuto = true,   // ignorato: sull'APP la selezione è sempre manuale
            OwnIds = { "a2" },
            ExtraIds = { "x1" },
        });

        var sel = await _service.GetRegulatedAsync(App);
        Assert.False(sel.OwnAuto);
        Assert.Equal(new[] { "a2" }, sel.OwnIds);
        Assert.Equal(new[] { "x1" }, sel.ExtraIds);

        var views = await _service.ResolveRegulatedAreasAsync(sel);
        Assert.Equal(new[] { "BBB", "XXX" }, views.Select(v => v.Name));   // proprie poi extra
    }

    [Fact]
    public async Task Regulated_Pickers_Split_Own_Acc_From_The_Others()
    {
        _db.Accs.AddRange(new Acc { Code = "LIMM", Name = "Milano" }, new Acc { Code = "LIBB", Name = "Brindisi" });
        _db.SpecialAreas.AddRange(
            Area("a1", "LIRR", "AAA"), Area("x1", "LIMM", "XXX"), Area("x2", "LIBB", "YYY"));
        await _db.SaveChangesAsync();

        var own = await _service.ListSpecialAreasAsync(App);
        var others = await _service.ListOtherAccSpecialAreasAsync(App);

        Assert.Equal(new[] { "AAA" }, own.Select(p => p.Name));
        Assert.Equal(new[] { "XXX", "YYY" }, others.Select(p => p.Name));   // ordinate per nome (l'ente è un filtro)
    }

    private static SpecialArea Area(string ivaoId, string acc, string name) => new()
    {
        IvaoId = ivaoId,
        Name = name,
        Centers = new List<SpecialAreaCenter> { new() { IvaoId = ivaoId, CenterId = acc } },
    };

    private static AirportSector Pos(string compose, string position, string freq) => new()
    {
        ComposePosition = compose, AirportIcao = "LIRP", AccCode = "LIRR", Position = position, Frequency = freq,
    };

    /// <summary>Un accordo con una clausola sola: la forma piu' corta di un caso di prova, adesso che il
    /// ricevente e' dell'accordo e non della riga.</summary>
    private static async Task Agreement(EfAgreementRepository tr, int from, int to, TransferFlowKind kind,
        string? icao, string cops, int level, bool feet = false)
    {
        var sec = await SectionAsync(tr, from, to, kind, icao);
        await tr.AddClauseAsync("LIRR", sec, new AgreementClauseInput
        {
            Cops = cops, LevelValue = level, LevelUnit = feet ? LevelUnit.Feet : LevelUnit.Fl,
            LevelConstraint = feet ? LevelConstraint.Exact : LevelConstraint.AtOrAbove,
        });
    }

    /// <summary>
    /// La sezione dove finiranno le clausole: l'accordo fra i due enti (riusato se c'è già — ne esiste UNO solo
    /// per coppia) e dentro una sezione col traffico e lo scalo.
    /// <para>⚠️ Il verso si legge dall'accordo SALVATO perché i lati stanno in forma canonica: «chi cede» può
    /// essere finito su A o su B, e darlo per scontato scriverebbe il contrario di ciò che il caso intende.</para>
    /// </summary>
    private static async Task<int> SectionAsync(EfAgreementRepository tr, int from, int to,
        TransferFlowKind kind, string? icao)
    {
        var id = await tr.FindByPairAsync("LIRR", from, to)
                 ?? await tr.AddAgreementAsync("LIRR", new AgreementInput { SideASectorId = from, SideBSectorId = to });

        var a = (await tr.ListByAccAsync("LIRR")).First(x => x.Id == id);
        var direction = a.SideA.SectorId == from ? AgreementDirection.AtoB : AgreementDirection.BtoA;

        return await tr.AddSectionAsync("LIRR", id, new AgreementSectionInput
        {
            Kind = kind,
            Direction = direction,
            Airports = icao is null ? Array.Empty<AgreementAirportInput>() : new[] { new AgreementAirportInput(icao) },
        });
    }

    /// <summary>Authz permissiva: le derivazioni di lettura non la invocano; presente solo per i ctor dei service.</summary>
    private sealed class AllowAuthz : IEditAuthorizationService
    {
        public bool IsAdmin => true;
        public int? CurrentUserId => 1;
        public string? CurrentName => "test";
        public Task EnsureCanEditAccAsync(string accCode, CancellationToken ct = default) => Task.CompletedTask;
        public Task EnsureCanEditDocumentAsync(int documentId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> CanEditAccAsync(string accCode, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> CanEditDocumentAsync(int documentId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<GrantRow>> ListGrantsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GrantRow>>(Array.Empty<GrantRow>());
        public Task<int> AddGrantAsync(int UserId, string? displayName, string accCode, CancellationToken ct = default) => Task.FromResult(0);
        public Task RevokeGrantAsync(int grantId, CancellationToken ct = default) => Task.CompletedTask;
        public void EnsureAdmin() { }
    }
}
