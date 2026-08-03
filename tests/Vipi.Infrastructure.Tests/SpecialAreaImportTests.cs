using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Import aree speciali/regolamentate: gate della policy di import (categoria esclusa = congelamento, niente
/// fetch e soprattutto niente prune) e upsert/prune per-ACC sul repository reale.
/// </summary>
public class SpecialAreaImportTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfAccAdminRepository _repo = default!;
    private EfImportPolicyStore _policy = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _repo = new EfAccAdminRepository(_db);
        _policy = new EfImportPolicyStore(_db);

        await _repo.ImportAsync(new[] { new SourceCenter("LIRR_CTR", "LIRR", "Roma Control", false, "124.000") });
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private static SourceSpecialArea Area(string id, string name = "LI R14A", string? polygon = "[[41,12]]") =>
        new(id, "R", name, "descrizione", "Permanently active", 0, 5000, false, "LIRR", polygon);

    [Fact]
    public async Task Default_policy_imports_areas()
    {
        var dir = new FakeAccDirectory { Areas = { ["LIRR"] = new() { Area("1"), Area("2", "LI R14B") } } };

        var r = await new SpecialAreaImportUseCase(_repo, dir, _policy).RunAsync();

        Assert.Equal(2, r.Created);
        Assert.Equal(2, await _db.SpecialAreas.CountAsync());
        Assert.Equal(1, dir.Calls);
    }

    [Fact]
    public async Task Excluded_category_skips_fetch_and_keeps_existing_areas()
    {
        // Un'area già in archivio da un import precedente.
        await _repo.ImportSpecialAreasAsync(new[] { Area("1") });

        // La sorgente ora non la espone più: con la categoria attiva verrebbe potata.
        var dir = new FakeAccDirectory { Areas = { ["LIRR"] = new() } };
        await _policy.SaveAsync(new ImportPolicySnapshot(true, true, true, true, SpecialAreas: false), 1);

        var r = await new SpecialAreaImportUseCase(_repo, dir, _policy).RunAsync();

        Assert.Equal(0, dir.Calls);                                  // nessuna fetch
        Assert.Equal(SpecialAreaImportResult.Empty, r);
        Assert.Equal(1, await _db.SpecialAreas.CountAsync());        // e nessun prune: l'area resta
    }

    [Fact]
    public async Task Second_run_skips_the_detail_of_areas_with_a_fresh_shape()
    {
        var dir = new FakeAccDirectory { Areas = { ["LIRR"] = new() { Area("1"), Area("2", "LI R14B") } } };
        var sut = new SpecialAreaImportUseCase(_repo, dir, _policy);

        await sut.RunAsync();                    // primo giro: shape assenti → dettaglio per tutte
        Assert.Empty(dir.SkippedDetails);

        dir.SkippedDetails.Clear();
        await sut.RunAsync();                    // secondo giro: shape in archivio e fresche → dettaglio saltato

        Assert.Equal(new[] { "1", "2" }, dir.SkippedDetails.OrderBy(x => x));
        // La shape salvata sopravvive al giro senza dettaglio (l'upsert non azzera su null).
        Assert.All(await _db.SpecialAreas.AsNoTracking().ToListAsync(), s => Assert.Equal("[[41,12]]", s.RegionMapPolygon));
    }

    [Fact]
    public async Task Area_without_shape_is_not_skipped()
    {
        var dir = new FakeAccDirectory { Areas = { ["LIRR"] = new() { Area("1", polygon: null), Area("2", "LI R14B") } } };
        var sut = new SpecialAreaImportUseCase(_repo, dir, _policy);

        await sut.RunAsync();
        dir.SkippedDetails.Clear();
        await sut.RunAsync();

        // Solo la 2 ha una shape in archivio: la 1 va ri-chiesta finché non arriva.
        Assert.Equal(new[] { "2" }, dir.SkippedDetails);
    }

    [Fact]
    public async Task Failed_acc_does_not_prune_its_areas()
    {
        await _repo.ImportSpecialAreasAsync(new[] { Area("1") });
        var dir = new FakeAccDirectory { Throw = { "LIRR" } };

        var r = await new SpecialAreaImportUseCase(_repo, dir, _policy).RunAsync();

        Assert.Equal("LIRR", Assert.Single(r.Failures).AccCode);
        Assert.Equal(1, await _db.SpecialAreas.CountAsync());   // fetch fallita ⇒ nessuna cancellazione
    }

    private sealed class FakeAccDirectory : IAccDirectory
    {
        public Dictionary<string, List<SourceSpecialArea>> Areas { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Throw { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int Calls { get; private set; }

        /// <summary>Id per cui il chiamante ha detto di saltare il dettaglio, come li ha visti il client.</summary>
        public List<string> SkippedDetails { get; } = new();

        public Task<IReadOnlyList<SourceSpecialArea>> GetSpecialAreasAsync(
            string accIcao, IReadOnlySet<string> skipDetailIds, CancellationToken ct = default)
        {
            Calls++;
            SkippedDetails.AddRange(skipDetailIds);
            if (Throw.Contains(accIcao)) throw new HttpRequestException($"specialAreas: nessuna risposta per {accIcao}.");

            // Come il client reale: per le aree in skip il dettaglio non si chiama, quindi la shape torna null.
            var all = Areas.TryGetValue(accIcao, out var a) ? a : new();
            return Task.FromResult<IReadOnlyList<SourceSpecialArea>>(
                all.Select(x => skipDetailIds.Contains(x.IvaoId) ? x with { RegionMapPolygon = null } : x).ToList());
        }

        public Task<IReadOnlyList<SourceCenter>> GetCentersAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SourceCenter>> GetCentersByCountryAsync(string countryId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SourceSubcenter>> GetSubcentersAsync(string accIcao, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
