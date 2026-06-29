using System.Globalization;
using System.Text.Json;
using Vipi.Application.Content;

namespace Vipi.Application.Aor;

/// <summary>
/// Proiezione PURA (no I/O, deterministica, testabile) del poligono shape IVAO (<c>RegionMapPolygon</c>, JSON grezzo)
/// in un <see cref="AppAorPolygon"/> SVG: parsing difensivo → proiezione equirettangolare (longitudine scalata per
/// cos(lat medio)) → normalizzazione a un viewBox fisso. JSON non parsabile / poligono degenere → null (la UI mostra
/// il placeholder). Accetta più forme: <c>[[lat,lon],…]</c>, <c>[{"lat":..,"lon":..},…]</c> ed eventuale annidamento
/// di un livello (es. GeoJSON-like <c>[[[lat,lon],…]]</c>).
/// </summary>
public static class AorPolygonProjector
{
    private const double Canvas = 400.0;   // lato lungo del viewBox normalizzato
    private const double Pad = 8.0;        // margine interno

    public static AppAorPolygon? Project(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return null;

        List<(double Lat, double Lon)> pts;
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            pts = ExtractPoints(doc.RootElement);
        }
        catch (JsonException) { return null; }   // JSON malformato → placeholder

        if (pts.Count < 3) return null;          // poligono degenere

        // Proiezione equirettangolare: x = lon·cos(latMedio), y = -lat (nord in alto). Aspetto corretto alle medie latitudini.
        var latMean = pts.Average(p => p.Lat);
        var k = Math.Cos(latMean * Math.PI / 180.0);
        var proj = pts.Select(p => (X: p.Lon * k, Y: -p.Lat)).ToList();

        double minX = proj.Min(p => p.X), maxX = proj.Max(p => p.X);
        double minY = proj.Min(p => p.Y), maxY = proj.Max(p => p.Y);
        double spanX = maxX - minX, spanY = maxY - minY;
        if (spanX <= 0 && spanY <= 0) return null;   // tutti i punti coincidenti

        // Scala uniforme per preservare la forma; il lato maggiore diventa (Canvas - 2·Pad).
        var span = Math.Max(spanX, spanY);
        var scale = span > 0 ? (Canvas - 2 * Pad) / span : 1.0;
        var w = spanX * scale + 2 * Pad;
        var h = spanY * scale + 2 * Pad;

        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < proj.Count; i++)
        {
            var x = (proj[i].X - minX) * scale + Pad;
            var y = (proj[i].Y - minY) * scale + Pad;
            sb.Append(i == 0 ? 'M' : 'L');
            sb.Append(R(x)).Append(' ').Append(R(y)).Append(' ');
        }
        sb.Append('Z');

        var viewBox = $"0 0 {R(w)} {R(h)}";

        // Estensione geografica reale (per la mappa): punti [lat,lon] + bounding box + centro.
        double minLat = pts.Min(p => p.Lat), maxLat = pts.Max(p => p.Lat);
        double minLon = pts.Min(p => p.Lon), maxLon = pts.Max(p => p.Lon);
        var points = pts.Select(p => new[] { p.Lat, p.Lon }).ToList();
        return new AppAorPolygon(viewBox, sb.ToString(), points,
            minLat, minLon, maxLat, maxLon, (minLat + maxLat) / 2.0, (minLon + maxLon) / 2.0);
    }

    private static List<(double Lat, double Lon)> ExtractPoints(JsonElement root)
    {
        // Oggetto wrapper: cerca una proprietà array nota (points/coordinates/polygon) e ricorre.
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var name in new[] { "points", "coordinates", "polygon", "coords" })
                if (root.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array)
                    return ExtractPoints(arr);
            return new();
        }

        if (root.ValueKind != JsonValueKind.Array) return new();

        var items = root.EnumerateArray().ToList();
        if (items.Count == 0) return new();

        // Annidamento di un livello (es. [[[lat,lon],…]]): scendi al primo anello.
        if (items[0].ValueKind == JsonValueKind.Array &&
            items[0].EnumerateArray().FirstOrDefault().ValueKind == JsonValueKind.Array)
            return ExtractPoints(items[0]);

        var result = new List<(double, double)>();
        foreach (var item in items)
        {
            if (item.ValueKind == JsonValueKind.Array)
            {
                var nums = item.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Number)
                    .Select(e => e.GetDouble()).ToList();
                // Formato IVAO `regionMapPolygon`: coppie [lng, lat] (longitudine prima, stile GeoJSON).
                if (nums.Count >= 2) result.Add((nums[1], nums[0]));
            }
            else if (item.ValueKind == JsonValueKind.Object)
            {
                // Formato IVAO `regionMap`: oggetti {lat, lng}.
                var lat = Num(item, "lat", "latitude", "y");
                var lon = Num(item, "lon", "lng", "longitude", "x");
                if (lat is double la && lon is double lo) result.Add((la, lo));
            }
        }
        return result;
    }

    private static double? Num(JsonElement obj, params string[] names)
    {
        foreach (var n in names)
            if (obj.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number)
                return v.GetDouble();
        return null;
    }

    private static string R(double v) => Math.Round(v, 1).ToString(CultureInfo.InvariantCulture);
}
