using Aip.Pdf.Italia;

namespace Aip.Pdf.Tests;

/// <summary>
/// ENR 5.4, gli ostacoli (carta «L'AIP intero», passo 4), su pagine girate costruite qui: la testata dritta della pagina e,
/// nella pagina girata, la regione, la riga dei numeri delle colonne, i bordi verticali, le righe degli ostacoli e la
/// tabella delle note. Colonne larghe 50 punti da x 50; dati inventati.
/// </summary>
public sealed class Enr54Tests
{
    private static PdfWord Parola(double x, double y, string testo) => new(testo, x, x + testo.Length * 4.5, y - 7, y + 1, y);

    private static IEnumerable<PdfWord> Riga(double x, double y, string testo)
    {
        foreach (var p in testo.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            yield return Parola(x, y, p);
            x += p.Length * 4.5 + 3;
        }
    }

    /// <summary>Una pagina con la testata dritta e, girata, il testo (colonna da 1, y, testo; colonna 0 = x 30, fuori tabella).</summary>
    private static PdfPageText Pagina(int numero, string riferimento, int colonne, params (int Colonna, double Y, string Testo)[] testo) =>
        Pagina(numero, riferimento, colonne, numeri: true, testo);

    private static PdfPageText Pagina(int numero, string riferimento, int colonne, bool numeri, params (int Colonna, double Y, string Testo)[] testo)
    {
        var parole = new List<PdfWord>();
        if (numeri) parole.AddRange(Riga(50, 140, string.Join(' ', Enumerable.Range(1, colonne))).Select((w, i) => w with { X0 = 50 + i * 50 + 22, X1 = 50 + i * 50 + 28 }));
        foreach (var (c, y, t) in testo)
            parole.AddRange(Riga(c == 0 ? 30 : 50 + (c - 1) * 50 + 2, y, t));
        var bordi = Enumerable.Range(0, colonne + 1).Select(i => new PdfRule(50 + i * 50, 130, 580)).ToList();
        var girata = new PdfPageText(numero, 842, 595, parole, bordi, []);
        var dritte = new List<PdfWord> { Parola(500, 12, riferimento), Parola(50, 24, "AIP"), Parola(70, 24, "-"), Parola(80, 24, "Italia"),
            Parola(500, 24, "27"), Parola(512, 24, "JUL"), Parola(530, 24, "2023") };
        return new PdfPageText(numero, 595, 842, dritte, []) { Turned = girata };
    }

    [Fact]
    public void Puntuali_UnaRigaPerSigla_IlTipoInDueLingue_LaSezioneDallaTestata()
    {
        var pagina = Pagina(1, "ENR 5.4.1.13 - 1", 13,
            (0, 60, "REGIONE/REGION: PUGLIA"),
            (1, 160, "BRINDISI"), (2, 160, "SAN VITO DEI"), (3, 160, "NIL"), (4, 160, "AEROGENERATORE"), (5, 160, "T003169"),
            (6, 160, "40°37'59''N"), (7, 160, "017°43'19''E"), (8, 160, "118.2"), (9, 160, "410"), (10, 160, "224"), (11, 160, "735"),
            (12, 160, "NO"), (13, 160, "NO"),
            (2, 172, "NORMANNI"), (4, 172, "WIND MILL"),
            (1, 187, "FOGGIA"), (2, 187, "ASCOLI"), (3, 187, "FONTANELLE"), (4, 187, "ANEMOMETRO"), (5, 187, "T004963"),
            (6, 187, "41°13'58''N"), (7, 187, "015°39'48''E"), (8, 187, "100"), (10, 187, "321"), (11, 187, "1053"), (12, 187, "Note RMKs"), (13, 187, "NO"),
            (4, 199, "ANEMOMETER"));

        var letto = ItalianEnr54.Read([pagina], ObstacleKind.Point);

        Assert.Empty(letto.Issues);
        Assert.Equal(2, letto.Obstacles.Count);
        var a = letto.Obstacles[0];
        Assert.Equal(("ENR 5.4.1.13", "T003169", "PUGLIA", "SAN VITO DEI NORMANNI", (string?)null), (a.Section, a.Designator, a.Region, a.Town, a.Site));
        Assert.Equal(("AEROGENERATORE", "WIND MILL"), (a.Type, a.TypeEnglish));
        Assert.Equal((118.2, 410d, 224d, 735d), (a.HeightM, a.HeightFt, a.ElevationM, a.ElevationFt));
        Assert.Equal(40 + 37 / 60d + 59 / 3600d, a.Position.Lat, 6);
        Assert.Equal(new AipPage("ENR 5.4.1.13-1", new DateOnly(2023, 7, 27)), a.Page);
        // La cella vuota resta vuota; la segnalazione com'è stampata.
        var b = letto.Obstacles[1];
        Assert.Equal(((double?)100, (double?)null, "Note RMKs", "NO"), (b.HeightM, b.HeightFt, b.DayMarking, b.NightMarking));
    }

