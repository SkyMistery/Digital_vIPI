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

    /// <summary>Documento mostrato, con la sezione «configurations» e il suo BodyJson (o senza).</summary>
    private static DocumentView Doc(string? configurationsJson = null) => new()
    {
        Title = "LIRP_APP",
        AiracCycle = "2609",
        Sections = new[]
        {
            new SectionView
            {
                Id = "s-1", Title = "Configurazioni", Depth = 0, SectionKey = "configurations",
                Blocks = configurationsJson is null
                    ? Array.Empty<BlockView>()
                    : new[] { new BlockView { Id = 1, Format = BlockFormat.Table, State = RenderState.Expanded, BodyJson = configurationsJson } },
                Children = Array.Empty<SectionView>(),
            },
        },
    };

    [Fact]
    public async Task Frozen_Wins_When_UseFrozen_And_Captured()
    {
        // Reader: solo "frequencies" congelata (2 righe); "aor"/"coordination" Live/assenti → null.
        var reader = new FakeReader { Frozen = { ["frequencies"] = new List<AppFreqRow> { Freq("A"), Freq("B") } } };
        var svc = new AppViewDerivationService(new FakeApp(), reader);

        var d = await svc.ResolveForViewAsync("LIRP_APP", Doc(), useFrozen: true);

        Assert.Equal(2, d.Freqs.Count);   // frozen
        Assert.Empty(d.Aor.Sectors);      // reader null per "aor" → live (AccAorView.Empty)
    }

    [Fact]
    public async Task Live_When_Not_UseFrozen()
    {
        var reader = new FakeReader { Frozen = { ["frequencies"] = new List<AppFreqRow> { Freq("A"), Freq("B") } } };
        var svc = new AppViewDerivationService(new FakeApp(), reader);

        var d = await svc.ResolveForViewAsync("LIRP_APP", Doc(), useFrozen: false);

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
        public Task<MinimaView> DeriveMinimaAsync(string appCallsign, CancellationToken ct = default) =>
            Task.FromResult(MinimaView.Empty);

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
        /// <summary>Configurazioni con cui la pagina ha chiesto la tabella: è ciò che il test vuole osservare.</summary>
        public IReadOnlyList<AccConfiguration>? ConfigsAsked { get; private set; }

        public Task<IReadOnlyList<AccConfigTableView>> DeriveConfigTableAsync(string a, CancellationToken ct = default) =>
            throw new InvalidOperationException("La vista non deve mai chiedere le configurazioni della versione di lavoro.");

        public Task<IReadOnlyList<AccConfigTableView>> DeriveConfigTableAsync(string a, IReadOnlyList<AccConfiguration> configs, CancellationToken ct = default)
        {
            ConfigsAsked = configs;
            return Task.FromResult<IReadOnlyList<AccConfigTableView>>(
                configs.Select(c => new AccConfigTableView(c.Key, c.Name, Array.Empty<AccConfigTableRow>())).ToList());
        }
        public Task<RegulatedSelection> GetRegulatedAsync(string a, CancellationToken ct = default) => throw new NotImplementedException();
        public Task SaveRegulatedAsync(string a, RegulatedSelection s, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SpecialAreaPick>> ListSpecialAreasAsync(string a, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SpecialAreaPick>> ListOtherAccSpecialAreasAsync(string a, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<AccSpecialAreaView>> ResolveRegulatedAreasAsync(RegulatedSelection s, CancellationToken ct = default) => throw new NotImplementedException();
    }

    // ---- doc 13 §3g: la tabella «Configurazioni» viene dal documento mostrato ----

    [Fact]
    public async Task Config_table_is_derived_from_the_configurations_of_the_shown_document()
    {
        var app = new FakeApp();
        var svc = new AppViewDerivationService(app, new FakeReader());
        var shown = Doc("""[{"Key":"nord","Name":"Nord","OpenCallsigns":["LIRP_APP"]}]""");

        var d = await svc.ResolveForViewAsync("LIRP_APP", shown, useFrozen: true);

        var asked = Assert.Single(app.ConfigsAsked!);
        Assert.Equal("nord", asked.Key);
        Assert.Equal("Nord", Assert.Single(d.ConfigTable).ConfigName);
    }

    [Fact]
    public async Task The_working_version_is_never_asked_for_the_configurations()
    {
        // Il difetto era esattamente questo: la pagina chiedeva le configurazioni al service, che risolve la
        // versione di LAVORO (bozza se esiste) — e la pagina pubblica mostrava configurazioni mai pubblicate.
        // Il fake fa esplodere l'overload che legge la versione di lavoro: se qualcuno lo rimette, si vede qui.
        var app = new FakeApp();
        var svc = new AppViewDerivationService(app, new FakeReader());

        await svc.ResolveForViewAsync("LIRP_APP", Doc(), useFrozen: true);

        Assert.Empty(app.ConfigsAsked!);
    }

    [Fact]
    public async Task A_document_without_the_configurations_section_yields_an_empty_table()
    {
        var app = new FakeApp();
        var svc = new AppViewDerivationService(app, new FakeReader());
        var noSection = new DocumentView { Title = "x", AiracCycle = "2609", Sections = Array.Empty<SectionView>() };

        var d = await svc.ResolveForViewAsync("LIRP_APP", noSection, useFrozen: true);

        Assert.Empty(d.ConfigTable);
    }
}
