namespace Aip.Aurora;

/// <summary>
/// Disegna un tratto di confine o di costa da <paramref name="from"/> a <paramref name="to"/>: i punti <b>in mezzo</b>,
/// senza i due capi. Null se non lo sa disegnare (allora il converter tira una retta e lo dice).
/// <para>Il modello non ha il tracciato (il PDF non lo dà): lo dà chi usa il converter, per l'Italia da
/// <c>itgeo.geo</c>.</para>
/// </summary>
public delegate IReadOnlyList<Position>? BorderTracer(BorderSegment segment, Position from, Position to);

/// <summary>Come disegnare: la densità degli archi e, se c'è, chi traccia i confini.</summary>
public sealed record OutlineOptions
{
    /// <summary>Punti per grado d'arco di base (decisione 13 della carta madre).</summary>
    public const double DefaultPointsPerDegree = 1;

    public const double MinPointsPerDegree = 0.1;

    public const double MaxPointsPerDegree = 10;

    /// <summary>
    /// Oltre questo scarto fra il raggio stampato e la distanza vera di un estremo dal centro, l'arco si segnala:
    /// 0,1 NM, la soglia della F1 (tutti i 128 estremi veri misurati stanno sotto; un centro sbagliato sbaglia di miglia).
    /// </summary>
    public const double RadiusToleranceNm = 0.1;

    /// <summary>Riportata fra <see cref="MinPointsPerDegree"/> e <see cref="MaxPointsPerDegree"/>.</summary>
    public double PointsPerDegree { get; init; } = DefaultPointsPerDegree;

    public BorderTracer? Border { get; init; }

    internal double Density => Math.Clamp(PointsPerDegree, MinPointsPerDegree, MaxPointsPerDegree);
}

/// <summary>
/// Da una geometria del modello ai vertici di un anello chiuso (il primo <b>non</b> si ripete in fondo).
/// <para>🔴 I vertici dichiarati dall'AIP escono <b>identici</b>: un arco passa esattamente per i suoi estremi, perché
/// il raggio si interpola fra la distanza vera dell'inizio e quella della fine (come la F1). Il raggio stampato serve
/// solo al controllo.</para>
/// </summary>
public static class Outline
{
    private const double SamePoint = 1e-9;

    public static IReadOnlyList<Position> Points(Geometry geometry, OutlineOptions? options, ICollection<AuroraIssue> issues, string key)
    {
        var o = options ?? new OutlineOptions();
        return geometry switch
        {
            CircleGeometry c => Circle(c, o.Density),
            RingGeometry r => Ring(r, o, issues, key),
            CorridorGeometry k => Corridor(k),
            _ => throw new ArgumentException($"Geometria sconosciuta: {geometry.GetType().Name}", nameof(geometry)),
        };
    }

    /// <summary>
    /// Il contorno di un corridoio: la linea spostata di metà ampiezza a destra, all'andata, e a sinistra, al ritorno; le
    /// testate dritte (l'AIP non dice altro). Nei punti di mezzo lo spostamento va sulla bisettrice, allungato perché
    /// l'ampiezza resti quella sui due lati (fino a tre volte la metà: oltre, in una svolta quasi all'indietro, si ferma).
    /// </summary>
    public static IReadOnlyList<Position> Corridor(CorridorGeometry corridor)
    {
        ArgumentNullException.ThrowIfNull(corridor);
        var p = corridor.Points;
        var meta = corridor.Width.ToNauticalMiles() / 2;
        if (p.Count < 2) return [.. p];
        var destra = new List<Position>(p.Count);
        var sinistra = new List<Position>(p.Count);
        for (var i = 0; i < p.Count; i++)
        {
            double rotta, quanto = meta;
            if (i == 0) rotta = Sphere.BearingDeg(p[0], p[1]);
            else if (i == p.Count - 1) rotta = Sphere.BearingDeg(p[i - 1], p[i]);
            else
            {
                var prima = Sphere.BearingDeg(p[i - 1], p[i]);
                var dopo = Sphere.BearingDeg(p[i], p[i + 1]);
                var svolta = Sphere.Normalize(dopo - prima);
                if (svolta > 180) svolta -= 360;
                rotta = Sphere.Normalize(prima + svolta / 2);
                quanto = meta / Math.Max(Math.Cos(svolta / 2 * Math.PI / 180), 1.0 / 3);
            }
            destra.Add(Sphere.Destination(p[i], Sphere.Normalize(rotta + 90), quanto));
            sinistra.Add(Sphere.Destination(p[i], Sphere.Normalize(rotta - 90), quanto));
        }
        sinistra.Reverse();
        return [.. destra, .. sinistra];
    }

