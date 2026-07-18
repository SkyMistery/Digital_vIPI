using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Cattura Frozen delle sezioni derivate (doc 10 §3b): il provider deve serializzare SOLO le sezioni in
/// RenderMode.Frozen la cui chiave è DERIVATA (aor/frequencies/coordination/…), saltando quelle Live (derivate al
/// view) e quelle editoriali (già nei blocchi statici del Doc).
/// </summary>
public class FrozenSectionCaptureTests
{
    private static RawSection Sec(int id, string key, RenderMode mode) => new()
    {
        Id = id, Title = key, Depth = 0, SectionKey = key, Order = id, RenderMode = mode,
    };

    private static RawDocument Doc(params RawSection[] roots) =>
        new() { Title = "vLOA", AiracCycle = "2606", Roots = roots };

    [Fact]
    public async Task Vloa_Captures_Only_Frozen_Derived_Sections()
    {
        var doc = Doc(
            Sec(10, "aor", RenderMode.Frozen),                    // derivata + Frozen → catturata
            Sec(20, "frequencies", RenderMode.Live),             // derivata ma Live → saltata
            Sec(30, "coordination", RenderMode.Frozen),          // derivata + Frozen → catturata
            Sec(40, "operationaltechnique", RenderMode.Frozen)); // editoriale → saltata (già nel Doc)

        var provider = new VloaFrozenSectionProvider(new FakeVloa());
        var frozen = await provider.CaptureFrozenAsync("123", doc);

        Assert.Equal(new[] { 10, 30 }, frozen.Keys.OrderBy(k => k).ToArray());
        Assert.All(frozen.Values, v => Assert.False(string.IsNullOrWhiteSpace(v)));
    }

    [Fact]
    public async Task Vloa_NonNumericKey_CapturesNothing()
    {
        var doc = Doc(Sec(10, "aor", RenderMode.Frozen));
        var provider = new VloaFrozenSectionProvider(new FakeVloa());
        Assert.Empty(await provider.CaptureFrozenAsync("not-a-docid", doc));
    }

    [Fact]
    public async Task Registry_UnknownType_CapturesNothing()
    {
        var registry = new FrozenSectionRegistry(new IFrozenSectionProvider[] { new VloaFrozenSectionProvider(new FakeVloa()) });
        var doc = Doc(Sec(10, "aor", RenderMode.Frozen));
        Assert.Empty(await registry.CaptureAsync(ReleaseTargetType.Airport, "LIRF", doc));
    }

    private sealed class FakeVloa : IVloaDerivationService
    {
        public Task<VloaPairMeta?> GetPairMetaAsync(int docId, CancellationToken ct = default) =>
            Task.FromResult<VloaPairMeta?>(new VloaPairMeta("LIRR", "DAAA", "Roma", "Alger"));
        public Task<VloaAorData> DeriveAorAsync(int docId, CancellationToken ct = default) => Task.FromResult(VloaAorData.Empty);
        public Task<VloaFreqData> DeriveFrequenciesAsync(int docId, CancellationToken ct = default) => Task.FromResult(VloaFreqData.Empty);
        public Task<VloaCoordination> DeriveCoordinationAsync(int docId, CancellationToken ct = default) => Task.FromResult(VloaCoordination.Empty);
        public Task ToggleAorSectorAsync(int docId, string callsign, CancellationToken ct = default) => Task.CompletedTask;
        public Task ToggleFrequencyAsync(int docId, string callsign, CancellationToken ct = default) => Task.CompletedTask;
        public Task ToggleSectionAsync(int docId, string sectionTitle, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> GetHiddenSectionsAsync(int docId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }
}
