using Vipi.Application.Aor;

namespace Vipi.Application.Airspace;

/// <summary>Un anello pronto per l'archivio: il JSON in forma IVAO, quanti punti, e il riquadro.</summary>
// ⚠️ Pubblico perché compare nella FIRMA di un tipo pubblico: chi lo restringe scopre che il
// compilatore lo dice da sé (CS0050/CS0051/CS0053). È superficie del modulo quanto il tipo che lo
// espone (ADR-0005 D6, revisione del 6 settembre 2026, R-009).
public sealed record AirspaceShape(
    string PolygonJson, int PointCount, double MinLat, double MinLon, double MaxLat, double MaxLon);

/// <summary>
/// Da anello letto ad anello archiviato. PURA, e sta in Application perché è qui che si decide una cosa che
/// conta: il volume entra in archivio nella <b>stessa forma del <c>regionMapPolygon</c> IVAO</b>, non in una
/// forma sua.
///
/// <para>È la scelta che fa funzionare tutto il resto senza toccarlo: <c>AorPolygonProjector</c>, la mappa
/// Leaflet, il viewer 3D, la stampa e il calcolo di adiacenza sanno già leggere quella forma, e continuano a
/// non sapere se il poligono che gli arriva viene da IVAO, dal sectorfile o dall'AIP.</para>
/// </summary>
public static class AirspaceShapeBuilder
{
    /// <summary>
    /// L'anello archiviato. ⚠️ <b>L'anello non si chiude</b>: il vertice di chiusura è una proprietà
    /// dell'anello e non un punto in più, e i consumatori chiudono da sé (lo fa già
    /// <c>PolygonGeometry.Contains</c>, che conta il lato ultimo→primo).
    /// </summary>
    public static AirspaceShape? Build(IReadOnlyList<(double Lat, double Lon)>? ring)
    {
        if (ring is null || ring.Count < 3) return null;

        return new AirspaceShape(
            IvaoPolygonJson.Write(ring),
            ring.Count,
            ring.Min(p => p.Lat), ring.Min(p => p.Lon),
            ring.Max(p => p.Lat), ring.Max(p => p.Lon));
    }
}
