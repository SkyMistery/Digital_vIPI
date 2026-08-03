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

        public Task<IReadOnlyList<SourceSpecialArea>> GetSpecialAreasAsync(string accIcao, CancellationToken ct = default)
        {
            Calls++;
            if (Throw.Contains(accIcao)) throw new HttpRequestException($"specialAreas: nessuna risposta per {accIcao}.");
            return Task.FromResult<IReadOnlyList<SourceSpecialArea>>(Areas.TryGetValue(accIcao, out var a) ? a : new());
        }

        public Task<IReadOnlyList<SourceCenter>> GetCentersAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SourceCenter>> GetCentersByCountryAsync(string countryId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SourceSubcenter>> GetSubcentersAsync(string accIcao, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
