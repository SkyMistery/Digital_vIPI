using Aip.Pdf.Italia;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Aip.Pdf.Tests;

/// <summary>
/// ENR 2 (carta F6, slice 1a) su pagine costruite qui con l'impaginazione di ENAV: le righe delle tabelle disegnate a
/// pezzi (una per riga di tabella), due tabelle nella stessa pagina, la barra di modifica a margine, la testata con la
/// riga dei numeri, i gruppi a zone, il rimando «As for», le fasce verticali. Dati inventati.
/// </summary>
public sealed class Enr2Tests
{
    private const double Altezza = 842;

    private sealed record Tabella(double[] Xs, double[] Righe);

    private sealed record Pagina(Tabella[] Tabelle, (double X, double Top, string Testo)[] Testo, double? BarraDiModifica = null);

    private static byte[] Pdf(params Pagina[] pagine)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var p in pagine)
        {
            var page = builder.AddPage(595, Altezza);
            foreach (var t in p.Tabelle)
                for (var r = 0; r + 1 < t.Righe.Length; r++)
                    foreach (var x in t.Xs)
                        page.DrawLine(new PdfPoint(x, Altezza - t.Righe[r]), new PdfPoint(x, Altezza - t.Righe[r + 1]), 0.5);
            if (p.BarraDiModifica is { } b)
                page.DrawLine(new PdfPoint(b, Altezza - 100), new PdfPoint(b, Altezza - 700), 1.5);
            // Il font standard di PdfPig non ha «°»: i gradi sono «º», che il lettore accetta come i PDF veri.
            foreach (var (x, top, testo) in p.Testo)
                page.AddText(testo.Replace('°', 'º'), 8, new PdfPoint(x, Altezza - top - 7), font);
        }
        return builder.Build();
    }

    private static readonly double[] TreColonne = [52, 250, 420, 565];

    private static (double, double, string)[] Testata(double top) =>
    [
        (55, top, "NOME E LIMITI LATERALI"), (255, top, "LIMITI VERTICALI E CLASSIFICAZIONE"), (425, top, "ENTE CHE FORNISCE"),
        (55, top + 12, "DESIGNATION AND LATERAL LIMITS"), (255, top + 12, "VERTICAL LIMITS AND CLASSIFICATION"), (425, top + 12, "UNIT PROVIDING SERVICE"),
    ];

    private static ImportResult Fir()
    {
        var uno = new Pagina(
            [
                new Tabella(TreColonne, [100, 130, 310]),
                // Sotto, una tabella delle frequenze a cinque colonne: non è delle aree, si salta.
                new Tabella([52, 150, 250, 350, 450, 565], [340, 360, 400]),
            ],
            [
                (450, 40, "ENR 2.1.1.1 - 1"), (45, 55, "AIP - Italia 27 JUL 2023"),
                .. Testata(104),
                (55, 140, "MILANO FIR"), (255, 140, "FL195"), (425, 140, "MILANO ACC/FIC 3)"),
                (55, 152, "45°00'00\"N 009°00'00\"E;"), (255, 152, "SFC"), (425, 152, "PADOVA ACC/FIC 4)"),
                (55, 164, "45°30'00\"N 009°30'00\"E;"), (255, 164, "\"G\" 1) 2)"),
                (55, 176, "45°00'00\"N 010°00'00\"E;"),
                (55, 188, "to point of origin."),
                (55, 210, "MILANO UIR"), (255, 210, "UNL"), (425, 210, "ROMA ACC/FIC 5)"),
                (55, 222, "Come per la FIR MILANO"), (255, 222, "FL660"),
                (55, 234, "As for MILANO FIR"), (255, 234, "\"G\""),
                (255, 246, "FL660"),
                (255, 258, "FL195"),
                (255, 270, "\"C\" 6) 7)"),
                (55, 344, "SERVIZIO"), (155, 344, "NOMINATIVO"), (255, 344, "FREQUENZA"),
                (55, 370, "45°00'00\"N 009°00'00\"E"), (155, 370, "MILANO"), (255, 370, "121.500"),
            ],
            BarraDiModifica: 48);

        var due = new Pagina(
            [new Tabella(TreColonne, [100, 130, 145, 400])],
            [
                (450, 40, "ENR 2.1.1.1 - 2"), (45, 55, "AIRAC effective date 28 NOV 2024 AIP - Italia"),
                .. Testata(104), (140, 134, "1"), (330, 134, "2"), (490, 134, "3"),
                (55, 150, "MILANO TMA"),
                (55, 162, "Zone '1' -LOMBARDIA-"), (255, 162, "FL95"), (425, 162, "MILANO ACC"),
                (55, 174, "45°49'51\"N 008°42'58\"E;"), (255, 174, "2000ft"),
                (55, 186, "45°42'07\"N 009°30'33\"E"), (255, 186, "AMSL"),
                (55, 198, "then arc of circle in clockwise direction radius 8.0 NM"), (255, 198, "\"A\""),
                (55, 210, "centred on"),
                (55, 222, "45°40'25\"N 009°41'42\"E till point"),
                (55, 234, "45°43'56\"N 009°31'26\"E;"),
                (55, 246, "45°29'00\"N 010°00'00\"E;"),
                (55, 258, "to point of origin."),
                (55, 280, "Zona/Zone '2'"), (255, 280, "FL95"), (425, 280, "MILANO ACC"),
                (55, 292, "Circular area centered on"), (255, 292, "FL55"),
                (55, 304, "45°10'00\"N 009°10'00\"E within a"), (255, 304, "\"D\""),
                (55, 316, "5.0 NM radius."),
            ]);

        return ItalianEnr2.Read(PdfText.Read(Pdf(uno, due)), "ENR 2.1", null);
    }

    [Fact]
    public void FirEUirConLeFasce_IlRimando_ELeDateDellePagine()
    {
        var letto = Fir();

        Assert.Equal(
            [new ImportPage(1, "ENR 2.1.1.1-1", new DateOnly(2023, 7, 27), true), new ImportPage(2, "ENR 2.1.1.1-2", new DateOnly(2024, 11, 28), true)],
            letto.Pages);
        var fir = letto.Airspaces[0];
        Assert.Equal((AirspaceType.Fir, "MILANO FIR", "FL195", "SFC", "G"), (fir.Type, fir.Designator, fir.Upper.Text, fir.Lower.Text, fir.Class));
        Assert.Null(fir.Bands);
        Assert.Equal("MILANO ACC/FIC\nPADOVA ACC/FIC", fir.Unit);
        Assert.Equal("Note 1) 2) 3) 4)", fir.Remarks);
        Assert.Equal(3, Assert.IsType<RingGeometry>(fir.Geometry).Segments.Count);

        var uir = letto.Airspaces[1];
        Assert.Equal((AirspaceType.Uir, "UNL", "FL195", (string?)null), (uir.Type, uir.Upper.Text, uir.Lower.Text, uir.Class));
        Assert.Equal(
            [("UNL", "FL660", "G"), ("FL660", "FL195", "C")],
            uir.Bands!.Select(b => (b.Upper.Text, b.Lower.Text, b.Class!)));
        // «As for MILANO FIR»: la geometria della FIR.
        Assert.Equal(fir.Geometry, uir.Geometry);
        Assert.Null(uir.Name);
    }

    [Fact]
    public void IlGruppoAZone_ColloLimiteACapo_ELaZonaBilingue()
    {
        var aree = Fir().Airspaces;

        var lombardia = aree[2];
        Assert.Equal((AirspaceType.Tma, "MILANO TMA Zone '1'", "LOMBARDIA"), (lombardia.Type, lombardia.Designator.Replace('’', '\''), lombardia.Name));
        Assert.Equal(("FL95", "2000ft AMSL", "A"), (lombardia.Upper.Text, lombardia.Lower.Text, lombardia.Class));
        var tratti = Assert.IsType<RingGeometry>(lombardia.Geometry).Segments;
        Assert.Equal(new Distance(8.0, DistanceUnit.Nm), Assert.IsType<ArcSegment>(tratti[1]).Radius);

        var due = aree[3];
        Assert.Equal("MILANO TMA Zona/Zone '2'", due.Designator.Replace('’', '\''));
        Assert.Equal(new Distance(5.0, DistanceUnit.Nm), Assert.IsType<CircleGeometry>(due.Geometry).Radius);
        Assert.Equal(4, aree.Count);
    }

    [Fact]
    public void LeTabelleSiTrovanoTutte_LaBarraDiModificaNonEUnaColonna()
    {
        var pagina = PdfText.Read(Pdf(Fir_PrimaPagina()))[0];

        var griglie = Table.FindAll(pagina, minSpan: 300);

        Assert.Equal(2, griglie.Count);
        Assert.Equal([52.0, 250.0, 420.0, 565.0], griglie[0].Columns);
        Assert.Equal(5, griglie[1].ColumnCount);
        Assert.True(griglie[0].Bottom < griglie[1].Top);
    }

    private static Pagina Fir_PrimaPagina() => new(
        [new Tabella(TreColonne, [100, 130, 310]), new Tabella([52, 150, 250, 350, 450, 565], [340, 360, 400])],
        [(55, 140, "MILANO FIR")],
        BarraDiModifica: 48);

    [Fact]
    public void LeNpzDalTitoloDellaSezione_ColTitoloInUnaPaginaPrima()
    {
        var titolo = new Pagina([], [(450, 40, "ENR 2.2 - 7"), (45, 55, "AIRAC effective date 03 SEP 2026"), (45, 300, "ENR 2.2.4 No Planning Zone - NPZ ENR 2.2.4 No Planning Zone - NPZ")]);
        var tabella = new Pagina(
            [new Tabella(TreColonne, [100, 130, 300])],
            [
                (450, 40, "ENR 2.2 - 8"), (45, 55, "AIRAC effective date 03 SEP 2026"),
                (55, 104, "IDENTIFICAZIONE, NOME E LIMITI LATERALI"), (255, 104, "LIMITE SUPERIORE - LIMITE INFERIORE"), (425, 104, "NOTE"),
                (55, 116, "IDENTIFICATION, DESIGNATION AND LATERAL LIMITS"), (255, 116, "UPPER LIMIT - LOWER LIMIT"), (425, 116, "REMARKS"),
                (55, 140, "LINPZ1 PROVA"), (255, 140, "FL660"), (425, 140, "1) H24"),
                (55, 152, "46°33'11\"N 013°21'40\"E;"), (255, 152, "FL145"),
                (55, 164, "46°18'33\"N 013°24'23\"E;"),
                (55, 176, "46°29'53\"N 013°16'02\"E;"),
                (55, 188, "to point of origin."),
            ]);

        var npz = Assert.Single(ItalianEnr2.Read(PdfText.Read(Pdf(titolo, tabella)), "ENR 2.2", null).Airspaces);

        Assert.Equal((AirspaceType.Npz, "LINPZ1 PROVA", "FL660", "FL145", "1) H24"), (npz.Type, npz.Designator, npz.Upper.Text, npz.Lower.Text, npz.Remarks));
        Assert.Null(npz.Unit);
    }

    [Fact]
    public void LeFasce_ILimitiACapo_IRimandi_EQuelCheNonELimite()
    {
        var problemi = new List<ImportIssue>();
        var rimandi = new SortedSet<int>();

        var (bande, extra) = ItalianEnr2.Fasce(["2000ft", "AMSL", "or 1000ft AGL", "whichever is higher", "SFC", "Vedere/see para. 3", "\"D\" 1)"], rimandi, "X", 1, problemi);

        Assert.Equal([("2000ft AMSL or 1000ft AGL whichever is higher", "SFC", "D")], bande.Select(b => (b.Upper.Text, b.Lower.Text, b.Class!)));
        Assert.Equal(["Vedere/see para. 3"], extra);
        Assert.Equal([1], rimandi);
        Assert.Empty(problemi);
    }

    [Fact]
    public void LeFasce_SenzaClasse_ESenzaLimiti()
    {
        var problemi = new List<ImportIssue>();

        var (bande, _) = ItalianEnr2.Fasce(["6000ft AMSL", "2000ft AMSL"], [], "X", 1, problemi);
        Assert.Equal([("6000ft AMSL", "2000ft AMSL", (string?)null)], bande.Select(b => (b.Upper.Text, b.Lower.Text, b.Class)));

        ItalianEnr2.Fasce(["\"G\""], [], "Y", 2, problemi);
        Assert.Equal(new ImportIssue(ImportIssueKind.LimitsMissing, "Y", 2, ""), Assert.Single(problemi));
    }

    [Fact]
    public void ILimitiDettiNelTestoDellaSezione()
    {
        string[] righe =
        [
            "Limiti Verticali Vertical limits",
            "•limite verticale inferiore: SFC •lower vertical limit: SFC",
            "•limite verticale superiore: il limite verticale inferiore degli spazi aerei •upper vertical limit: lower vertical limit of overlying controlled",
            "controllati sovrastanti (CTR, TMA, CTA, rotte ATS, ecc.). airspace (CTR, TMA, CTA, ATS routes, etc.).",
        ];

        Assert.Equal(
            ("lower vertical limit of overlying controlled airspace (CTR, TMA, CTA, ATS routes, etc.)", "SFC"),
            ItalianEnr2.LimitiDelTesto(righe, (null, null)));
        Assert.Equal(("A", "B"), ItalianEnr2.LimitiDelTesto(["niente qui"], ("A", "B")));
    }
}
