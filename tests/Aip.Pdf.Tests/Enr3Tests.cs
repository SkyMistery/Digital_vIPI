using Aip.Pdf.Italia;

namespace Aip.Pdf.Tests;

/// <summary>
/// ENR 3 (carta F6, slice 2): le rotte di ENR 3.2 e le attese di ENR 3.4.1, su pagine costruite qui con le parole e le
/// righe verticali al loro posto (come le dà <see cref="PdfText"/>). Non un PDF finto: il font standard di PdfPig non ha
/// ▲ ∆ ↑ ↓, che in queste tabelle sono dati. Le x delle colonne sono quelle dei PDF veri del 2609. Dati inventati.
/// </summary>
public sealed class Enr3Tests
{
    private static readonly double[] RotteDispari = [52, 170, 222, 269, 302, 340, 377, 566];
    private static readonly double[] AtteseDispari = [52, 107, 227, 272, 307, 347, 564];

    /// <summary>Una pagina: le righe verticali (da 46 a 780, a pezzi come nei PDF veri) e le parole (x, linea di base, testo).</summary>
    private static PdfPageText Pagina(int numero, double[] righe, string riferimento, string data, double dx, params (double X, double Y, string Testo)[] testo)
    {
        var regole = righe.SelectMany(x => new[] { new PdfRule(x + dx, 46, 132), new PdfRule(x + dx, 132, 780) }).ToList();
        var parole = new List<PdfWord>
        {
            Parola(509 + dx, 12, riferimento),
            Parola(50 + dx, 24, "AIP - Italia AIRAC effective date " + data),
        };
        foreach (var (x, y, t) in testo)
        {
            // Le parole come le dà PdfPig: una per gruppo di lettere, separate dagli spazi.
            var cx = x + dx;
            foreach (var p in t.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                parole.Add(Parola(cx, y, p));
                cx += p.Length * 4.5 + 3;
            }
        }
        return new PdfPageText(numero, 595, 842, parole, regole);
    }

    private static PdfWord Parola(double x, double y, string testo) => new(testo, x, x + testo.Length * 4.5, y - 7, y + 1, y);

    private static (double, double, string)[] TestataDelleRotte => [(108, 128, "1"), (193, 128, "2"), (243, 128, "3"), (283, 128, "4"), (337, 128, "5"), (469, 128, "6")];

    private static ImportResult Rotta()
    {
        var uno = Pagina(1, RotteDispari, "ENR 3.2 L99 - 1", "21 MAR 2024", 0,
        [
            .. TestataDelleRotte,
            (54, 146, "L99"), (54, 163, "(RNAV 5)"),
            (56, 182, "▲ UNIMI"), (224, 190, "For continuation"), (283, 190, "see AIP"), (312, 190, "AUSTRIA"),
            (67, 196, "46°51'39''N 011°03'55''E"),
            (67, 210, "Munich ACC/Padova ACC"),
            // Solo i livelli pari, al contrario (↑): da NAXAV verso UNIMI.
            (174, 227, "153° / 333°"), (235, 226, "FL195"), (362, 227, "↑"), (283, 233, "5"), (344, 238, "160"),
            (188, 239, "26.1"), (235, 241, "FL155"),
            (56, 258, "∆ NAXAV"),
            (67, 272, "46°27'50''N 011°19'20''E"),
            (67, 286, "CDR1"),
            (174, 302, "160° / 340°"), (235, 302, "FL195"), (324, 303, "↓"), (362, 303, "↑"), (382, 302, "Only by ATC"),
            (306, 306, "130"), (283, 308, "5"), (344, 313, "140"), (188, 314, "11.8"), (382, 316, "FL130-FL140 H24"),
            (235, 317, "FL125"),
            (56, 334, "▲ VICENZA VOR/DME"),
            (67, 348, "'VIC'"),
            (67, 362, "45°38'14''N 011°40'35''E"),
            // Il tratto da VIC continua nella pagina dopo, sopra il punto.
            (174, 380, "180° / 360°"), (235, 380, "FL195"), (324, 381, "↓"), (362, 381, "↑"),
        ]);
        var due = Pagina(2, RotteDispari, "ENR 3.2 L99 - 2", "11 JUL 2024", -20,
        [
            .. TestataDelleRotte,
            (283, 150, "5"), (308, 150, "90"), (346, 155, "80"), (188, 156, "29.0"), (234, 158, "5500ft"), (236, 170, "AMSL"),
            (56, 190, "▲ KOPER"),
            (67, 204, "45°09'14''N 011°38'29''E"),
            (56, 222, "\\ \\ \\ \\ \\ \\ \\ \\ \\ \\"), (200, 222, "\\ \\ \\ \\ \\ \\ \\ \\ \\ \\ \\ \\"), (400, 222, "\\ \\ \\ \\ \\ \\"),
            (56, 240, "▲ LUMAV"),
            (67, 254, "44°48'52''N 011°36'59''E"),
        ]);
        return ItalianEnr3.ReadRoutes([uno, due], "ENR 3.2");
    }

