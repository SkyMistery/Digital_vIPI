using System.Text.Json;

namespace Vipi.Application.Coordinates;

/// <summary>
/// Il GeoJSON <b>con il suo tipo</b> — <c>Feature</c>, <c>FeatureCollection</c>, <c>Polygon</c>, <c>MultiPolygon</c> —
/// per il convertitore di coordinate.
///
/// <para>🔴 U-222 (revisione totale 3): il convertitore passava ogni JSON a <c>PolygonGeometry.ParsePoints</c>, che
/// conosce le forme di IVAO (<c>regionMapPolygon</c>, un anello) e in un oggetto cerca solo
/// <c>points/coordinates/polygon/coords</c>. Un Feature — la forma che esce da qualunque strumento GIS — non aveva
/// niente di tutto questo in cima, usciva vuoto e cadeva nel lettore a righe: vertici a caso, latitudine e
/// longitudine invertite.</para>
///
/// <para>⚠️ Qui e non in <c>PolygonGeometry</c>: quello serve i cataloghi (AoR, statistiche, confinanti), dove un
/// anello per settore è la regola misurata. Il convertitore invece deve dare <b>un'area per poligono</b>.</para>
///
/// <para>⚠️ Come in tutto il GeoJSON la <b>longitudine viene prima</b>. I buchi (anelli oltre il primo) si
/// scartano e si dice, come per il KML (<see cref="CoordinateIssueKind.BucoScartato"/>).</para>
/// </summary>
internal static class GeoJsonReader
{
    /// <summary>Vero se il testo è GeoJSON con un tipo che si sa leggere; <paramref name="esito"/> ne porta le aree.</summary>
    public static bool Prova(string testo, out CoordinateReadResult esito)
    {
        esito = CoordinateReadResult.Vuoto;
        JsonDocument doc;
        try { doc = JsonDocument.Parse(testo); }
        catch (JsonException) { return false; }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || Tipo(root) is not { } tipo) return false;
            if (tipo is not ("Feature" or "FeatureCollection" or "Polygon" or "MultiPolygon")) return false;

            var aree = new List<CoordinateArea>();
            var segnalazioni = new List<CoordinateIssue>();
            if (tipo == "FeatureCollection")
            {
                if (root.TryGetProperty("features", out var features) && features.ValueKind == JsonValueKind.Array)
                    foreach (var f in features.EnumerateArray()) Feature(f, aree, segnalazioni);
            }
            else if (tipo == "Feature") Feature(root, aree, segnalazioni);
            else Geometria(root, null, aree, segnalazioni);

            if (aree.Count == 0) return false;
            var righe = testo.Replace("\r\n", "\n").Split('\n').Length;
            esito = new CoordinateReadResult(aree, segnalazioni, righe, righe);
            return true;
        }
    }

    private static string? Tipo(JsonElement o) =>
        o.ValueKind == JsonValueKind.Object && o.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String
            ? t.GetString() : null;

    private static void Feature(JsonElement f, List<CoordinateArea> aree, List<CoordinateIssue> segnalazioni)
    {
        if (Tipo(f) != "Feature" || !f.TryGetProperty("geometry", out var g)) return;
        string? nome = null;
        if (f.TryGetProperty("properties", out var p) && p.ValueKind == JsonValueKind.Object
            && p.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
            nome = n.GetString();
        Geometria(g, nome, aree, segnalazioni);
    }

    private static void Geometria(JsonElement g, string? nome, List<CoordinateArea> aree, List<CoordinateIssue> segnalazioni)
    {
        if (!g.TryGetProperty("coordinates", out var c) || c.ValueKind != JsonValueKind.Array) return;
        switch (Tipo(g))
        {
            case "Polygon":
                Poligono(c, nome, aree, segnalazioni);
                break;
            case "MultiPolygon":
                foreach (var poligono in c.EnumerateArray())
                    if (poligono.ValueKind == JsonValueKind.Array) Poligono(poligono, nome, aree, segnalazioni);
                break;
        }
    }

    private static void Poligono(JsonElement anelli, string? nome, List<CoordinateArea> aree, List<CoordinateIssue> segnalazioni)
    {
        var lista = anelli.EnumerateArray().Where(a => a.ValueKind == JsonValueKind.Array).ToList();
        if (lista.Count == 0) return;
        if (lista.Count > 1) segnalazioni.Add(new CoordinateIssue(CoordinateIssueKind.BucoScartato, 0, nome ?? ""));

        var punti = new List<(double Lat, double Lon)>();
        foreach (var v in lista[0].EnumerateArray())
        {
            if (v.ValueKind != JsonValueKind.Array) continue;
            var nums = v.EnumerateArray().ToList();
            if (nums.Count >= 2 && Numero(nums[0]) is double lon && Numero(nums[1]) is double lat) punti.Add((lat, lon));
        }
        if (punti.Count == 0) return;

        var chiuso = punti.Count > 2 && punti[0] == punti[^1];
        if (chiuso) punti.RemoveAt(punti.Count - 1);
        aree.Add(new CoordinateArea(nome, punti, chiuso));
    }

    /// <summary>Un numero finito, o null. ⚠️ <c>GetDouble</c> su <c>1e999</c> lancia (U-229).</summary>
    internal static double? Numero(JsonElement e) =>
        e.ValueKind == JsonValueKind.Number && e.TryGetDouble(out var d) && double.IsFinite(d) ? d : null;
}
