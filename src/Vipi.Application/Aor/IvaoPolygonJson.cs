using System.Globalization;
using System.Text;

namespace Vipi.Application.Aor;

/// <summary>
/// Scrive un anello nella forma <c>RegionMapPolygon</c> della sorgente: array JSON di coppie
/// <c>[lng, lat]</c>, <b>longitudine prima</b>, stile GeoJSON.
///
/// <para>Esiste per la stessa ragione per cui <see cref="PolygonGeometry.ParsePoints"/> esiste: quella forma
/// si <b>legge</b> in un posto solo, e deve <b>scriversi</b> in un posto solo. Fino al 29 agosto 2026 la
/// scriveva un metodo privato del cerchio di ripiego, che era l'unico a doverlo fare; da quando la scrive
/// anche il catalogo degli spazi aerei, due copie sarebbero due occasioni di invertire l'ordine — e un
/// poligono con lat e lon scambiati non si lamenta, si disegna ruotato di 90°.</para>
/// </summary>
public static class IvaoPolygonJson
{
    /// <summary>Sei decimali: circa 10 cm, molto oltre la precisione di qualunque sorgente di confini.</summary>
    private const int Decimali = 6;

    /// <summary>
    /// L'anello come JSON. <paramref name="chiudiAnello"/> ripete il primo vertice in coda — che è ciò che
    /// fa la sorgente, e che i consumatori sanno già gestire.
    /// </summary>
    public static string Write(IReadOnlyList<(double Lat, double Lon)> punti, bool chiudiAnello = false)
    {
        var sb = new StringBuilder();
        sb.Append('[');
        for (var i = 0; i < punti.Count; i++)
        {
            if (i > 0) sb.Append(',');
            Append(sb, punti[i].Lon, punti[i].Lat);
        }
        if (chiudiAnello && punti.Count > 0)
        {
            sb.Append(',');
            Append(sb, punti[0].Lon, punti[0].Lat);
        }
        sb.Append(']');
        return sb.ToString();
    }

    /// <summary>Una coppia <c>[lng,lat]</c>, arrotondata e in cultura invariante.</summary>
    public static void Append(StringBuilder sb, double lon, double lat) =>
        sb.Append('[')
          .Append(Math.Round(lon, Decimali).ToString(CultureInfo.InvariantCulture))
          .Append(',')
          .Append(Math.Round(lat, Decimali).ToString(CultureInfo.InvariantCulture))
          .Append(']');
}
