using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;

namespace Aip.Pdf;

/// <summary>
/// Una parola con la sua scatola, in punti, dall'<b>alto</b> della pagina (come pdfplumber nella F0), e la sua linea di
/// base. ⚠️ Le righe si fanno sulla linea di base, non sulla cima: PdfPig dà la scatola del <b>disegno</b> della parola, e
/// un trattino da solo ha la cima 3 punti più in basso delle lettere accanto (finiva su una riga sua).
/// </summary>
public sealed record PdfWord(string Text, double X0, double X1, double Top, double Bottom, double Baseline)
{
    public double XCenter => (X0 + X1) / 2;
}

/// <summary>Un tratto verticale disegnato sulla pagina (riga della tabella, bordo di una cella).</summary>
public sealed record PdfRule(double X, double Top, double Bottom);

/// <summary>Un tratto orizzontale disegnato sulla pagina: il bordo fra due righe di una tabella.</summary>
public sealed record PdfHorizontalRule(double Y, double X0, double X1);

/// <summary>
/// Una pagina: le parole e i tratti verticali e orizzontali. Il resto del disegno al lettore non serve. Gli orizzontali
/// servono dove una riga della tabella è una cella sola per oggetto (ENR 4: il testo sta in mezzo alla cella, non in
/// cima, e solo i bordi dicono dove finisce una radioassistenza); null = non letti (le pagine costruite nei test).
/// </summary>
public sealed record PdfPageText(int Number, double Width, double Height, IReadOnlyList<PdfWord> Words, IReadOnlyList<PdfRule> VerticalRules,
    IReadOnlyList<PdfHorizontalRule>? HorizontalRules = null);

/// <summary>
/// Legge un PDF con PdfPig: per ogni pagina le parole con la loro posizione e i tratti verticali. È l'unico posto che
/// conosce PdfPig: cambiarlo vuol dire cambiare questo file.
/// </summary>
public static class PdfText
{
    /// <summary>Più stretto di così un rettangolo è una riga (F0: <c>w &lt; 2,5</c>, <c>h &gt; 3</c>; per le orizzontali, al contrario).</summary>
    private const double LarghezzaMassima = 2.5;

    private const double AltezzaMinima = 3;

    /// <exception cref="InvalidDataException">Il file non è un PDF che PdfPig sa leggere.</exception>
    public static IReadOnlyList<PdfPageText> Read(string path) => Apri(() => PdfDocument.Open(path));

    /// <exception cref="InvalidDataException">I byte non sono un PDF che PdfPig sa leggere.</exception>
    public static IReadOnlyList<PdfPageText> Read(byte[] pdf) => Apri(() => PdfDocument.Open(pdf));

    // Le eccezioni di PdfPig non escono di qui: chi legge sa di un PDF illeggibile, non della libreria che lo diceva.
    private static IReadOnlyList<PdfPageText> Apri(Func<PdfDocument> apri)
    {
        try
        {
            using var doc = apri();
            return Read(doc);
        }
        catch (PdfDocumentFormatException e)
        {
            throw new InvalidDataException(e.Message, e);
        }
    }

    private static IReadOnlyList<PdfPageText> Read(PdfDocument doc)
    {
        var pagine = new List<PdfPageText>(doc.NumberOfPages);
        foreach (var page in doc.GetPages())
        {
            var h = page.Height;
            var parole = page.GetWords()
                .Where(w => !string.IsNullOrWhiteSpace(w.Text))
                .Select(w => new PdfWord(w.Text, w.BoundingBox.Left, w.BoundingBox.Right, h - w.BoundingBox.Top, h - w.BoundingBox.Bottom,
                    h - w.Letters[0].StartBaseLine.Y))
                .ToList();

            var righe = new List<PdfRule>();
            var orizzontali = new List<PdfHorizontalRule>();
            foreach (var path in page.Paths)
            {
                foreach (var sub in path)
                {
                    // Un rettangolo sottile (riga disegnata come riempimento) conta intero; per il resto, ogni lato.
                    var box = sub.GetBoundingRectangle();
                    if (box is { } b && b.Width < LarghezzaMassima && b.Height > AltezzaMinima)
                    {
                        righe.Add(new PdfRule((b.Left + b.Right) / 2, h - b.Top, h - b.Bottom));
                        continue;
                    }
                    if (box is { } o && o.Height < LarghezzaMassima && o.Width > AltezzaMinima)
                    {
                        orizzontali.Add(new PdfHorizontalRule(h - (o.Top + o.Bottom) / 2, o.Left, o.Right));
                        continue;
                    }
                    foreach (var cmd in sub.Commands)
                    {
                        if (cmd is not PdfSubpath.Line l) continue;
                        if (Math.Abs(l.From.X - l.To.X) < LarghezzaMassima && Math.Abs(l.From.Y - l.To.Y) > AltezzaMinima)
                            righe.Add(new PdfRule((l.From.X + l.To.X) / 2, h - Math.Max(l.From.Y, l.To.Y), h - Math.Min(l.From.Y, l.To.Y)));
                        else if (Math.Abs(l.From.Y - l.To.Y) < LarghezzaMassima && Math.Abs(l.From.X - l.To.X) > AltezzaMinima)
                            orizzontali.Add(new PdfHorizontalRule(h - (l.From.Y + l.To.Y) / 2, Math.Min(l.From.X, l.To.X), Math.Max(l.From.X, l.To.X)));
                    }
                }
            }

            pagine.Add(new PdfPageText(page.Number, page.Width, h, parole, righe, orizzontali));
        }
        return pagine;
    }
}
