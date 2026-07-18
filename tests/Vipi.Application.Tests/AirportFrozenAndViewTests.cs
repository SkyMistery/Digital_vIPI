using System.Text.Json;
using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Aeroporto (doc 10 §3e): cattura Frozen della sola sezione derivabile «sids» (solo se Frozen) e risoluzione al view
/// (frozen se pubblica+catturata, sennò derivazione live). Mirror di App/vLOA.
/// </summary>
public class AirportFrozenAndViewTests
{
    private static AirportSidView Sid(string fix) =>
        new(new[] { new AirportSidRowView("07", fix, $"{fix}7A", "—", "—", "—", "—", "—", "—") });

    private static RawSection Sec(int id, string key, RenderMode mode) => new()
    {
        Id = id, Title = key, Depth = 0, SectionKey = key, Order = id, RenderMode = mode,
    };

    private static RawDocument Doc(params RawSection[] roots) =>
        new() { Title = "vIPI LIRF", AiracCycle = "2606", Roots = roots };

    [Fact]
    public async Task Provider_Captures_Only_Frozen_Sids()
    {
        var doc = Doc(
            Sec(10, "sids", RenderMode.Frozen),      // derivata + Frozen → catturata
            Sec(20, "custom", RenderMode.Frozen));   // editoriale/statica → saltata

        var provider = new AirportFrozenSectionProvider(new FakeSid());
        var frozen = await provider.CaptureFrozenAsync("LIRF", doc);

        var kv = Assert.Single(frozen);
        Assert.Equal(10, kv.Key);
        Assert.Contains("ALAXI", kv.Value);
    }

    [Fact]
    public async Task Provider_Skips_Live_Sids()
    {
        var doc = Doc(Sec(10, "sids", RenderMode.Live));   // Live → non catturata (derivata al view)
        var provider = new AirportFrozenSectionProvider(new FakeSid());
        Assert.Empty(await provider.CaptureFrozenAsync("LIRF", doc));
    }

    [Fact]
    public async Task View_Frozen_Wins_When_UseFrozen_And_Captured()
    {
        var reader = new FakeReader { Frozen = { ["sids"] = Sid("FROZEN") } };
        var svc = new AirportViewDerivationService(new FakeSid(), reader);

        var v = await svc.ResolveSidsForViewAsync("LIRF", useFrozen: true);
        Assert.Equal("FROZEN", Assert.Single(v.Rows).Fix);
    }

    [Fact]
    public async Task View_Live_When_Not_UseFrozen()
    {
        var reader = new FakeReader { Frozen = { ["sids"] = Sid("FROZEN") } };
        var svc = new AirportViewDerivationService(new FakeSid(), reader);

        var v = await svc.ResolveSidsForViewAsync("LIRF", useFrozen: false);
        Assert.Equal("ALAXI", Assert.Single(v.Rows).Fix);   // live, reader non consultato
        Assert.False(reader.WasQueried);
    }

    private sealed class FakeSid : IAirportSidDerivationService
    {
        public Task<AirportSidView> DeriveAsync(string icao, CancellationToken ct = default) =>
            Task.FromResult(Sid("ALAXI"));
    }

    private sealed class FakeReader : IFrozenSectionReader
    {
        public Dictionary<string, object> Frozen { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool WasQueried { get; private set; }

        public Task<T?> GetFrozenByKeyAsync<T>(ReleaseTargetType type, string key, string sectionKey, CancellationToken ct = default)
        {
            WasQueried = true;
            return Task.FromResult(Frozen.TryGetValue(sectionKey, out var v) && v is T t ? t : default);
        }

        public Task<string?> GetFrozenJsonAsync(ReleaseTargetType type, string key, int sectionId, CancellationToken ct = default) =>
            Task.FromResult<string?>(null);
        public Task<T?> GetFrozenAsync<T>(ReleaseTargetType type, string key, int sectionId, CancellationToken ct = default) =>
            Task.FromResult<T?>(default);
    }
}