    [Fact]
    public void UnaRotta_IPuntiInOrdine_ColNomeSuDueRigheELaSigla()
    {
        var letto = Rotta();

        Assert.Empty(letto.Issues);
        var rotta = Assert.Single(letto.Routes);
        Assert.Equal(("ENR 3.2", "L99", "RNAV 5"), (rotta.Section, rotta.Designator, rotta.Navigation));
        Assert.Equal(["UNIMI", "NAXAV", "VIC", "KOPER", "LUMAV"], rotta.Points.Select(p => p.Designator));
        Assert.Equal("VICENZA VOR/DME 'VIC'", rotta.Points[2].Text);
        Assert.Equal([true, false, true, true, true], rotta.Points.Select(p => p.Compulsory));
        Assert.Equal(46.860833, rotta.Points[0].Position.Lat, 5);
        Assert.Equal(11.065278, rotta.Points[0].Position.Lon, 5);
        Assert.Equal("For continuation see AIP AUSTRIA\nMunich ACC/Padova ACC", rotta.Points[0].Remarks);
        Assert.Equal(new AipPage("ENR 3.2 L99-1", new DateOnly(2024, 3, 21)), rotta.Page);
        Assert.Equal([new DateOnly(2024, 3, 21), new DateOnly(2024, 7, 11)], letto.Pages.Select(p => p.Effective));
    }

    [Fact]
    public void ITratti_VersoEQuotePerParita_LeNoteELInterruzione()
    {
        var t = Assert.Single(Rotta().Routes).Segments;

        Assert.Equal(4, t.Count);
        // UNIMI → NAXAV: solo i pari, al contrario.
        Assert.Equal(("153° / 333°", 26.1, "FL195", "FL155", "5"), (t[0].Track, t[0].DistanceNm, t[0].Upper?.Text, t[0].Lower?.Text, t[0].LateralLimits));
        Assert.Equal((RouteDirection.None, RouteDirection.Backward, (string?)null, "160"), (t[0].Odd, t[0].Even, t[0].MinimumOdd, t[0].MinimumEven));
        Assert.False(t[0].Forward);
        Assert.True(t[0].Backward);
        // NAXAV → VIC: tutti e due i versi; la categoria della rotta condizionale e le note.
        Assert.Equal((RouteDirection.Forward, RouteDirection.Backward, "130", "140"), (t[1].Odd, t[1].Even, t[1].MinimumOdd, t[1].MinimumEven));
        Assert.Equal("CDR1 Only by ATC FL130-FL140 H24", t[1].Remarks);
        // VIC → KOPER: a cavallo di due pagine, con la quota in piedi su due righe.
        Assert.Equal(("180° / 360°", 29.0, "5500ft AMSL", "90", "80"), (t[2].Track, t[2].DistanceNm, t[2].Lower?.Text, t[2].MinimumOdd, t[2].MinimumEven));
        Assert.Equal((VerticalReference.Msl, 5500d), (t[2].Lower!.Reference, t[2].Lower!.Value));
        // KOPER → LUMAV: la riga di barre, la rotta si interrompe.
        Assert.True(t[3].Discontinuity);
        Assert.Null(t[3].Track);
    }

    [Fact]
    public void UnPuntoSenzaCoordinate_LaRottaNonEntra_ESiDice()
    {
        var pagina = Pagina(1, RotteDispari, "ENR 3.2 L98 - 1", "21 MAR 2024", 0,
        [
            .. TestataDelleRotte, (54, 146, "L98"),
            (56, 182, "▲ UNIMI"), (67, 196, "46°51'39''N 011°03'55''E"),
            (174, 227, "153° / 333°"), (235, 226, "FL195"), (235, 241, "FL155"),
            (56, 258, "▲ NAXAV"),
        ]);

        var letto = ItalianEnr3.ReadRoutes([pagina], "ENR 3.2");

        Assert.Empty(letto.Routes);
        Assert.Contains(letto.Issues, i => i.Kind == ImportIssueKind.Route && i.Detail == "NAXAV: senza coordinate");
    }

