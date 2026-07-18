using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Risoluzione al view delle derivate vLOA (doc 10 §3d): con useFrozen + release effettiva si legge l'output CONGELATO
/// (by-key da payload.Doc); una sezione Live/assente ricade su live; con useFrozen=false si deriva sempre live e il
/// reader frozen non è consultato.
/// </summary>
public class VloaViewDerivationServiceTests
{
    private static VloaFreqData FrozenFreq => new("FROZEN", "FROZEN", "", "", Array.Empty<VloaFreqRow>());

    [Fact]
    public async Task Frozen_Wins_When_UseFrozen_And_Captured()
    {
        var reader = new FakeReader { Frozen = { ["frequencies"] = FrozenFreq } };
        var svc = new VloaViewDerivationService(new FakeVloa(), reader);

        var d = await svc.ResolveForViewAsync(42, useFrozen: true);

        Assert.Equal("FROZEN", d.Freq.HomeAcc);   // frozen
        Assert.Empty(d.Aor.Map.Sectors);          // "aor" null → live (VloaAorData.Empty)
    }

    [Fact]
    public async Task Live_When_Not_UseFrozen()
    {
        var reader = new FakeReader { Frozen = { ["frequencies"] = FrozenFreq } };
        var svc = new VloaViewDerivationService(new FakeVloa(), reader);

        var d = await svc.ResolveForViewAsync(42, useFrozen: false);

        Assert.Equal("", d.Freq.HomeAcc);   // live (Empty), reader non consultato
        Assert.False(reader.WasQueried);
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

    private sealed class FakeVloa : IVloaDerivationService
    {
        public Task<VloaPairMeta?> GetPairMetaAsync(int docId, CancellationToken ct = default) =>
            Task.FromResult<VloaPairMeta?>(null);
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
