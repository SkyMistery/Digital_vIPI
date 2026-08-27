using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Aeroporto: cattura Frozen delle sezioni derivate (solo quelle Frozen) e risoluzione al view — frozen se
/// pubblica e catturata, sennò derivazione live. Mirror di App/vLOA.
/// <para>Fino alla carta 2026-08-26 la sola sezione derivabile era <c>sids</c>, perché tutto il resto del
/// documento era già <b>cotto</b> nei blocchi. Ora le sezioni fisse sono ancore senza corpo, e se non si
/// congelassero qui pubblicare non fisserebbe più niente.</para>
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

    private static AirportFrozenSectionProvider Provider(AirportData? data = null) =>
        new(new FakeProfilo(data), new FakeSettori(), new FakeSid());

    private static AirportViewDerivationService Derivazione(FakeReader reader, AirportData? data = null) =>
        new(new FakeProfilo(data), new FakeSettori(), new FakeSid(), reader);

    [Fact]
    public async Task Provider_Captures_Only_Frozen_Sids()
    {
        var doc = Doc(
            Sec(10, "sids", RenderMode.Frozen),      // derivata + Frozen → catturata
            Sec(20, "custom", RenderMode.Frozen));   // editoriale/statica → saltata

        var frozen = await Provider().CaptureFrozenAsync("LIRF", doc);

        var kv = Assert.Single(frozen);
        Assert.Equal(10, kv.Key);
        Assert.Contains("ALAXI", kv.Value);
    }

    [Fact]
    public async Task Provider_Skips_Live_Sids()
    {
        var doc = Doc(Sec(10, "sids", RenderMode.Live));   // Live → non catturata (derivata al view)
        Assert.Empty(await Provider().CaptureFrozenAsync("LIRF", doc));
    }

    [Fact]
    public async Task Provider_Captures_Every_Frozen_Airport_Section()
    {
        // Il senso della carta: pubblicare deve fissare anche piste, frequenze, TA e regole. Prima erano cotte
        // nei blocchi e lo snapshot se le portava dietro da sé; ora sono ancore vuote.
        var doc = Doc(
            Sec(10, "runwayrules", RenderMode.Frozen),
            Sec(20, "transition", RenderMode.Frozen),
            Sec(30, "frequencies", RenderMode.Frozen),
            Sec(40, "runways", RenderMode.Frozen));

        var frozen = await Provider(Profilo()).CaptureFrozenAsync("LIRF", doc);

        Assert.Equal(4, frozen.Count);
        Assert.Contains("vento in coda", frozen[10]);
        Assert.Contains("6000", frozen[20]);
        Assert.Contains("118.700", frozen[30]);
        Assert.Contains("16L", frozen[40]);
    }

    [Fact]
    public async Task Il_meteo_non_si_congela_mai()
    {
        // ⚠️ Anche messa Frozen a mano: un METAR dentro uno snapshot di release non è un documento d'archivio,
        // è meteo scaduto spacciato per attuale. L'editor non offre il toggle, ma la guardia sta anche qui.
        var doc = Doc(Sec(10, "weather", RenderMode.Frozen));
        Assert.Empty(await Provider(Profilo()).CaptureFrozenAsync("LIRF", doc));
    }

    [Fact]
    public async Task View_Frozen_Wins_When_UseFrozen_And_Captured()
    {
        var reader = new FakeReader { Frozen = { ["sids"] = Sid("FROZEN") } };
        var v = await Derivazione(reader).ResolveSidsForViewAsync("LIRF", useFrozen: true);
        Assert.Equal("FROZEN", Assert.Single(v.Rows).Fix);
    }

    [Fact]
    public async Task View_Live_When_Not_UseFrozen()
    {
        var reader = new FakeReader { Frozen = { ["sids"] = Sid("FROZEN") } };
        var v = await Derivazione(reader).ResolveSidsForViewAsync("LIRF", useFrozen: false);
        Assert.Equal("ALAXI", Assert.Single(v.Rows).Fix);   // live, reader non consultato
        Assert.False(reader.WasQueried);
    }

    [Fact]
    public async Task Una_sezione_congelata_e_una_live_convivono_nella_stessa_vista()
    {
        // È il caso normale dopo la carta: il meteo e le SID sono live, le piste congelate all'ultima release.
        var reader = new FakeReader
        {
            Frozen = { ["runways"] = new AirportRunwaysView(new[] { new AirportRunwayRowView("34R", 3900, "—", "—", "—", "—", "—") }) },
        };

        var v = await Derivazione(reader, Profilo()).ResolveForViewAsync("LIRF", useFrozen: true);

        Assert.Equal("34R", Assert.Single(v.Runways.Rows).Ident);   // congelata: la pista di un altro ciclo
        Assert.Equal("16L", Assert.Single(Profilo().Runways).Ident); // ...mentre il profilo dice 16L
        Assert.Equal("ALAXI", Assert.Single(v.Sids.Rows).Fix);      // live
        Assert.Equal(6000, v.Transition.TransitionAltitudeFt);      // live: non c'è payload congelato

        // doc 14 §3c — una lettura sola per TUTTE le sezioni, SID comprese. Erano cinque, e la quinta
        // arrivava per una strada diversa: ResolveForView chiamava il metodo pubblico delle SID, che
        // ricominciava da capo.
        Assert.Equal(1, reader.Letture);
    }

    [Fact]
    public async Task Senza_release_effettiva_si_ricade_su_live()
    {
        // ⚠️ È ciò che rende morbido il passaggio: gli aeroporti già pubblicati non hanno un payload congelato
        // per le chiavi nuove, quindi continuano a leggersi live finché non si ripubblica.
        var v = await Derivazione(new FakeReader(), Profilo()).ResolveForViewAsync("LIRF", useFrozen: true);

        Assert.Equal("16L", Assert.Single(v.Runways.Rows).Ident);
        Assert.Equal(6000, v.Transition.TransitionAltitudeFt);
        Assert.Equal("118.700", Assert.Single(v.Frequencies.Rows).Frequency);
    }

    private static AirportData Profilo() => new()
    {
        AirportId = 1, Icao = "LIRF", Name = "Roma Fiumicino", AccCode = "LIRR",
        TransitionAltitudeFt = 6000,
        TransitionLevels = new[] { new TlRow(1, 1013, null, "FL70") },
        Runways = new[] { new RunwayRow(1, "16L", 3902, 160, null, null, "ILS CAT III", null, null) },
        Rules = new[] { new RunwayRuleRow(1, "16R", "16L", "Sud", 5, null, RunwaySurface.Any, "vento da sud") },
        Sids = Array.Empty<SidRow>(),
        Links = Array.Empty<FrequencyLinkRow>(),
    };

    private sealed class FakeProfilo : IAirportProfileReader
    {
        private readonly AirportData? _data;
        public FakeProfilo(AirportData? data) => _data = data;
        public Task<AirportData?> LoadAsync(string icao, CancellationToken ct = default) => Task.FromResult(_data);

        /// <summary>Non serve a questi test — qui si prova la vista del singolo aeroporto, non l'elenco.</summary>
        public Task<IReadOnlyDictionary<string, PisteDiAeroporto>> ListRunwayDataAsync(
            IReadOnlyCollection<string> icaos, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, PisteDiAeroporto>>(
                new Dictionary<string, PisteDiAeroporto>(StringComparer.OrdinalIgnoreCase));
    }

    private sealed class FakeSettori : IAirportSectorService
    {
        public Task<IReadOnlyList<AirportSectorRow>> ListByAirportAsync(string icao, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AirportSectorRow>>(new[]
            {
                new AirportSectorRow(1, "LIRF_TWR", "LIRF", "LIRR", "TWR", null, "118.700", null, null, false, false, true, false),
                // Nascosto: sta nel catalogo per l'amministrazione dei settori, non nel documento.
                new AirportSectorRow(2, "LIRF_X_GND", "LIRF", "LIRR", "GND", "X", "121.700", null, null, true, false, false, false),
            });

        public Task<AirportSectorImportResult> ImportFromSourceAsync(string icao, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<int> ApplyGithubTwrShapesAsync(string icao, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SetHiddenAsync(int id, bool hidden, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SetLimitsAsync(int id, int? lower, int? upper, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SetPrimaryAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SetAccAppAsync(int id, bool isAccApp, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeSid : IAirportSidDerivationService
    {
        public Task<AirportSidView> DeriveAsync(string icao, CancellationToken ct = default) =>
            Task.FromResult(Sid("ALAXI"));
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
}
