using Vipi.AuroraBridge.Contracts;

namespace Vipi.AuroraBridge.Core;

/// <summary>Tutto ciò che il tool ha raccolto da Aurora su un volo, prima di chiedere al sito.</summary>
public sealed record FlightSnapshot(
    string Callsign,
    string? OwnerCallsign,
    FlightPlanRecord? FlightPlan,
    TrafficPositionRecord? Position,
    IReadOnlyList<(string Fix, string? Eto)> RoutePath,
    IReadOnlyList<RunwayConfiguration> Runways);

/// <summary>
/// Traduce ciò che dice Aurora nella richiesta per il sito. Puro: è la giuntura fra i due protocolli e
/// merita test propri, perché è dove i formati (FL «F330», flag «1», piste «16L:16R») diventano dati.
/// </summary>
public static class FlightContextBuilder
{
    public static TransferResolveRequest Build(FlightSnapshot snapshot)
    {
        var fp = snapshot.FlightPlan;
        var pos = snapshot.Position;

        var request = new TransferResolveRequest
        {
            // Il punto di vista è SEMPRE la mia postazione: il tool risponde a «dove devo cederlo io».
            // La stazione che ha assunto il traffico serve solo a sapere se Aurora accetterà la scrittura;
            // usarla qui darebbe le regole di un altro ente quando guardo un traffico non mio.
            OwnerCallsign = FirstNotEmpty(snapshot.OwnerCallsign, pos?.AssumedStation) ?? "",
            Departure = fp?.Departure,
            Arrival = fp?.Arrival,
            CruiseLevel = fp?.CruiseFlightLevel,
            Route = fp?.Route,
            CurrentAltitudeFt = pos?.AltitudeFt,
            VerticalSpeedFpm = pos?.VerticalSpeedFpm,
            OnGround = pos?.OnGround ?? false,
            NextStation = pos?.NextStation,
        };

        foreach (var (fix, eto) in snapshot.RoutePath)
            request.RouteFixes.Add(new RouteFix(fix, eto));

        foreach (var rwy in snapshot.Runways)
        {
            if (rwy.Departure.Count == 0 && rwy.Arrival.Count == 0) continue;
            request.RunwaysInUse[rwy.Icao] = new RunwayConfig
            {
                Departure = rwy.Departure.ToList(),
                Arrival = rwy.Arrival.ToList(),
            };
        }

        return request;
    }

    private static string? FirstNotEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
}
