using Aip.Pdf.Italia;
using Pagina = Aip.Pdf.Tests.Enr4Tests.Pagina;

namespace Aip.Pdf.Tests;

/// <summary>
/// ENR 5.2.2 (carta F6, slice 4c, committente: «prendi anche AAR, AT e INDIA»): i corridoi AAR coi punti per nome e le
/// lettere spaziate di AAR DELTA, le zone INDIA con più coordinate su una riga, le aree tattiche a due colonne. PDF costruiti
/// qui coi bordi orizzontali (<see cref="Enr4Tests.Pdf"/>). Dati inventati.
/// </summary>
public sealed class Enr5ZoneTests
{
    private static readonly double[] Aar = [52, 152, 252, 312, 412, 565];
    private static readonly double[] India = [58, 168, 468, 558];

    private static (double, double, string)[] Testata(string riferimento, double[] colonne, int numeri) =>
    [
        (450, 30, riferimento),
        (52, 45, "AIP - Italia 27 JUL 2023"),
        (60, 120, "Denominazione"),
        .. Enumerable.Range(0, numeri).Select(i => (colonne[i] + 4, 152.0, (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))),
    ];

    private static Position? Bular(string nome) => nome switch
    {
        "BULAR" => new Position(39.5, 9.5),
        "SIG TACAN" => new Position(37.4, 14.97),
        _ => null,
    };

    [Fact]
    public void LeAar_CorridoioCoiPuntiPerNome_PoligonoConLeLettereSpaziate_ELaTabellaFinisceAlParagrafo()
    {
        var c = Aar;
        var pagina = new Pagina(c, [100, 150, 162, 260, 380, 420, 480],
        [
            .. Testata("ENR 5.2.2.1 - 1", c, 5),
            // Un corridoio di 10 NM, due blocchi di livelli, un punto per coordinate e uno per nome.
            (54, 170, "AAR ALPHA"), (154, 170, "385200N"), (154, 182, "0090000E - BULAR"),
            (254, 170, "FL 280"), (254, 182, "FL 260"), (254, 194, "o/or"), (254, 206, "FL 230"), (254, 218, "FL 210"),
            (314, 170, "10"), (414, 170, "1.Su base tattica"), (414, 182, "2.Due blocchi"),
            // Un poligono (ampiezza NIL), le coordinate una per riga, l'ultima a lettere spaziate, il primo punto ripetuto.
            (54, 266, "AAR DELTA"), (154, 266, "line joining the"), (154, 278, "following points:"),
            (154, 290, "373324N 0150133E"), (154, 302, "370900N 0174700E"), (154, 314, "3 6 3 6 0 0 N 0 1 9 0 0 0 0 E"),
            (154, 326, "373324N 0150133E"),
            (254, 266, "FL 280"), (254, 278, "F L 2 6 0"), (314, 266, "NIL"),
            // Un punto che ENR 4 non ha: si dice, l'area non entra.
            (54, 386, "AAR NUOVO"), (154, 386, "SIG TACAN - XXXXX"), (254, 386, "FL 200"), (254, 398, "FL 170"), (314, 386, "10"),
            (54, 430, "ENR 5.2.2.1.2 Procedure speciali:"), (154, 430, "L 44: tra ADAMI"),
        ]);

        var r = ItalianEnr5Zone.ReadAirRefuelling(PdfText.Read(Enr4Tests.Pdf(pagina)), Bular);

        Assert.Equal(["AAR ALPHA", "AAR DELTA"], r.Airspaces.Select(a => a.Designator));
        var alpha = r.Airspaces[0];
        var k = Assert.IsType<CorridorGeometry>(alpha.Geometry);
        Assert.Equal(10, k.Width.Value);
        Assert.Equal(new Position(39.5, 9.5), k.Points[1]);
        Assert.Equal(38 + 52 / 60.0, k.Points[0].Lat, 9);
        Assert.Equal(("FL 280", "FL 210", 2), (alpha.Upper.Text, alpha.Lower.Text, alpha.Bands!.Count));
        Assert.Equal("1.Su base tattica\n2.Due blocchi", alpha.Remarks);
        var delta = Assert.IsType<RingGeometry>(r.Airspaces[1].Geometry);
        Assert.Equal(3, delta.Segments.Count);
        Assert.Equal(36.6, delta.Segments[2].From.Lat, 9);
        Assert.Equal("FL 260", r.Airspaces[1].Lower.Text);
        Assert.Contains(r.Issues, i => i.Key == "AAR NUOVO" && i.Detail == "punto non trovato in ENR 4: XXXXX");
        Assert.Contains(r.Issues, i => i.Key == "AAR NUOVO" && i.Kind == ImportIssueKind.NoGeometry);
    }

