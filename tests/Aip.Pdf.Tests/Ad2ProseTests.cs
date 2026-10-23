using Aip.Pdf.Italia;

namespace Aip.Pdf.Tests;

/// <summary>
/// Carta F7, slice 7b: la prosa delle convenzionali diventa tratti. Le frasi sono quelle dell'AIP (le forme vere: radiale
/// fino a una distanza DME, arco DME, due radiali che si incrociano, RWY heading); le stazioni e i punti sono inventati.
/// </summary>
public sealed class Ad2ProseTests
{
    // LMT a 39°N 16°E con 3° E di declinazione; RCA un grado più a sud; i punti con nome dove capita.
    private static readonly Dictionary<string, ItalianAd2Prose.Station> Stazioni = new()
    {
        ["LMT"] = new(new Position(39.0, 16.0), 3),
        ["RCA"] = new(new Position(38.0, 16.0), 4),
        ["OST"] = new(new Position(41.8, 12.24), 3),
    };

    private static readonly Dictionary<string, Position> Punti = new()
    {
        ["DELIK"] = new(38.70, 16.45), ["SUGEP"] = new(38.95, 16.40), ["ARBUM"] = new(39.30, 15.70), ["NENIG"] = new(41.85, 11.90),
        ["ELKAP"] = new(42.20, 12.00), ["TAQ"] = new(42.00, 11.85),
    };

    private static Procedure P(ProcedureKind genere, string nome, string prosa, params string[] piste) => new()
    {
        Aerodrome = "LIXX", Kind = genere, Designator = nome, Runways = piste, Description = prosa,
    };

    private static (IReadOnlyList<ProcedureLeg> Tratti, string? Problema) Leggi(Procedure p, ItalianAd2Prose.Runway? pista = null) =>
        ItalianAd2Prose.Decode(p, n => Punti.TryGetValue(n, out var x) ? x : null, n => Stazioni.GetValueOrDefault(n), pista);

    private static double Distanza(Position a, Position b)
    {
        const double r = Math.PI / 180;
        var h = Math.Pow(Math.Sin((b.Lat - a.Lat) * r / 2), 2) + Math.Cos(a.Lat * r) * Math.Cos(b.Lat * r) * Math.Pow(Math.Sin((b.Lon - a.Lon) * r / 2), 2);
        return 2 * 6371008.8 / 1852 * Math.Asin(Math.Sqrt(h));
    }

    private static double Rotta(Position a, Position b)
    {
        const double r = Math.PI / 180;
        var y = Math.Sin((b.Lon - a.Lon) * r) * Math.Cos(b.Lat * r);
        var x = Math.Cos(a.Lat * r) * Math.Sin(b.Lat * r) - Math.Sin(a.Lat * r) * Math.Cos(b.Lat * r) * Math.Cos((b.Lon - a.Lon) * r);
        return (Math.Atan2(y, x) / r + 360) % 360;
    }

