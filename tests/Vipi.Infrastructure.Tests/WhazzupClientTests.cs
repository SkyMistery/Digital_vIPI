using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Stats;
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
    public async Task Gli_ATC_escono_filtrati_alla_divisione()
    {
        var snap = await Client().GetSnapshotAsync();

        var atc = Assert.Single(snap.Atc);
        Assert.Equal("LIRF_TWR", atc.Callsign);      // l'ucraino UKLU_TWR resta fuori
        Assert.Equal(63243559, atc.SessionId);        // l'id di sessione IVAO, che ritroveremo nello storico
        Assert.Equal(762032, atc.UserId);
        Assert.Equal("TWR", atc.Position);
        Assert.Equal("118.700", atc.Frequency);
        Assert.Equal(2116, atc.ConnectedSeconds);
        Assert.Equal(new DateTimeOffset(2026, 8, 24, 12, 35, 16, TimeSpan.Zero), atc.StartUtc);
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

    private static IvaoWhazzupClient Client(string prefix = "LI")
    {
        var opt = Options.Create(new IvaoOptions { ClientId = "" /* endpoint pubblico: nessun token */ });
        var div = Options.Create(new Vipi.Application.DivisionOptions { IcaoPrefixes = new() { prefix } });
        var http = new HttpClient(new StubHandler(WhazzupReale));
        var token = new IvaoTokenProvider(new NullHttpClientFactory(), opt);
        return new IvaoWhazzupClient(new IvaoHttp(http, token, opt), opt, div);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _body;
        public StubHandler(string body) => _body = body;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            });
    }

    private sealed class NullHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
