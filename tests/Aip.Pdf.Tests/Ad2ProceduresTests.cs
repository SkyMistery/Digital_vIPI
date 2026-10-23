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

    // Passo 6 della carta «L'AIP intero»: i designatori col genere davanti (LIRP, LIEE), con le lettere spezzate (LIEE), con un
    // rimando fra parentesi che non fa una sezione (LIRV), a 21 punti dalla riga «Terminator» (LICC); e il conto del rapporto.
    [Fact]
    public void IDesignatoriInAltreForme_EIlConto()
    {
        var pagina = new Pagina([], [],
        [
            .. Testa("AD 2 LIXX 4 - 3"),
            (60, 60, "STAR RNAV (GNSS) RWY 03R/L"),
            (60, 80, "STAR MIVKI 1D"),
            .. Intestazione(110),
            .. Tratto(130, "IF", "MIVKI", null, null, "-", "+FL120"),
            (60, 160, "STAR K OVAS 1V"),
            .. Intestazione(190),
            .. Tratto(210, "IF", "KOVAS", null, null, "-", "+FL100"),
            (60, 240, "STAR UPADU 1A (ATC Discretion)"),
            .. Intestazione(270),
            .. Tratto(290, "IF", "UPADU", null, null, "-", "+FL110"),
            (60, 319, "STAR LIBRO 1H"),
            .. Intestazione(340),
            .. Tratto(360, "IF", "LIBRO", null, null, "-", "+FL110"),
        ]);
        var pagine = PdfText.Read(Enr4Tests.Pdf(pagina));

        var r = ItalianAd2Procedures.Read(pagine, "LIXX", ProcedureKind.Star);

        Assert.Equal([], r.Issues.Where(i => i.Kind != ImportIssueKind.PageWithoutDate).Select(i => $"{i.Kind} {i.Key} {i.Detail}"));
        Assert.Equal(["MIVKI 1D", "KOVAS 1V", "UPADU 1A", "LIBRO 1H"], r.Procedures.Select(p => p.Designator));
        Assert.All(r.Procedures, p => Assert.Equal((ProcedureKind.Star, 1), (p.Kind, p.Legs.Count)));
        Assert.All(r.Procedures, p => Assert.Equal(["03R", "03L"], p.Runways));
        Assert.Equal([new ExpectedCount("procedure", 4)], ItalianExpectedCounts.Of("LI-AD 2 LIXX - PROVA 4.pdf", pagine));
        Assert.Equal(4, ItalianExpectedCounts.Read("procedure", r));
    }

    // Passo 6: le piste in un titolo a sé (LIEA, LIEE), la descrizione che comincia con «Initial climb procedure completed» (non è
    // un'altra salita iniziale), il designatore con le piste e ripetuto sotto (LIRP), il designatore nel titolo (LIDT).
    [Fact]
    public void LeSid_PisteNelTitolo_DescrizioneDopoLaSalita_DesignatoreRipetuto()
    {
        var uno = new Pagina([], [],
        [
            .. Testa("AD 2 LIXX 6 - 3"),
            (60, 60, "DEPARTURES FROM RWY 02 – XXA VOR/DME"),
            (60, 75, "INITIAL CLIMB PROCEDURE"),
            (60, 85, "After take-off intercept and follow RDL 023 XXA VOR."),
            (60, 105, "SID DESCRIPTION"),
            (60, 120, "MINKA 7K"),
            (60, 130, "Initial climb procedure completed turn left until joining RDL 349 XXA VOR."),
            (60, 145, "MCA/MCL: RDL 349/4NM XXA VOR/DME 5000 FT."),
            (60, 175, "SID BEROK 6B RWY 03R/L"),
            (60, 190, "BEROK 6B"),
            .. Intestazione(220),
            .. Tratto(240, "IF", "RP601", null, null, "-", "+FL70"),
            (60, 270, "Prova RNP AVCOL 5A Departure"),
            .. Intestazione(300),
            .. Tratto(320, "IF", "AVCOL", null, null, "-", "+8000"),
        ]);

        var r = ItalianAd2Procedures.Read(PdfText.Read(Enr4Tests.Pdf(uno)), "LIXX", ProcedureKind.Sid);

        Assert.Equal([], r.Issues.Where(i => i.Kind != ImportIssueKind.PageWithoutDate).Select(i => $"{i.Kind} {i.Key} {i.Detail}"));
        Assert.Equal(["RWY 02", "MINKA 7K", "BEROK 6B", "AVCOL 5A"], r.Procedures.Select(p => p.Designator));
        var salita = r.Procedures[0];
        Assert.Equal((ProcedureKind.InitialClimb, "After take-off intercept and follow RDL 023 XXA VOR."), (salita.Kind, salita.Description));
        var minka = r.Procedures[1];
        Assert.Equal((ProcedureKind.Sid, "02"), (minka.Kind, Assert.Single(minka.Runways)));
        Assert.StartsWith("Initial climb procedure completed", minka.Description, StringComparison.Ordinal);
        Assert.Equal(["03R", "03L"], r.Procedures[2].Runways);
        Assert.Equal("RP601", Assert.Single(r.Procedures[2].Legs).Fix);
        Assert.Equal("AVCOL", Assert.Single(r.Procedures[3].Legs).Fix);
    }

    // Passo 6: gli scali militari stampano le procedure in due colonne, l'italiano a sinistra e l'inglese a destra (LIBF, LICC).
    [Fact]
    public void UnaPaginaInDueLingue_SiTieneLInglese()
    {
        var pagina = new Pagina([], [],
        [
            .. Testa("AD 2 LIXX 4 - 3"),
            (60, 60, "EKTOL 1A (a discrezione ATC)"), (320, 60, "EKTOL 1A (ATC Discretion)"),
            (60, 75, "EKTOL – RDL 216 XXG VOR (TR 036°) per XXG"), (320, 75, "EKTOL – RDL 216 XXG VOR (TR 036°) bound to XXG"),
            (60, 85, "VOR/DME."), (320, 85, "VOR/DME."),
            (320, 100, "MEA: EKTOL – XXG VOR/DME: 6000 FT."),
            // Una pagina con meno di 30 parole è un'immagine: la seconda procedura la riempie.
            (60, 130, "DOGUS 1A"), (320, 130, "DOGUS 1A"),
            (60, 145, "DOGUS – RDL 159 XXG VOR (TR 339°) per XXG"), (320, 145, "DOGUS – RDL 159 XXG VOR (TR 339°) bound to XXG"),
            (320, 160, "MEA: DOGUS – XXG VOR/DME: 3000 FT."),
        ]);
        var pagine = PdfText.Read(Enr4Tests.Pdf(pagina));

        var r = ItalianAd2Procedures.Read(pagine, "LIXX", ProcedureKind.Star);

        Assert.Equal(["EKTOL 1A", "DOGUS 1A"], r.Procedures.Select(p => p.Designator));
        var ektol = r.Procedures[0];
        Assert.Equal(("EKTOL 1A", "(ATC Discretion)\nEKTOL – RDL 216 XXG VOR (TR 036º) bound to XXG\nVOR/DME."), (ektol.Designator, ektol.Description));
        Assert.Equal("MEA: EKTOL – XXG VOR/DME: 6000 FT.", ektol.Minimums);
        Assert.True(ItalianAd2Procedures.EProcedure(pagine));
    }

    // Passo 6: la tabella delle attese (colonne diverse da scalo a scalo, il punto anche sopra la riga, una nota scritta dentro la
    // riga), la lista dei punti nel formato ARINC 424, un eliporto (AD 3).
    [Fact]
    public void LeAtteseDelleTabelle_LaListaArinc_UnEliporto()
    {
        double[] x = [60, 110, 165, 230, 290, 330, 375, 425, 455, 490];
        var pagina = new Pagina([], [],
        [
            .. Testa("AD 3 LIXH 4 - 3"),
            (60, 60, "HOLDING RWY 03"),
            (x[0], 81, "Path"), (x[1], 81, "Waypoint"), (x[2], 81, "Inbound"), (x[3], 81, "Leg"), (x[4], 81, "Turn"), (x[5], 81, "Altitude"),
            (x[6], 81, "Speed"), (x[7], 81, "D"), (x[8], 81, "WD"), (x[9], 81, "TYPE"),
            (x[0], 90, "Terminator"), (x[1], 90, "Identifier"), (x[2], 90, "Track"), (x[3], 90, "Distance"), (x[4], 90, "Direction"),
            (x[5], 90, "Constraint"), (x[6], 90, "Limit"),
            (x[0], 110, "HM"), (x[1], 110, "JESSY"), (x[2], 110, "033°(036.6°)"), (x[3], 110, "1"), (x[4], 110, "R"), (x[5], 110, "+4000"),
            (x[6], 110, "210"), (x[7], 110, "3.8"), (x[8], 110, "5.0"), (x[9], 110, "RNAV1"),
            // Il punto sopra la riga, la rotta vera sotto, una nota scritta nella colonna del tratto, il rimando dopo la quota.
            (x[1], 131, "ESCOB"), (x[0], 135, "HM"), (x[2], 135, "145°"), (x[2], 141, "(148.0°)"), (x[3] - 15, 132, "in case of coding"),
            (x[3], 135, "3.0"), (x[4], 135, "L"), (x[5], 135, "FL105"), (x[5] + 22, 135, "(4)"), (x[6], 135, "-200"),
            (60, 170, "Waypoint Table Formatted according ARINC 424 standards"),
            (60, 180, "WAYPOINT LATITUDE LONGITUDE"),
            (60, 190, "MIVKI N44132743 E010041382"),
            (60, 200, "ELB VOR N42435047 E010234461"),
            (60, 210, "RA503 N415153.56 E0123258.45"),
        ]);

        var r = ItalianAip.ReadFile("LI-AD 3 LIXH 4.pdf", PdfText.Read(Enr4Tests.Pdf(pagina)));

        Assert.Equal([], r.Issues.Where(i => i.Kind != ImportIssueKind.PageWithoutDate).Select(i => $"{i.Kind} {i.Key} {i.Detail}"));
        Assert.Equal(2, r.Holdings.Count);
        var jessy = r.Holdings[0];
        Assert.Equal(("AD 3 LIXH", "JESSY", 33, "036.6", TurnDirection.Right), (jessy.Section, jessy.Fix, jessy.InboundTrack, jessy.TrueTrack, jessy.Turn));
        Assert.Equal(("+4000", 4000d, "1", "210", "RNAV1"), (jessy.Minimum.Text, jessy.Minimum.Value!.Value, jessy.Leg, jessy.Speed, jessy.Navigation));
        var escob = r.Holdings[1];
        Assert.Equal(("ESCOB", 145, "148.0", TurnDirection.Left), (escob.Fix, escob.InboundTrack, escob.TrueTrack, escob.Turn));
        Assert.Equal(("FL105", VerticalReference.FlightLevel, "3.0", "-200", "(4)"), (escob.Minimum.Text, escob.Minimum.Reference, escob.Leg, escob.Speed, escob.NoteRefs));
        Assert.Contains("coding", escob.Text, StringComparison.Ordinal);
        Assert.Equal(["MIVKI", "RA503"], r.Points.Select(p => p.Designator));
        Assert.All(r.Points, p => Assert.Equal("AD 3 LIXH", p.Section));
        Assert.Equal(44 + 13 / 60.0 + 27.43 / 3600, r.Points[0].Position.Lat, 9);
        Assert.Equal(12 + 32 / 60.0 + 58.45 / 3600, r.Points[1].Position.Lon, 9);
    }

    // Passo 6: l'intestazione dell'attesa di LIRN ha «Waypoint» due volte (Identifier e Distance): il punto è sotto la prima, e
    // una quota («FL100») non è mai il punto, anche se è più vicina alla seconda.
    [Fact]
    public void LAttesaConDueWaypoint_IlPuntoSottoLaPrima()
    {
        double[] x = [60, 110, 165, 230, 270, 300, 340, 380, 420, 460];
        var pagina = new Pagina([], [],
        [
            .. Testa("AD 2 LIXX 4 - 5"),
            (60, 60, "WAYPOINT HOLDING"),
            (x[2], 72, "Inbound"), (x[3], 72, "Leg"), (x[5], 72, "Minimum"),
            (x[0], 81, "Path"), (x[1], 81, "Waypoint"), (x[8], 82, "Waypoint"), (x[5], 81, "Turn"), (x[6], 81, "Altitude"), (x[7], 81, "Speed"),
            (x[9], 81, "Navigation"),
            (x[0], 90, "Terminator"), (x[1], 90, "Identifier"), (x[2], 90, "Course"), (x[3], 90, "Distance"), (x[4], 90, "Distance"),
            (x[5], 90, "Direction"), (x[6], 90, "(FT)"), (x[7], 90, "Limit"), (x[8], 90, "Variation"), (x[9], 90, "Specification"),
            (x[0], 110, "HM"), (x[1], 110, "ALDAF"), (x[2], 104, "(059°)"), (x[2], 116, "055°"), (x[3], 110, "4.4"), (x[4], 110, "1/-"),
            (x[5], 110, "L"), (x[6], 110, "FL100"), (x[7], 110, "200"), (x[8], 110, "3.6"), (x[9], 110, "RNAV"),
            (60, 140, "(1) Nota dell'attesa, perché la pagina abbia le sue trenta parole come una vera."),
        ]);

        var r = ItalianAd2Procedures.Read(PdfText.Read(Enr4Tests.Pdf(pagina)), "LIXX", ProcedureKind.Star);

        Assert.Equal([], r.Issues.Where(i => i.Kind != ImportIssueKind.PageWithoutDate).Select(i => $"{i.Kind} {i.Key} {i.Detail}"));
        var aldaf = Assert.Single(r.Holdings);
        Assert.Equal(("ALDAF", 55, "059", "FL100", TurnDirection.Left), (aldaf.Fix, aldaf.InboundTrack, aldaf.TrueTrack, aldaf.Minimum.Text, aldaf.Turn));
    }

    [Theory]
    [InlineData("SID RNAV1 or RNP1 RWY24 - Description", "24")]
    [InlineData("STAR RWY 16 L/R", "16L,16R")]
    [InlineData("STAR RWY35L/R Milano Malpensa", "35L,35R")]
    [InlineData("SIDs VOR FOR CAT A, B, C & D RWY 10/28 DESCRIPTION", "10,28")]
    [InlineData("STAR CMP/TAQ RWY 07/25 DESCRIPTION", "07,25")]
    [InlineData("TRANSITION SEGMENTS TO ENROUTE RWY 17L/R DESCRIPTION", "17L,17R")]
    [InlineData("STAR VOR (ATC discretion) Bergamo / Orio al Serio", "")]
    // «RWY10 LMT»: la L di LMT non è la pista sinistra (LICA, trovato dal confronto della 7d).
    [InlineData("SID RWY10 LMT VOR DESCRIPTION", "10")]
    [InlineData("STAR LMT VOR RWY 28 description", "28")]
    public void LePisteDelTitolo(string titolo, string attese) =>
        Assert.Equal(attese.Length == 0 ? [] : attese.Split(','), ItalianAd2Procedures.Piste(titolo));

    // Segnalazioni 2611, N2 (revisione col committente): in fondo alle STAR convenzionali di LIPH la tabella dei punti è
    // intestata «Waypoint Latitude Longitude», senza «LIST»: PH001-PH006 non si leggevano, e le tre STAR si fermavano.
    [Fact]
    public void LaTabellaDeiPunti_IntestataWaypointLatitudeLongitude()
    {
        var pagina = new Pagina([], [],
        [
            .. Testa("AD 2 LIXX 4 - 4"),
            // In due lingue, come LIPH 4: il lettore tiene la colonna inglese, a destra; la tabella dei punti sta a sinistra.
            (60, 60, "ALESE 1A"), (330, 60, "ALESE 1A"),
            (60, 70, "ALESE – TR 160° (RDL 340 VIC VOR) – PH001 –"), (330, 70, "ALESE – TR 160° (RDL 340 VIC VOR) – PH001 –"),
            (60, 80, "PH002 – VIC VOR/DME."), (330, 80, "PH002 – VIC VOR/DME."),
            (60, 110, "BORMI 1A"), (330, 110, "BORMI 1A"),
            (60, 120, "BORMI – TR 134° (RDL 314 VIC VOR) – PH003 –"), (330, 120, "BORMI – TR 134° (RDL 314 VIC VOR) – PH003 –"),
            (60, 170, "Waypoint     Latitude     Longitude"),
            (60, 180, "PH001     N45523971     E011343924"),
            (60, 190, "PH002     N45430330     E011383667"),
        ]);

        var r = ItalianAip.ReadFile("LI-AD 2 LIXX - Prova 4.pdf", PdfText.Read(Enr4Tests.Pdf(pagina)));

        Assert.Equal(["PH001", "PH002"], r.Points.Select(p => p.Designator));
        Assert.Equal(45 + 52 / 60.0 + 39.71 / 3600, r.Points[0].Position.Lat, 9);
    }
}
