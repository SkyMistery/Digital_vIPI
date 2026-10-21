using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Graphics.Operations;
using UglyToad.PdfPig.Graphics.Operations.TextPositioning;
using UglyToad.PdfPig.Writer;

namespace Aip.Pdf.Tests;

/// <summary>
/// La pagina girata (carta «L'AIP intero», passo 4): le tabelle di ENR 5.4 sono stampate di traverso, il testo sale dal
/// basso in alto. Il PDF si costruisce qui: il testo scritto da PdfPig e poi girato con la matrice del testo, come nei PDF
/// veri (<c>0 1 -1 0 x y Tm</c>). Dati inventati.
/// </summary>
public sealed class PaginaGirataTests
{
    private const double Larghezza = 595;
    private const double Altezza = 842;

    /// <summary>
    /// Un PDF di una pagina: il testo dritto (x, y dal basso) e quello girato — <c>Lungo</c> è dove comincia lungo la lettura
    /// (la y del PDF), <c>Riga</c> la x della linea di base. Più una riga orizzontale e una verticale.
    /// </summary>
    private static byte[] Pdf((double X, double Y, string Testo)[] dritto, (double Lungo, double Riga, string Testo)[] girato)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(Larghezza, Altezza);
        foreach (var (x, y, t) in dritto)
            page.AddText(t, 8, new PdfPoint(x, y), font);
        foreach (var (lungo, riga, t) in girato)
        {
            page.AddText(t, 7, new PdfPoint(riga, lungo), font);
            // La posizione appena scritta diventa una matrice girata di 90° in senso antiorario: si legge salendo.
            var operazioni = (IList<IGraphicsStateOperation>)page.CurrentStream.Operations;
            var i = Enumerable.Range(0, operazioni.Count).Last(k => operazioni[k] is MoveToNextLineWithOffset);
            operazioni[i] = new SetTextMatrix([0, 1, -1, 0, riga, lungo]);
        }
        // Un bordo verticale (nella pagina girata è orizzontale) e uno orizzontale (lì verticale).
        page.DrawLine(new PdfPoint(100, 100), new PdfPoint(100, 700), 0.5);
        page.DrawLine(new PdfPoint(50, 400), new PdfPoint(550, 400), 0.5);
        return builder.Build();
    }

    [Fact]
    public void IlTestoGirato_SiLeggeSullaPaginaGirata_IlDrittoResta()
    {
        // 50 lettere girate almeno: sotto, è una scritta a margine.
        var girato = Enumerable.Range(0, 6).Select(i => (100.0 + i * 60, 150.0, "T00404" + i)).ToList();
        girato.Add((100, 165, "CHIETI CASTEL FRAIANO"));
        var pdf = Pdf([(450, 800, "ENR 5.4.1.1 - 1")], [.. girato]);

        var pagina = Assert.Single(PdfText.Read(pdf));

        Assert.Equal(["ENR", "5.4.1.1", "-", "1"], pagina.Words.Select(w => w.Text));
        var g = Assert.IsType<PdfPageText>(pagina.Turned);
        Assert.Equal((Altezza, Larghezza), (g.Width, g.Height));
        // Le parole nel verso della lettura: la x lungo la riga, la y dall'alto è la x del PDF.
        Assert.Equal(["T004040", "T004041", "T004042", "T004043", "T004044", "T004045"],
            g.Words.Where(w => Math.Abs(w.Baseline - 150) < 1).OrderBy(w => w.X0).Select(w => w.Text));
        var prima = g.Words.First(w => w.Text == "T004040");
        Assert.Equal(100, prima.X0, 1);
        Assert.Equal(["CHIETI", "CASTEL", "FRAIANO"], g.Words.Where(w => Math.Abs(w.Baseline - 165) < 1).OrderBy(w => w.X0).Select(w => w.Text));
        // Il bordo verticale a x 100 è orizzontale a y 100; quello orizzontale a y 400 (dal basso) è verticale a x 400.
        Assert.Contains(g.HorizontalRules!, r => Math.Abs(r.Y - 100) < 1 && Math.Abs(r.X0 - 100) < 1 && Math.Abs(r.X1 - 700) < 1);
        Assert.Contains(g.VerticalRules, r => Math.Abs(r.X - 400) < 1 && Math.Abs(r.Top - 50) < 1 && Math.Abs(r.Bottom - 550) < 1);
    }

    [Fact]
    public void PocheLettereGirate_NessunaPaginaGirata()
    {
        var pdf = Pdf([(450, 800, "ENR 5.4.2 - 1")], [(100, 150, "NOTE")]);

        Assert.Null(Assert.Single(PdfText.Read(pdf)).Turned);
    }
}
