using System.Globalization;
using System.Text;

namespace Vipi.Application.Aor;

/// <summary>
/// Genera un poligono shape circolare (PURO, deterministico, testabile) nel formato <c>RegionMapPolygon</c> della
/// sorgente: array JSON di coppie <c>[lng, lat]</c> (longitudine prima, stile GeoJSON — vedi
/// <see cref="AorPolygonProjector"/>), anello chiuso. Usato come fallback disegnabile per i settori TWR privi di
/// poligono reale. Approssimazione equirettangolare locale: 1 NM = 1852 m, 1° lat ≈ 111320 m, 1° lon scalato per
/// cos(lat). Adeguato a raggi piccoli (≈5 NM) alle latitudini italiane.
/// </summary>
public static class CircleShapeBuilder
{
    private const double MetersPerNm = 1852.0;
    private const double MetersPerDegLat = 111320.0;

    /// <summary>Costruisce il JSON del cerchio centrato in (lat, lon) con raggio in NM e numero di vertici dato.</summary>
    public static string Build(double centerLat, double centerLon, double radiusNm = 5.0, int points = 36)
    {
        if (points < 3) points = 3;
        var radiusM = radiusNm * MetersPerNm;
        var dLat = radiusM / MetersPerDegLat;
        var cos = Math.Cos(centerLat * Math.PI / 180.0);
        var dLon = radiusM / (MetersPerDegLat * (Math.Abs(cos) < 1e-9 ? 1e-9 : cos));

        var sb = new StringBuilder();
        sb.Append('[');
        for (var i = 0; i < points; i++)
        {
            var theta = 2.0 * Math.PI * i / points;
            var lat = centerLat + dLat * Math.Sin(theta);
            var lon = centerLon + dLon * Math.Cos(theta);
            if (i > 0) sb.Append(',');
            Append(sb, lon, lat);
        }
        // Chiude l'anello ripetendo il primo vertice (theta = 0 → lon = centerLon + dLon, lat = centerLat).
        sb.Append(',');
        Append(sb, centerLon + dLon, centerLat);
        sb.Append(']');
        return sb.ToString();
    }

    private static void Append(StringBuilder sb, double lon, double lat)
    {
        sb.Append('[')
          .Append(Math.Round(lon, 6).ToString(CultureInfo.InvariantCulture))
          .Append(',')
          .Append(Math.Round(lat, 6).ToString(CultureInfo.InvariantCulture))
          .Append(']');
    }
}
