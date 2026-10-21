using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Vipi.SectorLab.Tests.PdfAip;

/// <summary>
/// Una pagina di ENR 5.1 finta, con la stessa impaginazione (cinque righe verticali, testata «1 2 3 4», data AIRAC):
/// nei test niente PDF di ENAV (carta F6 §3). Un'area per voce di <c>aree</c>, poligoni di tre vertici.
/// ⚠️ Il font standard di PdfPig non ha «°»: i gradi sono «º», che l'importer accetta come i PDF veri.
/// </summary>
internal static class PdfAipFinto
{
    public static byte[] Enr51(string data, params string[] aree)
    {
        const double h = 842;
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(595, h);
        foreach (var x in new double[] { 40, 200, 300, 420, 560 })
            page.DrawLine(new PdfPoint(x, h - 100), new PdfPoint(x, h - 800), 0.5);

        void Testo(double x, double top, string t) => page.AddText(t, 8, new PdfPoint(x, h - top - 7), font);
        Testo(450, 40, "ENR 5.1.2 - 1");
        Testo(45, 55, "AIP - Italia AIRAC effective date " + data);
        Testo(100, 120, "1");
        Testo(240, 120, "2");
        Testo(350, 120, "3");
        Testo(480, 120, "4");

        var top = 140.0;
        for (var i = 0; i < aree.Length; i++, top += 70)
        {
            Testo(45, top, aree[i]);
            Testo(205, top, "FL95");
            Testo(205, top + 12, "SFC");
            Testo(45, top + 12, $"4{i}º30'00\"N 012º15'00\"E;");
            Testo(45, top + 24, $"4{i}º30'00\"N 012º30'00\"E;");
            Testo(45, top + 36, $"4{i}º15'00\"N 012º22'30\"E;");
            Testo(45, top + 48, "to point of origin.");
        }
        return builder.Build();
    }

    /// <summary>
    /// Un PDF di sola testata (una sezione che l'importer non legge ancora, o una carta): <paramref name="pagine"/> pagine
    /// «<paramref name="sezione"/> - k» con la data; quelle in <paramref name="vuote"/> senza una parola (ENR 6.3 nel 2611).
    /// </summary>
    public static byte[] Testate(string sezione, string data, int pagine, params int[] vuote)
    {
        const double h = 842;
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        for (var k = 1; k <= pagine; k++)
        {
            var page = builder.AddPage(595, h);
            if (vuote.Contains(k)) continue;
            page.AddText($"{sezione} - {k}", 8, new PdfPoint(450, h - 47), font);
            page.AddText("AIP - Italia AIRAC effective date " + data, 8, new PdfPoint(45, h - 62), font);
        }
        return builder.Build();
    }
}
