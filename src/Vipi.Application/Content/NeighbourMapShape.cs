namespace Vipi.Application.Content;

/// <summary>Forma per la mappa di verifica: callsign del settore, path SVG (viewBox condiviso) e i punti
/// geografici [lat,lng] dell'anello (per la mappa Leaflet reale).</summary>
// ⚠️ Pubblico perché compare nella FIRMA di un tipo pubblico: chi lo restringe scopre che il
// compilatore lo dice da sé (CS0050/CS0051/CS0053). È superficie del modulo quanto il tipo che lo
// espone (ADR-0005 D6, revisione del 6 settembre 2026, R-009).
public sealed record NeighbourMapShape(string Sector, string Path, IReadOnlyList<double[]> Points);
