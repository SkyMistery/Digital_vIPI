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

    /// <summary>Lo stesso provider, ma per l'edizione MILITARE dello scalo.</summary>
    private static AirportFrozenSectionProvider ProviderMil(AirportData? data = null) =>
        new(new FakeProfilo(data), new FakeSettori(), new FakeSid(), ReleaseTargetType.AirportMil);

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

    /// <summary>
    /// La cattura congela le SID al ciclo della RELEASE, non a quello di oggi: lo dice il
    /// <c>ShapeReleaseContext</c> che <c>ReleaseService</c> apre attorno allo snapshot. Una SID compare solo
    /// dal ciclo successivo al prelievo, quindi congelando «adesso» una release programmata al 2610 ci si
    /// scriveva dentro la tabella di oggi — e la release usciva con meno righe di quante ne avrà.
    /// </summary>
    [Fact]
    public async Task Capture_Freezes_Sids_At_The_Release_Cycle()
    {
        var sid = new FakeSid();
        var ciclo = new ShapeReleaseContext();
        var provider = new AirportFrozenSectionProvider(new FakeProfilo(null), new FakeSettori(), sid,
            ReleaseTargetType.Airport, cicloDiRilascio: ciclo);
        var doc = Doc(Sec(10, "sids", RenderMode.Frozen));

        using (ciclo.Capturing("2610"))
            await provider.CaptureFrozenAsync("LIRF", doc);

        Assert.Equal("2610", sid.CicloChiesto);
    }

    /// <summary>Fuori dal congelamento il contesto è nullo e si guarda al ciclo corrente: il comportamento
    /// di sempre, che nessuna release già scritta deve vedersi cambiare sotto.</summary>
    [Fact]
    public async Task Capture_Outside_A_Release_Asks_For_The_Current_Cycle()
    {
        var sid = new FakeSid();
        var provider = new AirportFrozenSectionProvider(new FakeProfilo(null), new FakeSettori(), sid);

        await provider.CaptureFrozenAsync("LIRF", Doc(Sec(10, "sids", RenderMode.Frozen)));

        Assert.Null(sid.CicloChiesto);
    }

    /// <summary>
    /// L'anteprima di una release passa il SUO ciclo alla derivazione live. È il caso vero: le derivate in
    /// anteprima si rendono live, e senza il ciclo la tabella SID era quella di oggi invece di quella che
    /// uscirà — chi guarda un'anteprima chiede «come sarà», non «com'è».
    /// </summary>
    [Fact]
    public async Task View_Forwards_The_Preview_Cycle_To_The_Live_Derivation()
    {
        var sid = new FakeSid();
        var derivazione = new AirportViewDerivationService(new FakeProfilo(Profilo()), new FakeSettori(), sid,
            new FakeReader());

        await derivazione.ResolveForViewAsync("LIRF", useFrozen: false, ReleaseTargetType.Airport, atCycle: "2610");
        Assert.Equal("2610", sid.CicloChiesto);

        // Vista pubblica e bozza: nessun ciclo chiesto, si guarda ad «adesso».
        await derivazione.ResolveForViewAsync("LIRF", useFrozen: false, ReleaseTargetType.Airport);
        Assert.Null(sid.CicloChiesto);
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

        var v = await Derivazione(reader, Profilo()).ResolveForViewAsync("LIRF", useFrozen: true,
                                                                          ReleaseTargetType.Airport);

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
        var v = await Derivazione(new FakeReader(), Profilo()).ResolveForViewAsync("LIRF", useFrozen: true,
                                                                                   ReleaseTargetType.Airport);

        Assert.Equal("16L", Assert.Single(v.Runways.Rows).Ident);
        Assert.Equal(6000, v.Transition.TransitionAltitudeFt);
        Assert.Equal("118.700", Assert.Single(v.Frequencies.Rows).Frequency);
    }

    // ---- L'EDIZIONE MILITARE dello stesso scalo (carta vSOP militari §2, correzione del 29 agosto 2026) ----
    //
    // ⚠️ Le tabelle sono le STESSE — piste, quote e frequenze di un campo sono quelle, qualunque documento
    // le mostri — ma le RELEASE sono due, e quindi due sono gli snapshot. Il motore di proiezione resta uno
    // solo: quel che cambia è il bersaglio da cui si legge e su cui si cattura.

    [Fact]
    public void Il_provider_MILITARE_e_lo_STESSO_motore_con_un_bersaglio_diverso()
    {
        // Se un giorno qualcuno ricopiasse la proiezione in una classe militare a parte, le due mappe
        // divergerebbero e una delle due sarebbe sbagliata senza che nessuno se ne accorga.
        Assert.Equal(ReleaseTargetType.Airport, Provider().Type);
        Assert.Equal(ReleaseTargetType.AirportMil, ProviderMil().Type);
    }

    [Fact]
    public async Task Pubblicare_un_vSOP_MILITARE_congela_le_sue_tabelle()
    {
        // ⚠️ È IL test della correzione. Senza un provider registrato per `AirportMil`, il registry rispondeva
        // `Empty` IN SILENZIO: pubblicare non fissava niente, e le tre tabelle derivate del documento
        // militare restavano appese alla release CIVILE dello scalo — al ciclo AIRAC di quella.
        var doc = Doc(
            Sec(10, "frequencies", RenderMode.Frozen),
            Sec(20, "runways", RenderMode.Frozen),
            Sec(30, "transition", RenderMode.Frozen));

        var frozen = await ProviderMil(Profilo()).CaptureFrozenAsync("LIRF", doc);

        Assert.Equal(3, frozen.Count);
        Assert.Contains("118.700", frozen[10]);
        Assert.Contains("16L", frozen[20]);
        Assert.Contains("6000", frozen[30]);
    }

    [Fact]
    public async Task La_vista_MILITARE_chiede_lo_snapshot_della_release_MILITARE()
    {
        // ⚠️ La chiave di release è l'ICAO per TUTTE E DUE le edizioni: il tipo è l'unica cosa che le
        // distingue. Chiedere `Airport` da qui non darebbe errore — darebbe la fotografia dell'altro
        // documento, che è molto peggio.
        var reader = new FakeReader();

        await Derivazione(reader, Profilo()).ResolveForViewAsync("LIRF", useFrozen: true,
                                                                 ReleaseTargetType.AirportMil);

        Assert.Equal(ReleaseTargetType.AirportMil, reader.TipoChiesto);
    }

    [Fact]
    public async Task La_vista_CIVILE_continua_a_chiedere_la_release_CIVILE()
    {
        var reader = new FakeReader();

        await Derivazione(reader, Profilo()).ResolveForViewAsync("LIRF", useFrozen: true,
                                                                 ReleaseTargetType.Airport);

        Assert.Equal(ReleaseTargetType.Airport, reader.TipoChiesto);
    }

    [Fact]
    public async Task Un_tipo_SENZA_provider_non_cattura_niente_e_NON_protesta()
    {
        // ⚠️ Il modo in cui il difetto è rimasto nascosto un giorno intero: `FrozenSectionRegistry` non
        // conosce l'elenco delle famiglie che DEVONO avere un provider, quindi una famiglia dimenticata non
        // è un errore — è un dizionario vuoto. Questo test non chiede di cambiarlo: lo mette per iscritto,
        // così chi aggiunge una famiglia sa che il silenzio è il comportamento previsto e la registrazione
        // in DI è l'unica cosa che lo evita.
        var registry = new FrozenSectionRegistry(new IFrozenSectionProvider[] { Provider(Profilo()) });
        var doc = Doc(Sec(10, "runways", RenderMode.Frozen));

        Assert.Empty(await registry.CaptureAsync(ReleaseTargetType.AirportMil, "LIRF", doc));
        Assert.NotEmpty(await registry.CaptureAsync(ReleaseTargetType.Airport, "LIRF", doc));
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
        /// <summary>A quale ciclo e' stata chiesta l'ultima derivazione (null = «adesso»).</summary>
        public string? CicloChiesto { get; private set; }

        public Task<AirportSidView> DeriveAsync(string icao, string? atCycle = null, CancellationToken ct = default)
        {
            CicloChiesto = atCycle;
            return Task.FromResult(Sid("ALAXI"));
        }
    }

    private sealed class FakeReader : IFrozenSectionReader
    {
        public Dictionary<string, object> Frozen { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Quante volte lo snapshot e' stato chiesto. Deve essere 0 o 1: leggerlo una volta per pagina
        /// e non una per sezione e' il punto del doc 14 §3c, e questo contatore e' la sua prova.</summary>
        public int Letture { get; private set; }
        public bool WasQueried => Letture > 0;

        /// <summary>Di quale FAMIGLIA e' stato chiesto lo snapshot. Non e' un dettaglio: la chiave e' l'ICAO
        /// per tutte e due le edizioni dello scalo, quindi il tipo e' l'unica cosa che le distingue.</summary>
        public ReleaseTargetType? TipoChiesto { get; private set; }

        public Task<FrozenSections> LoadAsync(ReleaseTargetType type, string key, CancellationToken ct = default)
        {
            Letture++;
            TipoChiesto = type;
            // Si passa per il JSON vero, non per gli oggetti: cosi' la prova copre anche la deserializzazione.
            return Task.FromResult(FrozenSections.FromKeys(
                Frozen.ToDictionary(kv => kv.Key, kv => System.Text.Json.JsonSerializer.Serialize(kv.Value, kv.Value.GetType()))));
        }
    }
}
