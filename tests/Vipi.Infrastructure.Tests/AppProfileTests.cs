using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Aor;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>Profilo APP standalone: round-trip editoriale + derivazione frequenze (sottoalbero+ATIS) e coordinamenti.</summary>
public class AppProfileTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfAppProfileRepository _repo = default!;
    private AppProfileService _service = default!;
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

        _repo = new EfAppProfileRepository(_db);
        var topo = new TopologyBuilder(_db);
        var authz = new AllowAuthz();
        var transfers = new TransferService(new EfTransferRepository(_db), authz, topo);
        _service = new AppProfileService(_repo, authz, topo, transfers);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    [Fact]
    public async Task Editorial_Roundtrips()
    {
        await _repo.SaveSeparationsAsync(App, new[] { new AppSeparationRow("1000 ft", "3 NM"), new AppSeparationRow("2000 ft", "5 NM") });
        await _repo.SaveSectionOrderAsync(App, new[] { "sep", "aor", "freq" });
        await _repo.SaveFrequencyOrderAsync(App, new[] { new AppFreqOrderOverride("LIRP_TWR", 1) });
        await _repo.SaveFrequencyLinksAsync(App, new[] { _neId });

        var data = await _repo.LoadAsync(App);
        Assert.NotNull(data);
        Assert.Equal(_appId, data!.SectorId);
        Assert.Equal(2, data.Separations.Count);
        Assert.Equal("1000 ft", data.Separations[0].Vertical);
        Assert.Equal("3 NM", data.Separations[0].Lateral);
        Assert.Equal(new[] { "sep", "aor", "freq" }, data.SectionOrder);
        Assert.Equal(1, Assert.Single(data.FreqOrder).Order);
        var link = Assert.Single(data.FrequencyLinks);
        Assert.Equal("LIRR_NE_CTR", link.Callsign);
        Assert.True(link.IsLink);
    }

    [Fact]
    public async Task Profile_Is_Created_On_First_Save_Only()
    {
        await _repo.SaveSeparationsAsync(App, Array.Empty<AppSeparationRow>());
        await _repo.SaveSectionOrderAsync(App, new[] { "sep" });
        Assert.Equal(1, await _db.AppProfiles.CountAsync(p => p.SectorId == _appId));
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
        // Genitore: LIRR_NE_CTR (parent dell'APP nell'albero) presente in fondo.
        Assert.Equal("LIRR_NE_CTR", freqs[^1].Callsign);
    }

    [Fact]
    public async Task Freq_Order_Override_Moves_Row_To_Front()
    {
        await _repo.SaveFrequencyOrderAsync(App, new[] { new AppFreqOrderOverride("LIRP_TWR", 0) });
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
