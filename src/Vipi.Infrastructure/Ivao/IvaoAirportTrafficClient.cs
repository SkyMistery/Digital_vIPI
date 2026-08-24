using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Ivao;

/// <summary>
/// Adapter IVAO dei movimenti d'aeroporto (<c>/v2/airports/{icao}/traffics?from&amp;to</c>).
///
/// <para>Misurato il 24 agosto 2026 col token app: risponde 200 e ritorna un oggetto con tre liste —
/// <c>inbound</c>, <c>outbound</c>, <c>flightover</c> — ognuna con callsign, piano di volo e ultimo
/// tracciato. Su LIRF, sei ore: 11 in arrivo, 17 in partenza, 0 sorvoli.</para>
/// </summary>
public sealed class IvaoAirportTrafficClient : IAirportTrafficSource
{
    private readonly IvaoHttp _http;
    private readonly IvaoOptions _opt;

    public IvaoAirportTrafficClient(IvaoHttp http, IOptions<IvaoOptions> opt)
    {
        _http = http;
        _opt = opt.Value;
    }

    public async Task<IReadOnlyList<SourceAirportMovement>> GetMovementsAsync(
        string icao, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var path = string.Format(CultureInfo.InvariantCulture, _opt.AirportTrafficsPathFormat, icao.ToUpperInvariant())
                   + $"?from={Iso(from)}&to={Iso(to)}";

        using var res = await _http.SendGetAsync(path, ct);
        res.EnsureSuccessStatusCode();

        var d = await res.Content.ReadFromJsonAsync<TrafficsDto>(cancellationToken: ct);
        if (d is null) return Array.Empty<SourceAirportMovement>();

        var esito = new List<SourceAirportMovement>();
        Aggiungi(d.Inbound, AirportMovementKind.Inbound);
        Aggiungi(d.Outbound, AirportMovementKind.Outbound);
        Aggiungi(d.Flightover, AirportMovementKind.Overflight);
        return esito;

        void Aggiungi(List<MovementDto>? voli, AirportMovementKind tipo)
        {
            foreach (var v in voli ?? new List<MovementDto>())
            {
                if (string.IsNullOrWhiteSpace(v.Callsign)) continue;
                esito.Add(new SourceAirportMovement(
                    tipo, v.Callsign!, v.UserId, v.FlightPlan?.Id,
                    v.FlightPlan?.DepartureId, v.FlightPlan?.ArrivalId, v.FlightPlan?.AircraftId));
            }
        }
    }

    private static string Iso(DateTimeOffset t) =>
        t.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    private sealed record TrafficsDto(
        [property: JsonPropertyName("inbound")] List<MovementDto>? Inbound,
        [property: JsonPropertyName("outbound")] List<MovementDto>? Outbound,
        [property: JsonPropertyName("flightover")] List<MovementDto>? Flightover);

    private sealed record MovementDto(
        [property: JsonPropertyName("callsign")] string? Callsign,
        [property: JsonPropertyName("userId")] int UserId,
        [property: JsonPropertyName("flightPlan")] FlightPlanDto? FlightPlan);

    private sealed record FlightPlanDto(
        [property: JsonPropertyName("id")] long? Id,
        [property: JsonPropertyName("departureId")] string? DepartureId,
        [property: JsonPropertyName("arrivalId")] string? ArrivalId,
        [property: JsonPropertyName("aircraftId")] string? AircraftId);
}