    [Fact]
    public void Lineari_LaFine_ILimiti_LeNoteInFondoAllaPagina()
    {
        var pagina = Pagina(1, "ENR 5.4.3.1 - 1", 18,
            (0, 60, "REGIONE/REGION: ABRUZZO"),
            (1, 160, "CHIETI"), (2, 160, "FARA FILIORUM-"), (3, 160, "GISSI"), (4, 160, "LINEA ELETTRICA"), (5, 160, "L003543"),
            (6, 160, "42°02'57''N"), (7, 160, "014°34'21''E"), (8, 160, "42°03'10''N"), (9, 160, "014°34'05''E"), (10, 160, "532"),
            (11, 160, "181"), (12, 160, "594"), (13, 160, "226"), (14, 160, "741"), (15, 160, "55"), (16, 160, "180"), (17, 160, "Note"), (18, 160, "NO"),
            (2, 172, "CASACANDITELLA"), (4, 172, "POWER LINE"), (17, 172, "RMKs"),
            (0, 300, "ID NOTE/REMARKS"),
            (0, 315, "L003543 Ostacolo lineare sul fiume Alento/Linear obstacle"),
            (0, 327, "on the Alento river"));

        var letto = ItalianEnr54.Read([pagina], ObstacleKind.Line);

        Assert.Empty(letto.Issues);
        var o = Assert.Single(letto.Obstacles);
        Assert.Equal("FARA FILIORUM-CASACANDITELLA", o.Town);
        Assert.Equal(42 + 3 / 60d + 10 / 3600d, o.End!.Value.Lat, 6);
        Assert.Equal((532d, 181d, 594d, 226d, 741d, 55d, 180d), (o.LengthM, o.LowestM, o.LowestFt, o.HighestM, o.HighestFt, o.HeightM, o.HeightFt));
        Assert.Equal("Note RMKs", o.DayMarking);
        Assert.Equal("Ostacolo lineare sul fiume Alento/Linear obstacle on the Alento river", o.Remarks);
    }

    [Fact]
    public void Piattaforme_IlNome_NilVuoto_UnaNotaSenzaOstacoloSiDice()
    {
        var pagina = Pagina(2, "ENR 5.4.2 - 2", 12,
            (1, 160, "ANCONA"), (2, 160, "ANCONA"), (3, 160, "MARE ADRIATICO"), (4, 160, "PIATTAFORMA"), (5, 160, "P000001"),
            (6, 160, "43°53'56''N"), (7, 160, "014°09'15''E"), (8, 160, "52"), (9, 160, "171"), (10, 160, "YES"), (11, 160, "YES"), (12, 160, "BARBARA T2"),
            (4, 172, "PLATFORM"),
            (1, 187, "ANCONA"), (2, 187, "ANCONA"), (3, 187, "MARE ADRIATICO"), (4, 187, "PIATTAFORMA"), (5, 187, "P000002"),
            (6, 187, "44°05'24''N"), (7, 187, "013°44'26''E"), (8, 187, "105"), (9, 187, "344"), (10, 187, "NO"), (11, 187, "NO"), (12, 187, "NIL"),
            (4, 199, "PLATFORM"),
            (0, 300, "ID NOTE/REMARKS"),
            (0, 315, "P000099 Nota di nessuno"));

        var letto = ItalianEnr54.Read([pagina], ObstacleKind.Platform);

        Assert.Equal(("ENR 5.4.2", "BARBARA T2", (string?)null), (letto.Obstacles[0].Section, letto.Obstacles[0].Name, letto.Obstacles[0].Region));
        Assert.Null(letto.Obstacles[1].Name);
        Assert.Equal((52d, 171d), (letto.Obstacles[0].ElevationM, letto.Obstacles[0].ElevationFt));
        var i = Assert.Single(letto.Issues);
        Assert.Equal((ImportIssueKind.Obstacle, "P000099", "nota senza ostacolo: Nota di nessuno"), (i.Kind, i.Key, i.Detail));
    }

    [Fact]
    public void UnaPaginaSenzaTesto_ETabellaDelleNoteNil()
    {
        var bianca = new PdfPageText(1, 595, 842, [Parola(500, 12, "ENR 5.4.2 - 1")], []);
        var nil = Pagina(2, "ENR 5.4.1.1 - 2", 13, numeri: false, (0, 60, "ID NOTE/REMARKS"), (0, 75, "NIL"));

        var letto = ItalianEnr54.Read([bianca, nil], ObstacleKind.Point);

        Assert.Empty(letto.Obstacles);
        Assert.Equal([], letto.Issues.Where(p => p.Kind != ImportIssueKind.PageWithoutDate).Select(p => $"{p.Kind} {p.Detail}"));
    }

    [Fact]
    public void IlConto_LeSigleFinoAlleNote()
    {
        var pagina = Pagina(1, "ENR 5.4.1.6 - 1", 13,
            (5, 160, "T001672"), (5, 187, "T001673"),
            (0, 300, "ID NOTE/REMARKS"),
            (0, 315, "T001672 Stesso ostacolo dell'ID: T001673"));

        Assert.Equal([new ExpectedCount("ostacoli", 2)], ItalianExpectedCounts.Of("LI-ENR 5.4.1 6 FRIULI.pdf", [pagina]));
    }
}
