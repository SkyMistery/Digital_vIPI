using Aip.Pdf.Italia;
using Pagina = Aip.Pdf.Tests.Enr4Tests.Pagina;

namespace Aip.Pdf.Tests;

/// <summary>
/// Carta F7, slice 7a: le procedure di AD 2 (i file 4 e 6 di uno scalo) su pagine costruite qui, con l'impaginazione dei PDF
/// veri: titolo di sezione, designatore, intestazione della tabella su due righe, un tratto per riga con la rotta magnetica
/// sopra e la vera sotto. Dati inventati.
/// </summary>
public sealed class Ad2ProceduresTests
{
    private static (double, double, string)[] Testa(string riferimento) =>
        [(450, 30, riferimento), (52, 45, "AIP - Italia AIRAC effective date 11 JUN 2026")];

    // Le x delle colonne, come nei PDF di LIRN: Path, Waypoint, Fly, Course, Magnetic, Distance, Turn, Altitude, Speed, Navigation.
    private static readonly double[] X = [60, 110, 165, 200, 245, 290, 330, 375, 425, 475];

    /// <summary>L'intestazione della tabella, con la riga «Terminator» a <paramref name="top"/>.</summary>
    private static (double, double, string)[] Intestazione(double top) =>
    [
        (X[0], top - 9, "Path"), (X[1], top - 9, "Waypoint"), (X[2], top - 9, "Fly"), (X[3], top - 9, "Course"), (X[4], top - 9, "Magnetic"),
        (X[5], top - 9, "Distance"), (X[6], top - 9, "Turn"), (X[7], top - 9, "Altitude"), (X[8], top - 14, "Speed"), (X[9], top - 9, "Navigation"),
        (X[0], top, "Terminator"), (X[1], top, "Identifier"), (X[2], top, "Over"), (X[3], top, "°M(°T)"), (X[4], top, "Variation"),
        (X[5], top, "(NM)"), (X[6], top, "Direction"), (X[7], top, "(ft)"), (X[8], top, "(kt)"), (X[9], top, "Specification"),
    ];

    /// <summary>Un tratto: le celle sulla riga a <paramref name="top"/>, la rotta 5 punti sopra e la vera 5 sotto.</summary>
    private static (double, double, string)[] Tratto(double top, string pt, string fix, string? rotta, string? vera, string distanza,
        string quota, string velocita = "-", string volo = "-", double ptPiuGiu = 0) =>
    [
        (X[0], top + ptPiuGiu, pt), (X[1], top, fix), (X[2], top, volo),
        .. rotta is null ? Array.Empty<(double, double, string)>() : [(X[3], top - 5, rotta), (X[3], top + 5, $"({vera})")],
        (X[4], top, "3.6°E"), (X[5], top, distanza), (X[6], top, "-"), (X[7], top, quota), (X[8], top, velocita), (X[9], top, "RNAV1"),
    ];

    private static ImportResult LeggiLeSid()
    {
        var uno = new Pagina([], [],
        [
            .. Testa("AD 2 LIXX 6 - 3"),
            (60, 70, "SID RNAV1 RWY 24 - Description"),
            (60, 90, "AGNIS 7G"),
            .. Intestazione(120),
            .. Tratto(140, "IF", "GEMMA", null, null, "-", "+6000"),
            .. Tratto(165, "TF", "SOR", "160°", "163.8", "8.0", "-"),
            // Il path terminator 5 punti sotto il resto della riga (LICA 6 p.3).
            .. Tratto(190, "DF", "XX501", null, null, "-", "+2150", "-210", ptPiuGiu: 5),
            .. Tratto(215, "TF", "AGNIS", "154°", "157.5", "50.2", "+FL110", volo: "Y"),
            (60, 240, "(1) Nota del rimando."),
            (60, 270, "WAYPOINT LIST"),
            (60, 280, "Waypoint Coordinates"),
            (60, 292, "XX501 40°51'28.8\"N 014°13'47.0\"E"),
        ]);
        var due = new Pagina([], [],
        [
            .. Testa("AD 2 LIXX 6 - 4"),
            (60, 70, "Initial climb procedure RWY 06"),
            (60, 90, "If cleared for ALAXI 7H:"),
            .. Intestazione(120),
            .. Tratto(140, "CF", "VUNHO", "055°", "058.2", "4.2", "+2000", "-210", volo: "Y"),
            // Un tratto RF: il centro sopra il punto, il raggio sotto (LIRF 6 p.31).
            (X[0], 160, "RF"), (X[0] + 12, 160, "centre:"),
            (X[0], 169, "XX620"), (X[1], 169, "XX604"), (X[5], 169, "2.5"), (X[6], 169, "R"), (X[9], 169, "RNP1"),
            (X[0], 178, "r=2.1NM"),
            // Una SID senza un titolo suo, subito dopo la salita iniziale.
            (60, 210, "ALAXI 7H"),
            .. Intestazione(240),
            .. Tratto(260, "TF", "ALAXI", "300°", "303.1", "12.0", "+FL100"),
            // La lista ripetuta, coi secondi a due decimali: lo stesso punto, più preciso.
            (60, 300, "Waypoint"), (60, 310, "XX501 40°51'28.81\"N 014°13'47.02\"E"),
            (60, 320, "XX620 40°50'15.7332\"N – 013°18'07.6647\"E"),
        ]);
        return ItalianAd2Procedures.Read(PdfText.Read(Enr4Tests.Pdf(uno, due)), "LIXX", ProcedureKind.Sid);
    }

