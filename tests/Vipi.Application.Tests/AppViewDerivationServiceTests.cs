using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Risoluzione al view delle derivate APP (doc 10 §3d): con useFrozen + release effettiva si legge l'output CONGELATO
/// (by-key da payload.Doc); una sezione Live/assente ricade su live; con useFrozen=false si deriva sempre live e il
/// reader frozen non è consultato.
/// </summary>
public class AppViewDerivationServiceTests
{
    private static AppFreqRow Freq(string cs) => new(null, cs, cs, "121.500", "", true, false);

    [Fact]
    public async Task Frozen_Wins_When_UseFrozen_And_Captured()
    {
        // Reader: solo "frequencies" congelata (2 righe); "aor"/"coordination" Live/assenti → null.
        var reader = new FakeReader { Frozen = { ["frequencies"] = new List<AppFreqRow> { Freq("A"), Freq("B") } } };
        var svc = new AppViewDerivationService(new FakeApp(), reader);

        var d = await svc.ResolveForViewAsync("LIRP_APP", useFrozen: true);

        Assert.Equal(2, d.Freqs.Count);   // frozen
        Assert.Empty(d.Aor.Sectors);      // reader null per "aor" → live (AccAorView.Empty)
    }

    [Fact]
    public async Task Live_When_Not_UseFrozen()
    {
        var reader = new FakeReader { Frozen = { ["frequencies"] = new List<AppFreqRow> { Freq("A"), Freq("B") } } };
        var svc = new AppViewDerivationService(new FakeApp(), reader);

        var d = await svc.ResolveForViewAsync("LIRP_APP", useFrozen: false);

        Assert.Single(d.Freqs);            // live, reader non consultato
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

    private sealed class FakeApp : IAppDocumentService
    {
        public static readonly List<AppFreqRow> LiveFreqs = new() { Freq("LIVE") };

        public Task<IReadOnlyList<AppFreqRow>> DeriveFrequenciesAsync(string appCallsign, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AppFreqRow>>(LiveFreqs);
        public Task<AppCoordination> DeriveCoordinationAsync(string appCallsign, CancellationToken ct = default) =>
            Task.FromResult(AppCoordination.Empty);
        public Task<AccAorView> GetAorViewAsync(string appCallsign, CancellationToken ct = default) =>
            Task.FromResult(AccAorView.Empty);

        // Resto non usato dal resolver.
        public Task<int> EnsureAsync(string a, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<LinkableFrequencyRow>> ListLinkableFrequenciesAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<AorExtraShapes> GetAorCustomizationAsync(string a, CancellationToken ct = default) => throw new NotImplementedException();
        public Task SaveAorCustomizationAsync(string a, AorExtraShapes d, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SectorShapePick>> ListSelectableSectorShapesAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<AppSeparationRow>> GetSeparationsAsync(string a, CancellationToken ct = default) => throw new NotImplementedException();
        public Task SaveSeparationsAsync(string a, IReadOnlyList<AppSeparationRow> r, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<AppVfrContent> GetVfrAsync(string a, CancellationToken ct = default) => throw new NotImplementedException();
        public Task SaveVfrAsync(string a, AppVfrContent c, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<AppDocumentIdentity?> GetIdentityAsync(string a, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<DocumentProfileData> GetOverridesAsync(string a, CancellationToken ct = default) => throw new NotImplementedException();
        public Task SaveFrequencyOrderAsync(string a, IReadOnlyList<AppFreqOrderOverride> o, CancellationToken ct = default) => throw new NotImplementedException();
        public Task SaveFrequencyLinksAsync(string a, IReadOnlyList<int> s, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<AccSectorPick>> ListSectorsAsync(string a, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<AccConfiguration>> GetConfigurationsAsync(string a, CancellationToken ct = default) => throw new NotImplementedException();
        public Task SaveConfigurationsAsync(string a, IReadOnlyList<AccConfiguration> c, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<AccConfigTableView>> DeriveConfigTableAsync(string a, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<RegulatedSelection> GetRegulatedAsync(string a, CancellationToken ct = default) => throw new NotImplementedException();
        public Task SaveRegulatedAsync(string a, RegulatedSelection s, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SpecialAreaPick>> ListSpecialAreasAsync(string a, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SpecialAreaPick>> ListOtherAccSpecialAreasAsync(string a, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<AccSpecialAreaView>> ResolveRegulatedAreasAsync(RegulatedSelection s, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
