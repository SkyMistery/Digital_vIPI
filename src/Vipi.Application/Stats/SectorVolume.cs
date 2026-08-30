using Vipi.Application.Aor;

namespace Vipi.Application.Stats;

/// <summary>
/// Il volume di competenza di un settore: i poligoni (limiti orizzontali) <b>e</b> le loro bande di quota
/// (limiti verticali), insieme. È l'unità con cui il servizio statistiche decide se un traffico è stato gestito.
///
/// <para>La regola, posta dal committente: un traffico che sorvola a FL260 uno spazio che finisce a FL195
/// <b>non</b> è stato gestito da quello spazio. Un'attribuzione solo orizzontale conterebbe come «gestito»
/// mezzo traffico intercontinentale d'Europa.</para>
///
/// <para>⚠️ <b>I pezzi sono N, e ognuno ha LA SUA banda</b> (carta
/// <c>docs/refactor/15-shape-del-settore-una-porta-sola.md</c>). Amendola è di due zone e Catania di sette:
/// misurato, <c>LIBA_APP</c> è <c>GND → FL105</c> su una zona e <c>7000 FT AMSL → FL195</c> sull'altra. Un
/// inviluppo unico — base più bassa, tetto più alto — darebbe <c>GND → FL195</c>, cioè esattamente il
/// monoblocco generoso dell'anagrafica: rivendicherebbe il cielo sopra la prima zona e quello sotto la
/// seconda, che il CTR non ha. Per questo <see cref="Contains"/> chiede «dentro <b>un pezzo</b>», non «dentro
/// il poligono e dentro la banda».</para>
///
/// <para>Non reinventa niente: il poligono lo legge <see cref="PolygonGeometry"/>, la banda la normalizza
/// <see cref="AorFlBand"/> — che è anche l'unico posto dove vive l'euristica «&gt;660 = piedi, ≤660 = già FL»,
/// necessaria perché a schema l'unità dei limiti NON è tracciata. Puro e deterministico, nessun I/O.</para>
/// </summary>
public sealed class SectorVolume
{
    /// <summary>Un pezzo: un anello con la <b>sua</b> banda. Le due cose non si separano mai.</summary>
    public sealed record Part(PolygonGeometry.Ring Ring, int BottomFl, int TopFl);

    private SectorVolume(string callsign, IReadOnlyList<Part> parts)
    {
        Callsign = callsign;
        Parts = parts;
        BottomFl = parts.Min(p => p.BottomFl);
        TopFl = parts.Max(p => p.TopFl);
        MinLat = parts.Min(p => p.Ring.MinLat);
        MinLon = parts.Min(p => p.Ring.MinLon);
        MaxLat = parts.Max(p => p.Ring.MaxLat);
        MaxLon = parts.Max(p => p.Ring.MaxLon);
    }

    /// <summary>Callsign del settore (<c>ComposePosition</c> del catalogo), es. <c>LIRR_NE1_CTR</c>.</summary>
    public string Callsign { get; }

    /// <summary>I pezzi, in ordine di disegno. Almeno uno: un volume senza pezzi non nasce.</summary>
    public IReadOnlyList<Part> Parts { get; }

    /// <summary>Pavimento in FL dell'<b>inviluppo</b> (0 = suolo). ⚠️ Serve a <b>ordinare</b> due volumi
    /// quando si contendono un aeroplano, non a decidere se un aeroplano è dentro: quello lo fa
    /// <see cref="Contains"/>, pezzo per pezzo.</summary>
    public int BottomFl { get; }

    /// <summary>Tetto in FL dell'inviluppo (<see cref="AorFlBand.Unlimited"/> = senza limite). Vedi
    /// <see cref="BottomFl"/> per il perché è un inviluppo.</summary>
    public int TopFl { get; }

    /// <summary>Bounding box di tutti i pezzi: serve solo a ordinare due volumi, non a misurare.</summary>
    public double MinLat { get; }
    public double MinLon { get; }
    public double MaxLat { get; }
    public double MaxLon { get; }

    /// <summary>
    /// Volume di un settore dai campi grezzi del catalogo: <b>un</b> poligono e <b>una</b> banda. <c>null</c>
    /// se il poligono manca o è degenere (&lt; 3 punti, JSON malformato): un settore senza shape non
    /// attribuisce traffico — vale per gli 11 settori ACC e i 50 d'aeroporto che oggi ne sono privi.
    /// </summary>
    public static SectorVolume? From(string callsign, string? regionMapPolygon, int? lowerLimit, int? upperLimit) =>
        From(callsign, new[] { (regionMapPolygon, lowerLimit, upperLimit) });

    /// <summary>
    /// Volume di un settore da <b>N pezzi</b>, ognuno col suo poligono e le sue quote. I pezzi che non si
    /// parsano si scartano — un anello rotto non deve portarsi via gli altri sei — e se non ne resta nessuno
    /// il volume è <c>null</c>, cioè «questo settore non rivendica niente», mai «rivendica tutto».
    /// </summary>
    public static SectorVolume? From(
        string callsign, IReadOnlyList<(string? PolygonJson, int? Lower, int? Upper)> parts)
    {
        if (parts.Count == 0) return null;

        var pezzi = new List<Part>(parts.Count);
        foreach (var (json, lower, upper) in parts)
        {
            var ring = PolygonGeometry.ToRing(json);
            if (ring is null) continue;
            var (bottom, top) = AorFlBand.Normalize(lower, upper);
            pezzi.Add(new Part(ring, bottom, top));
        }

        return pezzi.Count == 0 ? null : new SectorVolume(callsign, pezzi);
    }

    /// <summary>
    /// Vero se l'aeroplano è dentro il volume: dentro <b>un pezzo</b>, cioè dentro il suo poligono <b>e</b>
    /// dentro la banda di <b>quel</b> pezzo. <paramref name="altitudeFt"/> è la quota del tracciato IVAO
    /// (<c>lastTrack.altitude</c>), in PIEDI: qui diventa FL (÷100) perché le bande sono in FL.
    /// </summary>
    public bool Contains(double lat, double lon, double altitudeFt)
    {
        var fl = altitudeFt / 100.0;
        foreach (var p in Parts)
        {
            if (fl < p.BottomFl || fl > p.TopFl) continue;
            if (PolygonGeometry.Contains(p.Ring, lat, lon)) return true;
        }
        return false;
    }
}
