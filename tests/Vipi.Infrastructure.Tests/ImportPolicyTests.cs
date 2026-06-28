using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vipi.Application;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Policy di import globale (opt-out): le categorie importate sono in sola lettura (guard nei service),
/// l'import salta le categorie escluse, e lo store ha default "tutto importato".
/// </summary>
public class ImportPolicyTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfImportPolicyStore _store = default!;

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
    }

    private sealed class FakeDetails : IAirportDetailProvider
    {
        public List<SourceAtcPosition> Positions { get; } = new();
        public List<SourceRunway> Runways { get; } = new();
        public Task<IReadOnlyList<SourceAtcPosition>> GetAtcPositionsAsync(string icao, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<SourceAtcPosition>)Positions);
        public Task<IReadOnlyList<SourceRunway>> GetRunwaysAsync(string icao, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<SourceRunway>)Runways);
    }

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _store = new EfImportPolicyStore(_db);

        var structRepo = new EfStructureEditingRepository(_db);
        await structRepo.CreateFirAsync("LIRR", "Roma FIR", "LI");
        await structRepo.CreateAirportAsync("LIRR", "LIRF", "Roma Fiumicino");
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private AirportProfileService BuildService(FakeDirectory? dir = null, FakeDetails? det = null)
    {
        var provider = new FakeUser { User = new CurrentUser(1, "Admin", "LIRR", new[] { "IT-AOC" }) };
        var authz = new EditAuthorizationService(provider, new EfEditGrantRepository(_db),
            Options.Create(new AuthOptions()), Options.Create(new DivisionOptions()));
        return new AirportProfileService(new EfAirportProfileRepository(_db), authz,
            dir ?? new FakeDirectory(), det ?? new FakeDetails(), _store);
    }

    [Fact]
    public async Task Store_Defaults_To_All_Imported_And_RoundTrips()
    {
        var def = await _store.GetAsync();
        Assert.True(def is { TransitionAltitude: true, Atis: true, Runways: true, Sectors: true });

        await _store.SaveAsync(new ImportPolicySnapshot(false, true, false, true), updatedByUserId: 42);
        var saved = await _store.GetAsync();
        Assert.False(saved.TransitionAltitude);
        Assert.True(saved.Atis);
        Assert.False(saved.Runways);
        Assert.True(saved.Sectors);
    }

    [Fact]
    public async Task TA_Locked_By_Default_Rejects_Edit_But_Allows_When_Excluded()
    {
        var svc = BuildService();

        // Default: TA importata e bloccata → scrittura rifiutata.
        await Assert.ThrowsAsync<ValidationException>(() => svc.SetTransitionAltitudeAsync("LIRF", 6000));

        // Escludendo TA, la scrittura passa e persiste.
        await _store.SaveAsync(new ImportPolicySnapshot(false, true, true, true), 1);
        await svc.SetTransitionAltitudeAsync("LIRF", 6000);
        var ta = (await _db.Airports.AsNoTracking().FirstAsync(a => a.Icao == "LIRF")).TransitionAltitudeFt;
        Assert.Equal(6000, ta);
    }

    [Fact]
    public async Task Runways_Locked_Rejects_Geometry_Change_But_Allows_Editorial()
    {
        var profile = new EfAirportProfileRepository(_db);
        await profile.MergeFromSourceAsync("LIRF", null, null, new[] { ("16L", (int?)3902, (int?)160) });
        var svc = BuildService();
        var stored = (await profile.LoadAsync("LIRF"))!.Runways.Single();

        // Cambiare ident con piste bloccate → rifiutato.
        await Assert.ThrowsAsync<ValidationException>(() => svc.SaveRunwaysAsync("LIRF", new[]
        {
            stored with { Ident = "16R" },
        }));

        // Solo colonne editoriali (ident/lunghezza/bearing invariati) → consentito.
        await svc.SaveRunwaysAsync("LIRF", new[] { stored with { ToraM = "3000" } });
        Assert.Equal("3000", (await profile.LoadAsync("LIRF"))!.Runways.Single().ToraM);
    }

    [Fact]
    public async Task Reimport_Skips_Excluded_Categories()
    {
        var profile = new EfAirportProfileRepository(_db);
        // Stato iniziale editoriale dell'utente: TA 4000 + pista 16L lunga 3902.
        await profile.MergeFromSourceAsync("LIRF", 4000, null, new[] { ("16L", (int?)3902, (int?)160) });

        // Escludo TA e Piste → reimport non deve toccarle, anche se la sorgente fornisce valori diversi.
        await _store.SaveAsync(new ImportPolicySnapshot(false, true, false, true), 1);
        var dir = new FakeDirectory();
        dir.Airports.Add(new SourceAirport("LIRF", "Roma Fiumicino", "LIRR", null, 9000));
        var det = new FakeDetails();
        det.Runways.Add(new SourceRunway("16L", 9999, 160));

        await BuildService(dir, det).ReimportFromSourceAsync("LIRF");

        var data = await profile.LoadAsync("LIRF");
        Assert.Equal(4000, data!.TransitionAltitudeFt);            // TA esclusa: invariata
        Assert.Equal(3902, data.Runways.Single().LengthM);         // Piste escluse: invariate
    }
}
