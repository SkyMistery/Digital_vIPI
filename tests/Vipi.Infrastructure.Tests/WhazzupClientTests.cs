using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Stats;
using Vipi.Domain;
using Vipi.Infrastructure.Ivao;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// L'adapter della fotografia di rete (ATC + piloti). Il JSON qui sotto è <b>reale</b>: sono record presi
/// verbatim dal whazzup del 24 agosto 2026, solo ridotti di numero. Serve a garantire che il parsing regga
/// la forma vera, non una forma inventata da chi scrive il test.
/// </summary>
public class WhazzupClientTests
{
    private const string WhazzupReale = """
    {
      "clients": {
        "atcs": [
          { "id": 63243559, "userId": 762032, "callsign": "LIRF_TWR", "rating": 4,
            "createdAt": "2026-08-24T12:35:16.000Z", "time": 2116,
            "atcSession": { "frequency": 118.7, "position": "TWR" } },
          { "id": 63242066, "userId": 307959, "callsign": "UKLU_TWR", "rating": 8,
            "createdAt": "2026-08-24T07:29:21.000Z", "time": 20471,
            "atcSession": { "frequency": 126.9, "position": "TWR" } }
        ],
        "pilots": [
          { "id": 63243063, "userId": 472218, "callsign": "ITY081",
            "lastTrack": { "latitude": 41.798, "longitude": 12.256884, "altitude": 15, "groundSpeed": 0,
                           "onGround": true, "state": "On Blocks", "departureDistance": 453.62308 },
            "flightPlan": { "id": 72359580, "departureId": "LEPA", "arrivalId": "LIRF", "aircraftId": "BCS3" } },
          { "id": 63243222, "userId": 452325, "callsign": "AZA006",
            "lastTrack": { "latitude": 41.80312, "longitude": 12.262713, "altitude": 10, "groundSpeed": 0,
                           "onGround": true, "state": "Boarding", "departureDistance": 1.079877 },
            "flightPlan": { "id": 72360394, "departureId": "LIRF", "arrivalId": "LIRI", "aircraftId": "AT46" } },
          { "id": 63243787, "userId": 785127, "callsign": "AIB46",
            "flightPlan": { "id": 72360432, "departureId": "GVAC", "arrivalId": "SBRF", "aircraftId": "A321" } }
        ]
      }
    }
    """;

    [Fact]
    public async Task Gli_ATC_escono_TUTTI_e_quello_di_divisione_e_marcato()
    {
        var snap = await Client().GetSnapshotAsync();

        var atc = Assert.Single(snap.Atc, a => a.Callsign == "LIRF_TWR");
        Assert.False(atc.IsOutsideDivision);
        Assert.Equal(63243559, atc.SessionId);        // l'id di sessione IVAO, che ritroveremo nello storico
        Assert.Equal(762032, atc.UserId);
        Assert.Equal("TWR", atc.Position);
        Assert.Equal("118.700", atc.Frequency);
        Assert.Equal(2116, atc.ConnectedSeconds);
        Assert.Equal(new DateTimeOffset(2026, 8, 24, 12, 35, 16, TimeSpan.Zero), atc.StartUtc);
    }

    [Fact]
    public async Task L_ATC_fuori_divisione_non_si_butta_piu_ma_esce_marcato()
    {
        // Dal 28 agosto 2026 l'ucraino UKLU_TWR NON resta più fuori: si archivia come tutti gli altri, e a
        // distinguerlo è la sola marca. È il difetto che questa modifica esiste per togliere — la
        // fotografia è già pagata, e quel che si buttava non si poteva più recuperare da nessuna parte.
        var snap = await Client().GetSnapshotAsync();

        Assert.Equal(2, snap.Atc.Count);
        var estero = Assert.Single(snap.Atc, a => a.Callsign == "UKLU_TWR");
        Assert.True(estero.IsOutsideDivision);
        Assert.Equal(307959, estero.UserId);
        Assert.Equal("TWR", estero.Position);
    }

    [Fact]
    public async Task I_piloti_NON_sono_filtrati_per_callsign()
    {
        // Un volo dentro un settore italiano può chiamarsi in qualunque modo: il filtro giusto è geometrico.
        var snap = await Client().GetSnapshotAsync();
        Assert.Contains(snap.Pilots, p => p.Callsign == "ITY081");
        Assert.Contains(snap.Pilots, p => p.Callsign == "AZA006");
    }

    [Fact]
    public async Task Un_pilota_senza_tracciato_viene_scartato_senza_esplodere()
    {
        // Misurato: 1 pilota su 468 non ha lastTrack. Senza posizione non è attribuibile a nessun settore.
        var snap = await Client().GetSnapshotAsync();
        Assert.DoesNotContain(snap.Pilots, p => p.Callsign == "AIB46");
        Assert.Equal(2, snap.Pilots.Count);
    }

