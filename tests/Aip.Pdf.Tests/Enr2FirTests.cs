using Aip.Pdf.Italia;

namespace Aip.Pdf.Tests;

/// <summary>
/// ENR 2.1.1.1-3 (carta «L'AIP intero», passo 2c): le linee fra le aree degli ACC/FIC nelle note della FIR, e le aree VFR
/// della FIR. Pagine costruite qui con le posizioni del 2611: l'italiano a sinistra, l'inglese a destra (che non si legge).
/// </summary>
public sealed class Enr2FirTests
{
    private static PdfWord Parola(double x, double y, string testo) => new(testo, x, x + testo.Length * 4.5, y - 7, y + 1, y);

    private static PdfPageText Pagina(int numero, string riferimento, params (double X, double Y, string Testo)[] righe)
    {
        var parole = new List<PdfWord> { Parola(450, 12, riferimento), Parola(445, 24, "AIRAC effective date 01 OCT 2026") };
        foreach (var (x0, y, t) in righe)
        {
            var x = x0;
            foreach (var p in t.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                parole.Add(Parola(x, y, p));
                x += p.Length * 4.5 + 3;
            }
        }
        return new PdfPageText(numero, 595, 842, parole, []);
    }

    private static ImportResult Note() => ItalianEnr2Fir.Read(
    [
        Pagina(1, "ENR 2.1.1.1 - 1",
            (52, 300, "NOTE"), (320, 300, "REMARKS"),
            (52, 312, "3. Milano ACC/FIC:"), (320, 312, "3. Milano ACC/FIC:"),
            (60, 324, "▪ a nord-ovest della linea congiungente i punti"),
            (60, 336, "461715N 0100029E – LUSIL - LEVSI nello"),
            (60, 348, "spazio aereo compreso tra SFC e FL 195 (incluso)."),
            (60, 360, "▪ a nord-ovest della linea congiungente i punti"),
            (60, 372, "461715N 0100029E - LUSIL nello spazio aereo"),
            (60, 384, "compreso tra FL195 (escluso) e UNL."),
            (52, 396, "4. Padova ACC/FIC:"),
            (60, 408, "▪ ad est della linea congiungente i punti"),
            (60, 420, "433732N - 0121235E - DIPVO;"),
            (60, 432, "▪ a nord della linea che non si legge."),
            (52, 444, "5. Con esclusione delle aree P/R/D quando attive.")),
        Pagina(2, "ENR 2.1.1.1 - 2",
            (52, 60, "ENR 2.1.1.1.2 MILANO ACC AREA"),
            (52, 72, "6. Roma ACC/FIC:"), (60, 84, "▪ a sud della linea congiungente i punti 433732N 0121235E - DIPVO.")),
    ]);

    [Fact]
    public void LeLineeFraGliAcc_ConLatoQuotePunti()
    {
        var linee = Note().UnitLines;

        Assert.Equal([("Milano ACC/FIC", "nord-ovest"), ("Milano ACC/FIC", "nord-ovest"), ("Padova ACC/FIC", "est")], linee.Select(l => (l.Unit, l.Side)));
        Assert.Equal(("SFC", VerticalReference.Ground, "FL 195 (incluso)", 195.0), (linee[0].Lower!.Text, linee[0].Lower!.Reference, linee[0].Upper!.Text, linee[0].Upper!.Value));
        Assert.Equal(("FL195 (escluso)", VerticalReference.Unlimited), (linee[1].Lower!.Text, linee[1].Upper!.Reference));
        Assert.Equal([null, "LUSIL", "LEVSI"], linee[0].Points.Select(p => p.Designator));
        Assert.Equal(46 + 17 / 60.0 + 15 / 3600.0, linee[0].Points[0].Position!.Value.Lat, 6);
        // «433732N - 0121235E»: il trattino dentro la coordinata.
        Assert.Equal(12 + 12 / 60.0 + 35 / 3600.0, linee[2].Points[0].Position!.Value.Lon, 6);
        Assert.Equal("DIPVO", linee[2].Points[1].Designator);
        Assert.Null(linee[2].Lower);
        Assert.All(linee, l => Assert.Equal(("ENR 2.1.1.1", "ENR 2.1.1.1-1"), (l.Section, l.Page?.Reference)));
    }

    [Fact]
    public void UnaFraseCheNonSiLeggeSiDice_DopoIlTitoloNoteNonPiu()
    {
        var letto = Note();

        var problema = Assert.Single(letto.Issues);
        Assert.Equal((ImportIssueKind.UnitLine, "Padova ACC/FIC"), (problema.Kind, problema.Key));
        Assert.Contains("a nord della linea che non si legge", problema.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(letto.UnitLines, l => l.Unit == "Roma ACC/FIC");
    }

    [Fact]
    public void LAreaVfrDellaFir()
    {
        var letto = ItalianEnr2Fir.Read(
        [
            Pagina(15, "ENR 2.1.1.2 - 15",
                (52, 52, "a. Roma FIR VFR “Isola di Ponza”"), (321, 52, "a. Roma FIR VFR “Isola di Ponza”"),
                (183, 79, "NOME E LIMITI LATERALI"), (397, 79, "LIMITI VERTICALI"),
                (231, 99, "1"), (431, 99, "2"),
                (144, 117, "Area circolare centrata su/circular area centred on"), (414, 117, "1500ft AGL"),
                (152, 126, "Ponza VORTAC 'PNZ' 40º54'43\"N 012º57'27\"E"), (427, 126, "SFC"),
                (167, 134, "con un raggio di/with a radius of 2NM"),
                (52, 152, "b. All'interno della FIR di Roma è attiva la procedura TIBA")),
        ]);

        Assert.Empty(letto.Issues);
        var area = Assert.Single(letto.Airspaces);
        Assert.Equal(("Isola di Ponza", AirspaceType.Other, "ENR 2.1", "Roma FIR VFR"), (area.Designator, area.Type, area.Section, area.Remarks));
        Assert.Equal(("1500ft AGL", "SFC"), (area.Upper.Text, area.Lower.Text));
        var cerchio = Assert.IsType<CircleGeometry>(area.Geometry);
        Assert.Equal(new Distance(2, DistanceUnit.Nm), cerchio.Radius);
        Assert.Equal(40 + 54 / 60.0 + 43 / 3600.0, cerchio.Center.Lat, 6);
    }
}
