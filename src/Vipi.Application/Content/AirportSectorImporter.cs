using Vipi.Application.Abstractions;

namespace Vipi.Application.Content;

/// <inheritdoc cref="IAirportSectorImporter"/>
public sealed class AirportSectorImporter : IAirportSectorImporter
{
    private readonly IAirportDetailProvider _details;
    private readonly IAirportSectorRepository _repo;

    public AirportSectorImporter(IAirportDetailProvider details, IAirportSectorRepository repo)
    {
        _details = details;
        _repo = repo;
    }

    public async Task<(int Created, int Updated)> ImportAsync(string icao, CancellationToken ct = default)
    {
        icao = (icao ?? "").Trim().ToUpperInvariant();

        // 1) Lista postazioni ATC (callsign + freq + position/middle dalla lista).
        var positions = await _details.GetAtcPositionsAsync(icao, ct);
        if (positions.Count == 0) return (0, 0);

        // 2) Dettaglio per ogni postazione: freq + shape + limiti (best-effort; in errore tiene i dati di lista).
        var enriched = new List<SourceAtcPosition>(positions.Count);
        foreach (var p in positions)
        {
            var detail = await _details.GetAtcPositionDetailAsync(p.Callsign, ct);
            enriched.Add(detail is null ? p : p with
            {
                Frequency = detail.Frequency ?? p.Frequency,
                Position = detail.Position ?? p.Position,
                MiddleIdentifier = detail.MiddleIdentifier ?? p.MiddleIdentifier,
                RegionMapPolygon = detail.RegionMapPolygon,
                LowerLimit = detail.LowerLimit,
                UpperLimit = detail.UpperLimit,
                AirportLatitude = detail.AirportLatitude,
                AirportLongitude = detail.AirportLongitude,
            });
        }

        return await _repo.ImportForAirportAsync(icao, enriched, ct);
    }
}
