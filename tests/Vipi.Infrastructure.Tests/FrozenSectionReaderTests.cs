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

        var lotto = await _reader.LoadAsync(ReleaseTargetType.Vloa, "1");
        var frozen = lotto.Get<Dictionary<string, string>>(5);
        Assert.NotNull(frozen);
        Assert.Equal("AOR congelata", frozen!["name"]);
    }

    [Fact]
    public async Task Live_Or_Absent_Section_Returns_Null()
    {
        var json = PayloadWithFrozen((5, new { name = "solo la 5" }));
        await _releases.SaveReleaseAsync(ReleaseTargetType.Vloa, "1", "2607", DateTime.UtcNow.AddSeconds(-5), json, 1, null);

        var lotto = await _reader.LoadAsync(ReleaseTargetType.Vloa, "1");
        Assert.False(lotto.IsEmpty);                                              // la 5 c'è
        Assert.Null(lotto.Get<Dictionary<string, string>>(999));                   // la 999 no (Live/assente)
    }

    [Fact]
    public async Task No_Effective_Release_Returns_Null()
    {
        // Release SCHEDULATA nel futuro: non effettiva ora → nessun frozen servito.
        var json = PayloadWithFrozen((5, new { name = "futura" }));
        await _releases.SaveReleaseAsync(ReleaseTargetType.Vloa, "1", "9901", DateTime.UtcNow.AddYears(1), json, 1, null);

        var lotto = await _reader.LoadAsync(ReleaseTargetType.Vloa, "1");
        Assert.True(lotto.IsEmpty);
        Assert.Null(lotto.Get<Dictionary<string, string>>(5));
    }

    // --- By-key: risolve l'Id della sezione derivabile+Frozen da payload.Doc (doc a sezione unica App/vLOA) ---

    private static RawSection Derived(int id, string key, RenderMode mode) => new()
    {
        Id = id, Title = key, Depth = 0, SectionKey = key, Order = id, RenderMode = mode,
    };

    private static string PayloadWithDoc(RawSection[] roots, params (int SectionId, object Vm)[] frozen)
    {
        var p = new DocReleasePayload
        {
            Doc = new RawDocument { Title = "vLOA", AiracCycle = "2606", Roots = roots },
        };
        foreach (var (id, vm) in frozen) p.FrozenSections[id] = JsonSerializer.Serialize(vm);
        return JsonSerializer.Serialize(p);
    }

    [Fact]
    public async Task ByKey_Resolves_SectionId_From_PayloadDoc()
    {
        var roots = new[] { Derived(7, "aor", RenderMode.Frozen), Derived(9, "frequencies", RenderMode.Frozen) };
        var json = PayloadWithDoc(roots, (7, new { name = "AOR congelata" }), (9, new { name = "freq congelata" }));
        await _releases.SaveReleaseAsync(ReleaseTargetType.App, "LIRP_APP", "2607", DateTime.UtcNow.AddSeconds(-5), json, 1, null);

        var lotto = await _reader.LoadAsync(ReleaseTargetType.App, "LIRP_APP");
        Assert.Equal("AOR congelata", lotto.Get<Dictionary<string, string>>("aor")!["name"]);
        // ⚠️ Il punto del lotto: le due sezioni escono dalla STESSA lettura, non da due.
        Assert.Equal("freq congelata", lotto.Get<Dictionary<string, string>>("frequencies")!["name"]);
    }

    [Fact]
    public async Task ByKey_Live_Section_Returns_Null()
    {
        // Sezione Live: assente da FrozenDerived e da FrozenSections → by-key null → il chiamante deriva live.
        var roots = new[] { Derived(7, "aor", RenderMode.Live) };
        var json = PayloadWithDoc(roots);
        await _releases.SaveReleaseAsync(ReleaseTargetType.App, "LIRP_APP", "2607", DateTime.UtcNow.AddSeconds(-5), json, 1, null);

        var lotto = await _reader.LoadAsync(ReleaseTargetType.App, "LIRP_APP");
        Assert.Null(lotto.Get<Dictionary<string, string>>("aor"));
    }

    [Fact]
    public async Task Un_payload_illeggibile_non_fa_cadere_la_pagina()
    {
        // Vale la stessa regola di prima: se lo snapshot non si legge, si deriva live. Non si solleva addosso
        // a un lettore anonimo per un JSON rotto in archivio.
        await _releases.SaveReleaseAsync(ReleaseTargetType.Vloa, "1", "2607", DateTime.UtcNow.AddSeconds(-5),
            "{ questo non e' json", 1, null);

        var lotto = await _reader.LoadAsync(ReleaseTargetType.Vloa, "1");
        Assert.True(lotto.IsEmpty);
        Assert.Null(lotto.Get<Dictionary<string, string>>("aor"));
    }

    [Fact]
    public void Il_lotto_vuoto_risponde_null_a_tutto()
    {
        Assert.True(FrozenSections.Empty.IsEmpty);
        Assert.Null(FrozenSections.Empty.Get<Dictionary<string, string>>(1));
        Assert.Null(FrozenSections.Empty.Get<Dictionary<string, string>>("aor"));
        Assert.True(FrozenSections.FromKeys(null).IsEmpty);
        Assert.True(FrozenSections.FromSnapshot(null, null).IsEmpty);
    }
}
