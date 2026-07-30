using Vipi.Application.Abstractions;

namespace Vipi.Application.Content;

/// <summary>
/// Risolve dalla sorgente esterna (porte <see cref="IAccDirectory"/> / <see cref="IAirportDetailProvider"/>) un
/// settore estero aggiunto a mano, instradando per natura del callsign: subcenter dell'ACC estero (CTR/FSS) o
/// postazione d'aeroporto (APP/DEP/TWR/GND/DEL). Isolato da <see cref="NeighbourImportService"/> per testabilità
/// (stesso pattern di <see cref="ForeignAccFetcher"/>). Ritorna un <see cref="SourceSubcenter"/> pronto per
/// <c>PersistForeignCatalogAsync</c> (CenterId = ACC estero), oppure null se il callsign non esiste sulla sorgente.
/// </summary>
public sealed class ForeignSectorResolver
{
    private readonly IAccDirectory _directory;
    private readonly IAirportDetailProvider _details;

    public ForeignSectorResolver(IAccDirectory directory, IAirportDetailProvider details)
    {
        _directory = directory;
        _details = details;
    }

    public async Task<SourceSubcenter?> ResolveAsync(ForeignSectorCallsign p, string centerId, CancellationToken ct = default)
    {
        if (p.Kind == ForeignSectorKind.Center)
        {
            var subs = await _directory.GetSubcentersAsync(centerId, ct);
            var hit = subs.FirstOrDefault(s => string.Equals(s.ComposePosition, p.Callsign, StringComparison.OrdinalIgnoreCase));
            if (hit is null) return null;
            return new SourceSubcenter(p.Callsign, centerId, hit.Position ?? p.Suffix, hit.MiddleIdentifier,
                hit.Frequency, hit.RegionMapPolygon, hit.AtcCallsign, hit.LowerLimit, hit.UpperLimit);
        }

        // Aeroporto: la lista postazioni conferma l'esistenza + dà nome/freq; il dettaglio aggiunge poligono/limiti.
        var positions = await _details.GetAtcPositionsAsync(p.Icao, ct);
        var pos = positions.FirstOrDefault(x => string.Equals(x.Callsign, p.Callsign, StringComparison.OrdinalIgnoreCase));
        if (pos is null) return null;
        var detail = await _details.GetAtcPositionDetailAsync(p.Callsign, ct);
        return new SourceSubcenter(
            ComposePosition: p.Callsign,
            CenterId: centerId,
            Position: pos.Position ?? p.Suffix,
            MiddleIdentifier: pos.MiddleIdentifier,
            Frequency: detail?.Frequency ?? pos.Frequency,
            RegionMapPolygon: detail?.RegionMapPolygon,
            AtcCallsign: pos.AtcCallsign,
            LowerLimit: detail?.LowerLimit,
            UpperLimit: detail?.UpperLimit);
    }
}
