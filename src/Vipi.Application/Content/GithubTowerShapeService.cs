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
    /// <summary>Applica le shape GitHub alle TWR senza poligono. Se <paramref name="icao"/> è dato, si limita a
    /// quell'aeroporto (bottone manuale nell'editor); null = tutte (job automatico). Ritorna il numero applicate.</summary>
    Task<int> ApplyAsync(string? icao = null, CancellationToken ct = default);
}

/// <inheritdoc cref="IGithubTowerShapeService"/>
public sealed class GithubTowerShapeService : IGithubTowerShapeService
{
    private readonly IAirportSectorRepository _repo;
    private readonly ITowerShapeSource _source;
    private readonly ShapeFallbackScope _scope;

    public GithubTowerShapeService(
        IAirportSectorRepository repo, ITowerShapeSource source, ShapeFallbackScope? scope = null)
    {
        _repo = repo;
        _source = source;
        _scope = scope ?? new ShapeFallbackScope();
    }

    public async Task<int> ApplyAsync(string? icao = null, CancellationToken ct = default)
    {
        var filter = string.IsNullOrWhiteSpace(icao) ? null : icao.Trim().ToUpperInvariant();

        // Bersaglio: TWR senza poligono valido ("[]"/null → non proiettabile) OPPURE con un cerchio SINTETICO
        // di ripiego (il poligono reale GitHub è meglio del cerchio e lo rimpiazza). MAI una shape reale IVAO
        // (proiettabile e non sintetica): quella è verità primaria. La shape GitHub applicata (reale, non sintetica)
        // non è più un bersaglio → idempotente.
        var targets = (await _repo.ListTwrShapesAsync(ct))
            .Where(t => filter is null || string.Equals(t.AirportIcao, filter, StringComparison.OrdinalIgnoreCase))
            // ⚠️ Solo aeroporti della divisione: la TWR di un campo estero prende l'area da IVAO o resta senza
            // (ShapeFallbackScope). Vale anche col bottone manuale: se qualcuno passa un ICAO estero, non succede nulla.
            .Where(t => _scope.IsDomestic(t.AirportIcao))
            // ⚠️ E anche una shape presa dall'AIP: fra i due il sectorfile è la fonte primaria (decisione 2
            // del committente: l'AIP è secondaria, «solo se non la trovi nel sectorfile»). Senza questa riga
            // l'ATZ resterebbe al suo posto anche il giorno che `twrs.tfl` impara quella torre.
            .Where(t => t.IsShapeSynthetic
                        || t.ShapeSource == Vipi.Domain.ShapeSource.Aip
                        || AorPolygonProjector.Project(t.RawPolygon) is null)
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