    [Fact]
    public void UnaStar_FuoriSullaRadialeFinoAUnaDistanzaDme()
    {
        var (tratti, problema) = Leggi(P(ProcedureKind.Star, "DELIK 1H",
            "DELIK – TR 310° (RDL 130 LMT VOR) – LMT VOR/DME to be left on RDL 258 LMT VOR until 14 NM LMT DME, then turn right to join\n"
            + "RDL 273 LMT VOR (TR 093°) bound to SUGEP\nSUGEP: 4000 FT", "10"));

        Assert.Null(problema);
        Assert.Equal(["IF DELIK", "CF LMT", "CF RDL 258/14 NM LMT", "CF SUGEP"], tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
        Assert.Equal(["310°", "258°", "093°"], tratti.Skip(1).Select(t => t.Course));
        Assert.Equal("R", tratti[3].Turn);
        // La radiale 258 magnetica con 3° E è 261 vera, a 14 NM dalla stazione.
        var calcolato = tratti[2].Position!.Value;
        Assert.Equal(14, Distanza(Stazioni["LMT"].Position, calcolato), 2);
        Assert.Equal(261, Rotta(Stazioni["LMT"].Position, calcolato), 1);
        Assert.All(tratti.Where(t => t.Fix is not null), t => Assert.Null(t.Position));
    }

    [Fact]
    public void UnArcoDme_DaUnaRadialeAllAltra()
    {
        var (tratti, problema) = Leggi(P(ProcedureKind.Star, "ARBUM 4F",
            "ARBUM – TR 132° (RDL 312 LMT VOR) until 17 NM LMT DME, then turn right to join ARC 15 NM LMT DME, crossing RDL 283 LMT\n"
            + "VOR turn left until joining RDL 273 LMT VOR (TR 093°) bound to SUGEP.\nINT ARC 15 NM LMT DME/RDL 296 LMT VOR - SUGEP: 4000FT", "10"));

        Assert.Null(problema);
        Assert.Equal(["IF ARBUM", "CF RDL 312/17 NM LMT", "CF RDL 312/15 NM LMT", "AF RDL 283/15 NM LMT", "CF SUGEP"],
            tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
        var arco = tratti[3];
        Assert.Equal(("LMT", 15.0, "L"), (arco.ArcCenter, arco.ArcRadiusNm!.Value, arco.Turn));
        Assert.Equal(15, Distanza(Stazioni["LMT"].Position, arco.Position!.Value), 2);
        Assert.Equal(286, Rotta(Stazioni["LMT"].Position, arco.Position!.Value), 1);
    }

    // Segnalazioni 2611, B5: lo stesso arco con le parole che usano le altre pagine — «Crossing» maiuscolo (prima l'arco
    // restava senza la radiale dove finisce), «to intercept and follow ARC», «(IAS MAX 200 kt during turn)», «(ARC 15 NM … DME)»
    // letto da un altro DME, «inbound to».
    [Fact]
    public void UnArcoDme_ScrittoAltrimenti_StessiTratti()
    {
        var (tratti, problema) = Leggi(P(ProcedureKind.Star, "ARBUM 4F",
            "ARBUM – TR 132° (RDL 312 LMT VOR) until 17 NM LMT DME, then turn right (IAS MAX 200 kt during turn) to intercept and follow ARC 15 NM LMT DME\n"
            + "(ARC 15 NM IBXY DME). Crossing RDL 283 LMT VOR turn left until intercepting and following RDL 273 LMT VOR (TR 093°) inbound to SUGEP.", "10"));

        Assert.Null(problema);
        Assert.Equal(["IF ARBUM", "CF RDL 312/17 NM LMT", "CF RDL 312/15 NM LMT", "AF RDL 283/15 NM LMT", "CF SUGEP"],
            tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
        Assert.Equal("L", tratti[4].Turn);
    }

    // B5: «Crossing» maiuscolo dopo una radiale: l'incrocio (LIEE CAR 1F andava dritta al punto dopo).
    [Fact]
    public void CrossingMaiuscolo_ELIncrocio()
    {
        var (tratti, problema) = Leggi(P(ProcedureKind.Star, "LMT 1F", "LMT VOR/DME - TR 120° (RDL 120 LMT VOR). Crossing RDL 020 RCA VOR turn right bound to SUGEP."));

        Assert.Null(problema);
        Assert.Equal(["IF LMT", "CF INT RDL 120 LMT/RDL 020 RCA", "CF SUGEP"], tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
        Assert.Equal("R", tratti[2].Turn);
    }

    // B5: «Over TOP VOR proceed …», «Initial climb procedure completed …», «as soon as practicable», «Leave LMT VOR on RDL 130»:
    // si leggono come «da LMT», «dopo il decollo», niente, «LMT, poi la radiale 130 di LMT».
    [Theory]
    [InlineData("Over LMT VOR proceed on RDL 130 LMT VOR (TR 130°) until 14 NM LMT DME, then turn right bound to SUGEP.", "IF LMT|CF RDL 130/14 NM LMT|CF SUGEP")]
    [InlineData("Initial climb procedure completed turn left (IAS MAX 200 kt during turn) bound to LMT VOR. Leave LMT VOR on RDL 130. At 14NM LMT DME turn right bound to SUGEP.", "IF LMT|CF RDL 130/14 NM LMT|TF SUGEP")]
    [InlineData("After take-off, as soon as practicable, turn left bound to LMT VOR/DME, to be left on RDL 130 LMT VOR until 14 NM LMT DME, then turn right bound to SUGEP.", "IF LMT|CF RDL 130/14 NM LMT|TF SUGEP")]
    // (TF dove la rotta verso SUGEP non è scritta: niente «TR …»).
    public void LeFrasiDiPassaggio_SiLeggono(string prosa, string attesi)
    {
        var (tratti, problema) = Leggi(P(ProcedureKind.Sid, "SUGEP 1A", prosa));

        Assert.Null(problema);
        Assert.Equal(attesi.Split('|'), tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
    }

    // B5, secondo giro: dove finisce un arco. Una radiale della stessa stazione («then intercepting RDL 283 LMT VOR»); un punto
    // che sta sull'arco («bound to SUGEP»); non un punto fuori dall'arco, né la radiale di un'altra stazione: lì si ferma.
    [Theory]
    [InlineData("then intercepting RDL 283 LMT VOR turn left until joining RDL 273 LMT VOR (TR 093°) bound to SUGEP.", null)]
    [InlineData("bound to ULSOM.", null)]
    [InlineData("bound to SUGEP.", "non passa per SUGEP")]
    [InlineData("crossing RDL 300 RCA VOR turn left bound to SUGEP.", "finisce su una radiale di RCA")]
    public void LaFineDellArco(string fine, string? problemaAtteso)
    {
        // ULSOM: un punto sull'arco di 15 NM da LMT, sulla radiale 290 magnetica.
        var sullArco = new Position(39.0 + 15 / 60.0 * Math.Cos(293 * Math.PI / 180), 16.0 + 15 / 60.0 * Math.Sin(293 * Math.PI / 180) / Math.Cos(39 * Math.PI / 180));
        Punti.TryAdd("ULSOM", sullArco);
        var (tratti, problema) = Leggi(P(ProcedureKind.Star, "ARBUM 4F",
            "ARBUM – TR 132° (RDL 312 LMT VOR) until 17 NM LMT DME, then turn right to join ARC 15 NM LMT DME, " + fine, "10"));

        if (problemaAtteso is null)
        {
            Assert.Null(problema);
            var arco = tratti.Single(t => t.PathTerminator == "AF");
            Assert.Equal(("LMT", "L"), (arco.ArcCenter, arco.Turn));
            Assert.Equal(15, Distanza(Stazioni["LMT"].Position, arco.Position!.Value), 1);
        }
        else
        {
            Assert.Contains(problemaAtteso, problema, StringComparison.Ordinal);
        }
    }

    // B5: un percorso che continua dopo una riga col punto («… CRN VOR.» / «At 5 NM …»); «(to be crossed at 2800 ft or above)»;
    // «RDL 273 (TR 093°)» senza la stazione vale la rotta stampata; «(74 VIE DME)» fra parentesi è la definizione del punto.
    [Fact]
    public void IlPercorsoSuPiuRighe_ELeQuoteFraParentesi()
    {
        var (tratti, problema) = Leggi(P(ProcedureKind.Sid, "SUGEP 9G",
            "After take-off proceed on RDL 130 LMT VOR.\nAt 14 NM LMT DME (to be crossed at 2800 ft or above) turn right to join RDL 093 (TR 093°)\n"
            + "bound to SUGEP.\nMinimum climb gradient: 510 ft/NM (8.4%) until leaving FL90."));

        Assert.Null(problema);
        Assert.Equal(["IF RDL 130/14 NM LMT", "CF SUGEP"], tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
        Assert.Equal("093°", tratti[1].Course);
        Assert.Contains("crossing RDL 020 RCA VOR", ItalianAd2Prose.Normalizza("crossing RDL 020 RCA VOR (74 LMT DME) turn left"), StringComparison.Ordinal);
        Assert.DoesNotContain("74", ItalianAd2Prose.Normalizza("crossing RDL 020 RCA VOR (74 LMT DME) turn left"), StringComparison.Ordinal);
    }

    // B5, terzo giro: un punto RNAV di scalo con le cifre (BP401); «TR 214)» senza gradi; il grado col carattere privato del
    // font (U+F0B0, LIBP); «LMT VOR to be left on RDL 130» senza la stazione dopo la radiale; un VORTAC e «(or 14 NM … DME)».
    [Theory]
    [InlineData("DELIK - MP406, then turn right bound to SUGEP.", "IF DELIK|TF MP406|TF SUGEP")]
    [InlineData("DELIK - TR 310 (RDL 130 LMT VOR) until 14 NM LMT DME, then turn right bound to SUGEP.", "IF DELIK|CF RDL 130/14 NM LMT|CF SUGEP")]
    [InlineData("DELIK - TR 310° bound to LMT VOR to be left on RDL 130 until 14 NM LMT DME, then turn right bound to SUGEP.", "IF DELIK|CF LMT|CF RDL 130/14 NM LMT|CF SUGEP")]
    [InlineData("DELIK - TR 310° (RDL 130 LMT VORTAC) until crossing RDL 020 RCA VORTAC (or 14 NM LMT DME), then turn right bound to SUGEP.", "IF DELIK|CF INT RDL 130 LMT/RDL 020 RCA|CF SUGEP")]
    public void TerzoGiro_SiLeggono(string prosa, string attesi)
    {
        Punti.TryAdd("MP406", new Position(38.80, 16.50));

        var (tratti, problema) = Leggi(P(ProcedureKind.Star, "DELIK 1T", prosa));

        Assert.Null(problema);
        Assert.Equal(attesi.Split('|'), tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
    }

    // Committente, 23 ottobre: una prua («turn right heading 040°») «disegnala come se non ci fosse vento e segnalo»: la
    // procedura si legge, la prua vale come rotta, e l'approssimazione si dice.
    [Fact]
    public void UnaPrua_ComeRottaSenzaVento_ESiDice()
    {
        var p = P(ProcedureKind.Star, "DELIK 1H", "DELIK – turn right heading 040° to join and follow RDL 130 LMT VOR (TR-310°) until 14 NM LMT DME, then turn right bound to SUGEP.");

        var (tratti, problema, avvisi) = ItalianAd2Prose.DecodeWithWarnings(p, n => Punti.TryGetValue(n, out var x) ? x : null, n => Stazioni.GetValueOrDefault(n));

        Assert.Null(problema);
        Assert.Equal(["IF DELIK", "CF RDL 130/14 NM LMT", "CF SUGEP"], tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
        Assert.Equal(["prua 040° disegnata come rotta, senza vento"], avvisi);
        // Senza prua, niente da dire.
        Assert.Empty(ItalianAd2Prose.DecodeWithWarnings(P(ProcedureKind.Star, "DELIK 1T", "DELIK – TR 310° (RDL 130 LMT VOR) bound to SUGEP."),
            n => Punti.TryGetValue(n, out var x) ? x : null, n => Stazioni.GetValueOrDefault(n)).Warnings);
    }

    // Segnalazioni 2611, B6: «on TR 073° until joining RDL …» — il punto dove la rotta prende la radiale (prima si andava dritti
    // al punto dopo). Non dopo una virata («then turn left to join RDL …»: lì la rotta di prima non vale più).
    [Fact]
    public void LaRadialeSiIntercetta_DaUnaRottaPresaDopoLaVirata()
    {
        var (tratti, problema) = Leggi(P(ProcedureKind.Star, "DELIK 1J",
            "DELIK – TR 310° (RDL 130 LMT VOR) until 14 NM LMT DME, then turn right on TR 300° until joining RDL 250 LMT VOR bound to SUGEP."));

        Assert.Null(problema);
        Assert.Equal(["IF DELIK", "CF RDL 130/14 NM LMT", "CF INT TR 300°/RDL 250 LMT", "TF SUGEP"], tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
        // L'incrocio sta sulla radiale 250 (magnetica, 3° E) di LMT.
        Assert.Equal(253, Rotta(Stazioni["LMT"].Position, tratti[2].Position!.Value), 0);
    }

    // B6: una SID che comincia con una rotta parte dalla soglia della pista; se la rotta si scosta dalla pista, si dice.
    // (TF verso NENIG: la radiale non ha una rotta scritta.)
    [Theory]
    [InlineData(250, null)]
    [InlineData(200, "la virata dopo il decollo")]
    public void UnaSidDallaPista_ConUnaRotta(double pista, string? avvisoAtteso)
    {
        var soglia = new Position(41.81, 12.29);
        var p = P(ProcedureKind.Sid, "NENIG 6Z", "Proceed on TR 247° until joining RDL 278 OST VOR bound to NENIG.", "25");

        var (tratti, problema, avvisi) = ItalianAd2Prose.DecodeWithWarnings(p, n => Punti.TryGetValue(n, out var x) ? x : null,
            n => Stazioni.GetValueOrDefault(n), new ItalianAd2Prose.Runway(soglia, pista));

        Assert.Null(problema);
        Assert.Equal(["IF INT TR 247°/RDL 278 OST", "TF NENIG"], tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
        if (avvisoAtteso is null) Assert.Empty(avvisi);
        else Assert.Contains(avvisoAtteso, Assert.Single(avvisi), StringComparison.Ordinal);
    }

    [Fact]
    public void DueRadialiCheSiIncrociano()
    {
        var (tratti, problema) = Leggi(P(ProcedureKind.Star, "DELIK 9Z",
            "DELIK proceed on TR 300° (RDL 120 LMT VOR) until crossing RDL 020 RCA VOR, then turn right bound to SUGEP."));

        Assert.Null(problema);
        var incrocio = tratti[1].Position!.Value;
        Assert.Equal("INT RDL 120 LMT/RDL 020 RCA", tratti[1].Computed);
        Assert.Equal(123, Rotta(Stazioni["LMT"].Position, incrocio), 0);
        Assert.Equal(24, Rotta(Stazioni["RCA"].Position, incrocio), 0);
    }

    [Fact]
    public void UnaSidDallaPista_FinoAUnaDistanzaDme()
    {
        var soglia = new Position(41.81, 12.29);
        var (tratti, problema) = Leggi(P(ProcedureKind.Sid, "NENIG 6Y",
            "After take-off maintain RWY heading until 1NM OST DME (MCA 410 FT) then turn left (IAS MAX 210KT) to join RDL 278 OST VOR\n"
            + "bound to NENIG.", "25"), new ItalianAd2Prose.Runway(soglia, 250));

        Assert.Null(problema);
        Assert.Equal(["IF RWY heading/1 NM OST DME", "TF NENIG"], tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
        Assert.Equal(1, Distanza(Stazioni["OST"].Position, tratti[0].Position!.Value), 2);
        Assert.Equal(250, Rotta(soglia, tratti[0].Position!.Value), 0);
        // «turn left»: a sinistra (fino al 23 ottobre ogni virata della prosa usciva a destra).
        Assert.Equal("L", tratti[1].Turn);
    }

    // Segnalazioni 2611, B2: le stesse cose scritte in un altro modo si leggono, e portano agli stessi tratti.
    [Theory]
    [InlineData("DELIK – TR 310° (RDL 130 LMT VOR) until 14 NM LMT TACAN, then turn RIGHT bound to SUGEP.")]   // LICT: TACAN, RIGHT
    [InlineData("DELIK – TR 310° (R130 LMT DVOR) until 14 NM LMT DME, then turn right bound to SUGEP.")]       // LICC: R199, DVOR
    [InlineData("DELIK – TR 310° (RDL 130 LMT VOR or QDR 130° LMT NDB (TR 310°)) until 14 NM LMT DME, then turn right bound to SUGEP.")] // LICJ
    [InlineData("DELIK – TR 310° (RDL/QDR 130 LMT VOR/NDB) until 14 NM LMT DME, then turn right bound to SUGEP IAF.")]   // LICT, LIEE
    [InlineData("DELIK – TR 310° (RDL 130 LMT VOR) AT 14 NM LMT DME turn right bound to SUGEP.")]             // LIBG: AT
    public void LeFormeDiverse_StessiTratti(string prosa)
    {
        var (tratti, problema) = Leggi(P(ProcedureKind.Star, "DELIK 1T", prosa));

        Assert.Null(problema);
        Assert.Equal(["IF DELIK", "CF RDL 130/14 NM LMT", "CF SUGEP"], tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
        Assert.Equal("R", tratti[2].Turn);
    }

    // B1: la stessa distanza letta da un altro DME, fra parentesi, è un'alternativa: il percorso resta quello della prima.
    [Theory]
    [InlineData("DELIK – TR 310° (RDL 130 LMT VOR) until 14NM LMT DME (or 15.5 NM IBX DME), then turn right bound to SUGEP.")]  // LIBD
    [InlineData("DELIK – TR 310° (RDL 130 LMT VOR) until 14 NM LMT DME (12NM IBXY DME), then turn right bound to SUGEP.")]      // LIRZ
    public void LaDistanzaDaUnAltroDme_FraParentesi_SiToglie(string prosa)
    {
        var (tratti, problema) = Leggi(P(ProcedureKind.Star, "DELIK 1T", prosa));

        Assert.Null(problema);
        Assert.Equal(["IF DELIK", "CF RDL 130/14 NM LMT", "CF SUGEP"], tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
    }

    // B2: un'alternativa che il lettore non sa togliere non diventa un tratto: si ferma e lo dice.
    [Fact]
    public void UnaAlternativaSconosciuta_SiFerma()
    {
        var (tratti, problema) = Leggi(P(ProcedureKind.Star, "DELIK 1T", "DELIK – TR 310° (RDL 130 LMT VOR OR QDR 130 LMT LOCATOR) until 14 NM LMT DME bound to SUGEP."));

        Assert.Empty(tratti);
        Assert.NotNull(problema);
    }

    [Fact]
    public void UnaStarCheNonDiceDaDoveComincia_ParteDalPuntoDelSuoNome()
    {
        var (tratti, _) = Leggi(P(ProcedureKind.Star, "ELKAP 3P", "Proceed on TR 119° (RDL 299 LMT VOR) bound to TAQ"));

        Assert.Equal(["IF ELKAP", "CF TAQ"], tratti.Select(t => $"{t.PathTerminator} {t.Fix}"));
    }

    [Theory]
    [InlineData("Join RDL 303 LMT VOR (TR123°) till point DELIK then turn right to join RDL 353 RCA VOR (TR173°). Leaving 4000 FT turn right bound to SUGEP.", "Leaving 4000 FT")]
    [InlineData("ELKAP proceed on TR 260° (RDL 080 LMT VOR) until joining IBRM LOC direct to SUGEP.", "IBRM LOC")]
    [InlineData("ELKAP proceed on TR 260° (RDL 080 LMT VOR) when instructed by ATC bound to SUGEP.", "non capito")]
    [InlineData("ELKAP proceed on TR 260° (RDL 080 XYZ VOR) bound to SUGEP.", "XYZ")]
    [InlineData("ELKAP proceed on TR 267° (RDL 087 LMT VOR) until 3 NM RCA DME, then turn left bound to SUGEP.", "non arriva a 3 NM da RCA")]
    public void UnaFraseNonCapita_NessunTratto_EIlPerche(string prosa, string perche)
    {
        var (tratti, problema) = Leggi(P(ProcedureKind.Star, "ELKAP 1A", prosa));

        Assert.Empty(tratti);
        Assert.Contains(perche, problema, StringComparison.Ordinal);
    }

    [Fact]
    public void IlPercorso_SenzaLeNoteInTesta_ELeQuoteSotto_ELeScrittureStorte()
    {
        Assert.Equal("DIXER proceed on TR 062° (RDL 062 VOG VOR) until 14 NM VOG DME", ItalianAd2Prose.Route(
            "(ATC discretion)\nMinimum climb gradient 8.3% (500 ft/NM) until leaving 4500FT.\nDIXER proceed on TR 06 2° (RDL 062 VOG VOR) until 14NM VOG DME\nDIXER: 4000 FT"));
        Assert.Equal("SOR VOR - TR 284° (RDL 284 SOR VOR) until 16 NM SOR DME, then 20 NM OST DME and 43 NM PNZ DME",
            ItalianAd2Prose.Route("SOR V OR - TR 284° (RDL284 SOR VOR) until 16 NM SOR DME, then D20 OST DME and 43 NM PNZ TAC"));
        // Una riga con una quota e un verbo fa ancora parte del percorso.
        Assert.EndsWith("Leaving 4000 FT turn right bound to SUGEP.",
            ItalianAd2Prose.Route("Join RDL 303 LMT VOR (TR123°) to join RDL 353\nVOG VOR (TR173°). Leaving 4000 FT turn right bound to SUGEP."), StringComparison.Ordinal);
    }

    [Fact]
    public void TutteLeProcedure_ConLeRadioassistenzeDiScalo_ELePiste()
    {
        var documento = new AipDocument
        {
            State = "LI", Airac = "2611", Airspaces = [],
            Points = [new SignificantPoint { Section = "ENR 4.4", Designator = "SUGEP", Position = Punti["SUGEP"] }],
            // LMT è una radioassistenza di scalo (AD 2.19), come BRL e CMP: non sta in ENR 4.1.
            Aerodromes =
            [
                new Aerodrome
                {
                    Icao = "LIXX", Name = "Prova",
                    NavigationAids = [new Navaid { Section = "AD 2.19", Designator = "LMT", Name = "LAMEZIA", Type = "DVOR/DME", MagneticVariation = "3° E-2010.0", Position = Stazioni["LMT"].Position, Components = [] }],
                },
            ],
            Procedures =
            [
                P(ProcedureKind.Star, "SUGEP 3G", "SUGEP – TR 093° (RDL 273 LMT VOR) – LMT VOR to be left on RDL 115 LMT VOR until 16 NM LMT DME."),
                P(ProcedureKind.Star, "SUGEP 4G", "SUGEP – TR 093° (RDL 273 LMT VOR) – Leaving 4000 FT turn right."),
                new Procedure { Aerodrome = "LIXX", Kind = ProcedureKind.Sid, Designator = "XIBRI 5A", Legs = [new ProcedureLeg { PathTerminator = "TF", Fix = "SUGEP" }] },
            ],
        };

        var (letto, nonLette, _) = ItalianAd2Prose.DecodeAll(documento);

        var sugep = letto.Procedures![0];
        Assert.True(sugep.LegsFromDescription);
        Assert.Equal(["SUGEP", "LMT", "RDL 115/16 NM LMT"], sugep.Legs.Select(t => t.Fix ?? t.Computed));
        Assert.Same(documento.Procedures[2], letto.Procedures[2]);
        var (nonLetta, perche) = Assert.Single(nonLette);
        Assert.Equal("SUGEP 4G", nonLetta.Designator);
        Assert.Contains("Leaving 4000 FT", perche, StringComparison.Ordinal);
        Assert.False(letto.Procedures[1].LegsFromDescription);
    }

    // Segnalazioni 2611, B1 e B3: un localizzatore o un NDB non stampano la declinazione. Si tengono: quella dello scalo, se
    // è di uno scalo; se no quella del VOR più vicino che la stampa.
    [Fact]
    public void LeRadioassistenzeSenzaDeclinazione_PrendonoQuellaDelloScaloODelVorPiuVicino()
    {
        Navaid N(string sigla, string tipo, Position dove, string? declinazione = null) =>
            new() { Section = "AD 2.19", Designator = sigla, Name = sigla, Type = tipo, MagneticVariation = declinazione, Position = dove, Components = [] };
        var documento = new AipDocument
        {
            State = "LI", Airac = "2611", Airspaces = [],
            Points = [new SignificantPoint { Section = "ENR 4.4", Designator = "SUGEP", Position = Punti["SUGEP"] }],
            // DEC (un NDB di ENR 4.1, senza declinazione) sta accanto a LMT, che ha 3° E.
            Navaids = [N("LMT", "VOR/DME", Stazioni["LMT"].Position, "3° E-2010.0") with { Section = "ENR 4.1" }, N("DEC", "NDB", new Position(39.05, 16.05)) with { Section = "ENR 4.1" }],
            Aerodromes =
            [
                new Aerodrome { Icao = "LIXX", Name = "Prova", MagneticVariation = "10° E (2020.0)", NavigationAids = [N("IBX", "LLZ/DME", new Position(38.9, 16.2))] },
            ],
            Procedures =
            [
                // Una radiale di IBX: la sua declinazione è quella dello scalo, 10° E.
                P(ProcedureKind.Star, "SUGEP 1X", "SUGEP – TR 093° bound to IBX VOR to be left on RDL 180 IBX VOR until 10 NM IBX DME."),
                // Una radiale di DEC: senza scalo, quella di LMT, il VOR più vicino.
                P(ProcedureKind.Star, "SUGEP 1Y", "SUGEP – TR 093° bound to DEC VOR to be left on RDL 180 DEC VOR until 10 NM DEC DME."),
            ],
        };

        var (letto, nonLette, _) = ItalianAd2Prose.DecodeAll(documento);

        Assert.Empty(nonLette);
        var ibx = letto.Procedures![0].Legs[^1].Position!.Value;
        var dec = letto.Procedures[1].Legs[^1].Position!.Value;
        // La radiale 180 magnetica è 190 vera con 10° E (scalo), 183 vera con 3° E (LMT).
        Assert.Equal(190, Rotta(new Position(38.9, 16.2), ibx), 0);
        Assert.Equal(183, Rotta(new Position(39.05, 16.05), dec), 0);
    }

    // B1: il DME di un ILS («7 NM IFA DME») è una stazione: dove sta l'antenna del DME, non il localizzatore.
    [Fact]
    public void IlDmeDiUnIls_DaDoveStaLAntennaDelDme()
    {
        var dme = new Position(39.10, 16.10);
        var documento = new AipDocument
        {
            State = "LI", Airac = "2611", Airspaces = [],
            Points = [new SignificantPoint { Section = "ENR 4.4", Designator = "SUGEP", Position = Punti["SUGEP"] }],
            Aerodromes =
            [
                new Aerodrome
                {
                    Icao = "LIXX", Name = "Prova", MagneticVariation = "3° E (2020.0)",
                    Ils = [new Ils { Runway = "22", Designator = "IFA", MagneticVariation = "2° E-2005.0", Components = [new IlsComponent("LOC", new Position(39.20, 16.30)), new IlsComponent("DME", dme)] }],
                },
            ],
            Procedures = [P(ProcedureKind.Star, "SUGEP 1Z", "SUGEP – TR 093° bound to IFA VOR to be left on RDL 180 IFA VOR until 10 NM IFA DME.")],
        };

        var (letto, nonLette, _) = ItalianAd2Prose.DecodeAll(documento);

        Assert.Empty(nonLette);
        var ultimo = letto.Procedures![0].Legs[^1].Position!.Value;
        Assert.Equal(10, Distanza(dme, ultimo), 1);
        Assert.Equal(182, Rotta(dme, ultimo), 0);
    }

    // Revisione col committente delle 166 non lette (carta §6), passo 1: il testo rotto dal PDF, le distanze, la radiale «R»
    // e il QDR scritti in un altro modo portano agli stessi tratti della frase scritta bene.
    [Theory]
    [InlineData("DELIK – TR 310° (RDL 130LMT VOR) until 14 NM LMT DME, then turn right bound to SUGEP.")]          // R1: LICD «RDL 064LPD»
    [InlineData("DELIK – TR 310° (RDL 13 0L MT VOR) until 14 NM LMT DME, then turn right bound to SUGEP.")]        // R5: LICD «RDL 07 8L PD»
    [InlineData("DELIK – tr 310° (RDL 130 LMT VOR) until 14 NM LMT DME, then turn right bound to SUGEP.")]         // R6: «tr»
    [InlineData("D ELIK – TR 310° (RDL 130 LMT VOR) until 14 NM LMT DME, then turn right bound to SUGEP.")]        // R4: «M OROB»
    [InlineData("ELIK – TR 310° (RDL 130 LMT VOR) until 14 NM LMT DME, then turn right bound to SUGEP.")]          // R4: «ELDA» per NELDA
    [InlineData("DELIK – TR 310° (R 130 LMT VOR) until 14 NM LMT DME, then turn right bound to SUGEP.")]           // G3.1: «R 199 RCA»
    [InlineData("DELIK – TR 310° (RDL 130 LMT VOR) until 14 NM LMT VOR/DME, then turn right bound to SUGEP.")]     // D1
    [InlineData("DELIK – TR 310° (RDL 130 LMT VOR) until 14NM LMT VORTAC, then turn right bound to SUGEP.")]       // D1
    [InlineData("DELIK – TR 310° (RDL 130 LMT VOR) until 14 LMT DME, then turn right bound to SUGEP.")]            // D4
    [InlineData("DELIK – TR 310° (RDL 130 LMT VOR) until 14NM LMT, then turn right bound to SUGEP.")]              // D5
    [InlineData("DELIK – TR 310° (RDL 130 LMT VOR) until 14NM LMT/RCA TACAN, then turn right bound to SUGEP.")]    // D7
    [InlineData("DELIK – TR 310° (RDL 130 LMT VOR) until RDL 130/D14 LMT VOR/DME, then turn right bound to SUGEP.")] // D6
    [InlineData("DELIK – TR 310° (RDL 130 LMT VOR) until RDL 130/D14 LMT VOR/DME (D15 RCA DME), then turn right bound to SUGEP.")] // D6, LICR FOSQA
    [InlineData("DELIK – TR 310° (RDL 130 LMT VOR) (QDR 131° LMT NDB) until 14 NM LMT DME, then turn right bound to SUGEP.")] // N1
    [InlineData("DELIK – TR 310° (RDL 130 LMT VOR) or TR 311° (QDR 131° LMT NDB) until 14 NM LMT DME, then turn right bound to SUGEP.")] // N1
    [InlineData("DELIK – TR 310° (RDL 130 LMT VOR or TR 311° (QDR 131° LMT NDB) until 14 NM LMT DME, then turn right bound to SUGEP.")] // N1, la parentesi non chiusa
    public void LaRevisione_LeFormeDiverse_StessiTratti(string prosa)
    {
        var (tratti, problema) = Leggi(P(ProcedureKind.Star, "DELIK 1T", prosa));

        Assert.Null(problema);
        Assert.Equal(["IF DELIK", "CF RDL 130/14 NM LMT", "CF SUGEP"], tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
        Assert.Equal("R", tratti[2].Turn);
    }

    // R4: il nome senza la prima lettera non si ricuce se è già un altro nome dell'AIP.
    [Fact]
    public void IlNomeSenzaLaPrimaLettera_NonSeEUnAltroNome()
    {
        var (tratti, _) = Leggi(P(ProcedureKind.Star, "XTAQ 1A", "TAQ – TR 310° (RDL 130 LMT VOR) until 14 NM LMT DME, then turn right bound to SUGEP."));

        Assert.Equal("TAQ", tratti[0].Fix);
    }

    // Gruppo 2, U4, U5 e DNE: l'arco scritto in un altro modo è lo stesso arco.
    [Theory]
    [InlineData("then turn right to join ARC15 NM LMT DME")]           // R2: «ARC16 NM»
    [InlineData("then turn right to join arc 15.0NM LMT DME")]         // U4: «arc» minuscolo
    [InlineData("then turn right to join 15 NM ARC LMT DME")]          // D2
    [InlineData("then turn right to join ARC 15 LMT TACAN")]           // D3
    [InlineData("then turn right to join ARC 15 NM LMT until")]        // D3: senza DME
    [InlineData("then turn right to join ARC 15NM LMT(IBXY) DME")]     // U5: la sigla attaccata fra parentesi
    [InlineData("then turn right to join ARC 15 NM LMT DNE")]          // gruppo 7: DNE
    public void LaRevisione_LArcoScrittoAltrimenti(string arco)
    {
        var (tratti, problema) = Leggi(P(ProcedureKind.Star, "ARBUM 4F",
            $"ARBUM – TR 132° (RDL 312 LMT VOR) until 17 NM LMT DME, {arco}, crossing RDL 283 LMT VOR turn left until joining RDL 273 LMT VOR (TR 093°) bound to SUGEP.", "10"));

        Assert.Null(problema);
        Assert.Equal(["IF ARBUM", "CF RDL 312/17 NM LMT", "CF RDL 312/15 NM LMT", "AF RDL 283/15 NM LMT", "CF SUGEP"],
            tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
    }

    // Gruppo 4 e LIPZ: i verbi nuovi.
    [Theory]
    [InlineData("P roceed to LMT VOR/DME, to be left on RDL 130 LMT VOR until 14 NM LMT DME, then turn right bound to SUGEP.", "IF LMT|CF RDL 130/14 NM LMT|TF SUGEP")] // R3
    [InlineData("Bound to LMT VOR/DME. Leave LMT VOR/DME on RDL 130 LMT VOR (TR 130°) bound to SUGEP.", "IF LMT|CF SUGEP")]              // V1
    [InlineData("Continue on RDL 130 LMT VOR/DME. At 14 NM LMT VOR/DME turn right bound to SUGEP.", "IF RDL 130/14 NM LMT|TF SUGEP")]   // V1, D1
    [InlineData("Turn left bound to LMT VOR to leave on RDL 130 LMT VOR (TR 130°) bound to SUGEP.", "IF LMT|CF SUGEP")]                 // V2
    [InlineData("Turn left bound to LMT VOR to leave on RDL 130° LMT VOR (TR 130°) bound to SUGEP.", "IF LMT|CF SUGEP")]                // V2, LIRS ILOWE 1X
    [InlineData("Continue on RDL 130 LMT VOR/DME. When at 14 NM LMT DME turn right bound to SUGEP.", "IF RDL 130/14 NM LMT|TF SUGEP")]   // LIEE KOVAS 7A
    [InlineData("Overflying LMT VOR turn right to intercept and follow RDL 130 LMT VOR (TR 130°) bound to SUGEP.", "IF LMT|CF SUGEP")]  // V4
    [InlineData("Proceed on RDL 130 LMT VOR (TR 130° in case of LMT VOR unavailability). At 14NM LMT DME turn right bound to SUGEP.", "IF RDL 130/14 NM LMT|TF SUGEP")] // LIPZ
    public void LaRevisione_IVerbiNuovi(string prosa, string attesi)
    {
        var (tratti, problema) = Leggi(P(ProcedureKind.Sid, "SUGEP 1A", prosa));

        Assert.Null(problema);
        Assert.Equal(attesi.Split('|'), tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
    }

    // V3: «Leave KOVAS on TR 165°»; N1: il QDR come unico percorso è la radiale dell'NDB; L1: il LOC fra parentesi si toglie.
    [Theory]
    [InlineData(ProcedureKind.Star, "Leave DELIK on TR 310° (RDL 130 LMT VOR), then turn right bound to SUGEP.", "IF DELIK|CF SUGEP")]
    [InlineData(ProcedureKind.Sid, "Leave LMT NDB on QDR 093° (TR 093°) bound to SUGEP.", "IF LMT|CF SUGEP")]
    [InlineData(ProcedureKind.Star, "DELIK – TR 310° until joining RDL 130 LMT VOR (310° IBXY LOC) bound to LMT VOR.", "IF DELIK|TF LMT")]
    public void LaRevisione_LeStarScritteAltrimenti(ProcedureKind genere, string prosa, string attesi)
    {
        var (tratti, problema) = Leggi(P(genere, "DELIK 1T", prosa));

        Assert.Null(problema);
        Assert.Equal(attesi.Split('|'), tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
    }

    // V6: «at 17 NM CMP DME (intersection RDL 073 OST VOR)»: si legge se il punto sta sulla radiale, se no si dice.
    [Fact]
    public void IlPuntoDefinitoDueVolte_SiControlla()
    {
        const string fine = ", then turn right bound to SUGEP.";
        var (giusti, _) = Leggi(P(ProcedureKind.Star, "DELIK 1T", "DELIK – TR 310° (RDL 130 LMT VOR) until 14 NM LMT DME" + fine));
        var punto = giusti[1].Position!.Value;
        var radiale = (int)Math.Round((Rotta(Stazioni["RCA"].Position, punto) - Stazioni["RCA"].Variation + 360) % 360);

        var (tratti, problema) = Leggi(P(ProcedureKind.Star, "DELIK 1T", $"DELIK – TR 310° (RDL 130 LMT VOR) until 14 NM LMT DME (intersection RDL {radiale:000} RCA VOR)" + fine));
        var (_, sbagliato) = Leggi(P(ProcedureKind.Star, "DELIK 1T", $"DELIK – TR 310° (RDL 130 LMT VOR) until 14 NM LMT DME (intersection RDL {(radiale + 20) % 360:000} RCA VOR)" + fine));

        Assert.Null(problema);
        Assert.Equal(3, tratti.Count);
        Assert.Contains("non sta sulla RDL", sbagliato, StringComparison.Ordinal);
    }

    // Gruppo 7: gli errori di stampa che lo stesso testo corregge si leggono, e si dicono.
    [Theory]
    [InlineData("DELIK – TR 310\" (RDL 130 LMT VOR) until 14 NM LMT DME, then turn right bound to SUGEP.", "«TR 310\"»", "IF DELIK|CF RDL 130/14 NM LMT|CF SUGEP")]
    [InlineData("DELIK – 310° (RDL 130 LMT VOR) until 14 NM LMT DME, then turn right bound to SUGEP.", "«310°» senza «TR»", "IF DELIK|CF RDL 130/14 NM LMT|CF SUGEP")]
    [InlineData("DELIK – TR 310° … (RDL 130 LMT VOR) until 14 NM LMT DME, then turn right bound to SUGEP.", "«TR 310° …", "IF DELIK|CF RDL 130/14 NM LMT|CF SUGEP")]
    [InlineData("DELIK – TR 310° (RDL 130 LMT VOR) – LMT VOR//DME.", "«VOR//DME»", "IF DELIK|CF LMT")]
    [InlineData("DELIK – TR 310° (RDL 130 LMT VOR) bound to LMT- DVOR/DME.", "«LMT- DVOR»", "IF DELIK|CF LMT")]
    [InlineData("DELIK – TR 310° (RDL 130 VOR) bound to LMT VOR.", "«RDL 130 VOR» senza la stazione", "IF DELIK|CF LMT")]
    public void UnErroreDiStampa_SiLeggeESiDice(string prosa, string detto, string attesi)
    {
        var (tratti, problema, avvisi) = ItalianAd2Prose.DecodeWithWarnings(P(ProcedureKind.Star, "DELIK 1T", prosa),
            n => Punti.TryGetValue(n, out var x) ? x : null, n => Stazioni.GetValueOrDefault(n));

        Assert.Null(problema);
        Assert.Equal(attesi.Split('|'), tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
        Assert.Contains(avvisi, a => a.Contains(detto, StringComparison.Ordinal) && a.Contains("errore di stampa", StringComparison.Ordinal));
    }

    // U1: LIMC LEGLO 4U/4X, «until 3 NM VOG DME» su una radiale di SRN che non passa mai vicino a VOG, e il tratto dopo è
    // ancora di SRN: è la distanza da SRN, stampata male. Senza un tratto dopo della stessa stazione resta non letta.
    [Fact]
    public void LaDistanzaDallaStazioneSbagliata_SeIlTestoDiceQualeE()
    {
        var (tratti, problema, avvisi) = ItalianAd2Prose.DecodeWithWarnings(P(ProcedureKind.Star, "ELKAP 1A",
                "ELKAP proceed on TR 267° (RDL 087 LMT VOR) until 3 NM RCA DME, then turn left on TR 182° (RDL 182 LMT VOR) bound to SUGEP."),
            n => Punti.TryGetValue(n, out var x) ? x : null, n => Stazioni.GetValueOrDefault(n));

        Assert.Null(problema);
        Assert.Equal("CF RDL 087/3 NM LMT", $"{tratti[1].PathTerminator} {tratti[1].Computed}");
        Assert.Equal(3, Distanza(Stazioni["LMT"].Position, tratti[1].Position!.Value), 2);
        Assert.Contains(avvisi, a => a.Contains("«3 NM RCA DME» è un errore di stampa", StringComparison.Ordinal));
    }

    // U2: la riga delle quote che finisce con «: TRL» non è percorso (LIMJ IDONA 2S).
    [Fact]
    public void LaRigaDelleQuoteColTrl_NonEPercorso()
    {
        Assert.Equal("IDONA - TR 311° (RDL 131 LMT VOR) - LMT VOR/DME", ItalianAd2Prose.Route(
            "IDONA – TR 311° (RDL 131 LMT VOR) – LMT VOR/DME\nRDL 131/34 NM LMT VOR/DME (or RDL 131 LMT VOR/27NM RCA DME) – RDL 131/29 NM LMT VOR/DME: TRL(1)"));
        Assert.Equal("IDONA - TR 311° (RDL 131 LMT VOR) - LMT VOR/DME", ItalianAd2Prose.Route(
            "IDONA – TR 311° (RDL 131 LMT VOR) – LMT VOR/DME\nRDL 131/34 NM LMT VOR/DME (or RDL 131 LMT VOR/27NM RCA DME) – RDL 131/29 NM LMT VOR/DME\n(or RDL 131 LMT VOR/23NM RCA DME): TRL(1)"));
        // Con una parentesi aperta sulla riga prima, la riga che comincia con un punto ne fa parte (LIPO LICFE 6C).
        Assert.EndsWith("or RDL 319/19 NM RCA VOR/DME).", ItalianAd2Prose.Route(
            "Turn right to intercept and follow RDL 139 LMT VOR inbound to SUGEP (RDL 139/49.4NM LMT VOR/DME or\nRDL319/19NM RCA VOR/DME)."), StringComparison.Ordinal);
    }
}
