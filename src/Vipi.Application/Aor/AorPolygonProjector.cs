using System.Globalization;
using Vipi.Application.Content;

namespace Vipi.Application.Aor;

/// <summary>
/// Proiezione PURA (no I/O, deterministica, testabile) del poligono shape IVAO (<c>RegionMapPolygon</c>, JSON grezzo)
/// in un <see cref="AppAorPolygon"/> SVG: parsing difensivo → proiezione equirettangolare (longitudine scalata per
/// cos(lat medio)) → normalizzazione a un viewBox fisso. JSON non parsabile / poligono degenere → null (la UI mostra
/// il placeholder). Accetta più forme: <c>[[lng,lat],…]</c>, <c>[{"lat":..,"lng":..},…]</c> ed eventuale annidamento
/// di un livello (es. GeoJSON-like <c>[[[lng,lat],…]]</c>).
///
/// <para>⚠️ <b>Nelle coppie la longitudine viene PRIMA</b> — formato IVAO <c>regionMapPolygon</c>, stile
/// GeoJSON. Questo commento diceva il contrario fino al 23 agosto 2026: chi ne avesse ricavato una fixture
/// avrebbe scritto un poligono ruotato di 90°, e la proiezione non se ne lamenta — disegna. L'ordine si
/// decide in un posto solo, <see cref="PolygonGeometry.ParsePoints"/>, ed è quella la verità.</para>
/// </summary>
public static class AorPolygonProjector
{
    private const double Canvas = 400.0;   // lato lungo del viewBox normalizzato
    private const double Pad = 8.0;        // margine interno

    public static AppAorPolygon? Project(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return null;

        var pts = PolygonGeometry.ParsePoints(rawJson);   // parsing condiviso (JSON malformato → lista vuota)
        if (pts.Count < 3) return null;                   // poligono degenere

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

    private static string R(double v) => Math.Round(v, 1).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Proietta PIÙ poligoni in UN unico viewBox condiviso (stessa scala/origine), così che le forme mantengano la
    /// posizione relativa reale — necessario per giudicare visivamente un confine tra due settori. Ogni elemento di
    /// output è allineato per indice all'input (null se il grezzo è non parsabile/degenere). Ritorna null se nessun
    /// poligono è valido.
    /// </summary>
    public static SharedProjection? ProjectShared(IReadOnlyList<string?> rawPolygons)
    {
        var parsed = rawPolygons.Select(PolygonGeometry.ParsePoints).ToList();
        var all = parsed.Where(p => p.Count >= 3).SelectMany(p => p).ToList();
        if (all.Count < 3) return null;

        var latMean = all.Average(p => p.Lat);
        var k = Math.Cos(latMean * Math.PI / 180.0);
        double minX = all.Min(p => p.Lon * k), maxX = all.Max(p => p.Lon * k);
        double minY = all.Min(p => -p.Lat), maxY = all.Max(p => -p.Lat);
        double spanX = maxX - minX, spanY = maxY - minY;
        var span = Math.Max(spanX, spanY);
        if (span <= 0) return null;
        var scale = (Canvas - 2 * Pad) / span;
        var w = spanX * scale + 2 * Pad;
        var h = spanY * scale + 2 * Pad;

        var shapes = new List<SharedPolygon?>(parsed.Count);
        foreach (var pts in parsed)
        {
            if (pts.Count < 3) { shapes.Add(null); continue; }
            var sb = new System.Text.StringBuilder();
            for (var i = 0; i < pts.Count; i++)
            {
                var x = (pts[i].Lon * k - minX) * scale + Pad;
                var y = (-pts[i].Lat - minY) * scale + Pad;
                sb.Append(i == 0 ? 'M' : 'L').Append(R(x)).Append(' ').Append(R(y)).Append(' ');
            }
            sb.Append('Z');
            shapes.Add(new SharedPolygon(sb.ToString()));
        }
        return new SharedProjection($"0 0 {R(w)} {R(h)}", shapes);
    }
}

/// <summary>Poligono proiettato in un viewBox condiviso (solo il path SVG; le coordinate sono già relative).</summary>
public sealed record SharedPolygon(string Path);

/// <summary>Risultato di <see cref="AorPolygonProjector.ProjectShared"/>: viewBox comune + un path per input (per indice).</summary>
public sealed record SharedProjection(string ViewBox, IReadOnlyList<SharedPolygon?> Polygons);
