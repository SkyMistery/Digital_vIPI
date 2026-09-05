using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vipi.Application;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// «Genera documenti» e la policy di import.
///
/// <para>Il merge da sorgente (<c>MergeFromSourceAsync</c>) lo chiamano due percorsi: il reimport dell'editor
/// aeroporto e la generazione del documento. Fino al 22 agosto 2026 solo il primo leggeva la policy: con
/// «Transition Altitude» e «Piste» escluse in Sorgenti, generare il documento sovrascriveva comunque la TA
/// scritta a mano, riportava lunghezza e bearing della sorgente e faceva <b>rientrare</b> le piste che
/// l'utente aveva tolto. Questi test sono la caratterizzazione di quel confine.</para>
/// </summary>
public class GenerateAirportDocumentPolicyTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfImportPolicyStore _policy = default!;
    private EfAirportRepository _profile = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _policy = new EfImportPolicyStore(_db);
        _profile = new EfAirportRepository(_db, new EfMediaMaintenance(_db));

        var structRepo = new EfStructureEditingRepository(_db);
        await structRepo.CreateAccAsync("LIRR", "Roma ACC", "LI");
        await structRepo.CreateAirportAsync("LIRR", "LIRF", "Roma Fiumicino");
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    [Fact]
    public async Task Generate_respects_excluded_TA_and_runways()
    {
        // Stato editoriale dell'utente: TA a mano, una pista con le sue misure, e una seconda pista TOLTA.
        await _profile.MergeFromSourceAsync("LIRF", 4000, new[] { new SourceRunway("16L", 3902, 160) });
        await _policy.SaveAsync(new ImportPolicySnapshot(TransitionAltitude: false, Runways: false, true, true, true), 1);

        // La sorgente dice altro, e ha una pista in più.
        var dir = new FakeDirectory { Airports = { new SourceAirport("LIRF", "Roma Fiumicino", "LIRR", null, 9000) } };
        var det = new FakeDetails
        {
            Runways = { new SourceRunway("16L", 9999, 160), new SourceRunway("16R", 3900, 160) },
            Positions = { new SourceAtcPosition("LIRF_TWR", "118.700", "TWR") },
        };

        var r = await BuildService(dir, det).GenerateAirportDocumentAsync("LIRF");

        Assert.True(r.Created);
        var data = await _profile.LoadAsync("LIRF");
        Assert.Equal(4000, data!.TransitionAltitudeFt);                        // TA esclusa: invariata
        var pista = Assert.Single(data.Runways);                               // la 16R NON rientra
        Assert.Equal("16L", pista.Ident);
        Assert.Equal(3902, pista.LengthM);                                     // Piste escluse: misure invariate
    }

    [Fact]
    public async Task Generate_still_merges_the_imported_categories()
    {
        // Default (tutto importato): il comportamento storico non cambia.
        var dir = new FakeDirectory { Airports = { new SourceAirport("LIRF", "Roma Fiumicino", "LIRR", null, 9000) } };
        var det = new FakeDetails
        {
            Runways = { new SourceRunway("16L", 3902, 160) },
            Positions = { new SourceAtcPosition("LIRF_TWR", "118.700", "TWR") },
        };

        await BuildService(dir, det).GenerateAirportDocumentAsync("LIRF");

        var data = await _profile.LoadAsync("LIRF");
        Assert.Equal(9000, data!.TransitionAltitudeFt);
        Assert.Equal(3902, Assert.Single(data.Runways).LengthM);
    }

    [Fact]
    public async Task Generate_says_why_when_sectors_are_excluded_and_the_catalog_is_empty()
    {
        // Senza catalogo la generazione lo importa; con «Settori» escluso l'import non fa nulla per scelta,
        // e il documento uscirebbe senza settori. Deve dirlo, non generarlo monco.
        await _policy.SaveAsync(new ImportPolicySnapshot(true, true, Sectors: false, true, true), 1);
        var det = new FakeDetails { Positions = { new SourceAtcPosition("LIRF_TWR", "118.700", "TWR") } };

        var r = await BuildService(new FakeDirectory(), det).GenerateAirportDocumentAsync("LIRF");

        Assert.False(r.Created);
        Assert.Contains("Sorgenti", r.Skipped);
        Assert.Equal(0, det.PositionCalls);                    // il gate ha fermato l'import prima della fetch
        Assert.Equal(0, await _db.AirportSectors.CountAsync());
    }

    private StructureEditingService BuildService(FakeDirectory dir, FakeDetails det)
    {
        var provider = new FakeUser { User = new CurrentUser(1, "Admin", "LIRR", new[] { "IT-AOC" }) };
        var authz = new EditAuthorizationService(provider,
            new Vipi.Application.Auth.RoleResolver(new Vipi.Application.Auth.AuthOptions(), new Vipi.Application.DivisionOptions()), SenzaPromozioni.Instance);
        var sectors = new EfAirportSectorRepository(_db);
        var importer = new AirportSectorImporter(det, sectors, _policy);
        return new StructureEditingService(
            new EfStructureEditingRepository(_db), _profile, authz, dir, det, _policy,
            sectors, importer, new EfSectorProjectionService(_db),
            new AirportImportUseCase(dir, new EfStructureEditingRepository(_db), importer, new EfSectorProjectionService(_db)),
            new DocumentImpactService(new EfDocumentImpactRepository(_db), authz));
    }

    private sealed class FakeUser : ICurrentUserProvider
    {
        public CurrentUser? User { get; set; }
        public CurrentUser? Get() => User;
    }

    private sealed class FakeDirectory : IAirportDirectory
    {
        public List<SourceAirport> Airports { get; } = new();
        public Task<IReadOnlyList<SourceAirport>> GetAirportsAsync(CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<SourceAirport>)Airports);
        public Task<SourceAirport?> GetByIcaoAsync(string icao, CancellationToken ct = default)
            => Task.FromResult(Airports.FirstOrDefault(a => string.Equals(a.Icao, icao, StringComparison.OrdinalIgnoreCase)));
    }

    private sealed class FakeDetails : IAirportDetailProvider
    {
        public List<SourceAtcPosition> Positions { get; } = new();
        public List<SourceRunway> Runways { get; } = new();
        public int PositionCalls;

        public Task<IReadOnlyList<SourceAtcPosition>> GetAtcPositionsAsync(string icao, CancellationToken ct = default)
        {
            PositionCalls++;
            return Task.FromResult((IReadOnlyList<SourceAtcPosition>)Positions);
        }

        public Task<SourceAtcPosition?> GetAtcPositionDetailAsync(string composePosition, CancellationToken ct = default)
            => Task.FromResult(Positions.FirstOrDefault(p => p.Callsign == composePosition));

        public Task<IReadOnlyList<SourceRunway>> GetRunwaysAsync(string icao, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<SourceRunway>)Runways);
    }
}
