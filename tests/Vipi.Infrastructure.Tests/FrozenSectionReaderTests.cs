using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Lettura al view del frozen (doc 10 §3d): FrozenSectionReader restituisce il JSON congelato di una sezione dalla
/// release effettiva, e null quando manca la release / la sezione è Live-assente dal payload.
/// </summary>
public class FrozenSectionReaderTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfReleaseRepository _releases = default!;
    private FrozenSectionReader _reader = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _releases = TestReleaseTargets.ReleaseRepo(_db);
        _reader = new FrozenSectionReader(_releases);
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private static string PayloadWithFrozen(params (int SectionId, object Vm)[] frozen)
    {
        var p = new DocReleasePayload
        {
            Doc = new RawDocument { Title = "vLOA", AiracCycle = "2606", Roots = Array.Empty<RawSection>() },
        };
        foreach (var (id, vm) in frozen) p.FrozenSections[id] = JsonSerializer.Serialize(vm);
        return JsonSerializer.Serialize(p);
    }

    [Fact]
    public async Task Reads_Frozen_Json_From_Effective_Release()
    {
        var json = PayloadWithFrozen((5, new { name = "AOR congelata" }));
        await _releases.SaveReleaseAsync(ReleaseTargetType.Vloa, "1", "2607", DateTime.UtcNow.AddSeconds(-5), json, 1, null);

        var frozen = await _reader.GetFrozenAsync<Dictionary<string, string>>(ReleaseTargetType.Vloa, "1", 5);
        Assert.NotNull(frozen);
        Assert.Equal("AOR congelata", frozen!["name"]);
    }

    [Fact]
    public async Task Live_Or_Absent_Section_Returns_Null()
    {
        var json = PayloadWithFrozen((5, new { name = "solo la 5" }));
        await _releases.SaveReleaseAsync(ReleaseTargetType.Vloa, "1", "2607", DateTime.UtcNow.AddSeconds(-5), json, 1, null);

        Assert.Null(await _reader.GetFrozenJsonAsync(ReleaseTargetType.Vloa, "1", 999));   // sezione non catturata (Live/assente)
    }

    [Fact]
    public async Task No_Effective_Release_Returns_Null()
    {
        // Release SCHEDULATA nel futuro: non effettiva ora → nessun frozen servito.
        var json = PayloadWithFrozen((5, new { name = "futura" }));
        await _releases.SaveReleaseAsync(ReleaseTargetType.Vloa, "1", "9901", DateTime.UtcNow.AddYears(1), json, 1, null);

        Assert.Null(await _reader.GetFrozenJsonAsync(ReleaseTargetType.Vloa, "1", 5));
    }
}
