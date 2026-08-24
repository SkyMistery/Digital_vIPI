using Vipi.Application.Aor;

namespace Vipi.Application.Stats;

/// <summary>
/// Il volume di competenza di un settore: il poligono (limiti orizzontali) <b>e</b> la banda di quota (limiti
/// verticali), insieme. È l'unità con cui il servizio statistiche decide se un traffico è stato gestito.
///
/// <para>La regola, posta dal committente: un traffico che sorvola a FL260 uno spazio che finisce a FL195
/// <b>non</b> è stato gestito da quello spazio. Un'attribuzione solo orizzontale conterebbe come «gestito»
/// mezzo traffico intercontinentale d'Europa.</para>
///
/// <para>Non reinventa niente: il poligono lo legge <see cref="PolygonGeometry"/>, la banda la normalizza
/// <see cref="AorFlBand"/> — che è anche l'unico posto dove vive l'euristica «&gt;660 = piedi, ≤660 = già FL»,
/// necessaria perché a schema l'unità dei limiti NON è tracciata. Puro e deterministico, nessun I/O.</para>
/// </summary>
public sealed class SectorVolume
{
    private SectorVolume(string callsign, PolygonGeometry.Ring ring, int bottomFl, int topFl)
    {
        Callsign = callsign;
        Ring = ring;
        BottomFl = bottomFl;
        TopFl = topFl;
    }

    /// <summary>Callsign del settore (<c>ComposePosition</c> del catalogo), es. <c>LIRR_NE1_CTR</c>.</summary>
    public string Callsign { get; }

    /// <summary>Anello geografico con bounding box.</summary>
    public PolygonGeometry.Ring Ring { get; }

    /// <summary>Pavimento in FL (0 = suolo).</summary>
    public int BottomFl { get; }

    /// <summary>Tetto in FL (<see cref="AorFlBand.Unlimited"/> = senza limite).</summary>
    public int TopFl { get; }

    /// <summary>
    /// Volume del settore dai campi grezzi del catalogo. <c>null</c> se il poligono manca o è degenere
    /// (&lt; 3 punti, JSON malformato): un settore senza shape non attribuisce traffico — vale per gli 11
    /// settori ACC e i 50 d'aeroporto che oggi ne sono privi.
    /// </summary>
    public static SectorVolume? From(string callsign, string? regionMapPolygon, int? lowerLimit, int? upperLimit)
    {
        var ring = PolygonGeometry.ToRing(regionMapPolygon);
        if (ring is null) return null;

        var (bottom, top) = AorFlBand.Normalize(lowerLimit, upperLimit);
        return new SectorVolume(callsign, ring, bottom, top);
    }

    /// <summary>
    /// Vero se l'aeroplano è dentro il volume: dentro il poligono <b>e</b> dentro la banda.
    /// <paramref name="altitudeFt"/> è la quota del tracciato IVAO (<c>lastTrack.altitude</c>), in PIEDI:
    /// qui diventa FL (÷100) perché la banda è in FL.
    /// </summary>
    public bool Contains(double lat, double lon, double altitudeFt)
    {
        var fl = altitudeFt / 100.0;
        if (fl < BottomFl || fl > TopFl) return false;
        return PolygonGeometry.Contains(Ring, lat, lon);
    }
}
