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

    /// <summary>Un PDF di pagine fatte di testi (x, distanza dall'alto, testo); una pagina vuota = nessuna parola.</summary>
    public static byte[] Pagine(params (double X, double Top, string Testo)[][] pagine)
    {
        const double h = 842;
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var testi in pagine)
        {
            var page = builder.AddPage(595, h);
            foreach (var (x, top, testo) in testi)
                page.AddText(testo, 8, new PdfPoint(x, h - top - 8), font);
        }
        return builder.Build();
    }

    /// <summary>La testata di una pagina dell'AIP: riferimento e data.</summary>
    public static (double, double, string)[] Testata(string riferimento, string data) =>
        [(450, 17, riferimento), (52, 29, "AIP - Italia AIRAC effective date " + data)];

    /// <summary>
    /// Un emendamento AIRAC finto con le posizioni dell'A12/26 (carta «L'AIP intero», passo 7): la copertina, l'elenco a due
    /// colonne (riferimenti a x 24 e 301, date a 135 e 411), le pagine nuove nell'ordine dell'elenco (quelle in
    /// <paramref name="vuote"/>, contate in <paramref name="metti"/>, senza una parola), e il GEN 0.4 come prima pagina nuova,
    /// con le righe <paramref name="valide"/> («ENR 4.4 - 1 11 JUN 2026»).
    /// </summary>
    public static byte[] Emendamento(string numero, string inVigore, (string R, string D)[] togli, (string R, string D)[] metti, string[] valide, params int[] vuote)
    {
        var elenco = new List<(double, double, string)> { (101, 29, "PAGINE DA DISTRUGGERE"), (388, 29, "PAGINE DA INSERIRE") };
        for (var i = 0; i < Math.Max(togli.Length, metti.Length + 1); i++)
        {
            var top = 56.0 + 14 * i;
            if (i < togli.Length) elenco.AddRange([(24, top, togli[i].R), (135, top, togli[i].D)]);
            // La prima riga a destra è il GEN 0.4.
            var m = i == 0 ? ("GEN 0.4 - 1", inVigore) : i - 1 < metti.Length ? metti[i - 1] : default;
            if (m.Item1 is not null) elenco.AddRange([(301, top, m.Item1), (411, top, m.Item2)]);
        }
        var gen04 = new List<(double, double, string)>(Testata("GEN 0.4 - 1", inVigore));
        for (var i = 0; i < valide.Length; i++)
        {
            var riga = valide[i];
            gen04.Add((26, 75 + 12 * i, riga[..^12].TrimEnd()));
            gen04.Add((120, 75 + 12 * i, riga[^11..]));
        }
        var pagine = new List<(double, double, string)[]>
        {
            new[] { (300.0, 64.0, $"({numero})"), (60.0, 200.0, "Data di entrata in vigore/Effective date"), (60.0, 214.0, inVigore) },
            elenco.ToArray(),
            gen04.ToArray(),
        };
        pagine.AddRange(metti.Select((m, i) => vuote.Contains(i) ? [] : Testata(m.R, m.D)));
        return Pagine([.. pagine]);
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
