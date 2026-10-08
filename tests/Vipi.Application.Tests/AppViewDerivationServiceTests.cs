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

        // doc 14 §3c — lo snapshot si legge UNA volta, non una per sezione. Prima erano quattro letture
        // dello stesso payload di release, ognuna con la sua query e la sua deserializzazione completa.
        Assert.Equal(1, reader.Letture);
    }

    [Fact]
    public async Task Live_When_Not_UseFrozen()
    {
        var reader = new FakeReader { Frozen = { ["frequencies"] = new List<AppFreqRow> { Freq("A"), Freq("B") } } };
        var svc = new AppViewDerivationService(new FakeApp { DellaStruttura = Array.Empty<AccConfiguration>() }, reader);

        var d = await svc.ResolveForViewAsync("LIRP_APP", Doc(), useFrozen: false);

        Assert.Single(d.Freqs);            // live, reader non consultato
        Assert.False(reader.WasQueried);
    }

    private sealed class FakeReader : IFrozenSectionReader
    {
        public Dictionary<string, object> Frozen { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Quante volte lo snapshot e' stato chiesto. Deve essere 0 o 1: leggerlo una volta per pagina
        /// e non una per sezione e' il punto del doc 14 §3c, e questo contatore e' la sua prova.</summary>
        public int Letture { get; private set; }
        public bool WasQueried => Letture > 0;

        /// <summary>Vero = lo snapshot è di una release nata con le configurazioni in Struttura, e non ha
        /// nessuna sezione congelata (la sezione <c>configurations</c> è Live).</summary>
        public bool NuovaSenzaCongelate { get; set; }

        public Task<FrozenSections> LoadAsync(ReleaseTargetType type, string key, CancellationToken ct = default)
        {
            Letture++;
            if (NuovaSenzaCongelate)
                return Task.FromResult(FrozenSections.FromSnapshot(null, null, configurazioniDallaStruttura: true));
            // Si passa per il JSON vero, non per gli oggetti: cosi' la prova copre anche la deserializzazione.
            return Task.FromResult(FrozenSections.FromKeys(
                Frozen.ToDictionary(kv => kv.Key, kv => System.Text.Json.JsonSerializer.Serialize(kv.Value, kv.Value.GetType()))));
        }
    }

    private sealed class FakeApp : IAppDocumentService
    {
        public static readonly List<AppFreqRow> LiveFreqs = new() { Freq("LIVE") };

        public Task<IReadOnlyList<AppFreqRow>> DeriveFrequenciesAsync(string appCallsign, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AppFreqRow>>(LiveFreqs);
        public Task<AppCoordination> DeriveCoordinationAsync(string appCallsign, CancellationToken ct = default) =>
            Task.FromResult(AppCoordination.Empty);
        public Task<AccAorView> GetAorViewAsync(string appCallsign, CancellationToken ct = default) =>
            throw new InvalidOperationException("La vista non deve mai chiedere la mappa AoR della versione di lavoro.");

        /// <summary>Personalizzazione e configurazioni con cui la pagina ha chiesto la mappa.</summary>
        public (AorExtraShapes Custom, IReadOnlyList<AccConfiguration> Configs)? AorAsked { get; private set; }

        public Task<AccAorView> GetAorViewAsync(string appCallsign, AorExtraShapes custom,
            IReadOnlyList<AccConfiguration> configs, CancellationToken ct = default)
        {
            AorAsked = (custom, configs);
            return Task.FromResult(AccAorView.Empty);
        }
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
        public Task<AppDocumentIdentity?> GetIdentityAsync(string a, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<string>> WhereCitedAsync(string a, string c, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<DocumentProfileData> GetOverridesAsync(string a, CancellationToken ct = default) => throw new NotImplementedException();
        public Task SaveFrequencyOrderAsync(string a, IReadOnlyList<AppFreqOrderOverride> o, CancellationToken ct = default) => throw new NotImplementedException();
        public Task SaveFrequencyLinksAsync(string a, IReadOnlyList<int> s, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<AccSectorPick>> ListSectorsAsync(string a, CancellationToken ct = default) => throw new NotImplementedException();
        /// <summary>Le configurazioni che la Struttura ha adesso per l'ente; null = chiederle è un errore del test.</summary>
        public IReadOnlyList<AccConfiguration>? DellaStruttura { get; set; }
        public Task<IReadOnlyList<AccConfiguration>> GetConfigurationsAsync(string a, CancellationToken ct = default) =>
            DellaStruttura is { } s
                ? Task.FromResult(s)
                : throw new InvalidOperationException("Una release di prima non deve chiedere le configurazioni alla Struttura.");
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

    /// <summary>
    /// 🔴 T-028 (revisione del 13 settembre 2026): con la sezione AoR in Live la mappa si chiedeva al service, che
    /// legge shape extra, colori e configurazioni dalla versione di LAVORO — la pagina pubblica mostrava la
    /// personalizzazione di una bozza mai pubblicata. Era il gemello, rimasto indietro, della tabella qui sopra.
    /// </summary>
    [Fact]
    public async Task La_mappa_AoR_live_usa_personalizzazione_e_configurazioni_del_documento_mostrato()
    {
        var app = new FakeApp();
        var svc = new AppViewDerivationService(app, new FakeReader());
        var shown = new DocumentView
        {
            Title = "LIRP_APP", AiracCycle = "2609",
            Sections = new[]
            {
                Doc("""[{"Key":"nord","Name":"Nord","OpenCallsigns":["LIRP_APP"]}]""").Sections[0],
                new SectionView
                {
                    Id = "s-2", Title = "AoR", Depth = 0, SectionKey = "aor", Children = Array.Empty<SectionView>(),
                    Blocks = new[] { new BlockView { Id = 2, Format = BlockFormat.Table, State = RenderState.Expanded,
                        BodyJson = """{"Callsigns":["LIRP_TWR"],"Colors":{"LIRP_APP":"#123456"}}""" } },
                },
            },
        };

        await svc.ResolveForViewAsync("LIRP_APP", shown, useFrozen: true);

        var (custom, configs) = app.AorAsked!.Value;
        Assert.Equal("LIRP_TWR", Assert.Single(custom.Callsigns));
        Assert.Equal("#123456", custom.Colors["LIRP_APP"]);
        Assert.Equal("nord", Assert.Single(configs).Key);
    }

    // ---- 8 ottobre 2026: le configurazioni stanno in Struttura (carta 2026-10-08-configurazioni-possibili) ----

    private static IReadOnlyList<AccConfiguration> Una(string chiave) =>
        new[] { new AccConfiguration { Key = chiave, Name = chiave, Open = { new AccConfigOpen { Callsign = "LIRP_APP" } } } };

    private const string RimastaNelDocumento = """[{"Key":"vecchia","Name":"Vecchia","OpenCallsigns":["LIRP_APP"]}]""";

    /// <summary>La versione di lavoro (anteprima di una bozza) mostra la Struttura di adesso: una bozza delle
    /// configurazioni non esiste più, e il <c>BodyJson</c> rimasto nel documento non conta.</summary>
    [Fact]
    public async Task La_versione_di_lavoro_legge_le_configurazioni_dalla_struttura()
    {
        var app = new FakeApp { DellaStruttura = Una("struttura") };
        var svc = new AppViewDerivationService(app, new FakeReader());

        var d = await svc.ResolveForViewAsync("LIRP_APP", Doc(RimastaNelDocumento), useFrozen: false);

        Assert.Equal("struttura", Assert.Single(app.ConfigsAsked!).Key);
        Assert.Equal("struttura", Assert.Single(app.AorAsked!.Value.Configs).Key);
        Assert.Equal("struttura", Assert.Single(d.ConfigTable).ConfigKey);
    }

    /// <summary>Una release nuova con la sezione congelata dice le configurazioni di allora: né la Struttura di
    /// adesso, né il <c>BodyJson</c> rimasto nel documento.</summary>
    [Fact]
    public async Task Una_release_nuova_congelata_usa_la_voce_congelata()
    {
        var app = new FakeApp();   // la Struttura non va chiesta
        var reader = new FakeReader { Frozen = { ["configurations"] = Una("congelata").ToList() } };
        var svc = new AppViewDerivationService(app, reader);

        await svc.ResolveForViewAsync("LIRP_APP", Doc(RimastaNelDocumento), useFrozen: true);

        Assert.Equal("congelata", Assert.Single(app.ConfigsAsked!).Key);
    }

    /// <summary>🔴 Una release nuova con la sezione Live non ha una voce congelata — ma non è una release di prima:
    /// lo dice il suo segno. Segue la Struttura, e il <c>BodyJson</c> rimasto nel documento NON passa per buono.</summary>
    [Fact]
    public async Task Una_release_nuova_con_la_sezione_live_segue_la_struttura()
    {
        var app = new FakeApp { DellaStruttura = Una("struttura") };
        var svc = new AppViewDerivationService(app, new FakeReader { NuovaSenzaCongelate = true });

        await svc.ResolveForViewAsync("LIRP_APP", Doc(RimastaNelDocumento), useFrozen: true);

        Assert.Equal("struttura", Assert.Single(app.ConfigsAsked!).Key);
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
