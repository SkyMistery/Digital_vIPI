using Vipi.Application.Abstractions;

namespace Vipi.Application.Content;

/// <summary>
/// Orchestratore (senza authz) dell'import dei settori ATC di un aeroporto dalla sorgente:
/// lista postazioni → dettaglio per posizione (freq/shape/limiti) → upsert nel catalogo.
/// Dipende solo dalle porte neutre (nessun service): riusato da <see cref="IAirportSectorService"/>
/// (wrapper ACC-gated), dal job di import automatico e dalla generazione documento.
/// </summary>
public interface IAirportSectorImporter
{
    /// <summary>Importa/aggiorna i settori (incl. APP) dell'aeroporto. Ritorna (creati, aggiornati).</summary>
    Task<(int Created, int Updated)> ImportAsync(string icao, CancellationToken ct = default);
}

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