    // Carta «L'AIP intero», passo 2d: ENR 3.2-1 nota 3, «in mancanza di dati valgono i valori precedenti» (limiti verticali
    // e ampiezza; rotta, distanza e livelli no); e la nota dopo l'ultimo punto è della rotta (M740: «For continuation see AIP
    // MALTA», una cella unita su più colonne).
    [Fact]
    public void ValeIlDatoDiPrima_ELaNotaDopoLUltimoPunto()
    {
        var pagina = Pagina(1, RotteDispari, "ENR 3.2 L97 - 1", "21 MAR 2024", 0,
        [
            .. TestataDelleRotte, (54, 146, "L97"),
            (56, 182, "▲ UNIMI"), (67, 196, "46°51'39''N 011°03'55''E"),
            (174, 227, "153° / 333°"), (235, 226, "FL195"), (283, 233, "5"), (188, 239, "26.1"), (235, 241, "FL155"),
            (56, 258, "▲ NAXAV"), (67, 272, "46°27'50''N 011°19'20''E"),
            // Il tratto da NAXAV non ripete limiti e ampiezza.
            (174, 302, "160° / 340°"), (188, 314, "11.8"),
            (56, 334, "▲ KOPER"), (67, 348, "45°09'14''N 011°38'29''E"), (224, 350, "For continuation"), (283, 350, "see AIP MALTA"),
        ]);

        var letto = ItalianEnr3.ReadRoutes([pagina], "ENR 3.2");

        Assert.Empty(letto.Issues);
        var rotta = Assert.Single(letto.Routes);
        var t = rotta.Segments[1];
        Assert.Equal(("160° / 340°", 11.8, "FL195", "FL155", "5"), (t.Track, t.DistanceNm, t.Upper?.Text, t.Lower?.Text, t.LateralLimits));
        Assert.Equal("For continuation see AIP MALTA", rotta.Remarks);
    }

    [Fact]
    public void SenzaLimitiNelPrimoTratto_SiDice()
    {
        var pagina = Pagina(1, RotteDispari, "ENR 3.2 L96 - 1", "21 MAR 2024", 0,
        [
            .. TestataDelleRotte, (54, 146, "L96"),
            (56, 182, "▲ UNIMI"), (67, 196, "46°51'39''N 011°03'55''E"),
            (174, 227, "153° / 333°"), (188, 239, "26.1"),
            (56, 258, "▲ NAXAV"), (67, 272, "46°27'50''N 011°19'20''E"),
        ]);

        var letto = ItalianEnr3.ReadRoutes([pagina], "ENR 3.2");

        Assert.Contains(letto.Issues, i => i.Kind == ImportIssueKind.Route && i.Detail == "UNIMI: senza limiti verticali e senza un tratto prima");
    }

    // Carta «L'AIP intero», passo 3: il riquadro «NOTE / REMARKS» sotto la tabella, l'italiano a sinistra e l'inglese
    // sotto «REMARKS», note numerate; può finire nella pagina dopo, che non ha la tabella ed è sfalsata (M725).
    private static PdfPageText DueTratti(string rotta, params (double X, double Y, string Testo)[] sotto) =>
        Pagina(1, RotteDispari, $"ENR 3.2 {rotta} - 1", "21 MAR 2024", 0,
        [
            .. TestataDelleRotte, (54, 146, rotta),
            (56, 182, "▲ UNIMI"), (67, 196, "46°51'39''N 011°03'55''E"),
            (174, 227, "153° / 333°"), (235, 226, "FL195"), (283, 233, "5"), (188, 239, "26.1"), (235, 241, "FL155"),
            (56, 258, "▲ NAXAV"), (67, 272, "46°27'50''N 011°19'20''E"),
            .. sotto,
        ]);

    private static PdfPageText SenzaTabella(int numero, params (double X, double Y, string Testo)[] testo)
    {
        var parole = new List<PdfWord> { Parola(47, 12, $"ENR 3.2 L95 - {numero}"), Parola(47, 24, "AIRAC effective date 21 MAR 2024") };
        foreach (var (x, y, t) in testo)
        {
            var cx = x;
            foreach (var p in t.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                parole.Add(Parola(cx, y, p));
                cx += p.Length * 4.5 + 3;
            }
        }
        parole.Add(Parola(47, 832, "(A03/24) ENAV - Roma"));
        return new PdfPageText(numero, 595, 842, parole, []);
    }

