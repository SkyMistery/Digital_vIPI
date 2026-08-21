using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Gate della policy di import sulla categoria «Settori».
///
/// <para>Il gate sta nel corpo condiviso (<see cref="AirportSectorImporter"/>) e non nei chiamanti: gli
/// import dei settori partono da quattro posti (job 24h, bottone dell'editor aeroporto, massivo di
/// <c>/vsop/admin/airports</c>, «Genera documenti»). Fino al 22 agosto 2026 la pagina Sorgenti prometteva
/// «l'import non la tocca più» e nessuno dei quattro leggeva la policy: escludere «Settori» permetteva di
/// aggiungerli a mano e poi il giro successivo ci ripassava sopra.</para>
/// </summary>
public class AirportSectorImportPolicyTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfAirportSectorRepository _repo = default!;
    private EfImportPolicyStore _policy = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        var acc = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        _db.Accs.Add(acc);
        _db.Airports.Add(new Airport { Icao = "LIRN", Name = "Napoli", Acc = acc });
        await _db.SaveChangesAsync();

        _repo = new EfAirportSectorRepository(_db);
        _policy = new EfImportPolicyStore(_db);
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    [Fact]
    public async Task Default_policy_imports_sectors()
    {
        var source = new FakeAirportDetails();

        var (created, updated) = await new AirportSectorImporter(source, _repo, _policy).ImportAsync("LIRN");

        Assert.Equal(2, created);
        Assert.Equal(0, updated);
        Assert.Equal(1, source.PositionCalls);
        Assert.Equal(2, await _db.AirportSectors.CountAsync());
    }

    [Fact]
    public async Task Excluded_category_skips_fetch_and_keeps_the_catalog_as_it_is()
    {
        // Un settore aggiunto a mano dopo l'esclusione: è proprio quello che l'import ripassava.
        var source = new FakeAirportDetails();
        await new AirportSectorImporter(source, _repo, _policy).ImportAsync("LIRN");
        await _policy.SaveAsync(new ImportPolicySnapshot(true, true, Sectors: false, true, true), 1);

        source.Positions = new[] { Pos("LIRN_TWR", "999.999") };   // la sorgente cambia: non deve entrare
        source.PositionCalls = 0;

        var (created, updated) = await new AirportSectorImporter(source, _repo, _policy).ImportAsync("LIRN");

        Assert.Equal((0, 0), (created, updated));
        Assert.Equal(0, source.PositionCalls);                     // nessuna fetch
        var twr = (await _repo.ListByAirportAsync("LIRN")).Single(s => s.ComposePosition == "LIRN_TWR");
        Assert.Equal("118.300", twr.Frequency);                    // e nessuna scrittura
    }

    private static SourceAtcPosition Pos(string callsign, string freq) =>
        new(callsign, freq, callsign[(callsign.LastIndexOf('_') + 1)..]);

    private sealed class FakeAirportDetails : IAirportDetailProvider
    {
        public IReadOnlyList<SourceAtcPosition> Positions { get; set; } =
            new[] { Pos("LIRN_TWR", "118.300"), Pos("LIRN_GND", "121.900") };

        public int PositionCalls;

        public Task<IReadOnlyList<SourceAtcPosition>> GetAtcPositionsAsync(string icao, CancellationToken ct = default)
        {
            PositionCalls++;
            return Task.FromResult(Positions);
        }

        public Task<SourceAtcPosition?> GetAtcPositionDetailAsync(string composePosition, CancellationToken ct = default) =>
            Task.FromResult<SourceAtcPosition?>(null);

        public Task<IReadOnlyList<SourceRunway>> GetRunwaysAsync(string icao, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SourceRunway>>(Array.Empty<SourceRunway>());
    }
}
