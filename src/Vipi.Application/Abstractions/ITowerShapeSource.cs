namespace Vipi.Application.Abstractions;

/// <summary>
/// Porta neutra: fornisce i poligoni TWR reali dalla sorgente esterna (impl. GitHub/Aurora <c>twrs.tfl</c> in
/// Infrastructure). Alternativa migliore al cerchio sintetico di fallback per le TWR senza shape dalla sorgente IVAO.
/// </summary>
public interface ITowerShapeSource
{
    /// <summary>
    /// Mappa callsign (es. <c>LIBA_TWR</c>, MAIUSCOLO) → poligono JSON nel formato <c>RegionMapPolygon</c>
    /// (array di coppie <c>[lng, lat]</c>, stile GeoJSON). Vuota se la sorgente non è configurata o è irraggiungibile.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetTowerPolygonsAsync(CancellationToken ct = default);
}
