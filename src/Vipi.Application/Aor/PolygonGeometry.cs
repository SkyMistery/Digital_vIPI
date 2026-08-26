using System.Text.Json;

namespace Vipi.Application.Aor;

/// <summary>
/// Geometria pura (no I/O, deterministica, testabile) sui poligoni shape IVAO (<c>RegionMapPolygon</c>,
/// JSON grezzo). Centralizza il parsing dei punti <c>(lat,lon)</c> — riusato da <see cref="AorPolygonProjector"/>
/// per la proiezione SVG e dall'adiacenza per capire quali settori confinano. Distanze in miglia nautiche
/// con approssimazione equirettangolare (buona alle medie latitudini, dove operano le divisioni europee).
/// </summary>
public static class PolygonGeometry
{
    private const double NmPerDegLat = 60.0;   // 1° di latitudine ≈ 60 NM

    /// <summary>
    /// La sorgente non ha mandato <b>niente</b>: campo assente, oppure un contenitore vuoto (<c>[]</c>,
    /// <c>{}</c>, <c>null</c>). Serve agli upsert per non scambiare un'assenza per un ordine di cancellare.
    ///
    /// <para><b>Perché è costata cara.</b> Gli upsert dei cataloghi preservavano già la shape quando la sorgente
    /// non la mandava — ma con un <c>is not null</c>, e dal 26 agosto 2026 IVAO risponde
    /// <c>regionMapPolygon: []</c> (misurato su <b>tutte e 229</b> le righe italiane), che quel controllo lo
    /// passa benissimo. Misurato su una copia del database vero: un solo giro d'import portava <b>83 poligoni a
    /// zero</b> — 66 reali presi da GitHub e 17 cerchi di ripiego — lasciando 142 righe con <c>"[]"</c>.</para>
    ///
    /// <para>⚠️ <b>Chiede se è vuoto, non se è valido</b>, e la differenza è voluta. Un <c>ParsePoints(...).Count
    /// &gt;= 3</c> sarebbe stato un <b>validatore</b>: il giorno in cui la sorgente manda una forma che questo
    /// parser non sa ancora leggere, un upsert-validatore la butterebbe via in silenzio tenendosi quella vecchia.
    /// Giudicare se una shape si disegna è compito di chi la disegna — <see cref="AorPolygonProjector.Project"/>
    /// e i ripieghi TWR — e quelli hanno già il loro ripiego. Qui si risponde solo alla domanda che l'upsert deve
    /// porsi: «la sorgente mi ha dato qualcosa?».</para>
    /// </summary>
    public static bool IsEmptyShape(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return true;
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            return doc.RootElement.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => true,
                JsonValueKind.Array => doc.RootElement.GetArrayLength() == 0,
                JsonValueKind.Object => !doc.RootElement.EnumerateObject().Any(),
                _ => false,
            };
        }
        catch (JsonException)
        {
            return false;   // non è JSON: è qualcosa, e non tocca a noi giudicarlo
        }
    }

    /// <summary>Anello di punti geografici + bounding box, pronto per test di adiacenza.</summary>
    public sealed record Ring(IReadOnlyList<(double Lat, double Lon)> Points,
        double MinLat, double MinLon, double MaxLat, double MaxLon);

    /// <summary>
    /// Estrae i punti <c>(lat,lon)</c> dal JSON <c>regionMapPolygon</c>/<c>regionMap</c> IVAO. Accetta più forme:
    /// <c>[[lng,lat],…]</c> (GeoJSON, longitudine prima), <c>[{lat,lng},…]</c>, wrapper oggetto
    /// (<c>points/coordinates/polygon/coords</c>) e un livello di annidamento (<c>[[[lng,lat],…]]</c>).
    /// JSON malformato → lista vuota.
    /// </summary>
    public static List<(double Lat, double Lon)> ParsePoints(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return new();
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            return SenzaPuntiGemelli(SenzaAnelliRipetuti(ExtractPoints(doc.RootElement)));
        }
        catch (JsonException) { return new(); }
    }

    /// <summary>
    /// Toglie le ripetizioni dell'<b>identico</b> anello: certe shape della sorgente contengono lo stesso
    /// contorno due volte di fila.
    ///
    /// <para>⚠️ Non è cosmesi. Col test pari/dispari un anello doppio <b>si annulla</b>: ogni attraversamento
    /// è contato due volte, la parità torna sempre pari, e il poligono non contiene <b>niente</b>. Misurato
    /// sul <c>vipi.db</c> reale (24 agosto 2026): 2 poligoni su 283, ma uno è <c>LIRR_TS_CTR</c> — un settore
    /// vero di Roma che nell'attribuzione del traffico non compariva mai, e nessuno se ne sarebbe accorto
    /// guardando la mappa, dove il contorno doppio si disegna identico a uno solo.</para>
    ///
    /// <para>Riconosce qualunque molteplicità (×2, ×3…), anche quando ogni copia si chiude ripetendo il
    /// primo punto. Un anello normale non viene toccato.</para>
    /// </summary>
    private static List<(double Lat, double Lon)> SenzaAnelliRipetuti(List<(double Lat, double Lon)> punti)
    {
        var copie = CopieDellAnello(punti);
        return copie > 1 ? punti.GetRange(0, punti.Count / copie) : punti;
    }

    /// <summary>
    /// Toglie i punti ripetuti <b>di fila</b>, cioè i lati di lunghezza zero.
    ///
    /// <para>Per il test punto-in-poligono sono innocui (un lato orizzontale-degenere non attraversa mai il
    /// raggio), ma per la <b>triangolazione</b> dell'estrusione 3D no: un vertice doppio produce facce
    /// degeneri, ed è il sospetto numero uno quando una shape a schermo «si vede strana».</para>
    ///
    /// <para>Misura sul <c>vipi.db</c> reale (25 agosto 2026): li ha il <b>29% dei poligoni</b> — 81 su 283,
    /// per 1547 lati a lunghezza zero in tutto, con punte di 489 su un solo settore (<c>DTTC_FSS</c>).</para>
    ///
    /// <para>Il punto di chiusura finale (uguale al primo) <b>si conserva</b>: è legittimo e i consumatori
    /// lo gestiscono già.</para>
    /// </summary>
    private static List<(double Lat, double Lon)> SenzaPuntiGemelli(List<(double Lat, double Lon)> punti)
    {
        if (punti.Count < 3) return punti;

        var puliti = new List<(double Lat, double Lon)>(punti.Count) { punti[0] };
        for (var i = 1; i < punti.Count; i++)
            if (punti[i] != punti[i - 1])
                puliti.Add(punti[i]);

        return puliti.Count >= 3 ? puliti : punti;
    }

    /// <summary>
    /// I punti <b>senza nessuna riparazione</b>: servono a chi deve <i>raccontare</i> l'anomalia invece di
    /// conviverci — la diagnostica. <see cref="ParsePoints"/> ripara, quindi guardando il suo risultato
    /// l'anomalia non c'è più.
    /// </summary>
    public static List<(double Lat, double Lon)> PuntiGrezzi(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return new();
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            return ExtractPoints(doc.RootElement);
        }
        catch (JsonException) { return new(); }
    }

    /// <summary>Quante volte l'identico anello si ripete dentro l'elenco di punti; 1 = normale.</summary>
    public static int CopieDellAnello(IReadOnlyList<(double Lat, double Lon)> punti)
    {
        var n = punti.Count;
        if (n < 6) return 1;

        for (var copie = 2; copie <= 6; copie++)
        {
            if (n % copie != 0) continue;

            var lunghezza = n / copie;
            var uguali = true;
            for (var i = lunghezza; i < n && uguali; i++)
                uguali = punti[i] == punti[i % lunghezza];

            if (uguali) return copie;
        }
        return 1;
    }

    /// <summary>Costruisce un <see cref="Ring"/> (punti + bbox) dal JSON grezzo; null se poligono degenere (&lt;3 punti).</summary>
    public static Ring? ToRing(string? rawJson)
    {
        var pts = ParsePoints(rawJson);
        if (pts.Count < 3) return null;
        return new Ring(pts,
            pts.Min(p => p.Lat), pts.Min(p => p.Lon), pts.Max(p => p.Lat), pts.Max(p => p.Lon));
    }

    /// <summary>
    /// Vero se i due anelli confinano: la distanza minima tra i loro bordi è &lt; <paramref name="thresholdNm"/>.
    /// Prefiltro con i bounding box (espansi della soglia) per scartare a costo O(1) le coppie lontane, poi
    /// distanza minima segmento-segmento. Cattura confini reali anche se i poligoni grezzi non combaciano esatti.
    /// </summary>
    public static bool AreAdjacent(Ring? a, Ring? b, double thresholdNm)
    {
        if (a is null || b is null || a.Points.Count < 2 || b.Points.Count < 2) return false;

        // Prefiltro bbox: se i box espansi della soglia non si sovrappongono, i bordi distano più della soglia.
        var latPad = thresholdNm / NmPerDegLat;
        var lonPad = thresholdNm / Math.Max(1e-6, NmPerDegLon((a.MinLat + a.MaxLat) / 2.0));
        if (a.MinLat - latPad > b.MaxLat || b.MinLat - latPad > a.MaxLat) return false;
        if (a.MinLon - lonPad > b.MaxLon || b.MinLon - lonPad > a.MaxLon) return false;

        // Early-exit: basta sapere se la distanza minima è sotto soglia (esce al primo segmento sotto soglia), non
        // serve il minimo esatto. Grande risparmio su poligoni molto densi (migliaia di punti) che si toccano.
        return EdgesWithinNm(a.Points, b.Points, thresholdNm);
    }

    /// <summary>Vero se esiste una coppia di lati (uno per anello) con distanza &lt; <paramref name="thresholdNm"/>.
    /// Esce alla prima coppia sotto soglia (non calcola il minimo assoluto).</summary>
    private static bool EdgesWithinNm(IReadOnlyList<(double Lat, double Lon)> a, IReadOnlyList<(double Lat, double Lon)> b, double thresholdNm)
    {
        var latMean = a.Concat(b).Average(p => p.Lat);
        var k = Math.Cos(latMean * Math.PI / 180.0);
        (double X, double Y) P((double Lat, double Lon) p) => (p.Lon * k, p.Lat);
        var thrDeg = thresholdNm / NmPerDegLat;

        for (int i = 0; i < a.Count; i++)
        {
            var a1 = P(a[i]); var a2 = P(a[(i + 1) % a.Count]);
            for (int j = 0; j < b.Count; j++)
            {
                var b1 = P(b[j]); var b2 = P(b[(j + 1) % b.Count]);
                if (SegSegDistance(a1, a2, b1, b2) < thrDeg) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Vero se il punto <paramref name="lat"/>/<paramref name="lon"/> cade dentro l'anello. Ray casting
    /// orizzontale (regola pari/dispari) sull'anello considerato CHIUSO: il catalogo IVAO non ripete il punto
    /// di chiusura, quindi il lato ultimo→primo va contato. Prefiltro con il bounding box, che sui poligoni
    /// densi (migliaia di punti) scarta a costo O(1) la quasi totalità dei piloti del mondo.
    /// <para>Il piano è (lon, lat) senza riscalare la longitudine: per l'appartenenza non serve isotropia —
    /// il test è topologico, non metrico — e riscalare cambierebbe solo la lunghezza dei lati, non da che
    /// parte del bordo sta il punto.</para>
    /// </summary>
    public static bool Contains(Ring? ring, double lat, double lon)
    {
        if (ring is null || ring.Points.Count < 3) return false;
        if (lat < ring.MinLat || lat > ring.MaxLat || lon < ring.MinLon || lon > ring.MaxLon) return false;

        var pts = ring.Points;
        var inside = false;
        for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
        {
            double yi = pts[i].Lat, xi = pts[i].Lon;
            double yj = pts[j].Lat, xj = pts[j].Lon;

            // Il lato attraversa la latitudine del punto? (un solo estremo incluso: niente doppio conteggio
            // quando il raggio passa esattamente per un vertice)
            if ((yi > lat) == (yj > lat)) continue;

            var xCross = xi + (lat - yi) / (yj - yi) * (xj - xi);
            if (lon < xCross) inside = !inside;
        }
        return inside;
    }

    /// <summary>Distanza minima (NM) tra i bordi di due anelli chiusi (ogni lato di A contro ogni lato di B).</summary>
    public static double MinEdgeDistanceNm(IReadOnlyList<(double Lat, double Lon)> a, IReadOnlyList<(double Lat, double Lon)> b)
    {
        // Scala longitudine per cos(lat medio) così che le distanze in gradi siano isotrope, poi in NM.
        var latMean = (a.Concat(b).Average(p => p.Lat));
        var k = Math.Cos(latMean * Math.PI / 180.0);
        (double X, double Y) P((double Lat, double Lon) p) => (p.Lon * k, p.Lat);

        double best = double.MaxValue;
        for (int i = 0; i < a.Count; i++)
        {
            var a1 = P(a[i]); var a2 = P(a[(i + 1) % a.Count]);
            for (int j = 0; j < b.Count; j++)
            {
                var b1 = P(b[j]); var b2 = P(b[(j + 1) % b.Count]);
                var dDeg = SegSegDistance(a1, a2, b1, b2);
                if (dDeg < best) best = dDeg;
            }
        }
        return best * NmPerDegLat;   // gradi → NM (l'asse Y è latitudine; X già riscalato a lat)
    }

    private static double NmPerDegLon(double lat) => NmPerDegLat * Math.Max(1e-6, Math.Cos(lat * Math.PI / 180.0));

    // --- Distanza segmento-segmento nel piano proiettato ---
    private static double SegSegDistance((double X, double Y) p1, (double X, double Y) p2,
        (double X, double Y) q1, (double X, double Y) q2)
    {
        if (SegmentsIntersect(p1, p2, q1, q2)) return 0.0;
        return Math.Min(
            Math.Min(PointSegDistance(p1, q1, q2), PointSegDistance(p2, q1, q2)),
            Math.Min(PointSegDistance(q1, p1, p2), PointSegDistance(q2, p1, p2)));
    }

    private static double PointSegDistance((double X, double Y) p, (double X, double Y) a, (double X, double Y) b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double len2 = dx * dx + dy * dy;
        double t = len2 <= 1e-18 ? 0.0 : ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2;
        t = Math.Clamp(t, 0.0, 1.0);
        double cx = a.X + t * dx, cy = a.Y + t * dy;
        return Math.Sqrt((p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy));
    }

    private static bool SegmentsIntersect((double X, double Y) p1, (double X, double Y) p2,
        (double X, double Y) p3, (double X, double Y) p4)
    {
        double d1 = Cross(p3, p4, p1), d2 = Cross(p3, p4, p2);
        double d3 = Cross(p1, p2, p3), d4 = Cross(p1, p2, p4);
        return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
               ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
    }

    private static double Cross((double X, double Y) a, (double X, double Y) b, (double X, double Y) c) =>
        (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    // --- Parsing (spostato da AorPolygonProjector per riuso) ---
    private static List<(double Lat, double Lon)> ExtractPoints(JsonElement root)
    {
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

        // Annidamento di un livello (es. [[[lng,lat],…]]): scendi al primo anello.
        //
        // ⚠️ In GeoJSON quel livello è l'elenco dei poligoni (MultiPolygon) o degli anelli (esterno + buchi).
        // Prendendo solo `items[0]` un settore composto da due aree disgiunte entrerebbe con la SOLA prima,
        // in silenzio — e da lì alimenterebbe la mappa AoR, il calcolo di adiacenza dei confinanti e i
        // poligoni pubblicati. Un settore che perde metà della propria forma sbaglia i vicini, e i vicini
        // decidono i coordinamenti.
        //
        // <b>Misurato l'11 agosto 2026 sui dati reali</b> (`vipi.db`, 1338 poligoni fra AccSectors,
        // AirportSectors e SpecialAreas): <b>zero</b> casi con più di un anello a questo livello. 1273
        // anelli singoli, 50 colonne vuote, 15 array vuoti `[]`. Quindi oggi il ramo non perde niente.
        // Resta una trappola per domani, non un difetto di adesso — e per questo la si lascia com'è invece
        // di far restituire più anelli a `ToRing`, che vorrebbe dire toccare tutti i consumatori per un caso
        // che non si verifica. Se la misura cambia, questa è la riga da cui ripartire.
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
}
