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

        var repo = new EfAppProfileRepository(_db);
        var topo = new TopologyBuilder(_db);
        var authz = new AllowAuthz();
        var transfers = new TransferService(new EfTransferRepository(_db), authz, topo);
        var editing = new EfEditingRepository(_db, new AiracService());
        var docProfiles = new EfDocumentProfileRepository(_db);
        _service = new AppDocumentService(repo, editing, authz, topo, transfers,
            new StubCoordinationSentenceTemplate(), docProfiles);
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
        var tr = new EfTransferRepository(_db);
        // Partenza verso ACC (NE): va in TowardAcc.
        var fDep = await tr.AddFlowAsync("LIRR", new TransferFlowInput { OwningSectorId = _appId, Kind = TransferFlowKind.Departure });
        await tr.AddPointAsync("LIRR", fDep, Point("VALMA", 150, _neId));
        // Arrivo verso torre (LIRP_TWR): va in TowardTowers.
        var fArr = await tr.AddFlowAsync("LIRR", new TransferFlowInput { OwningSectorId = _appId, Kind = TransferFlowKind.Arrival });
        await tr.AddPointAsync("LIRR", fArr, Point("", 2000, _ptwrId, feet: true));
        // Partenza verso torre: NON deve comparire (verso torri = solo arrivi).
        var fDepTwr = await tr.AddFlowAsync("LIRR", new TransferFlowInput { OwningSectorId = _appId, Kind = TransferFlowKind.Departure });
        await tr.AddPointAsync("LIRR", fDepTwr, Point("XYZ", 3000, _ptwrId, feet: true));

        var coord = await _service.DeriveCoordinationAsync(App);

        var acc = Assert.Single(coord.TowardAcc);
        Assert.Equal("LIRR_NE_CTR", acc.TargetCallsign);
        Assert.Equal(TransferFlowKind.Departure, Assert.Single(acc.Rows).Kind);

        var twr = Assert.Single(coord.TowardTowers);
        Assert.Equal("LIRP_TWR", twr.TargetCallsign);
        var row = Assert.Single(twr.Rows);
        Assert.Equal(TransferFlowKind.Arrival, row.Kind);   // la partenza-verso-torre è esclusa
    }

    [Fact]
    public async Task Derive_Coordination_Includes_Inbound_Arrival_From_Acc()
    {
        var tr = new EfTransferRepository(_db);
        // Arrivo che l'ACC (NE) consegna all'APP: flusso di PROPRIETÀ del CTR, Next = APP.
        var fIn = await tr.AddFlowAsync("LIRR", new TransferFlowInput { OwningSectorId = _neId, Kind = TransferFlowKind.Arrival });
        await tr.AddPointAsync("LIRR", fIn, Point("MAREL", 150, _appId));

        var coord = await _service.DeriveCoordinationAsync(App);

        var acc = Assert.Single(coord.TowardAcc);
        Assert.Equal("LIRR_NE_CTR", acc.TargetCallsign);     // referente = l'ACC che possiede il flusso
        var row = Assert.Single(acc.Rows);
        Assert.Equal(TransferFlowKind.Arrival, row.Kind);
        Assert.Equal("MAREL", row.Cop);
        Assert.Equal("LIRR_NE_CTR", row.Next);
        Assert.Empty(coord.TowardTowers);
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
    public async Task GetAorView_Includes_Child_App_Polygons()
    {
        // Il primario ha un poligono; aggiungo un APP figlio (E) con poligono proprio.
        (await _db.AirportSectors.FirstAsync(s => s.ComposePosition == App)).RegionMapPolygon =
            "[[10.4,43.6],[10.5,43.6],[10.5,43.7],[10.4,43.7]]";
        await _db.SaveChangesAsync();
        await AddChildAppAsync("LIRP_E_APP", "[[10.5,43.6],[10.6,43.6],[10.6,43.7],[10.5,43.7]]");

        var view = await _service.GetAorViewAsync(App);

        Assert.Contains(view.Sectors, s => s.Callsign == App);           // primario
        Assert.Contains(view.Sectors, s => s.Callsign == "LIRP_E_APP");  // figlio nel dominio
    }

    [Fact]
    public async Task Derive_Coordination_Includes_Child_App_Flows()
    {
        var childId = await AddChildAppAsync("LIRP_E_APP", "[[10.5,43.6],[10.6,43.6],[10.6,43.7],[10.5,43.7]]");

        // Flusso di PROPRIETÀ del figlio (E) verso l'ACC NE: deve comparire nel doc del padre.
        var tr = new EfTransferRepository(_db);
        var fDep = await tr.AddFlowAsync("LIRR", new TransferFlowInput { OwningSectorId = childId, Kind = TransferFlowKind.Departure });
        await tr.AddPointAsync("LIRR", fDep, Point("VALMA", 150, _neId));

        var coord = await _service.DeriveCoordinationAsync(App);

        var acc = Assert.Single(coord.TowardAcc);
        Assert.Equal("LIRR_NE_CTR", acc.TargetCallsign);
        Assert.Contains(acc.Rows, r => r.OwnerCallsign == "LIRP_E_APP");   // riga del figlio, non solo del primario
    }

    private static AirportSector Pos(string compose, string position, string freq) => new()
    {
        ComposePosition = compose, AirportIcao = "LIRP", AccCode = "LIRR", Position = position, Frequency = freq,
    };

    private static TransferPointInput Point(string cop, int level, int next, bool feet = false) => new()
    {
        Cop = cop, LevelValue = level, LevelUnit = feet ? LevelUnit.Feet : LevelUnit.Fl,
        LevelConstraint = feet ? LevelConstraint.Exact : LevelConstraint.AtOrAbove, NextSectorId = next,
    };

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
