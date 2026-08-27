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

        // doc 14 §3c — una lettura sola. La vLOA più grande dell'archivio pesa 221 KB e questo metodo la
        // rileggeva tre volte: più di mezzo megabyte a ogni apertura di pagina.
        Assert.Equal(1, reader.Letture);
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

        /// <summary>Quante volte lo snapshot e' stato chiesto. Deve essere 0 o 1: leggerlo una volta per pagina
        /// e non una per sezione e' il punto del doc 14 §3c, e questo contatore e' la sua prova.</summary>
        public int Letture { get; private set; }
        public bool WasQueried => Letture > 0;

        public Task<FrozenSections> LoadAsync(ReleaseTargetType type, string key, CancellationToken ct = default)
        {
            Letture++;
            // Si passa per il JSON vero, non per gli oggetti: cosi' la prova copre anche la deserializzazione.
            return Task.FromResult(FrozenSections.FromKeys(
                Frozen.ToDictionary(kv => kv.Key, kv => System.Text.Json.JsonSerializer.Serialize(kv.Value, kv.Value.GetType()))));
        }
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
    }
}
