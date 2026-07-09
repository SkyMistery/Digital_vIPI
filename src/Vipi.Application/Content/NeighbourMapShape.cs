namespace Vipi.Application.Content;

/// <summary>Forma per la mappa di verifica: callsign del settore, path SVG (viewBox condiviso) e i punti
/// geografici [lat,lng] dell'anello (per la mappa Leaflet reale).</summary>
public sealed record NeighbourMapShape(string Sector, string Path, IReadOnlyList<double[]> Points);
