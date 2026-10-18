namespace Aip.Aurora;

/// <summary>
/// Distanze, rotte e punti di arrivo <b>sulla sfera</b>. Come il lettore della F1 (carta
/// <c>2026-09-18-f1-archi-convertitore.md</c> §2): su 64 archi veri dell'AIP lo scarto fra sfera ed ellissoide sta
/// sotto la precisione del dato (coordinate al secondo, ~30 m), e una libreria geodetica non comprerebbe niente.
/// <para>Scritto qui e non preso dalla F1 perché il converter si porta via da solo (carta F6 §3).</para>
/// </summary>
public static class Sphere
{
    /// <summary>Raggio medio della Terra (IUGG), in NM.</summary>
    public const double EarthRadiusNm = 6371008.8 / 1852.0;

    private const double Rad = Math.PI / 180.0;

    /// <summary>La distanza ortodromica, in NM (formula dell'emisenoverso).</summary>
    public static double DistanceNm(Position a, Position b)
    {
        var dLat = (b.Lat - a.Lat) * Rad;
        var dLon = (b.Lon - a.Lon) * Rad;
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(a.Lat * Rad) * Math.Cos(b.Lat * Rad) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * EarthRadiusNm * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    /// <summary>La rotta vera iniziale da <paramref name="a"/> a <paramref name="b"/>, in gradi [0, 360).</summary>
    public static double BearingDeg(Position a, Position b)
    {
        var dLon = (b.Lon - a.Lon) * Rad;
        var y = Math.Sin(dLon) * Math.Cos(b.Lat * Rad);
        var x = Math.Cos(a.Lat * Rad) * Math.Sin(b.Lat * Rad)
            - Math.Sin(a.Lat * Rad) * Math.Cos(b.Lat * Rad) * Math.Cos(dLon);
        return Normalize(Math.Atan2(y, x) / Rad);
    }

    /// <summary>Il punto a <paramref name="distanceNm"/> da <paramref name="from"/> lungo la rotta <paramref name="bearingDeg"/>.</summary>
    public static Position Destination(Position from, double bearingDeg, double distanceNm)
    {
        var d = distanceNm / EarthRadiusNm;
        var b = bearingDeg * Rad;
        var lat1 = from.Lat * Rad;
        var lon1 = from.Lon * Rad;
        var lat2 = Math.Asin(Math.Sin(lat1) * Math.Cos(d) + Math.Cos(lat1) * Math.Sin(d) * Math.Cos(b));
        var lon2 = lon1 + Math.Atan2(Math.Sin(b) * Math.Sin(d) * Math.Cos(lat1), Math.Cos(d) - Math.Sin(lat1) * Math.Sin(lat2));
        return new Position(lat2 / Rad, (lon2 / Rad + 540) % 360 - 180);
    }

    /// <summary>Un angolo riportato in [0, 360).</summary>
    public static double Normalize(double deg)
    {
        var r = deg % 360.0;
        return r < 0 ? r + 360.0 : r;
    }
}