    [Fact]
    public void LeNoteDellaRotta_InDueLingue_AncheNellaPaginaDopo()
    {
        var uno = DueTratti("L95",
            (52, 790, "NOTE"), (320, 790, "REMARKS"),
            (52, 800, "1.Il tratto UNIMI-NAXAV è utilizzabile"), (320, 800, "1.Route usable northbound"),
            (52, 810, "solo in direzione nord."), (320, 810, "only."));
        var due = SenzaTabella(2,
            (47, 100, "2.Nel tratto VIC-LUMAV livelli di volo"), (315, 100, "2.In the segment VIC-LUMAV"),
            (47, 110, "assegnati da Padova ACC."), (315, 110, "assigned by Padova ACC."));

        var letto = ItalianEnr3.ReadRoutes([uno, due], "ENR 3.2");

        Assert.Empty(letto.Issues);
        Assert.Equal(
        [
            new RouteNote(1, "Il tratto UNIMI-NAXAV è utilizzabile solo in direzione nord.", "Route usable northbound only."),
            new RouteNote(2, "Nel tratto VIC-LUMAV livelli di volo assegnati da Padova ACC.", "In the segment VIC-LUMAV assigned by Padova ACC."),
        ], Assert.Single(letto.Routes).Notes);
    }

    [Fact]
    public void NoteNil_RiquadroVuoto_SenzaRiquadro()
    {
        var nil = ItalianEnr3.ReadRoutes([DueTratti("L94", (52, 790, "NOTE"), (320, 790, "REMARK"), (52, 800, "NIL"), (320, 800, "NIL"))], "ENR 3.2");
        var vuoto = ItalianEnr3.ReadRoutes([DueTratti("L93", (52, 790, "NOTE"), (320, 790, "REMARKS"))], "ENR 3.2");
        var senza = ItalianEnr3.ReadRoutes([DueTratti("L92")], "ENR 3.2");

        Assert.Empty(nil.Issues.Concat(vuoto.Issues).Concat(senza.Issues));
        Assert.Equal([], Assert.Single(nil.Routes).Notes);
        Assert.Equal([], Assert.Single(vuoto.Routes).Notes);
        Assert.Null(Assert.Single(senza.Routes).Notes);
    }

    [Fact]
    public void TestoSottoLaTabellaSenzaRiquadro_EUnaNotaSenzaInglese_SiDicono()
    {
        var senzaRiquadro = ItalianEnr3.ReadRoutes([DueTratti("L91", (52, 800, "Rotta chiusa"))], "ENR 3.2");
        var mezza = ItalianEnr3.ReadRoutes([DueTratti("L90", (52, 790, "NOTE"), (320, 790, "REMARKS"),
            (52, 800, "1.Prima nota."), (320, 800, "1.First note."), (52, 810, "2.Seconda nota."))], "ENR 3.2");

        Assert.Contains(senzaRiquadro.Issues, i => i.Kind == ImportIssueKind.Route && i.Detail == "testo sotto la tabella senza il riquadro delle note: Rotta chiusa");
        Assert.Null(Assert.Single(senzaRiquadro.Routes).Notes);
        Assert.Contains(mezza.Issues, i => i.Kind == ImportIssueKind.Route && i.Detail == "note: 2 in italiano, 1 in inglese");
        Assert.Equal([new RouteNote(1, "Prima nota.", "First note."), new RouteNote(2, "Seconda nota.", null)], Assert.Single(mezza.Routes).Notes);
    }

    // --- le attese ---------------------------------------------------------------------------------------------------

    private static (double, double, string)[] TestataDelleAttese => [(77, 154, "1"), (164, 154, "2"), (284, 154, "3"), (453, 154, "4")];

    private static ImportResult Attese() => ItalianEnr3.ReadHoldings(
    [
        Pagina(1, AtteseDispari, "ENR 3.4.1 - 1", "03 SEP 2026", 0,
        [
            .. TestataDelleAttese,
            (54, 170, "ABBOZ"), (240, 170, "9000"), (287, 170, "R"), (318, 170, "225°"), (349, 170, "RNAV Holding - Outbound Leg 1.8NM"),
            (349, 178, "IAS MAX 90kt"),
            // Due riferimenti: nella seconda attesa la rotta sta 8 punti più su della quota (BABIK nel 2609).
            (54, 200, "BABIK"), (109, 200, "RDL255/8NM PAN"), (240, 200, "4500"), (287, 200, "R"), (318, 200, "075°"),
            (349, 200, "1.Outbound leg must not exceed 12NM PAN DME"), (244, 208, "(1)"),
            (318, 242, "075°"),
            (109, 250, "RDL 255 PAN/8.4NM IPTL"), (240, 250, "4500"), (287, 250, "R"), (244, 259, "(2)"),
            // «5000 only», e «FL70» / «or» / «TRL».
            (54, 290, "BAVOM"), (232, 290, "5000 only"), (287, 290, "R"), (318, 290, "263°"),
            (54, 320, "IPKOV"), (241, 320, "FL70"), (287, 320, "R"), (318, 320, "142°"), (246, 328, "or"), (243, 336, "TRL"),
            // Virata e rotta senza la quota: si dice.
            (54, 370, "TUCOZ"), (109, 370, "RDL103/11NM LIN"), (287, 370, "R"), (318, 370, "283°"),
        ]),
    ]);

