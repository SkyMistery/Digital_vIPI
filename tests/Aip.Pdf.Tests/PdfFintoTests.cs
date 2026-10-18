using Aip.Pdf.Italia;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Aip.Pdf.Tests;

/// <summary>
/// L'importer italiano su un PDF costruito qui, con l'impaginazione di ENR 5.1 (quattro colonne fra cinque righe
/// verticali, testata con «1 2 3 4», pagine pari sfalsate, data AIRAC per pagina). Dati inventati: nei test niente PDF
/// di ENAV (carta F6 §3). La misura sui PDF veri gira fuori dalla CI (`tools/Aip.Pdf.Cli`).
/// </summary>
public sealed class PdfFintoTests
{
    private const double Altezza = 842;

    /// <summary>Una pagina finta: le righe della tabella alle x date, e il testo (x, distanza dall'alto, testo).</summary>
    internal sealed record Pagina(double[] Righe, (double X, double Top, string Testo)[] Testo);

    internal static byte[] Pdf(params Pagina[] pagine)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var p in pagine)
        {
            var page = builder.AddPage(595, Altezza);
            foreach (var x in p.Righe)
                page.DrawLine(new PdfPoint(x, Altezza - 100), new PdfPoint(x, Altezza - 800), 0.5);
            // ⚠️ Il font standard di PdfPig non ha «°»: nel PDF finto i gradi sono «º», che il lettore accetta come i PDF veri.
            foreach (var (x, top, testo) in p.Testo)
                page.AddText(testo.Replace('°', 'º'), 8, new PdfPoint(x, Altezza - top - 7), font);
        }
        return builder.Build();
    }

    private static (double, double, string)[] Testata(double dx, string riferimento, string data) =>
    [
        (450 + dx, 40, riferimento),
        (45 + dx, 55, "AIP - Italia AIRAC effective date " + data),
        (45 + dx, 105, "Identificazione"),
        (100 + dx, 120, "1"), (240 + dx, 120, "2"), (350 + dx, 120, "3"), (480 + dx, 120, "4"),
    ];

    private static readonly double[] RigheDispari = [40, 200, 300, 420, 560];

    private static readonly double[] RighePari = [45, 205, 305, 425, 565];

    private static ImportResult Leggi()
    {
        var uno = new Pagina(RigheDispari,
        [
            .. Testata(0, "ENR 5.1.2 - 1", "09 JUL 2026"),
            (45, 140, "LI R901 - Prova Uno"), (205, 139, "FL95"), (305, 140, "Esercitazioni"), (425, 140, "1) HR: H24"),
            (45, 152, "41°30'00\"N 012°15'00\"E;"), (205, 152, "SFC"), (425, 152, "2) Ente: Roma ACC"),
            (45, 164, "41°30'00\"N 012°30'00\"E;"),
            (45, 176, "41°15'00\"N 012°22'30\"E;"),
            (45, 188, "to point of origin."),

            (45, 210, "LI R902 - Prova Due"), (205, 210, "2000ft"),
            (45, 222, "Circular area centered on"), (205, 222, "AMSL"),
            (45, 234, "42°00'45\"N 013°36'00\"E within a"), (205, 234, "GND"),
            (45, 246, "1.0 NM radius."),

            (45, 300, "LI D903 - Prova Tre"), (205, 300, "UNL"),
            (45, 312, "45°54'00\"N 006°54'00\"E"), (205, 312, "FL285"),
            (45, 324, "45°54'00\"N 007°06'00\"E then arc of"),
            (45, 336, "circle in clockwise direction radius"),
            (45, 348, "5.0 NM centred on 45°50'00\"N 007°00'00\"E"),
            (45, 360, "till point"),
        ]);
        var due = new Pagina(RighePari,
        [
            .. Testata(5, "ENR 5.1.2 - 2", "06 AUG 2026"),
            (50, 140, "45°48'00\"N 007°06'00\"E Italian northern"),
            (50, 152, "geographical border till point"),
            (50, 164, "45°40'00\"N 007°00'00\"E"),
            (50, 176, "to point of origin."),
            (50, 200, "LI X12 - Strana"),
        ]);
        var tre = new Pagina([], [(450, 40, "ENR 5.1.2 - 3"), (45, 55, "AIRAC effective date 06 AUG 2026"), (45, 140, "Pagina lasciata in bianco")]);

        return ItalianAip.ReadEnr51(PdfText.Read(Pdf(uno, due, tre)));
    }

    [Fact]
    public void LePagineColLoroRiferimentoELaLoroData()
    {
        var letto = Leggi();

        Assert.Equal(
            [
                new ImportPage(1, "ENR 5.1.2-1", new DateOnly(2026, 7, 9), true),
                new ImportPage(2, "ENR 5.1.2-2", new DateOnly(2026, 8, 6), true),
                new ImportPage(3, "ENR 5.1.2-3", new DateOnly(2026, 8, 6), false),
            ],
            letto.Pages);
        Assert.Contains(new ImportIssue(ImportIssueKind.PageWithoutTable, null, 3), letto.Issues);
    }

    [Fact]
    public void LeAreeConTutteLeColonne()
    {
        var aree = Leggi().Airspaces;

        Assert.Equal(["LI R901", "LI R902", "LI D903"], aree.Select(a => a.Designator));

        var uno = aree[0];
        Assert.Equal("Prova Uno", uno.Name);
        Assert.Equal(AirspaceType.Restricted, uno.Type);
        Assert.Equal("ENR 5.1", uno.Section);
        Assert.Equal("FL95", uno.Upper.Text);
        Assert.Equal(VerticalReference.FlightLevel, uno.Upper.Reference);
        Assert.Equal(VerticalReference.Ground, uno.Lower.Reference);
        Assert.Equal("Esercitazioni", uno.Activity);
        Assert.Equal("1) HR: H24\n2) Ente: Roma ACC", uno.Remarks);
        Assert.Equal(new AipPage("ENR 5.1.2-1", new DateOnly(2026, 7, 9)), uno.Page);
        Assert.Equal(3, Assert.IsType<RingGeometry>(uno.Geometry).Segments.Count);

        var due = aree[1];
        Assert.Equal("2000ft AMSL", due.Upper.Text);
        Assert.Equal(2000, due.Upper.Value);
        Assert.Equal(new Distance(1.0, DistanceUnit.Nm), Assert.IsType<CircleGeometry>(due.Geometry).Radius);
        Assert.Null(due.Activity);
    }

    [Fact]
    public void UnAreaCheContinuaNellaPaginaDopo_SfalsataDiCinquePunti()
    {
        var tre = Leggi().Airspaces[2];

        Assert.Equal(AirspaceType.Danger, tre.Type);
        Assert.Equal(("UNL", "FL285"), (tre.Upper.Text, tre.Lower.Text));
        var tratti = Assert.IsType<RingGeometry>(tre.Geometry).Segments;
        Assert.Equal(["line", "arc", "border", "line"], tratti.Select(s => s switch
        {
            ArcSegment => "arc",
            BorderSegment => "border",
            _ => "line",
        }));
        Assert.Equal(new Distance(5.0, DistanceUnit.Nm), ((ArcSegment)tratti[1]).Radius);
        Assert.Equal("Italian northern geographical border", ((BorderSegment)tratti[2]).Text);
        // La pagina è quella dove l'area comincia.
        Assert.Equal("ENR 5.1.2-1", tre.Page!.Reference);
    }

    // 🔴 La guardia della F0: l'identificativo fuori standard si dice due volte — come riga che sembra un
    // identificativo, e come parole che la geometria dell'area prima non capisce. Non si salda in silenzio.
    [Fact]
    public void UnIdentificativoFuoriStandardNonSiSaldaInSilenzio()
    {
        var problemi = Leggi().Issues;

        Assert.Contains(new ImportIssue(ImportIssueKind.UnknownIdentifier, null, 2, "LI X12 - Strana"), problemi);
        Assert.Contains(problemi, p => p is { Kind: ImportIssueKind.Geometry, Key: "LI D903 Prova Tre" }
            && p.Detail!.StartsWith("UnknownWords", StringComparison.Ordinal));
    }

    [Fact]
    public void IlModelloLettoPassaIControlliEVaInJson()
    {
        var doc = new AipDocument { State = ItalianAip.State, Airac = "2611", Airspaces = Leggi().Airspaces };

        Assert.Empty(AipCheck.Run(doc));
        Assert.Equal(doc, AipJson.Deserialize(AipJson.Serialize(doc)));
    }

    [Fact]
    public void UnFileCheNonEUnPdfELoDiceSenzaPdfPig()
    {
        var e = Assert.Throws<InvalidDataException>(() => PdfText.Read("questo non è un PDF"u8.ToArray()));
        Assert.NotNull(e.InnerException);
    }

    [Fact]
    public void UnFileCheNonSaLeggereLoDice()
    {
        var letto = ItalianAip.ReadFile("LI-ENR 4.4.pdf");

        Assert.Empty(letto.Airspaces);
        Assert.Equal(new ImportIssue(ImportIssueKind.Unsupported, null, null, "LI-ENR 4.4.pdf"), Assert.Single(letto.Issues));
    }
}
