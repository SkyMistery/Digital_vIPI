using Aip.Pdf.Italia;

namespace Aip.Pdf.Tests;

/// <summary>
/// ENR 5.2 (carta F6, slice 4a): i poligoni militari di ENR 5.2.1 (nomi senza identificativo, il riferimento fra parentesi
/// sotto o accanto) e le zone e i corridoi APR di ENR 5.2.2.5 (cinque colonne, la tabella che finisce alle note generali),
/// su PDF costruiti qui (<see cref="PdfFintoTests.Pdf"/>). Dati inventati.
/// </summary>
public sealed class Enr5Tests
{
    private static readonly double[] Quattro = [40, 200, 300, 420, 560];
    private static readonly double[] Cinque = [40, 200, 260, 330, 420, 560];

    private static (double, double, string)[] Testata(string riferimento, string data, params double[] colonne) =>
    [
        (450, 40, riferimento),
        (45, 55, "AIP - Italia AIRAC effective date " + data),
        .. colonne.Select((x, i) => (x + 5, 120.0, (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))),
    ];

    [Fact]
    public void IPoligoniMilitari_IlNomeColRiferimento_SottoOAccanto()
    {
        var pagina = new PdfFintoTests.Pagina(Quattro,
        [
            .. Testata("ENR 5.2.1.1 - 1", "05 OCT 2023", 40, 200, 300, 420),
            (45, 140, "ARPY"), (205, 140, "7000ft"), (305, 140, "Esercitazioni di tiro"), (425, 140, "Poligono occasionale"),
            (45, 152, "( W Morgex )"), (205, 152, "AMSL"),
            (45, 164, "45°44'21\"N 007°00'23\"E;"), (205, 164, "SFC"), (305, 164, "Firing exercises"),
            (45, 176, "45°43'32\"N 006°59'43\"E;"),
            (45, 188, "45°44'28\"N 006°58'54\"E;"),
            (45, 200, "to point of origin."),
            (45, 220, "DRASI (SE Agrigento)"), (205, 220, "4265ft"), (305, 220, "Esercitazioni di tiro"), (425, 220, "NIL"),
            (45, 232, "Circular area centered on"), (205, 232, "AMSL"),
            (45, 244, "37°12'00\"N 013°40'00\"E"), (205, 244, "SFC"),
            (45, 256, "within a 3.0 NM radius."),
        ]);

        var r = ItalianEnr5.ReadFiringAreas(PdfText.Read(PdfFintoTests.Pdf(pagina)));

        Assert.Empty(r.Issues);
        Assert.Equal(["ARPY ( W Morgex )", "DRASI (SE Agrigento)"], r.Airspaces.Select(a => a.Key));
        var arpy = r.Airspaces[0];
        Assert.Equal(("ENR 5.2", AirspaceType.Military, "7000ft AMSL", "SFC"), (arpy.Section, arpy.Type, arpy.Upper.Text, arpy.Lower.Text));
        Assert.Equal("Esercitazioni di tiro Firing exercises", arpy.Activity);
        Assert.Equal(3, Assert.IsType<RingGeometry>(arpy.Geometry).Segments.Count);
        Assert.Null(r.Airspaces[1].Remarks);
        Assert.IsType<CircleGeometry>(r.Airspaces[1].Geometry);
    }

    [Fact]
    public void LeZoneEICorridoiApr_CinqueColonne_ELaTabellaFinisceAlleNote()
    {
        var uno = new PdfFintoTests.Pagina(Cinque,
        [
            .. Testata("ENR 5.2.2.5 - 1", "03 SEP 2026", 40, 200, 260, 330, 420),
            (45, 140, "ALBA Corridor"), (205, 140, "FL200"), (265, 140, "H24"), (335, 140, "Brindisi SCCAM"), (425, 140, "1) Solo a blocchi"),
            (45, 152, "Corridor 10 NM wide joining following points:"), (205, 152, "FL150"),
            (45, 164, "41°00'00\"N 018°35'00\"E;"),
            (45, 176, "40°53'30\"N 018°57'06\"E;"),
            (45, 200, "VICTOR Corridor - segment 9"), (205, 200, "FL250"), (265, 200, "H24"), (335, 200, "Roma SCCAM"), (425, 200, "NIL"),
            (45, 212, "Corridor 6 NM wide joining following points:"), (205, 212, "FL80"),
            (45, 224, "39°40'00\"N 009°40'00\"E;"),
        ]);
        // La pagina dopo: il seguito del corridoio, poi le note generali (con righe verticali alle stesse x: non sono aree).
        var due = new PdfFintoTests.Pagina(Cinque,
        [
            .. Testata("ENR 5.2.2.5 - 2", "03 SEP 2026", 40, 200, 260, 330, 420),
            (45, 140, "39°31'58\"N 009°42'49\"E;"),
            (45, 170, "NOTE GENERALI"),
            (45, 182, "1 Ai piloti è richiesto di prestare attenzione"),
            (45, 194, "ENR 5.2.2.5.2 Procedure speciali:"),
        ]);
        var tre = new PdfFintoTests.Pagina(Cinque, [.. Testata("ENR 5.2.2.5 - 3", "03 SEP 2026", 40, 200, 260, 330, 420), (45, 140, "L613 tra/between")]);

        var r = ItalianEnr5.ReadUasAreas(PdfText.Read(PdfFintoTests.Pdf(uno, due, tre)));

        Assert.Empty(r.Issues);
        Assert.Equal(["ALBA Corridor", "VICTOR Corridor - segment 9"], r.Airspaces.Select(a => a.Key));
        var alba = r.Airspaces[0];
        Assert.Equal(("H24", "Brindisi SCCAM", "1) Solo a blocchi"), (alba.Hours, alba.Unit, alba.Remarks));
        Assert.Null(alba.Activity);
        var k = Assert.IsType<CorridorGeometry>(alba.Geometry);
        Assert.Equal((10.0, 2), (k.Width.Value, k.Points.Count));
        // VICTOR continua nella pagina dopo, e si ferma prima delle note.
        Assert.Equal(2, Assert.IsType<CorridorGeometry>(r.Airspaces[1].Geometry).Points.Count);
        Assert.Equal(3, r.Pages.Count);
        Assert.False(r.Pages[2].Table);
    }
}
