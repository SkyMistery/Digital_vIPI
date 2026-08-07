using System.Net;
using System.Text.Json;
using Vipi.AuroraBridge.Contracts;
using Vipi.AuroraBridge.Core;

namespace Vipi.AuroraBridge.Tests;

/// <summary>
/// Orchestratore e traduzione del contesto: da ciò che dice Aurora alla richiesta per il sito, e dalla
/// risposta del sito alla scrittura nel tag. La regola non negoziabile è che **il tool non scrive mai da solo**.
/// </summary>
public class BridgeOrchestratorTests
{
    private const string Owner = "LIBB_ES_CTR";

    /// <summary>Aurora finta con un traffico assunto, in rotta LIBD→LIRF via ASPIR.</summary>
    private static FakeAuroraServer Aurora(bool assumed = true, string selected = "AZA123")
    {
        var assumedBy = assumed ? Owner : "";
        return new FakeAuroraServer()
            .Reply("#CONN", $"#CONN;{Owner}")
            .Reply("#SELTFC", $"#SELTFC;{selected};")
            .Reply("#FP", $"#FP;{selected};LIBD;LIRF;LIRA;0820;A320;M;I;S;SDE;F350;N0450;0212;0114;PISIP UM984 ASPIR;DOF/260803;")
            .Reply("#TRPOS", $"#TRPOS;{selected};213;209;37987;470;43.3;8.6;2000;;;;;{assumedBy};;0;1;1;;1;;-48;;")
            .Reply("#TRPATHL", $"#TRPATHL;{selected};PISIP:0910;ASPIR:0925;LIRF:0940;")
            .Reply("#CTRLRWY", "#CTRLRWY;LIRF;25;16L:16R;")
            .Reply("#LBALT", $"#LBALT;{selected};210");
    }

