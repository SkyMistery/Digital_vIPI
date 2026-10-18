namespace Aip.Pdf;

/// <summary>Le colonne di una tabella in una pagina: le x delle righe verticali, e la banda in altezza.</summary>
public sealed record TableGrid(IReadOnlyList<double> Columns, double Top, double Bottom, bool Inherited)
{
    public int ColumnCount => Columns.Count - 1;
}

/// <summary>Una riga di testo di una colonna: la linea di base della sua prima parola e le parole unite.</summary>
public sealed record TextLine(int Column, double Y, string Text);

/// <summary>
/// La tabella di una pagina dell'AIP, dalle righe <b>verticali</b> (F0, carta madre §5): le orizzontali non delimitano le
/// aree (in ENR 5.1.4 una riga ne tiene due), quindi si lavora sulla y del testo. Pagine pari e dispari sono sfalsate:
/// la colonna si ricava pagina per pagina, e una pagina che non la dà la eredita dall'ultima della stessa parità.
/// </summary>
public static class Table
{
    /// <summary>
    /// Le colonne della pagina: il gruppo più numeroso di righe verticali che cominciano e finiscono alle stesse altezze
    /// (la griglia della tabella; cornice e disegni non hanno quella firma). Null se non sono <paramref name="rules"/>
    /// righe larghe almeno <paramref name="minSpan"/> punti.
    /// </summary>
    public static TableGrid? Find(PdfPageText page, int rules, double minSpan)
    {
        var segmenti = page.VerticalRules;
        if (segmenti.Count == 0) return null;

        var xs = Clusters(segmenti.Select(s => s.X), 2.5);
        var ygrid = Clusters(segmenti.SelectMany(s => new[] { s.Top, s.Bottom }), 2.5);
        int Yi(double v) => Enumerable.Range(0, ygrid.Count).MinBy(i => Math.Abs(ygrid[i] - v));
        double Xc(double x) => Math.Round(xs.MinBy(c => Math.Abs(c - x)), 1);

        var capi = new Dictionary<double, SortedSet<int>>();
        foreach (var s in segmenti)
        {
            var x = Xc(s.X);
            if (!capi.TryGetValue(x, out var set)) capi[x] = set = [];
            set.Add(Yi(s.Top));
            set.Add(Yi(s.Bottom));
        }

        var gruppi = capi
            .GroupBy(kv => string.Join(",", kv.Value))
            .Select(g => (Firma: g.First().Value, Xs: g.Select(kv => kv.Key).Order().ToList()))
            .OrderByDescending(g => g.Xs.Count).ThenByDescending(g => g.Firma.Count)
            .First();

        if (gruppi.Xs.Count != rules || gruppi.Xs[^1] - gruppi.Xs[0] < minSpan) return null;
        var ys = gruppi.Firma.Select(i => ygrid[i]).ToList();
        return new TableGrid(gruppi.Xs, ys.Min(), ys.Max(), Inherited: false);
    }

    /// <summary>
    /// Una pagina con la geometria sporca (tabella più un blocco fuori): le colonne di <paramref name="previous"/>, se
    /// almeno due righe interne stanno a quelle x, e la banda fino alla fine della più corta (F0, <c>column_x_fallback</c>).
    /// </summary>
    public static TableGrid? Inherit(PdfPageText page, TableGrid? previous)
    {
        if (previous is null || page.VerticalRules.Count == 0) return null;
        var cime = new List<double>();
        var fondi = new List<double>();
        foreach (var c in previous.Columns.Skip(1).SkipLast(1))
        {
            var lì = page.VerticalRules.Where(s => Math.Abs(s.X - c) < 1.5).ToList();
            if (lì.Count == 0) continue;
            cime.Add(lì.Min(s => s.Top));
            fondi.Add(lì.Max(s => s.Bottom));
        }
        return cime.Count < 2 ? null : new TableGrid(previous.Columns, cime.Min(), fondi.Min(), Inherited: true);
    }

    /// <summary>
    /// Le righe di testo per colonna, in ordine di y e poi di colonna. Le colonne hanno interlinee diverse: si raggruppa
    /// colonna per colonna (una parola con la linea di base a più di 3 punti dalla prima della riga ne apre un'altra). Con una griglia
    /// ereditata si tiene solo il testo nella sua banda.
    /// </summary>
    public static IReadOnlyList<TextLine> Lines(PdfPageText page, TableGrid grid)
    {
        var perColonna = new Dictionary<int, List<PdfWord>>();
        foreach (var w in page.Words)
        {
            if (grid.Inherited && (w.Baseline < grid.Top - 1 || w.Baseline > grid.Bottom + 1)) continue;
            var xc = w.XCenter;
            for (var i = 0; i < grid.ColumnCount; i++)
            {
                if (xc >= grid.Columns[i] - 2 && xc <= grid.Columns[i + 1] + 2)
                {
                    if (!perColonna.TryGetValue(i, out var l)) perColonna[i] = l = [];
                    l.Add(w);
                    break;
                }
            }
        }

        var righe = new List<TextLine>();
        foreach (var (col, parole) in perColonna)
        {
            var ordinate = parole.OrderBy(w => w.Baseline).ThenBy(w => w.X0).ToList();
            var riga = new List<PdfWord>();
            foreach (var w in ordinate)
            {
                if (riga.Count > 0 && w.Baseline - riga[0].Baseline > 3.0)
                {
                    righe.Add(Chiudi(col, riga));
                    riga = [];
                }
                riga.Add(w);
            }
            if (riga.Count > 0) righe.Add(Chiudi(col, riga));
        }
        return righe.OrderBy(r => r.Y).ThenBy(r => r.Column).ToList();
    }

    /// <summary>Il testo di tutta la pagina a righe, dall'alto (per l'intestazione e il piè di pagina).</summary>
    public static IReadOnlyList<string> PlainLines(PdfPageText page)
    {
        var righe = new List<string>();
        var riga = new List<PdfWord>();
        foreach (var w in page.Words.OrderBy(w => w.Baseline).ThenBy(w => w.X0))
        {
            if (riga.Count > 0 && w.Baseline - riga[0].Baseline > 3.0)
            {
                righe.Add(string.Join(' ', riga.OrderBy(x => x.X0).Select(x => x.Text)));
                riga = [];
            }
            riga.Add(w);
        }
        if (riga.Count > 0) righe.Add(string.Join(' ', riga.OrderBy(x => x.X0).Select(x => x.Text)));
        return righe;
    }

    private static TextLine Chiudi(int col, List<PdfWord> riga) =>
        new(col, riga[0].Baseline, string.Join(' ', riga.OrderBy(x => x.X0).Select(x => x.Text)));

    internal static List<double> Clusters(IEnumerable<double> valori, double tolleranza)
    {
        var gruppi = new List<List<double>>();
        foreach (var v in valori.Order())
        {
            if (gruppi.Count > 0 && v - gruppi[^1][^1] <= tolleranza) gruppi[^1].Add(v);
            else gruppi.Add([v]);
        }
        return gruppi.Select(g => g.Average()).ToList();
    }
}
