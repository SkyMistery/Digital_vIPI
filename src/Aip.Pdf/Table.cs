namespace Aip.Pdf;

/// <summary>Le colonne di una tabella in una pagina: le x delle righe verticali, e la banda in altezza.</summary>
/// <param name="Inherited">Ereditata dalla pagina prima: il testo si tiene solo nella sua banda.</param>
/// <param name="Cut">Trovata come banda (<see cref="Table.FindAll"/>): anche lei tiene solo il testo nella sua banda.</param>
public sealed record TableGrid(IReadOnlyList<double> Columns, double Top, double Bottom, bool Inherited, bool Cut = false)
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
    /// Tutte le tabelle della pagina, dall'alto (ENR 2: una pagina ha la tabella delle aree e sotto quella delle frequenze,
    /// o le note). Le righe delle tabelle dell'AIP sono disegnate a pezzi, uno per riga della tabella, e due tabelle sono
    /// separate da un vuoto: una tabella è una <b>banda</b> di tratti verticali che si toccano, e le sue colonne sono le x
    /// che ne coprono almeno metà dell'altezza (una cella di testata unita non le toglie). I tratti più corti di
    /// <paramref name="minRule"/> non contano: sono pezzi di lettere disegnati come tracciati (7 punti, misurati su ENR
    /// 2.1.3). Si tengono le bande con almeno tre righe larghe almeno <paramref name="minSpan"/>.
    /// </summary>
    public static IReadOnlyList<TableGrid> FindAll(PdfPageText page, double minSpan, double minRule = 10)
    {
        var lunghi = page.VerticalRules.Where(r => r.Bottom - r.Top >= minRule).ToList();
        if (lunghi.Count == 0) return [];

        // Per ogni x, i tratti uniti dove si toccano.
        var xs = Clusters(lunghi.Select(r => r.X), 2.5);
        var perX = xs.Select(x => Unisci(lunghi.Where(r => Math.Abs(r.X - x) <= 2.5).Select(r => (r.Top, r.Bottom)))).ToList();

        // Le bande: dove almeno tre righe verticali diverse corrono insieme. ⚠️ Non l'unione di tutti i tratti: una barra
        // di modifica a margine, lunga quanto la pagina, salderebbe due tabelle in una (e nessuna colonna coprirebbe metà).
        var tagli = perX.SelectMany(i => i).SelectMany(i => new[] { i.Cima, i.Fondo }).Distinct().Order().ToList();
        var coperti = new List<(double Cima, double Fondo)>();
        for (var t = 0; t + 1 < tagli.Count; t++)
        {
            var (a, b) = (tagli[t], tagli[t + 1]);
            var dentro = perX.Count(intervalli => intervalli.Any(i => i.Cima <= a + 0.01 && i.Fondo >= b - 0.01));
            if (dentro >= 3) coperti.Add((a, b));
        }
        var bande = Unisci(coperti);
        var griglie = new List<TableGrid>();
        foreach (var (cima, fondo) in bande)
        {
            var altezza = fondo - cima;
            if (altezza < 20) continue;
            var colonne = new List<(double X, double Coperto)>();
            for (var k = 0; k < xs.Count; k++)
            {
                var coperto = perX[k].Sum(i => Math.Max(0, Math.Min(i.Fondo, fondo) - Math.Max(i.Cima, cima)));
                if (coperto < altezza * 0.5) continue;
                // ⚠️ Una barra di modifica dell'AIRAC (il segno a margine delle parti cambiate) corre accanto al bordo della
                // tabella, a 4 punti (ENR 2.2 p.1: 47,5 contro 51,7): due righe così vicine sono una, la più lunga.
                if (colonne.Count > 0 && xs[k] - colonne[^1].X < 6)
                {
                    // A parità, sul bordo sinistro vince la riga più interna: la barra sta fuori dalla tabella.
                    if (coperto > colonne[^1].Coperto + 0.5 || (colonne.Count == 1 && coperto >= colonne[^1].Coperto - 0.5))
                        colonne[^1] = (Math.Round(xs[k], 1), coperto);
                    continue;
                }
                colonne.Add((Math.Round(xs[k], 1), coperto));
            }
            if (colonne.Count >= 3 && colonne[^1].X - colonne[0].X >= minSpan)
                griglie.Add(new TableGrid(colonne.Select(c => c.X).ToList(), cima, fondo, Inherited: false, Cut: true));
        }
        return griglie;
    }

    private static List<(double Cima, double Fondo)> Unisci(IEnumerable<(double Cima, double Fondo)> tratti)
    {
        var uniti = new List<(double Cima, double Fondo)>();
        foreach (var t in tratti.OrderBy(t => t.Cima))
        {
            if (uniti.Count > 0 && t.Cima <= uniti[^1].Fondo + 2) uniti[^1] = (uniti[^1].Cima, Math.Max(uniti[^1].Fondo, t.Fondo));
            else uniti.Add(t);
        }
        return uniti;
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
            if ((grid.Inherited || grid.Cut) && (w.Baseline < grid.Top - 1 || w.Baseline > grid.Bottom + 1)) continue;
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

    /// <summary>
    /// Le righe della tabella dai bordi <b>orizzontali</b>: le bande fra due bordi che chiudono la prima cella (da prima
    /// della prima colonna a dopo la seconda riga verticale), dall'alto. Servono dove un oggetto è una riga sola e il suo
    /// testo sta in mezzo alla cella (ENR 4.1: il nome della radioassistenza è più in basso della copertura): lì la y del
    /// testo non dice di chi è, i bordi sì. Vuota se la pagina non ha i bordi (o non li ha letti).
    /// </summary>
    public static IReadOnlyList<(double Top, double Bottom)> RowBands(PdfPageText page, TableGrid grid)
    {
        if (page.HorizontalRules is not { Count: > 0 } bordi || grid.Columns.Count < 2) return [];
        var ys = Clusters(bordi
            .Where(b => b.X0 <= grid.Columns[0] + 5 && b.X1 >= grid.Columns[1] - 5)
            .Where(b => b.Y >= grid.Top - 2 && b.Y <= grid.Bottom + 2)
            .Select(b => b.Y), 1.0);
        var bande = new List<(double Top, double Bottom)>();
        for (var i = 0; i + 1 < ys.Count; i++) bande.Add((ys[i], ys[i + 1]));
        return bande;
    }

    /// <summary>Il testo di tutta la pagina a righe, dall'alto (per l'intestazione e il piè di pagina).</summary>
    public static IReadOnlyList<string> PlainLines(PdfPageText page) => PlainLinesWithY(page).Select(r => r.Text).ToList();

    /// <summary>Come <see cref="PlainLines"/>, con la linea di base di ogni riga (i titoli sopra una tabella).</summary>
    public static IReadOnlyList<(double Y, string Text)> PlainLinesWithY(PdfPageText page)
    {
        var righe = new List<(double Y, string Text)>();
        var riga = new List<PdfWord>();
        foreach (var w in page.Words.OrderBy(w => w.Baseline).ThenBy(w => w.X0))
        {
            if (riga.Count > 0 && w.Baseline - riga[0].Baseline > 3.0)
            {
                righe.Add((riga[0].Baseline, string.Join(' ', riga.OrderBy(x => x.X0).Select(x => x.Text))));
                riga = [];
            }
            riga.Add(w);
        }
        if (riga.Count > 0) righe.Add((riga[0].Baseline, string.Join(' ', riga.OrderBy(x => x.X0).Select(x => x.Text))));
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
