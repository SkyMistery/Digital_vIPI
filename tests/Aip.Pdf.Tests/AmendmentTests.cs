using Aip.Pdf.Italia;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Aip.Pdf.Tests;

/// <summary>
/// L'emendamento AIRAC (carta «L'AIP intero e gli emendamenti», passo 7): il lettore e la scrittura delle pagine. PDF
/// costruiti qui con le posizioni dell'AMDT A12/26 (elenco: riferimenti a x 24 e 301, date a 135 e 411, intestazioni a 101
/// e 388; GEN 0.4 a tre colonne); dati inventati.
/// </summary>
public sealed class AmendmentTests
{
    private const double Altezza = 842;

    /// <summary>Una pagina: testi (x, distanza dall'alto, testo); vuota = nessuna parola.</summary>
    internal static byte[] Pdf(params (double X, double Top, string Testo)[][] pagine)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var testi in pagine)
        {
            var page = builder.AddPage(595, Altezza);
            foreach (var (x, top, testo) in testi)
                page.AddText(testo, 8, new PdfPoint(x, Altezza - top - 8), font);
        }
        return builder.Build();
    }

    internal static (double, double, string)[] Copertina(string numero, string data) =>
    [
        (300, 50, "AIP AIRAC AMDT"), (300, 64, $"({numero})"), (60, 200, "Data di entrata in vigore/Effective date"), (60, 214, data),
    ];

    /// <summary>Una pagina dell'elenco: (riferimento, data) a sinistra e a destra, riga per riga; null = la colonna vuota.</summary>
    internal static (double, double, string)[] Elenco(params ((string R, string D)? Togli, (string R, string D)? Metti)[] righe)
    {
        var testi = new List<(double, double, string)> { (101, 29, "PAGINE DA DISTRUGGERE"), (388, 29, "PAGINE DA INSERIRE") };
        for (var i = 0; i < righe.Length; i++)
        {
            var top = 56.0 + 14 * i;
            if (righe[i].Togli is { } t) testi.AddRange([(24, top, t.R), (135, top, t.D)]);
            if (righe[i].Metti is { } m) testi.AddRange([(301, top, m.R), (411, top, m.D)]);
        }
        return [.. testi];
    }

    internal static (double, double, string)[] Pagina(string riferimento, string data) =>
        [(450, 17, riferimento), (52, 29, "AIP - Italia AIRAC effective date " + data)];

    /// <summary>Una pagina del GEN 0.4: tre colonne di righe (intestazioni o «sezione - n  data»).</summary>
    internal static (double, double, string)[] Gen04(string riferimento, string data, params string[][] colonne)
    {
        var testi = new List<(double, double, string)>(Pagina(riferimento, data));
        double[] x = [26, 210, 394];
        for (var c = 0; c < colonne.Length; c++)
            for (var i = 0; i < colonne[c].Length; i++)
            {
                var riga = colonne[c][i];
                var top = 75.0 + 12 * i;
                // La data a destra nella colonna, come nel PDF vero.
                var spazio = riga.LastIndexOf(" - ", StringComparison.Ordinal);
                if (spazio > 0 && riga.Length > 11 && char.IsAsciiDigit(riga[^1]))
                {
                    testi.Add((x[c], top, riga[..^12].TrimEnd()));
                    testi.Add((x[c] + 94, top, riga[^11..]));
                }
                else
                {
                    testi.Add((x[c], top, riga));
                }
            }
        return [.. testi];
    }

    private static AmendmentRead UnEmendamento() => ItalianAmendment.Read(PdfText.Read(Pdf(
        Copertina("A12/26", "26 NOV 2026"),
        Elenco(
            (("GEN 0.4 - 1", "29 OCT 2026"), ("GEN 0.4 - 1", "26 NOV 2026")),
            (("ENR 4.4 - 77", "29 OCT 2026"), ("ENR 4.4 - 77", "26 NOV 2026")),
            (("ENR 6.3 - 3", "03 SEP 2026"), ("ENR 6.3 - 3", "26 NOV 2026")),
            (null, ("ENR 2.1.1.4.9 - 1", "26 NOV 2026")),
            (("AD 2 LIDR 1 - 11", "22 JAN 2026"), null)),
        Gen04("GEN 0.4 - 1", "26 NOV 2026",
            ["GEN 0", "0.4 - 1 26 NOV 2026", "ENR 2", "2.1.1.4.9 - 1 26 NOV 2026"],
            ["ENR 4", "4.4 - 77 26 NOV 2026", "ENR 6", "6.3 - 3 26 NOV 2026"],
            ["AD 2 (Altri AD/Other AD)", "AD 2 LIDR 1 - 10 22 JAN 2026"]),
        Pagina("ENR 4.4 - 77", "26 NOV 2026"),
        [],
        Pagina("ENR 2.1.1.4.9 - 1", "26 NOV 2026"))));

    [Fact]
    public void LaCopertinaELElencoADueColonne()
    {
        var letto = UnEmendamento();

        Assert.Equal(("A12/26", new DateOnly(2026, 11, 26)), (letto.Number, letto.Effective));
        Assert.Equal(["GEN 0.4-1", "ENR 4.4-77", "ENR 6.3-3", "AD 2 LIDR 1-11"], letto.Destroy.Select(d => d.Reference));
        Assert.Equal(new DateOnly(2026, 1, 22), letto.Destroy[3].Effective);
        Assert.Equal(["GEN 0.4-1", "ENR 4.4-77", "ENR 6.3-3", "ENR 2.1.1.4.9-1"], letto.Insert.Select(d => d.Reference));
        Assert.All(letto.Insert, i => Assert.Equal(new DateOnly(2026, 11, 26), i.Effective));
        Assert.Empty(letto.Issues);
    }

    [Fact]
    public void IlCorpoSegueLElenco_LaPaginaVuotaDallaPosizione()
    {
        var letto = UnEmendamento();

        Assert.Equal(["GEN 0.4-1", "ENR 4.4-77", "ENR 6.3-3", "ENR 2.1.1.4.9-1"], letto.Pages.Select(p => p.Reference));
        Assert.Equal([3, 4, 5, 6], letto.Pages.Select(p => p.Position));
        Assert.Equal([false, false, true, false], letto.Pages.Select(p => p.ByPosition));
    }

    [Fact]
    public void IlGen04ConLaParteDalleIntestazioni()
    {
        var letto = UnEmendamento();

        Assert.Equal(
            ["AD 2 LIDR 1-10", "ENR 2.1.1.4.9-1", "ENR 4.4-77", "ENR 6.3-3", "GEN 0.4-1"],
            letto.Checklist.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(new DateOnly(2026, 1, 22), letto.Checklist["AD 2 LIDR 1-10"]);
    }

    [Fact]
    public void UnCorpoCheNonSegueLElencoSiDice()
    {
        var letto = ItalianAmendment.Read(PdfText.Read(Pdf(
            Copertina("A01/27", "21 JAN 2027"),
            Elenco((null, ("ENR 4.4 - 1", "21 JAN 2027")), (null, ("ENR 4.4 - 2", "21 JAN 2027"))),
            Pagina("ENR 4.4 - 2", "21 JAN 2027"))));

        Assert.Contains(letto.Issues, p => p.Kind == AmendmentIssueKind.BodyCount && p.Detail == "1 pagine nel corpo, 2 da inserire");
        Assert.Contains(letto.Issues, p => p.Kind == AmendmentIssueKind.BodyMismatch && p.Page == 3 && p.Detail == "ENR 4.4-2 al posto di ENR 4.4-1");
        Assert.Contains(letto.Issues, p => p.Kind == AmendmentIssueKind.NoChecklist);
    }

    [Fact]
    public void SenzaElencoESenzaDataSiDice()
    {
        var letto = ItalianAmendment.Read(PdfText.Read(Pdf(Pagina("ENR 4.4 - 1", "26 NOV 2026"))));

        Assert.Contains(letto.Issues, p => p.Kind == AmendmentIssueKind.NoEffectiveDate);
        Assert.Contains(letto.Issues, p => p.Kind == AmendmentIssueKind.NoList);
    }

    // PdfPages: le pagine copiate nell'ordine dato, anche da più file; il file nuovo si legge come i sorgenti.
    [Fact]
    public void LePagineSiCopianoNellOrdineDato()
    {
        var cartella = Directory.CreateTempSubdirectory("pdf-pagine-").FullName;
        try
        {
            var a = Path.Combine(cartella, "a.pdf");
            var b = Path.Combine(cartella, "b.pdf");
            File.WriteAllBytes(a, Pdf(Pagina("ENR 4.4 - 1", "11 JUN 2026"), Pagina("ENR 4.4 - 2", "11 JUN 2026"), []));
            File.WriteAllBytes(b, Pdf(Pagina("ENR 4.4 - 2", "26 NOV 2026")));
            var scritto = Path.Combine(cartella, "nuovo.pdf");

            PdfPages.Write(scritto, [(a, 1), (b, 1), (a, 3)]);

            var indice = PageIndex.Of("nuovo.pdf", PdfText.Read(scritto));
            Assert.Equal(["ENR 4.4-1", "ENR 4.4-2", "ENR 4.4-3"], indice.Select(p => p.Reference));
            Assert.Equal([new DateOnly(2026, 6, 11), new DateOnly(2026, 11, 26), (DateOnly?)null], indice.Select(p => p.Effective));
            Assert.False(File.Exists(scritto + ".scrivendo"));
        }
        finally
        {
            Directory.Delete(cartella, recursive: true);
        }
    }
}