    /// <summary>Il cerchio dal nord, in senso orario: <c>ceil(360 × densità)</c> vertici sul raggio.</summary>
    public static IReadOnlyList<Position> Circle(CircleGeometry circle, double pointsPerDegree)
    {
        var n = Math.Max(3, (int)Math.Ceiling(360 * pointsPerDegree - 1e-9));
        var r = circle.Radius.ToNauticalMiles();
        var punti = new List<Position>(n);
        for (var i = 0; i < n; i++)
            punti.Add(Sphere.Destination(circle.Center, 360.0 * i / n, r));
        return punti;
    }

    /// <summary>
    /// I vertici di un arco da <paramref name="from"/> a <paramref name="to"/> attorno a <paramref name="center"/>:
    /// <paramref name="from"/> compreso, <paramref name="to"/> escluso (è l'inizio del tratto dopo). Se i due capi
    /// coincidono l'arco è il giro intero («till point of origin» su un'area fatta del solo arco).
    /// </summary>
    public static IReadOnlyList<Position> Arc(Position from, Position to, Position center, ArcDirection direction, double pointsPerDegree)
    {
        var clockwise = direction == ArcDirection.Clockwise;
        var r0 = Sphere.DistanceNm(center, from);
        var r1 = Sphere.DistanceNm(center, to);
        var b0 = Sphere.BearingDeg(center, from);
        var b1 = Sphere.BearingDeg(center, to);

        var ampiezza = Same(from, to) ? 360.0 : Sphere.Normalize(clockwise ? b1 - b0 : b0 - b1);
        var verso = clockwise ? 1.0 : -1.0;
        var tratti = Math.Max(1, (int)Math.Ceiling(ampiezza * pointsPerDegree - 1e-9));

        var punti = new List<Position>(tratti) { from };
        for (var i = 1; i < tratti; i++)
        {
            var t = (double)i / tratti;
            punti.Add(Sphere.Destination(center, b0 + verso * ampiezza * t, r0 + (r1 - r0) * t));
        }
        return punti;
    }

    private static List<Position> Ring(RingGeometry ring, OutlineOptions o, ICollection<AuroraIssue> issues, string key)
    {
        var punti = new List<Position>();
        var tratti = ring.Segments;
        for (var i = 0; i < tratti.Count; i++)
        {
            var tratto = tratti[i];
            var fine = tratti[(i + 1) % tratti.Count].From;
            switch (tratto)
            {
                case LineSegment:
                    punti.Add(tratto.From);
                    break;

                case ArcSegment arco:
                    if (arco.Radius is { } raggio)
                    {
                        var r = raggio.ToNauticalMiles();
                        var scarto = Math.Max(
                            Math.Abs(Sphere.DistanceNm(arco.Center, arco.From) - r),
                            Math.Abs(Sphere.DistanceNm(arco.Center, fine) - r));
                        if (scarto > OutlineOptions.RadiusToleranceNm)
                            issues.Add(new AuroraIssue(AuroraIssueKind.RadiusMismatch, key,
                                scarto.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)));
                    }
                    punti.AddRange(Arc(arco.From, fine, arco.Center, arco.Direction, o.Density));
                    break;

                case BorderSegment confine:
                    punti.Add(confine.From);
                    var traccia = o.Border?.Invoke(confine, confine.From, fine);
                    if (traccia is null)
                        issues.Add(new AuroraIssue(AuroraIssueKind.BorderNotDrawn, key, confine.Text));
                    else
                        punti.AddRange(traccia);
                    break;

                default:
                    throw new ArgumentException($"Tratto sconosciuto: {tratto.GetType().Name}", nameof(ring));
            }
        }
        return punti;
    }

    private static bool Same(Position a, Position b) =>
        Math.Abs(a.Lat - b.Lat) < SamePoint && Math.Abs(a.Lon - b.Lon) < SamePoint;
}

/// <summary>Cosa il converter ha fatto con una scelta da dire. ⚠️ Un codice, non una frase.</summary>
public sealed record AuroraIssue(AuroraIssueKind Kind, string Key, string? Detail = null);

public enum AuroraIssueKind
{
    /// <summary>Un tratto di confine o di costa senza tracciato: i due capi uniti da una retta. <c>Detail</c> = la frase.</summary>
    BorderNotDrawn,

    /// <summary>Un estremo dell'arco dista dal centro oltre la soglia dal raggio stampato. <c>Detail</c> = lo scarto in NM.</summary>
    RadiusMismatch,

    /// <summary>Due aree con lo stesso nome nello stesso file: alla seconda si è aggiunto il suo nome. <c>Detail</c> = il nome scritto.</summary>
    DuplicateName,

    /// <summary>Il nome aveva caratteri che il formato non regge (<c>;</c>, <c>"</c>): sostituiti. <c>Detail</c> = il nome scritto.</summary>
    NameChanged,
}
