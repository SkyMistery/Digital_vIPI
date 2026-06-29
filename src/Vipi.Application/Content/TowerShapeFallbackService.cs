using Vipi.Application.Abstractions;
using Vipi.Application.Aor;

namespace Vipi.Application.Content;

/// <summary>
/// Genera una shape tonda di fallback (cerchio di raggio fisso, default 5 NM) per i settori TWR privi di poligono
/// dalla sorgente, così da poterli comunque disegnare. La shape è marcata sintetica (IsShapeSynthetic): mai
/// sovrascrive una shape reale, e il futuro fallback GitHub potrà rimpiazzarla. Il centro è il riferimento
/// aeroporto (<see cref="Vipi.Domain.Entities.Airport.Latitude"/>/Longitude), popolato all'import dal blocco
/// "airport" del dettaglio postazione IVAO (<c>/v2/ATCPositions/{compose}</c>, presente su ogni postazione).
/// Come ultimo ripiego, se le coord non sono note, usa il centro del poligono di un settore fratello già presente
/// (es. APP). Job di sistema (no authz): invocato dopo l'import automatico dei settori d'aeroporto.
/// </summary>
public interface ITowerShapeFallbackService
{
    /// <summary>Applica il fallback a tutte le TWR senza poligono. Ritorna il numero di shape generate.</summary>
    Task<int> ApplyAsync(double radiusNm = 5.0, CancellationToken ct = default);
}

/// <inheritdoc cref="ITowerShapeFallbackService"/>
public sealed class TowerShapeFallbackService : ITowerShapeFallbackService
{
    private readonly IAirportSectorRepository _repo;

    public TowerShapeFallbackService(IAirportSectorRepository repo) => _repo = repo;

    public async Task<int> ApplyAsync(double radiusNm = 5.0, CancellationToken ct = default)
    {
        // "Vuoto/degenere" = il poligono attuale non si proietta (null, "[]", < 3 punti…). Decisione col projector,
        // così becchiamo anche le TWR che la sorgente espone come array vuoto.
        var targets = (await _repo.ListTwrShapesAsync(ct))
            .Where(t => AorPolygonProjector.Project(t.RawPolygon) is null)
            .ToList();
        if (targets.Count == 0) return 0;

        // Ripiego (solo se manca la coord aeroporto): centro dal poligono di un settore fratello (es. APP).
        var needFallback = targets.Any(t => t.Latitude is null || t.Longitude is null);
        var siblingCenters = needFallback ? await LoadSiblingCentersAsync(ct) : new Dictionary<string, (double, double)>();

        var generated = 0;
        foreach (var t in targets)
        {
            double lat, lon;
            if (t.Latitude is double la && t.Longitude is double lo) { lat = la; lon = lo; }   // coord aeroporto (ATCPositions)
            else if (siblingCenters.TryGetValue(t.AirportIcao, out var sib)) { (lat, lon) = sib; }
            else continue;   // centro ignoto: niente cerchio finché l'import non popola le coord aeroporto

            var polygon = CircleShapeBuilder.Build(lat, lon, radiusNm);
            await _repo.SetSyntheticShapeAsync(t.SectorId, polygon, ct);
            generated++;
        }
        return generated;
    }

    /// <summary>Per ogni aeroporto, il centro (bounding-box) del primo poligono di settore proiettabile. Approssima
    /// la posizione dell'aeroporto quando le coordinate reali non sono ancora note (es. import non ancora girato).</summary>
    private async Task<IReadOnlyDictionary<string, (double Lat, double Lon)>> LoadSiblingCentersAsync(CancellationToken ct)
    {
        var dict = new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in await _repo.ListNonSyntheticPolygonsAsync(ct))
        {
            if (dict.ContainsKey(p.AirportIcao)) continue;
            var poly = AorPolygonProjector.Project(p.RawPolygon);
            if (poly is not null) dict[p.AirportIcao] = (poly.CenterLat, poly.CenterLon);
        }
        return dict;
    }
}