    [Fact]
    public void LeZoneIndia_PiuCoordinateSuUnaRiga_ILivelliComeStampati()
    {
        var c = India;
        var pagina = new Pagina(c, [100, 150, 162, 200, 240],
        [
            .. Testata("ENR 5.2.2.2 - 1", c, 3),
            (60, 170, "INDIA 2"), (60, 182, "(Valle del Po/Po valley)"),
            (170, 170, "452800N 0092200E - 452800N 0111000E - 444300N 0111000E"), (170, 182, "444300N 0092200E - 452800N 0092200E"),
            (470, 170, "FL 310 o/or FL 290"),
        ]);

        var r = ItalianEnr5Zone.ReadIndia(PdfText.Read(Enr4Tests.Pdf(pagina)));

        var india = Assert.Single(r.Airspaces);
        Assert.Equal(("INDIA 2", "(Valle del Po/Po valley)"), (india.Designator, india.Name));
        Assert.Equal(4, Assert.IsType<RingGeometry>(india.Geometry).Segments.Count);
        Assert.Equal(("FL 310 o/or FL 290", "FL 310 o/or FL 290"), (india.Upper.Text, india.Lower.Text));
        Assert.Empty(r.Issues);
    }

    [Fact]
    public void LeAreeTattiche_LaColonnaItaliana_CoordinateOltreLaMeta()
    {
        // Come ENR 5.2.2.6 p.3: il nome a 54, le coordinate da 263 a 354 (oltre la metà della pagina), l'inglese a 439.
        var pagina = new Pagina([], [],
        [
            (450, 30, "ENR 5.2.2.6 - 3"), (52, 45, "AIP - Italia AIRAC effective date 30 NOV 2023"),
            (54, 160, "AREA EMILIA"), (263, 162, "44°39'00\"N 009°00'00\"E;"), (439, 160, "EMILIA AREA"),
            (263, 174, "45°00'00\"N 009°12'00\"E;"),
            (263, 186, "44°15'00\"N 011°00'00\"E;"),
            (263, 198, "to point of origin."),
        ]);

        var r = ItalianEnr5Zone.ReadTacticalAreas(PdfText.Read(Enr4Tests.Pdf(pagina)));

        var at = Assert.Single(r.Airspaces);
        Assert.Equal("AT EMILIA", at.Designator);
        Assert.Equal(3, Assert.IsType<RingGeometry>(at.Geometry).Segments.Count);
        Assert.Equal(("NIL", "500ft AGL"), (at.Upper.Text, at.Lower.Text));
        Assert.Empty(r.Issues);
    }

    [Fact]
    public void IlRisolutore_IlPuntoELApparatoDelTipoDetto()
    {
        var trp = new Navaid
        {
            Section = "ENR 4.1", Designator = "TRP", Name = "TRAPANI", Type = "VORTAC", Kind = NavaidKind.Vortac, Position = new Position(37.9, 12.5),
            Components = [new NavaidComponent("VOR", new Position(37.9, 12.5)), new NavaidComponent("TACAN", new Position(37.9, 12.51))],
        };
        var ndb = new Navaid
        {
            Section = "ENR 4.1", Designator = "TRP", Name = "TRAPANI", Type = "NDB", Kind = NavaidKind.Ndb, Position = new Position(37.91, 12.49),
            Components = [new NavaidComponent(null, new Position(37.91, 12.49))],
        };
        var bular = new SignificantPoint { Section = "ENR 4.4", Designator = "BULAR", Position = new Position(39.5, 9.5) };
        var risolvi = ItalianEnr5Zone.Risolutore([ndb, trp], [bular]);

        Assert.Equal(new Position(37.9, 12.51), risolvi("TRP TACAN"));
        Assert.Equal(new Position(37.9, 12.5), risolvi("TRP VORTAC"));
        Assert.Equal(new Position(39.5, 9.5), risolvi("BULAR"));
        Assert.Null(risolvi("NESSUNO"));
    }
}
