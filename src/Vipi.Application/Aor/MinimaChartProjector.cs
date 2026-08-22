using System.Globalization;
using System.Text;
using Vipi.Application.Abstractions;

namespace Vipi.Application.Aor;

/// <summary>Un tracciato proiettato: percorso SVG e se è un'area (chiuso) o una linea (aperto).</summary>
public sealed record MinimaSvgPath(string Path, bool IsClosed, string Name);

/// <summary>Un'etichetta proiettata: testo verbatim e posizione nel viewBox.</summary>
public sealed record MinimaSvgLabel(string Text, double X, double Y);

/// <summary>Una carta MRVA proiettata in SVG, tutta nello stesso viewBox.</summary>
public sealed record MinimaChartSvg(string ViewBox, IReadOnlyList<MinimaSvgPath> Paths, IReadOnlyList<MinimaSvgLabel> Labels);

/// <summary>
/// Proiezione PURA (no I/O, deterministica, testabile) di una carta MRVA in SVG. Gemella di
/// <see cref="AorPolygonProjector"/>, con due differenze che vengono dal dato:
/// <list type="bullet">
/// <item><b>Un viewBox solo per tutta la carta</b>, calcolato su tracciati ED etichette insieme — l'etichetta è
/// un elemento a sé, piazzato a una coordinata sua, e proiettarla con un'altra scala la sposterebbe;</item>
/// <item><b>i tracciati aperti restano aperti</b>: sono archi e linee di confine, non aree, e chiuderli
/// disegnerebbe una figura che nel sectorfile non c'è.</item>
/// </list>
/// Serve alla resa senza JavaScript e alla stampa: la mappa interattiva la costruisce <c>vipi-mva.js</c> dagli
/// stessi dati in gradi.
/// </summary>
public static class MinimaChartProjector
{
    private const double Canvas = 400.0;   // lato lungo del viewBox normalizzato
    private const double Pad = 10.0;       // margine interno: le etichette stanno sul bordo più spesso dei vertici

    /// <summary>Proietta la carta; null se non c'è niente di posizionabile (nessun vertice e nessuna etichetta).</summary>
    public static MinimaChartSvg? Project(MvaChart? chart)
    {
        if (chart is null || chart.IsEmpty) return null;

        // Il viewBox si calcola su TUTTI i punti disegnati, vertici ed etichette: se lo si calcolasse sui soli
        // poligoni, un'etichetta fuori dai contorni (succede: 13 su 345 non cadono dentro nessuna area) finirebbe
        // fuori dalla cornice, e sparirebbe senza che nulla lo segnali.
        var all = chart.Shapes.SelectMany(s => s.Points)
            .Concat(chart.Labels.Select(l => new MvaPoint(l.Lat, l.Lon)))
            .ToList();
        if (all.Count == 0) return null;

        // Equirettangolare con la longitudine scalata per cos(lat medio): stessa scelta dell'AoR, aspetto corretto
        // alle latitudini italiane.
        var latMean = all.Average(p => p.Lat);
        var k = Math.Cos(latMean * Math.PI / 180.0);

        double minX = all.Min(p => p.Lon * k), maxX = all.Max(p => p.Lon * k);
        double minY = all.Min(p => -p.Lat), maxY = all.Max(p => -p.Lat);
        double spanX = maxX - minX, spanY = maxY - minY;
        var span = Math.Max(spanX, spanY);
        if (span <= 0) return null;   // tutto in un punto solo: non c'è una carta da disegnare

        var scale = (Canvas - 2 * Pad) / span;
        var w = spanX * scale + 2 * Pad;
        var h = spanY * scale + 2 * Pad;

        var paths = new List<MinimaSvgPath>(chart.Shapes.Count);
        foreach (var s in chart.Shapes)
        {
            if (s.Points.Count < 2) continue;
            var sb = new StringBuilder();
            for (var i = 0; i < s.Points.Count; i++)
            {
                var (x, y) = ToCanvas(s.Points[i]);
                sb.Append(i == 0 ? 'M' : 'L')
                  .Append(F(x)).Append(' ').Append(F(y)).Append(' ');
            }
            // "Z" solo sui chiusi: sull'aperto disegnerebbe il lato che manca.
            if (s.IsClosed) sb.Append('Z');
            paths.Add(new MinimaSvgPath(sb.ToString().TrimEnd(), s.IsClosed, s.Name));
        }

        var labels = chart.Labels.Select(l =>
        {
            var (x, y) = ToCanvas(new MvaPoint(l.Lat, l.Lon));
            return new MinimaSvgLabel(l.Text, Math.Round(x, 2), Math.Round(y, 2));
        }).ToList();

        var viewBox = $"0 0 {F(w)} {F(h)}";
        return new MinimaChartSvg(viewBox, paths, labels);

        (double X, double Y) ToCanvas(MvaPoint p) =>
            ((p.Lon * k - minX) * scale + Pad, (-p.Lat - minY) * scale + Pad);
    }

    private static string F(double v) => Math.Round(v, 2).ToString(CultureInfo.InvariantCulture);
}