    [Fact]
    public void LeAttese_UnaPerTerna_ColRiferimentoERimandi()
    {
        var a = Attese().Holdings;

        Assert.Equal(["ABBOZ", "BABIK", "BABIK", "BAVOM", "IPKOV"], a.Select(h => h.Fix));
        Assert.Equal(("9000", TurnDirection.Right, 225), (a[0].Minimum.Text, a[0].Turn, a[0].InboundTrack));
        Assert.Equal("RNAV Holding - Outbound Leg 1.8NM\nIAS MAX 90kt", a[0].Remarks);
        Assert.Equal(("RDL255/8NM PAN", "(1)", 75), (a[1].Bearing, a[1].NoteRefs, a[1].InboundTrack));
        Assert.Equal(("RDL 255 PAN/8.4NM IPTL", "(2)", 75), (a[2].Bearing, a[2].NoteRefs, a[2].InboundTrack));
        // La quota senza unità: piedi sul livello del mare (committente, 21 ottobre); il testo resta com'è stampato.
        Assert.Equal(("9000", VerticalReference.Msl, 9000.0, VerticalUnit.Ft), (a[0].Minimum.Text, a[0].Minimum.Reference, a[0].Minimum.Value, a[0].Minimum.Unit));
        Assert.Equal(("5000 only", VerticalReference.Msl, 5000.0), (a[3].Minimum.Text, a[3].Minimum.Reference, a[3].Minimum.Value));
        Assert.Equal(VerticalReference.FlightLevel, a[4].Minimum.Reference);
        Assert.Equal(("FL70 or TRL", 142), (a[4].Minimum.Text, a[4].InboundTrack));
        Assert.Equal(new AipPage("ENR 3.4.1-1", new DateOnly(2026, 9, 3)), a[0].Page);
    }

    [Fact]
    public void UnaVirataSenzaQuota_SiDice()
    {
        var problemi = Attese().Issues;

        Assert.Equal(["senza quota: R", "senza quota: 283°"], problemi.Where(p => p.Kind == ImportIssueKind.Holding).Select(p => p.Detail));
        Assert.All(problemi, p => Assert.Equal("TUCOZ", p.Key));
    }

    // Carta «L'AIP intero», passo 2a: con i bordi orizzontali il punto è la sua fascia, e il nome sta in mezzo alla cella.
    // TUCOZ nel 2611: la quota 3000 e le note 1-2 trenta punti sopra il nome; senza fasce finivano a TOPRU.
    [Fact]
    public void LaFasciaDellaTabella_IlNomeInMezzoAllaCella()
    {
        PdfHorizontalRule Bordo(double y) => new(y, 52, 564);
        var pagina = Pagina(1, AtteseDispari, "ENR 3.4.1 - 18", "11 JUN 2026", 0,
        [
            .. TestataDelleAttese,
            (54, 210, "TOPRU"), (109, 210, "RDL188/14NM LIN"), (240, 210, "4000"), (287, 210, "R"), (318, 210, "196°"),
            (349, 210, "1. IAS MAX 185kt"),
            (240, 250, "3000"), (349, 250, "1. Holding reserved to missed approach"), (349, 264, "2. Outbound leg must not exceed 14NM LIN DME"),
            (54, 280, "TUCOZ"), (109, 280, "RDL103/11NM LIN"), (287, 280, "R"), (318, 280, "283°"), (349, 281, "3. IAS MAX 210kt"),
        ]) with { HorizontalRules = [Bordo(200), Bordo(240), Bordo(300)] };

        var letto = ItalianEnr3.ReadHoldings([pagina]);

        Assert.DoesNotContain(letto.Issues, p => p.Kind == ImportIssueKind.Holding);
        var (topru, tucoz) = (letto.Holdings[0], letto.Holdings[1]);
        Assert.Equal(("TOPRU", "1. IAS MAX 185kt"), (topru.Fix, topru.Remarks));
        Assert.Equal(("TUCOZ", "3000", 3000.0, TurnDirection.Right, 283, "RDL103/11NM LIN"),
            (tucoz.Fix, tucoz.Minimum.Text, tucoz.Minimum.Value, tucoz.Turn, tucoz.InboundTrack, tucoz.Bearing));
        Assert.Equal("1. Holding reserved to missed approach\n2. Outbound leg must not exceed 14NM LIN DME\n3. IAS MAX 210kt", tucoz.Remarks);
    }

