using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Content;

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
    IReadOnlyList<PdfHorizontalRule>? HorizontalRules = null)
{
    /// <summary>
    /// La pagina girata, dove la tabella è stampata di traverso (ENR 5.4, gli ostacoli: il testo sale dal basso in alto):
    /// la stessa pagina vista in orizzontale, larga quanto l'altezza di questa, con le parole girate e i bordi nel verso
    /// della lettura (carta «L'AIP intero», passo 4). Null se il testo girato non c'è. <see cref="Words"/> qui ha solo il
    /// testo dritto (la testata e il piè di pagina).
    /// </summary>
    public PdfPageText? Turned { get; init; }
}

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
            var girate = page.Letters.Where(l => l.TextOrientation == TextOrientation.Rotate270).ToList();
            var girata = girate.Count >= MinimoDiLettereGirate;
            var parole = page.GetWords()
                .Where(w => !string.IsNullOrWhiteSpace(w.Text) && !(girata && w.TextOrientation == TextOrientation.Rotate270))
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

            pagine.Add(new PdfPageText(page.Number, page.Width, h, parole, righe, orizzontali)
            {
                Turned = girata ? Girata(page.Number, page.Width, h, girate, righe, orizzontali) : null,
            });
        }
        return pagine;
    }

    /// <summary>Sotto queste lettere girate una pagina non ha una tabella di traverso (una scritta a margine).</summary>
    private const int MinimoDiLettereGirate = 50;

    /// <summary>
    /// La pagina vista in orizzontale. Il testo girato di 270° sale dal basso: nel foglio girato la x è la y del PDF (la
    /// lettura va avanti salendo) e la y dall'alto è la x del PDF (la cima delle lettere guarda a sinistra). I bordi si
    /// scambiano: un verticale diventa orizzontale e viceversa.
    /// </summary>
    internal static PdfPageText Girata(int numero, double larghezza, double altezza, IReadOnlyList<Letter> lettere,
        IReadOnlyList<PdfRule> verticali, IReadOnlyList<PdfHorizontalRule> orizzontali)
    {
        var parole = new List<PdfWord>();
        // Le righe: lettere con la stessa linea di base (la x del PDF), entro mezzo punto.
        var righe = new List<List<Letter>>();
        foreach (var l in lettere.OrderBy(l => l.StartBaseLine.X))
        {
            if (righe.Count > 0 && l.StartBaseLine.X - righe[^1][0].StartBaseLine.X <= 0.5) righe[^1].Add(l);
            else righe.Add([l]);
        }
        foreach (var riga in righe)
        {
            var parola = new List<Letter>();
            foreach (var l in riga.OrderBy(l => l.StartBaseLine.Y))
            {
                // Una parola finisce a uno spazio o dove le lettere si staccano.
                var staccata = parola.Count > 0 && l.StartBaseLine.Y - parola[^1].EndBaseLine.Y > Math.Max(1.0, 0.15 * l.PointSize);
                if (string.IsNullOrWhiteSpace(l.Value) || staccata)
                {
                    Chiudi();
                    if (string.IsNullOrWhiteSpace(l.Value)) continue;
                }
                parola.Add(l);
            }
            Chiudi();

            void Chiudi()
            {
                if (parola.Count == 0) return;
                parole.Add(new PdfWord(string.Concat(parola.Select(p => p.Value)), parola[0].StartBaseLine.Y, parola[^1].EndBaseLine.Y,
                    parola.Min(p => p.BoundingBox.Left), parola.Max(p => p.BoundingBox.Right), parola[0].StartBaseLine.X));
                parola = [];
            }
        }

        // Un verticale (x, dall'alto top-bottom) diventa orizzontale alla y = x, dalla x = altezza - bottom; e al contrario.
        var nuoviOrizzontali = verticali.Select(r => new PdfHorizontalRule(r.X, altezza - r.Bottom, altezza - r.Top)).ToList();
        var nuoviVerticali = orizzontali.Select(o => new PdfRule(altezza - o.Y, o.X0, o.X1)).ToList();
        return new PdfPageText(numero, altezza, larghezza, parole, nuoviVerticali, nuoviOrizzontali);
    }
}
