using Aip.Pdf.Italia;

namespace Aip.Pdf.Tests;

/// <summary>
/// ENR 2.1.1: le tabelle delle frequenze ACC/SCCAM/FIC (carta «L'AIP intero», passo 2b), su pagine costruite qui con le x
/// delle colonne di ENR 2.1.1.1 nel 2611 e i bordi orizzontali solo dove li ha il PDF vero: la stazione è una cella unita
/// su più righe, il bordo fra avvicinamento e area non la attraversa.
/// </summary>
public sealed class Enr2FrequenciesTests
{
    private static readonly double[] Colonne = [52, 125, 199, 273, 346, 420, 494, 568];

    private static PdfWord Parola(double x, double y, string testo) => new(testo, x, x + testo.Length * 4.5, y - 7, y + 1, y);

    private static PdfPageText Pagina(int numero, string riferimento, (double Y, double DaX)[] bordi, params (int Colonna, double Y, string Testo)[] celle)
    {
        var parole = new List<PdfWord> { Parola(450, 17, riferimento), Parola(50, 29, "AIP - Italia AIRAC effective date 01 OCT 2026") };
        void Testo(double x, double y, string t)
        {
            foreach (var p in t.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                parole.Add(Parola(x, y, p));
                x += p.Length * 4.5 + 3;
            }
        }
        // La testata: i titoli e la riga dei numeri.
        Testo(Colonne[0] + 3, 102, "STAZIONE");
        Testo(Colonne[2] + 3, 102, "CHIAMATA O");
        Testo(Colonne[4] + 3, 102, "FREQUENZA");
        for (var c = 0; c < 7; c++) Testo(Colonne[c] + 30, 150, (c + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
        foreach (var (colonna, y, t) in celle) Testo(Colonne[colonna] + 3, y, t);
        var verticali = Colonne.Select(x => new PdfRule(x, 90, 780)).ToList();
        var orizzontali = new[] { (90.0, Colonne[0]), (155.0, Colonne[0]) }.Concat(bordi)
            .Select(b => new PdfHorizontalRule(b.Item1, b.Item2, Colonne[^1])).ToList();
        return new PdfPageText(numero, 595, 842, parole, verticali, orizzontali);
    }

    private static ImportResult Tabella() => ItalianEnr2Frequencies.Read(
    [
        Pagina(3, "ENR 2.1.1.1 - 3", [(180, Colonne[0]), (260, Colonne[0]), (330, Colonne[1]), (780, Colonne[0])],
            (0, 168, "Milano ACC/FIC"), (1, 168, "Emergenza"), (3, 168, "A3E"), (4, 168, "121.500"), (5, 168, "H24"), (6, 168, "1. A discrezione"),
            (1, 176, "Emergency"),
            (0, 190, "Milano ACC"), (1, 190, "Servizio di"), (2, 190, "Milano Radar"), (3, 198, "A3E"), (4, 198, "126.750"), (5, 198, "H24"),
            (1, 199, "controllo di"), (1, 230, "avvicinamento/"), (3, 207, "A3E"), (4, 207, "CH 118.805"), (5, 207, "(1)"),
            (4, 216, "124.250 (7)"), (4, 224, "XYZ"),
            // Il servizio d'area: la stazione non si ripete (cella unita), il bordo a 260 parte dalla colonna del servizio.
            (1, 270, "Servizio di"), (1, 286, "area/"), (2, 270, "Padova Infor-"), (2, 278, "mazioni/"),
            (4, 280, "125.275"), (4, 290, "CH125.485"), (4, 298, "(1) (2)"), (5, 290, "0600-2200"), (5, 298, "(0500-2100)")),
        // La pagina dopo: la riga dell'area continua (niente stazione, servizio, nominativo).
        Pagina(4, "ENR 2.1.1.1 - 4", [(400, Colonne[0])],
            (3, 170, "A3E"), (4, 170, "CH 136.205"), (5, 170, "(1)"), (6, 170, "2. Note")),
    ]);

    [Fact]
    public void LeRigheDellaTabella()
    {
        var righe = Tabella().UnitFrequencies;

        Assert.Equal(
            [("Milano ACC/FIC", "Emergenza Emergency", null), ("Milano ACC", "Servizio di controllo di avvicinamento/", "Milano Radar"),
             ("Milano ACC", "Servizio di area/", "Padova Informazioni/")],
            righe.Select(r => (r.Station, r.Service, r.CallSign)));
        Assert.All(righe, r => Assert.Equal(("ENR 2.1.1.1", "ENR 2.1.1.1-3"), (r.Section, r.Page?.Reference)));
    }

    [Fact]
    public void LeFrequenze_ConEmissioneERimandi_EIRimandiSottoLaFrequenza()
    {
        var righe = Tabella().UnitFrequencies;

        Assert.Equal(("121.500", 121.5, "A3E"), (righe[0].Frequencies[0].Text, righe[0].Frequencies[0].Megahertz, righe[0].Frequencies[0].Emission));
        Assert.Equal(["126.750", "CH 118.805", "124.250"], righe[1].Frequencies.Select(f => f.Text));
        Assert.Equal([null, null, "(7)"], righe[1].Frequencies.Select(f => f.NoteRefs));
        Assert.Equal([125.275, 125.485, 136.205], righe[2].Frequencies.Select(f => f.Megahertz));
        Assert.Equal("(1)(2)", righe[2].Frequencies[1].NoteRefs);
    }

    [Fact]
    public void GliOrariELeNoteSonoDellaRiga_LaRigaContinuaNellaPaginaDopo()
    {
        var righe = Tabella().UnitFrequencies;

        Assert.Equal(("H24", "1. A discrezione"), (righe[0].Hours, righe[0].Remarks));
        Assert.Equal("H24\n(1)", righe[1].Hours);
        Assert.Equal(("0600-2200\n(0500-2100)\n(1)", "2. Note"), (righe[2].Hours, righe[2].Remarks));
    }

    [Fact]
    public void UnTestoCheNonEUnaFrequenzaSiDice()
    {
        var problema = Assert.Single(Tabella().Issues);

        Assert.Equal((ImportIssueKind.Frequency, "Milano ACC", 3, "XYZ"), (problema.Kind, problema.Key, problema.Page, problema.Detail));
    }
}