    /// <summary>Sito finto: cattura la richiesta ricevuta e restituisce una risposta fissa.</summary>
    private sealed class FakeSite : HttpMessageHandler
    {
        public TransferResolveRequest? LastRequest { get; private set; }
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public int Calls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            var body = await request.Content!.ReadAsStringAsync(ct);
            LastRequest = JsonSerializer.Deserialize<TransferResolveRequest>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));

            if (Status != HttpStatusCode.OK) return new HttpResponseMessage(Status);

            var response = new TransferResolveResponse
            {
                AsOf = DateTimeOffset.UnixEpoch,
                OnlineAsOf = DateTimeOffset.UnixEpoch,
                ResolvedOwner = Owner,
                AccCode = "LIBB",
                Candidates =
                {
                    new TransferCandidate
                    {
                        FlowId = 1, PointId = 1, FlowKind = "Arrival", AirportIcao = "LIRF",
                        Cop = "ASPIR", CopEto = "0925", AuroraValue = "210", Writable = true, Score = 0.8,
                        Level = new CandidateLevel { Value = 210, Unit = "Fl", Constraint = "AtOrBelow", Text = "FL210-" },
                    },
                },
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                    System.Text.Encoding.UTF8, "application/json"),
            };
        }
    }

    private static (BridgeOrchestrator Orchestrator, VipiApiClient Api) Build(FakeAuroraServer server, FakeSite site, string cacheDir)
    {
        var client = new AuroraClient(new AuroraClientOptions("127.0.0.1", server.Port, 1500));
        var api = new VipiApiClient(
            new VipiApiOptions("http://localhost", CacheDirectory: cacheDir),
            new HttpClient(site) { BaseAddress = new Uri("http://localhost") });
        return (new BridgeOrchestrator(new AuroraSession(client), api), api);
    }

    private static string TempCache() =>
        Path.Combine(Path.GetTempPath(), "vipi-bridge-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Il_contesto_inviato_al_sito_riflette_cio_che_dice_Aurora()
    {
        await using var server = Aurora();
        var site = new FakeSite();
        var (orchestrator, api) = Build(server, site, TempCache());
        using var _ = api;

        await orchestrator.RefreshAsync(force: true);

        var sent = site.LastRequest!;
        Assert.Equal(Owner, sent.OwnerCallsign);
        Assert.Equal("LIBD", sent.Departure);
        Assert.Equal("LIRF", sent.Arrival);
        Assert.Equal(350, sent.CruiseLevel);                       // «F350» → 350
        Assert.Contains("ASPIR", sent.Route);
        Assert.Equal(3, sent.RouteFixes.Count);
        Assert.Equal("0925", sent.RouteFixes[1].Eto);
        Assert.Equal(new[] { "16L", "16R" }, sent.RunwaysInUse["LIRF"].Arrival);
        Assert.False(sent.OnGround);
    }

    [Fact]
    public async Task Lo_stato_espone_traffico_assunto_e_candidato_migliore()
    {
        await using var server = Aurora();
        var site = new FakeSite();
        var (orchestrator, api) = Build(server, site, TempCache());
        using var _ = api;

        var state = await orchestrator.RefreshAsync(force: true);

        Assert.True(state.AuroraConnected);
        Assert.Equal("AZA123", state.SelectedTraffic);
        Assert.True(state.TrafficAssumed);
        Assert.Equal("210", state.Best!.AuroraValue);
    }

    [Fact]
    public async Task Senza_selezione_non_si_chiama_il_sito()
    {
        await using var server = new FakeAuroraServer()
            .Reply("#CONN", $"#CONN;{Owner}")
            .Reply("#SELTFC", "#SELTFC;;");
        var site = new FakeSite();
        var (orchestrator, api) = Build(server, site, TempCache());
        using var _ = api;

        var state = await orchestrator.RefreshAsync(force: true);

        Assert.Equal(0, site.Calls);
        Assert.Null(state.SelectedTraffic);
        Assert.Contains("Nessun traffico selezionato", state.Notice);
    }

    [Fact]
    public async Task Aurora_non_raggiungibile_lo_dice_col_rimedio()
    {
        var site = new FakeSite();
        var client = new AuroraClient(new AuroraClientOptions("127.0.0.1", 1, 200));
        using var api = new VipiApiClient(new VipiApiOptions("http://localhost", CacheDirectory: TempCache()), new HttpClient(site));
        var orchestrator = new BridgeOrchestrator(new AuroraSession(client), api);

        var state = await orchestrator.RefreshAsync(force: true);

        Assert.False(state.AuroraConnected);
        Assert.Contains("3rd Party Software Access", state.Notice);
        Assert.Equal(0, site.Calls);
    }

    [Fact]
    public async Task La_stessa_selezione_non_ripete_la_chiamata_al_sito()
    {
        await using var server = Aurora();
        var site = new FakeSite();
        var (orchestrator, api) = Build(server, site, TempCache());
        using var _ = api;

        await orchestrator.RefreshAsync(force: true);
        await orchestrator.RefreshAsync();
        await orchestrator.RefreshAsync();

        Assert.Equal(1, site.Calls);
    }

    [Fact]
    public async Task Sito_giu_ma_contesto_gia_visto_la_proposta_arriva_dalla_cache()
    {
        var cache = TempCache();
        await using var server = Aurora();
        var site = new FakeSite();

        var (first, api1) = Build(server, site, cache);
        using (api1) await first.RefreshAsync(force: true);

        site.Status = HttpStatusCode.ServiceUnavailable;
        var (second, api2) = Build(server, site, cache);
        using (api2)
        {
            var state = await second.RefreshAsync(force: true);

            Assert.True(state.ProposalFromCache);
            Assert.Equal("210", state.Best!.AuroraValue);
            Assert.Contains("503", state.Notice);
        }
    }

    /// <summary>
    /// Il bridge nasce spento sul sito (`AuroraBridge:Enabled=false`), quindi «endpoint non montato» è il
    /// primo errore che incontra chi punta il tool a un sito qualunque. Un «Il sito ha risposto 405» non gli
    /// direbbe cosa fare; il messaggio deve nominare la causa e il rimedio.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.MethodNotAllowed)]
    public async Task Bridge_spento_sul_sito_lo_dice_col_rimedio(HttpStatusCode status)
    {
        await using var server = Aurora();
        var site = new FakeSite { Status = status };
        var (orchestrator, api) = Build(server, site, TempCache());
        using var _ = api;

        var state = await orchestrator.RefreshAsync(force: true);

        Assert.Contains("non è attivo", state.Notice);
        Assert.DoesNotContain("405", state.Notice);
    }

    [Fact]
    public async Task La_scrittura_arriva_ad_Aurora_solo_quando_la_si_chiede()
    {
        await using var server = Aurora();
        var site = new FakeSite();
        var (orchestrator, api) = Build(server, site, TempCache());
        using var _ = api;

        var state = await orchestrator.RefreshAsync(force: true);
        Assert.DoesNotContain(server.Received, r => r.StartsWith("#LBALT"));   // il giro di polling NON scrive

        var result = await orchestrator.WriteAsync(state.Best!);

        Assert.True(result.Ok);
        Assert.Contains("#LBALT;AZA123;210", server.Received);
    }

    [Fact]
    public async Task Su_traffico_non_assunto_la_scrittura_si_ferma_prima_di_Aurora()
    {
        await using var server = Aurora(assumed: false);
        var site = new FakeSite();
        var (orchestrator, api) = Build(server, site, TempCache());
        using var _ = api;

        var state = await orchestrator.RefreshAsync(force: true);
        var result = await orchestrator.WriteAsync(state.Best!);

        Assert.False(state.TrafficAssumed);
        Assert.False(result.Ok);
        Assert.Contains("non è assunto", result.Error);
        Assert.DoesNotContain(server.Received, r => r.StartsWith("#LBALT"));
    }

    [Fact]
    public async Task Con_l_override_l_assunzione_si_valuta_sulla_postazione_CONNESSA()
    {
        // Aurora connessa come LIZZ_AEW_CTR (callsign non presente nel sito), traffico assunto da LEI;
        // l'utente forza le regole di LIBB_ES_CTR. La scrittura DEVE restare possibile: chi comanda il
        // traffico è la connessione, non l'override. Con il confronto sbagliato il tool diceva
        // «non assunto» e rifiutava una scrittura che Aurora invece accetta (visto dal vivo il 3 ago 2026).
        await using var server = new FakeAuroraServer()
            .Reply("#CONN", "#CONN;LIZZ_AEW_CTR")
            .Reply("#SELTFC", "#SELTFC;IBE0980;")
            .Reply("#FP", "#FP;IBE0980;LIRN;LEMD;LEAB;0920;A321;M;I;S;SDE;F350;N0462;0313;0154;ESINO DCT;;")
            .Reply("#TRPOS", "#TRPOS;IBE0980;264;264;30362;466;40.8;12.7;1000;;;;;LIZZ_AEW_CTR;;0;1;1;;1;;924;;")
            .Reply("#TRPATHL", "#TRPATHL;IBE0980;ESINO:0930;")
            .Reply("#CTRLRWY", "#CTRLRWY;")
            .Reply("#LBALT", "#LBALT;IBE0980;210");

        var site = new FakeSite();
        var client = new AuroraClient(new AuroraClientOptions("127.0.0.1", server.Port, 1500));
        using var api = new VipiApiClient(
            new VipiApiOptions("http://localhost", CacheDirectory: TempCache()),
            new HttpClient(site) { BaseAddress = new Uri("http://localhost") });
        var orchestrator = new BridgeOrchestrator(new AuroraSession(client), api, ownerOverride: Owner);

        var state = await orchestrator.RefreshAsync(force: true);

        Assert.Equal(Owner, state.OwnerCallsign);              // le regole sono quelle forzate…
        Assert.Equal("LIZZ_AEW_CTR", state.ConnectedCallsign); // …ma la connessione resta quella vera
        Assert.Equal(Owner, site.LastRequest!.OwnerCallsign);
        Assert.True(state.TrafficAssumed);

        var result = await orchestrator.WriteAsync(state.Best!);
        Assert.True(result.Ok);
    }

    [Fact]
    public async Task La_scorciatoia_scrive_il_primo_candidato_quando_e_scrivibile()
    {
        await using var server = Aurora();
        var site = new FakeSite();
        var (orchestrator, api) = Build(server, site, TempCache());
        using var _ = api;
        var log = new BridgeLog(Path.Combine(Path.GetTempPath(), $"vipi-{Guid.NewGuid():N}.log"));
        var model = new BridgeViewModel(orchestrator, new BridgeSettings(), log);

        await orchestrator.RefreshAsync(force: true);
        var message = await model.WriteBestAsync();

        Assert.Contains("Scritto «210»", message);
        Assert.Contains("#LBALT;AZA123;210", server.Received);
        Assert.Contains("SCRITTO", File.ReadAllText(log.FilePath));
        File.Delete(log.FilePath);
    }

    [Fact]
    public async Task La_scorciatoia_NON_ripiega_su_un_altro_livello_se_il_primo_non_e_scrivibile()
    {
        // Traffico non assunto: il primo candidato non si può scrivere. La scorciatoia deve fermarsi e dire
        // perché, non cercare in silenzio un candidato più in basso — sarebbe un livello diverso da quello
        // che il controllore si aspetta di aver premuto.
        await using var server = Aurora(assumed: false);
        var site = new FakeSite();
        var (orchestrator, api) = Build(server, site, TempCache());
        using var _ = api;
        var model = new BridgeViewModel(orchestrator, new BridgeSettings());

        await orchestrator.RefreshAsync(force: true);
        var message = await model.WriteBestAsync();

        Assert.Contains("Non scrivo", message);
        Assert.Contains("non assunto", message);
        Assert.DoesNotContain(server.Received, r => r.StartsWith("#LBALT"));
    }

    [Fact]
    public async Task Senza_candidati_la_scorciatoia_lo_dice_e_non_tocca_Aurora()
    {
        await using var server = new FakeAuroraServer()
            .Reply("#CONN", $"#CONN;{Owner}")
            .Reply("#SELTFC", "#SELTFC;;");
        var site = new FakeSite();
        var (orchestrator, api) = Build(server, site, TempCache());
        using var _ = api;
        var model = new BridgeViewModel(orchestrator, new BridgeSettings());

        await orchestrator.RefreshAsync(force: true);
        var message = await model.WriteBestAsync();

        Assert.Contains("Nessun candidato", message);
        Assert.DoesNotContain(server.Received, r => r.StartsWith("#LBALT"));
    }

    [Fact]
    public async Task Un_candidato_non_scrivibile_viene_rifiutato()
    {
        await using var server = Aurora();
        var site = new FakeSite();
        var (orchestrator, api) = Build(server, site, TempCache());
        using var _ = api;

        await orchestrator.RefreshAsync(force: true);
        var result = await orchestrator.WriteAsync(new TransferCandidate { Writable = false, AuroraValue = null });

        Assert.False(result.Ok);
        Assert.Contains("non è scrivibile", result.Error);
    }

    [Fact]
    public async Task Aurora_che_rifiuta_la_scrittura_produce_un_messaggio_comprensibile()
    {
        await using var server = Aurora();
        server.Reply("#LBALT", "@ERR;#LBALT;AZA123;210;Traffic not assumed.");
        var site = new FakeSite();
        var (orchestrator, api) = Build(server, site, TempCache());
        using var _ = api;

        var state = await orchestrator.RefreshAsync(force: true);
        var result = await orchestrator.WriteAsync(state.Best!);

        Assert.False(result.Ok);
        Assert.Contains("solo sul traffico assunto", result.Error);
    }
}
