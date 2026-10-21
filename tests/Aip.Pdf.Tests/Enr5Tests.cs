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

    private static readonly double[] Sei = [40, 160, 220, 270, 330, 400, 560];

    // ENR 5.5.1 (carta «L'AIP intero», passo 4): sei colonne, i periodi, a chi si notifica, l'ente del collegamento radio.
    [Fact]
    public void LeZoneDiLancio_SeiColonne_NotificaEdEnte()
    {
        var pagina = new PdfFintoTests.Pagina(Sei,
        [
            .. Testata("ENR 5.5.1 - 3", "09 JUL 2026", 40, 160, 220, 270, 330, 400),
            (45, 140, "ACQUI TERME -CIV-"), (165, 140, "FL80"), (225, 140, "HJ"), (275, 140, "MILANO"), (335, 140, "MILANO"), (405, 140, "1) Ente ATS"),
            (45, 152, "Circular area centered on"), (165, 152, "SFC"), (225, 152, "DAILY"), (275, 152, "SCCAM"), (335, 152, "ACC/FIC"),
            (45, 164, "44°40'44\"N 008°30'10\"E"),
            (45, 176, "within a 2.0 NM radius."),
        ]);

        var r = ItalianEnr5.ReadSportAreas(PdfText.Read(PdfFintoTests.Pdf(pagina)), "LI-ENR 5.5.1");

        Assert.Empty(r.Issues);
        var a = Assert.Single(r.Airspaces);
        Assert.Equal(("ENR 5.5", AirspaceType.Parachute, "ACQUI TERME -CIV-"), (a.Section, a.Type, a.Designator));
        Assert.Equal(("HJ DAILY", "MILANO SCCAM", "MILANO ACC/FIC"), (a.Hours, a.Notification, a.Unit));
        Assert.IsType<CircleGeometry>(a.Geometry);
    }

    // ENR 5.5.3: il nome su due righe ha accanto alla seconda il limite inferiore, non è un'area nuova.
    [Fact]
    public void IlNomeSuDueRighe_AccantoAlLimiteInferiore_NonEUnAreaNuova()
    {
        var pagina = new PdfFintoTests.Pagina(Quattro,
        [
            .. Testata("ENR 5.5.3 - 1", "09 JUL 2026", 40, 200, 300, 420),
            (45, 140, "AEROMODELLISTI PROVA -CIV-"), (205, 140, "120m AGL"), (305, 140, "Attivita aeromodellistica"), (425, 140, "1) HR: HJ"),
            (45, 152, "Zona di lavoro aereo/Aerial work area"), (205, 152, "SFC"),
            (45, 164, "42°18'08\"N 013°31'08\"E;"),
            (45, 176, "42°18'00\"N 013°31'39\"E;"),
            (45, 188, "42°17'40\"N 013°31'30\"E;"),
            (45, 200, "to point of origin."),
            (45, 220, "AGNONE Settore A -MIL-"), (205, 220, "500ft AGL"), (305, 220, "Voli militari"), (425, 220, "1) HR: H24"),
            (45, 232, "Zona di addestramento/Training area"), (205, 232, "SFC"),
            (45, 244, "37°18'42\"N 015°00'49\"E;"),
            (45, 256, "37°22'50\"N 015°03'41\"E;"),
            (45, 268, "37°20'00\"N 015°05'00\"E;"),
            (45, 280, "to point of origin."),
        ]);

        var r = ItalianEnr5.ReadSportAreas(PdfText.Read(PdfFintoTests.Pdf(pagina)), "LI-ENR 5.5.3");

        Assert.Empty(r.Issues);
        Assert.Equal(["AEROMODELLISTI PROVA -CIV- Zona di lavoro aereo/Aerial work area", "AGNONE Settore A -MIL- Zona di addestramento/Training area"],
            r.Airspaces.Select(a => a.Key));
        Assert.Equal((AirspaceType.AerialWork, "120m AGL", "SFC", "Attivita aeromodellistica"), (r.Airspaces[0].Type, r.Airspaces[0].Upper.Text, r.Airspaces[0].Lower.Text, r.Airspaces[0].Activity));
    }

    private static readonly double[] Tre = [40, 220, 300, 560];

    // ENR 5.6.1.12 p.12 (passo 4): due tabelle sulla stessa pagina. Prima si perdeva tutto quello che stava sopra la seconda
    // testata — qui «Sacra» e la chiusura dell'anello della pagina prima.
    [Fact]
    public void DueTabelleSullaStessaPagina_SiLeggonoTutteEDue()
    {
        var uno = new PdfFintoTests.Pagina(Tre,
        [
            .. Testata("ENR 5.6.1.12 - 11", "07 AUG 2025", 40, 220, 300),
            (45, 140, "Parco Prova"), (225, 140, "4500m AMSL"), (305, 140, "1) Sorvolo vietato"),
            (45, 152, "45°41'00\"N 007°11'30\"E;"), (225, 152, "SFC"),
            (45, 164, "45°37'34\"N 007°22'03\"E;"),
            (45, 176, "45°35'33\"N 007°34'48\"E;"),
        ]);
        var due = new PdfFintoTests.Pagina(Tre,
        [
            .. Testata("ENR 5.6.1.12 - 12", "07 AUG 2025", 40, 220, 300),
            (45, 140, "to point of origin."),
            (45, 160, "Sacra Prova (TO)"), (225, 160, "4430ft AMSL"), (305, 160, "1) Sorvolo vietato ai VDS"),
            (45, 172, "45°05'45\"N 007°20'32\"E;"), (225, 172, "SFC"),
            (45, 184, "45°05'49\"N 007°20'39\"E;"),
            (45, 196, "45°05'52\"N 007°20'40\"E;"),
            (45, 208, "to point of origin."),
            (225, 400, "Limite sup."), (45, 412, "Nome - Limiti laterali"), (225, 412, "Limite inf."), (305, 412, "Note"),
            (45, 440, "1"), (225, 440, "2"), (305, 440, "3"),
            (45, 460, "Riserva Prova"), (225, 460, "1500ft AGL"), (305, 460, "NIL"),
            (45, 472, "45°14'26\"N 008°14'01\"E;"), (225, 472, "SFC"),
            (45, 484, "45°14'09\"N 008°15'10\"E;"),
            (45, 496, "45°14'18\"N 008°16'25\"E;"),
            (45, 508, "to point of origin."),
        ]);

        var r = ItalianEnr5.ReadNatureParks(PdfText.Read(PdfFintoTests.Pdf(uno, due)));

        Assert.Empty(r.Issues);
        Assert.Equal(["Parco Prova", "Sacra Prova (TO)", "Riserva Prova"], r.Airspaces.Select(a => a.Key));
        Assert.All(r.Airspaces, a => Assert.Equal(("ENR 5.6", AirspaceType.NaturePark), (a.Section, a.Type)));
        Assert.Equal("1500ft AGL", r.Airspaces[2].Upper.Text);
        Assert.Null(r.Airspaces[2].Remarks);
    }

    // Il conto (passo 4): le coordinate della prima colonna, un punto ripetuto di seguito o il primo riscritto alla
    // chiusura una volta sola, quelle delle note no.
    [Fact]
    public void IlContoDelleCoordinate()
    {
        var pagina = new PdfFintoTests.Pagina(Tre,
        [
            .. Testata("ENR 5.6.1.10 - 1", "07 AUG 2025", 40, 220, 300),
            (45, 140, "Parco Prova"), (225, 140, "1500ft AGL"), (305, 140, "1) Fra 42°04'57\"N 012°16'49\"E e il fiume"),
            (45, 152, "43°19'36\"N 013°01'24\"E;"), (225, 152, "SFC"),
            (45, 164, "43°23'21\"N 012°55'37\"E;"),
            (45, 176, "43°23'21\"N 012°55'37\"E;"),
            (45, 188, "43°25'37\"N 012°54'47\"E;"),
            (45, 200, "43°19'36\"N 013°01'24\"E;"),
            (45, 212, "to point of origin."),
            (45, 230, "Area Prova"), (225, 230, "500ft AGL"),
            (45, 242, "Circular area centered on"), (225, 242, "SFC"),
            (45, 254, "43°19'36\"N 013°01'24\"E"),
            (45, 266, "within a 0.5 NM radius."),
        ]);
        var pagine = PdfText.Read(PdfFintoTests.Pdf(pagina));

        Assert.Equal([new ExpectedCount("coordinate", 4)], ItalianExpectedCounts.Of("LI-ENR 5.6.1.10.pdf", pagine));
        Assert.Equal(4, ItalianExpectedCounts.Read("coordinate", ItalianEnr5.ReadNatureParks(pagine)));
    }
}