    [Fact]
    public async Task Il_tracciato_arriva_intero_e_la_fase_si_ricava()
    {
        var snap = await Client().GetSnapshotAsync();

        var ity = snap.Pilots.Single(p => p.Callsign == "ITY081");
        Assert.Equal("On Blocks", ity.State);
        Assert.Equal(453.62308, ity.DepartureDistanceNm);
        Assert.Equal("LEPA", ity.DepIcao);
        Assert.Equal("LIRF", ity.ArrIcao);
        Assert.Equal(72359580, ity.FlightPlanId);

        // È a Fiumicino ma ci è ARRIVATO: non è una partenza della DEL.
        Assert.Equal(FlightPhase.Ground,
            FlightPhases.Of(ity.OnGround, ity.GroundSpeed, ity.State, ity.DepartureDistanceNm));

        var aza = snap.Pilots.Single(p => p.Callsign == "AZA006");
        Assert.Equal(FlightPhase.Parked,
            FlightPhases.Of(aza.OnGround, aza.GroundSpeed, aza.State, aza.DepartureDistanceNm));
    }

    private static IvaoWhazzupClient Client(string prefix = "LI", string corpo = WhazzupReale)
    {
        var opt = Options.Create(new IvaoOptions { ClientId = "" /* endpoint pubblico: nessun token */ });
        var div = Options.Create(new Vipi.Application.DivisionOptions { IcaoPrefixes = new() { prefix } });
        var http = new HttpClient(new StubHandler(corpo));
        var token = new IvaoTokenProvider(new NullHttpClientFactory(), opt);
        return new IvaoWhazzupClient(new IvaoHttp(http, token, opt), opt, div);
    }

    // ---- U-025 e U-131 (revisione totale 3) ----

    /// <summary>
    /// 🔴 U-025: il whazzup è pubblico, ma la richiesta passava da <c>AuthorizeAsync</c>, che chiede il token quando
    /// il ClientId c'è. Un segreto ruotato male, un token endpoint giù: la GET — che il token non lo vuole — non
    /// partiva, e dopo tre giri vista live e statistiche si spegnevano per tutti.
    /// </summary>
    [Fact]
    public async Task Il_whazzup_non_chiede_il_token_e_regge_un_segreto_rotto()
    {
        var opt = Options.Create(new IvaoOptions { ClientId = "prova", ClientSecret = "sbagliato" });
        var div = Options.Create(new Vipi.Application.DivisionOptions { IcaoPrefixes = new() { "LI" } });
        var handler = new StubHandler(WhazzupReale);
        var token = new IvaoTokenProvider(new FabbricaCheRifiuta(), opt);
        var client = new IvaoWhazzupClient(new IvaoHttp(new HttpClient(handler), token, opt), opt, div);

        var snap = await client.GetSnapshotAsync();

        Assert.Equal(2, snap.Atc.Count);
        Assert.Null(handler.Autorizzazione);   // nessun Bearer su un endpoint pubblico
    }

    /// <summary>
    /// 🔴 U-131: la fotografia era datata all'ora d'ARRIVO. Un whazzup fermo servito con 200 (cache, generatore
    /// bloccato) sembrava fresco: la scadenza della cache (T-034) non scattava, e i piloti congelati accumulavano
    /// minuti di traffico. Ora la data è quella di generazione, <c>updatedAt</c>.
    /// </summary>
    [Fact]
    public async Task La_fotografia_porta_la_data_in_cui_la_sorgente_l_ha_generata()
    {
        // ⚠️ Il formato è quello VERO, letto sul filo il 27 settembre 2026: NOVE cifre di frazione («…15.107470159Z»).
        // Un parser che non le reggesse farebbe fallire ogni poll, cioè spegnerebbe la vista live per tutti.
        var corpo = WhazzupReale.Replace("\"clients\":", "\"updatedAt\": \"2026-08-24T13:10:05.107470159Z\", \"clients\":");

        var snap = await Client(corpo: corpo).GetSnapshotAsync();

        Assert.Equal(new DateTimeOffset(2026, 8, 24, 13, 10, 5, TimeSpan.Zero).AddTicks(1074701), snap.AsOf);
    }

    /// <summary>
    /// 🔴 U-131: una risposta senza <c>clients</c> (o senza <c>atcs</c>/<c>pilots</c>) era «nessuno online»: la cache
    /// si svuotava e il poller chiudeva in massa le sessioni aperte (l'innesco di U-026). È un poll fallito.
    /// </summary>
    [Theory]
    [InlineData("""{ "updatedAt": "2026-08-24T13:10:05.000Z" }""")]
    [InlineData("""{ "clients": null }""")]
    [InlineData("""{ "clients": { "pilots": [] } }""")]
    [InlineData("""{ "clients": { "atcs": [] } }""")]
    public async Task Una_risposta_senza_elenchi_e_un_poll_fallito_non_nessuno_online(string corpo)
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => Client(corpo: corpo).GetSnapshotAsync());
    }

    [Fact]
    public async Task Elenchi_vuoti_ma_presenti_sono_davvero_nessuno_online()
    {
        var snap = await Client(corpo: """{ "clients": { "atcs": [], "pilots": [] } }""").GetSnapshotAsync();
        Assert.Empty(snap.Atc);
        Assert.Empty(snap.Pilots);
    }

    private sealed class FabbricaCheRifiuta : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new Rifiuto());

        private sealed class Rifiuto : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("""{"error":"invalid_client"}""", Encoding.UTF8, "application/json"),
                });
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _body;
        public StubHandler(string body) => _body = body;
        public string? Autorizzazione { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Autorizzazione = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class NullHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