    [Fact]
    public void IFileSiRiconosconoDalNome()
    {
        // Un nome di ENR 3.4.3 (che non c'è fra i PDF) non si legge: lo dice, non tace.
        var letto = ItalianAip.ReadFile(Path.Combine(Path.GetTempPath(), "LI-ENR 3.4.3.pdf"));

        Assert.Equal(ImportIssueKind.Unsupported, Assert.Single(letto.Issues).Kind);
    }

    /// <summary>
    /// ENR 3.4.2: la sigla col tipo e sotto il nome fra parentesi, quota · virata · rotta, le rotte e procedure interessate,
    /// le note; i rimandi anche sulla riga della quota («5000 (1)», CHI); una radioassistenza senza attese (AHO) non dà niente.
    /// </summary>
    private static ImportResult AtteseSulleRadioassistenze() => ItalianEnr3.ReadNavaidHoldings(
    [
        Pagina(1, AtteseDispari, "ENR 3.4.2 - 1", "03 SEP 2026", 0,
        [
            .. TestataDelleAttese,
            (54, 170, "AEA VOR/DME"), (54, 178, "(Alghero)"), (110, 170, "4000"), (230, 170, "L"), (275, 170, "048°"),
            (310, 170, "EA star"), (310, 178, "EA sid"), (350, 170, "1.IAS MAX 210kt"), (114, 178, "(1)"),
            (110, 200, "4000"), (230, 200, "R"), (275, 200, "176°"), (114, 208, "(2)"),
            (54, 240, "AHO TACAN"), (54, 248, "(Alghero)"), (310, 240, "EA star"),
            (54, 280, "CHI VOR/DME"), (54, 288, "(Chioggia)"), (110, 280, "5000 (1)"), (230, 280, "R"), (275, 280, "329°"),
            (310, 280, "L612"), (310, 288, "M859"), (350, 280, "1.IAS MAX 210kt"),
        ]),
    ]);

    [Fact]
    public void LeAtteseSulleRadioassistenze_ConIlTipoLeRotteInteressateEIRimandi()
    {
        var letto = AtteseSulleRadioassistenze();
        var a = letto.Holdings;

        Assert.Equal(["AEA", "AEA", "CHI"], a.Select(h => h.Fix));
        Assert.All(a, h => Assert.Equal(("ENR 3.4.2", "VOR/DME", (string?)null), (h.Section, h.FixType, h.Bearing)));
        Assert.Equal(("4000", TurnDirection.Left, 48, "(1)"), (a[0].Minimum.Text, a[0].Turn, a[0].InboundTrack, a[0].NoteRefs));
        Assert.Equal(("EA star\nEA sid", "1.IAS MAX 210kt"), (a[0].Affected, a[0].Remarks));
        Assert.Equal(("(2)", 176), (a[1].NoteRefs, a[1].InboundTrack));
        Assert.Equal(("5000", "(1)", 329, "L612\nM859"), (a[2].Minimum.Text, a[2].NoteRefs, a[2].InboundTrack, a[2].Affected));
        Assert.DoesNotContain(letto.Issues, p => p.Kind == ImportIssueKind.Holding);

        // L'elenco intero, anche la radioassistenza senza attese (AHO) con le sue procedure (carta «L'AIP intero», passo 2a).
        Assert.Equal(
            [("AEA", "VOR/DME", "Alghero", "EA star\nEA sid"), ("AHO", "TACAN", "Alghero", "EA star"), ("CHI", "VOR/DME", "Chioggia", "L612\nM859")],
            letto.NavaidProcedures.Select(n => (n.Designator, n.Type, n.Name, n.Affected)));
        Assert.All(letto.NavaidProcedures, n => Assert.Equal(("ENR 3.4.2", "ENR 3.4.2-1"), (n.Section, n.Page?.Reference)));
    }
}
