using Vipi.Application.Abstractions;
using Vipi.Application.Aor;

namespace Vipi.Application.Content;

/// <summary>
/// Applica i poligoni TWR REALI presi dalla sorgente GitHub Aurora (<c>twrs.tfl</c>, via <see cref="ITowerShapeSource"/>)
/// alle TWR prive di shape dalla sorgente IVAO. È il ripiego "buono": va eseguito PRIMA del cerchio sintetico
/// (<see cref="ITowerShapeFallbackService"/>), così il cerchio copre solo le TWR che nemmeno GitHub ha. Match per
/// callsign (es. <c>LIBA_TWR</c>). Idempotente: bersaglia solo le TWR il cui poligono attuale non si proietta.
/// Job di sistema (no authz): invocato dopo l'import automatico dei settori d'aeroporto.
/// </summary>
public interface IGithubTowerShapeService
{
    /// <summary>Applica le shape GitHub alle TWR senza poligono. Ritorna il numero di shape applicate.</summary>
    Task<int> ApplyAsync(CancellationToken ct = default);
}

/// <inheritdoc cref="IGithubTowerShapeService"/>
public sealed class GithubTowerShapeService : IGithubTowerShapeService
{
    private readonly IAirportSectorRepository _repo;
    private readonly ITowerShapeSource _source;

    public GithubTowerShapeService(IAirportSectorRepository repo, ITowerShapeSource source)
    {
        _repo = repo;
        _source = source;
    }

    public async Task<int> ApplyAsync(CancellationToken ct = default)
    {
        // "Vuoto/degenere" = il poligono attuale non si proietta (stesso criterio del cerchio sintetico): così
        // becchiamo le TWR che la sorgente IVAO espone come "[]" o null, senza mai toccare una shape reale IVAO.
        var targets = (await _repo.ListTwrShapesAsync(ct))
            .Where(t => AorPolygonProjector.Project(t.RawPolygon) is null)
            .ToList();
        if (targets.Count == 0) return 0;

        var polygons = await _source.GetTowerPolygonsAsync(ct);
        if (polygons.Count == 0) return 0;

        var applied = 0;
        foreach (var t in targets)
        {
            if (!polygons.TryGetValue(t.ComposePosition, out var json)) continue;   // GitHub non ha questa TWR
            if (AorPolygonProjector.Project(json) is null) continue;                 // poligono GitHub degenere: salta
            await _repo.SetRealShapeAsync(t.SectorId, json, ct);
            applied++;
        }
        return applied;
    }
}