    [Fact]
    public void LaTabellaDeiTratti_ColonnaPerColonna()
    {
        var r = LeggiLeSid();

        Assert.Empty(r.Issues);
        var sid = r.Procedures[0];
        Assert.Equal((ProcedureKind.Sid, "AGNIS 7G", "AD 2 LIXX 6-3"), (sid.Kind, sid.Designator, sid.Page?.Reference));
        Assert.Equal(["24"], sid.Runways);
        Assert.Equal("SID RNAV1 RWY 24 - Description", sid.Section);
        Assert.Equal(["IF GEMMA", "TF SOR", "DF XX501", "TF AGNIS"], sid.Legs.Select(t => $"{t.PathTerminator} {t.Fix}"));
        var sor = sid.Legs[1];
        Assert.Equal(("160º", "163.8", "3.6ºE", 8.0), (sor.Course, sor.TrueCourse, sor.MagneticVariation, sor.DistanceNm!.Value));
        Assert.Null(sor.Turn);
        Assert.Null(sor.Altitude);
        Assert.Equal(("+2150", "-210"), (sid.Legs[2].Altitude, sid.Legs[2].Speed));
        Assert.True(sid.Legs[3].FlyOver);
        Assert.Equal("+FL110", sid.Legs[3].Altitude);
        Assert.Equal("(1) Nota del rimando.", sid.Remarks);
    }

    [Fact]
    public void LaSalitaIniziale_GliArchiRF_ELaSidSenzaTitolo()
    {
        var r = LeggiLeSid();

        var salita = r.Procedures[1];
        Assert.Equal((ProcedureKind.InitialClimb, "If cleared for ALAXI 7H"), (salita.Kind, salita.Designator));
        Assert.Equal(["06"], salita.Runways);
        var arco = salita.Legs[1];
        Assert.Equal(("RF", "XX604", "XX620", 2.1, "R"), (arco.PathTerminator, arco.Fix, arco.ArcCenter, arco.ArcRadiusNm!.Value, arco.Turn));
        var alaxi = r.Procedures[2];
        Assert.Equal((ProcedureKind.Sid, "ALAXI 7H"), (alaxi.Kind, alaxi.Designator));
        Assert.Equal("ALAXI", Assert.Single(alaxi.Legs).Fix);
        Assert.Equal(3, r.Procedures.Count);
    }

    [Fact]
    public void IPuntiDelloScalo_UnaVoltaSola_LaScritturaPiuPrecisa()
    {
        var r = LeggiLeSid();

        Assert.Equal(["XX501", "XX620"], r.Points.Select(p => p.Designator));
        Assert.All(r.Points, p => Assert.Equal("AD 2 LIXX", p.Section));
        Assert.Equal(40 + 51 / 60.0 + 28.81 / 3600, r.Points[0].Position.Lat, 9);
        Assert.Equal(13 + 18 / 60.0 + 7.6647 / 3600, r.Points[1].Position.Lon, 9);
    }

    [Fact]
    public void UnaStarInProsa_ELaTabellaDelleAtteseNo()
    {
        var pagina = new Pagina([], [],
        [
            .. Testa("AD 2 LIXX 4 - 9"),
            (60, 70, "STA R RWY 34 L/R"),
            (60, 80, "ATC DISCRETION DESCRIPTION"),
            (60, 100, "ELKAP 3R"),
            (60, 110, "Proceed on TR 119° (RDL 299 TAQ VOR) bound to TAQ VOR, then"),
            (60, 120, "turn right on TR170° bound to RATIR"),
            (60, 135, "MEL/MEA: ELKAP – BIBEK: FL190"),
            (60, 160, "ALA XI 1C"),
            .. Intestazione(190),
            .. Tratto(210, "IF", "ALAXI", null, null, "-", "-FL170"),
            (60, 250, "Holding RNAV"),
            .. Intestazione(280),
            .. Tratto(300, "HM", "OLEVA", "051°", "055.0", "3.0", "4000"),
        ]);

        var r = ItalianAd2Procedures.Read(PdfText.Read(Enr4Tests.Pdf(pagina)), "LIXX", ProcedureKind.Star);

        Assert.Empty(r.Issues);
        Assert.Equal(["ELKAP 3R", "ALAXI 1C"], r.Procedures.Select(p => p.Designator));
        var elkap = r.Procedures[0];
        Assert.Equal((ProcedureKind.Star, 0), (elkap.Kind, elkap.Legs.Count));
        Assert.Equal(["34L", "34R"], elkap.Runways);
        Assert.Equal("Proceed on TR 119º (RDL 299 TAQ VOR) bound to TAQ VOR, then\nturn right on TR170º bound to RATIR", elkap.Description);
        Assert.StartsWith("MEL/MEA: ELKAP", elkap.Minimums, StringComparison.Ordinal);
        // L'attesa (HM) non entra nella STAR di sopra.
        Assert.Equal(["IF ALAXI"], r.Procedures[1].Legs.Select(t => $"{t.PathTerminator} {t.Fix}"));
    }

    [Theory]
    [InlineData("SID RNAV1 or RNP1 RWY24 - Description", "24")]
    [InlineData("STAR RWY 16 L/R", "16L,16R")]
    [InlineData("STAR RWY35L/R Milano Malpensa", "35L,35R")]
    [InlineData("SIDs VOR FOR CAT A, B, C & D RWY 10/28 DESCRIPTION", "10,28")]
    [InlineData("STAR CMP/TAQ RWY 07/25 DESCRIPTION", "07,25")]
    [InlineData("TRANSITION SEGMENTS TO ENROUTE RWY 17L/R DESCRIPTION", "17L,17R")]
    [InlineData("STAR VOR (ATC discretion) Bergamo / Orio al Serio", "")]
    public void LePisteDelTitolo(string titolo, string attese) =>
        Assert.Equal(attese.Length == 0 ? [] : attese.Split(','), ItalianAd2Procedures.Piste(titolo));
}
