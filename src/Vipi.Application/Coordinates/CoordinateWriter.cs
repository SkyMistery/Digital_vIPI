using System.Globalization;
using System.Text;

namespace Vipi.Application.Coordinates;

/// <summary>I due formati d'uscita, e per il sectorfile le due forme.</summary>
public enum CoordinateOutput
{
    /// <summary>DB IVAO: un vertice per riga, <c>lat:lon</c> in gradi decimali.</summary>
    DbIvao,

    /// <summary>Sectorfile, elenco punti: un vertice per riga, <c>N…;E…;</c>. È il default.</summary>
    SectorfilePunti,

    /// <summary>Sectorfile, segmenti: <c>latA;lonA;latB;lonB;TIPO;NOME;</c>, un lato per riga.</summary>
    SectorfileSegmenti,
}

/// <summary>Le scelte che accompagnano un'uscita. I valori di default sono quelli che servono più spesso.</summary>
/// <param name="Decimali">Cifre decimali del formato DB (6 o 8; il DB IVAO ne scrive 8).</param>
/// <param name="Forma">Puntata o compatta, per le due uscite sectorfile.</param>
/// <param name="Tipo">5° campo delle righe a segmenti.</param>
/// <param name="Nome">6° campo delle righe a segmenti; vuoto = non si scrive.</param>
/// <param name="ChiudiAnello">Genera il lato che riporta l'ultimo vertice sul primo.</param>
public sealed record CoordinateWriteOptions(
    int Decimali = 8,
    DmsCoordinate.Forma Forma = DmsCoordinate.Forma.Puntata,
    string Tipo = "RESTRICT",
    string? Nome = null,
    bool ChiudiAnello = true)
{
    public static CoordinateWriteOptions Default { get; } = new();
}

/// <summary>
/// Scrive i vertici nei formati d'uscita. Puro e senza I/O, come il lettore.
///
/// <para>⚠️ <b>Vertici e segmenti non sono la stessa cosa</b>: il DB e l'elenco punti elencano <i>vertici</i>, la
/// forma a segmenti elenca <i>lati</i>. Cinque vertici fanno cinque lati solo perché l'ultimo torna sul primo, e
/// quel lato qui va <b>generato</b>: nell'elenco non c'è.</para>
/// </summary>
public static class CoordinateWriter
{
    public static string Write(IReadOnlyList<(double Lat, double Lon)> punti,
        CoordinateOutput formato, CoordinateWriteOptions? opzioni = null)
    {
        var o = opzioni ?? CoordinateWriteOptions.Default;
        if (punti.Count == 0) return "";

        return formato switch
        {
            CoordinateOutput.DbIvao => Db(punti, o),
            CoordinateOutput.SectorfilePunti => Punti(punti, o),
            CoordinateOutput.SectorfileSegmenti => Segmenti(punti, o),
            _ => "",
        };
    }

    /// <summary>
    /// <c>42.00777778:11.96833333</c>. ⚠️ Gli <b>zeri finali si tagliano</b> — il DB scrive <c>41.975</c>, non
    /// <c>41.97500000</c> — e il separatore decimale è il punto in ogni lingua: è un formato macchina.
    /// </summary>
    private static string Db(IReadOnlyList<(double Lat, double Lon)> punti, CoordinateWriteOptions o)
    {
        return string.Join('\n', punti.Select(p =>
            Decimale(p.Lat, o.Decimali) + ":" + Decimale(p.Lon, o.Decimali)));
    }

    /// <summary><c>N042.00.28.000;E011.58.06.000;</c>, un vertice per riga. Il punto e virgola finale ci vuole.</summary>
    private static string Punti(IReadOnlyList<(double Lat, double Lon)> punti, CoordinateWriteOptions o)
    {
        return string.Join('\n', punti.Select(p => Dms(p, o)));
    }

    /// <summary><c>latA;lonA;latB;lonB;TIPO;NOME;</c>. L'ultimo lato chiude l'anello, se richiesto.</summary>
    private static string Segmenti(IReadOnlyList<(double Lat, double Lon)> punti, CoordinateWriteOptions o)
    {
        var coda = new StringBuilder(o.Tipo.Trim());
        coda.Append(';');
        if (!string.IsNullOrWhiteSpace(o.Nome)) coda.Append(o.Nome.Trim()).Append(';');

        var righe = new List<string>();
        var lati = o.ChiudiAnello ? punti.Count : punti.Count - 1;
        for (var i = 0; i < lati; i++)
        {
            var a = punti[i];
            var b = punti[(i + 1) % punti.Count];
            righe.Add(Dms(a, o) + Dms(b, o) + coda);
        }
        return string.Join('\n', righe);
    }

    private static string Dms((double Lat, double Lon) p, CoordinateWriteOptions o) =>
        DmsCoordinate.Format(p.Lat, isLatitudine: true, o.Forma) + ";" +
        DmsCoordinate.Format(p.Lon, isLatitudine: false, o.Forma) + ";";

    private static string Decimale(double v, int decimali)
    {
        var arrotondato = Math.Round(v, Math.Clamp(decimali, 0, 15), MidpointRounding.AwayFromZero);
        // "0.########" taglia gli zeri finali da solo: è ciò che fa il DB, e ripeterli sarebbe rumore.
        return arrotondato.ToString("0." + new string('#', Math.Clamp(decimali, 0, 15)), CultureInfo.InvariantCulture);
    }
}
