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
        Assert.Equal(["IF DELIK", "CF LMT", "FD RDL 258/14 NM LMT", "CF SUGEP"], tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
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
        Assert.Equal(["IF ARBUM", "FD RDL 312/17 NM LMT", "CD RDL 312/15 NM LMT", "AF RDL 283/15 NM LMT", "CF SUGEP"],
            tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
        var arco = tratti[3];
        Assert.Equal(("LMT", 15.0, "L"), (arco.ArcCenter, arco.ArcRadiusNm!.Value, arco.Turn));
        Assert.Equal(15, Distanza(Stazioni["LMT"].Position, arco.Position!.Value), 2);
        Assert.Equal(286, Rotta(Stazioni["LMT"].Position, arco.Position!.Value), 1);
        // Passo 8: dove l'arco finisce, per ATCSim.
        Assert.Equal(("LMT", 283.0, 15.0), (arco.Navaid, Math.Round(arco.Radial!.Value), arco.DmeNm));
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
        Assert.Equal(["IF ARBUM", "FD RDL 312/17 NM LMT", "CD RDL 312/15 NM LMT", "AF RDL 283/15 NM LMT", "CF SUGEP"],
            tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
        Assert.Equal("L", tratti[4].Turn);
    }

    // B5: «Crossing» maiuscolo dopo una radiale: l'incrocio (LIEE CAR 1F andava dritta al punto dopo).
    [Fact]
    public void CrossingMaiuscolo_ELIncrocio()
    {
        var (tratti, problema) = Leggi(P(ProcedureKind.Star, "LMT 1F", "LMT VOR/DME - TR 120° (RDL 120 LMT VOR). Crossing RDL 020 RCA VOR turn right bound to SUGEP."));

        Assert.Null(problema);
        Assert.Equal(["IF LMT", "CR INT RDL 120 LMT/RDL 020 RCA", "DF SUGEP"], tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
        Assert.Equal("R", tratti[2].Turn);
    }

    // B5: «Over TOP VOR proceed …», «Initial climb procedure completed …», «as soon as practicable», «Leave LMT VOR on RDL 130»:
    // si leggono come «da LMT», «dopo il decollo», niente, «LMT, poi la radiale 130 di LMT».
    [Theory]
    [InlineData("Over LMT VOR proceed on RDL 130 LMT VOR (TR 130°) until 14 NM LMT DME, then turn right bound to SUGEP.", "IF LMT|FD RDL 130/14 NM LMT|DF SUGEP")]
    [InlineData("Initial climb procedure completed turn left (IAS MAX 200 kt during turn) bound to LMT VOR. Leave LMT VOR on RDL 130. At 14NM LMT DME turn right bound to SUGEP.", "IF LMT|FD RDL 130/14 NM LMT|DF SUGEP")]
    [InlineData("After take-off, as soon as practicable, turn left bound to LMT VOR/DME, to be left on RDL 130 LMT VOR until 14 NM LMT DME, then turn right bound to SUGEP.", "IF LMT|FD RDL 130/14 NM LMT|DF SUGEP")]
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
    [InlineData("crossing RDL 300 RCA VOR turn left bound to SUGEP.", "non incrocia la RDL 300 RCA")]   // passo 6: si calcola, se c'è
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
    [InlineData("DELIK - TR 310 (RDL 130 LMT VOR) until 14 NM LMT DME, then turn right bound to SUGEP.", "IF DELIK|FD RDL 130/14 NM LMT|DF SUGEP")]
    [InlineData("DELIK - TR 310° bound to LMT VOR to be left on RDL 130 until 14 NM LMT DME, then turn right bound to SUGEP.", "IF DELIK|CF LMT|FD RDL 130/14 NM LMT|DF SUGEP")]
    [InlineData("DELIK - TR 310° (RDL 130 LMT VORTAC) until crossing RDL 020 RCA VORTAC (or 14 NM LMT DME), then turn right bound to SUGEP.", "IF DELIK|CR INT RDL 130 LMT/RDL 020 RCA|DF SUGEP")]
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
        Assert.Equal(["IF DELIK", "FD RDL 130/14 NM LMT", "DF SUGEP"], tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
        Assert.Equal(["prua 040° disegnata come rotta, senza vento"], avvisi);
        // Senza prua, niente da dire.
        Assert.Empty(ItalianAd2Prose.DecodeWithWarnings(P(ProcedureKind.Star, "DELIK 1T", "DELIK – TR 310° (RDL 130 LMT VOR) bound to SUGEP."),
            n => Punti.TryGetValue(n, out var x) ? x : null, n => Stazioni.GetValueOrDefault(n)).Warnings);
    }

    // Segnalazioni 2611, passo 8 (committente, per ATCSim): un tratto verso un punto calcolato ha il tipo dell'ARINC 424 e dice
    // la stazione, la radiale e la distanza DME dove finisce; la posizione resta, e si disegna come prima. «bound to» dopo un
    // tratto così è un DF.
    [Theory]
    [InlineData("DELIK – TR 310° (RDL 130 LMT VOR) until 14 NM LMT DME, then turn right bound to SUGEP.", "FD", "LMT", 130.0, 14.0)]
    [InlineData("DELIK – TR 310° (RDL 130 LMT VORTAC) until crossing RDL 020 RCA VORTAC, then turn right bound to SUGEP.", "CR", "RCA", 20.0, null)]
    [InlineData("LMT VOR – heading 040° until 10 NM LMT DME, then turn right bound to SUGEP.", "VD", "LMT", null, 10.0)]
    public void IlTipoDelTratto_ELaStazioneDoveFinisce(string prosa, string tipo, string stazione, double? radiale, double? dme)
    {
        var (tratti, problema, _) = ItalianAd2Prose.DecodeWithWarnings(P(ProcedureKind.Star, "DELIK 1Z", prosa),
            n => Punti.TryGetValue(n, out var x) ? x : null, n => Stazioni.GetValueOrDefault(n));

        Assert.Null(problema);
        var t = tratti[1];
        Assert.Equal((tipo, stazione, dme), (t.PathTerminator, t.Navaid, t.DmeNm));
        Assert.NotNull(t.Position);
        if (radiale is { } r)
            Assert.Equal(r, t.Radial!.Value, 1);
        else if (dme is not null)
            Assert.Equal(dme.Value, Distanza(Stazioni[stazione].Position, t.Position!.Value), 2);
        Assert.Equal("DF SUGEP", $"{tratti[2].PathTerminator} {tratti[2].Fix}");
    }

    // Passo 8 (committente: «per tutte le STAR»): la MEA che cambia su un punto DME (LICB ENEPA 1T, «ENEPA – RDL 327/25NM COM
    // VOR/DME: 6000FT; RDL327/25NM COM VOR/DME - COM VOR/DME: 5000FT») spezza il tratto lì; ogni tratto ha la MEA del suo pezzo.
    [Fact]
    public void LaMeaCheCambiaSuUnPuntoDme_SpezzaIlTratto()
    {
        var star = P(ProcedureKind.Star, "DELIK 1T", "DELIK – TR 308° (RDL 128 LMT VOR) – LMT VOR/DME.") with
        {
            Minimums = "MEA: DELIK – RDL 128/14NM LMT VOR/DME: 6000FT; RDL128/14NM LMT VOR/DME - LMT VOR/DME: 5000FT",
        };

        var (tratti, problema, _) = ItalianAd2Prose.DecodeWithWarnings(star, n => Punti.TryGetValue(n, out var x) ? x : null, n => Stazioni.GetValueOrDefault(n));

        Assert.Null(problema);
        Assert.Equal(["IF DELIK", "FD RDL 128/14 NM LMT 308° 6000FT", "CF LMT 308° 5000FT"],
            tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}{(t.Course is { } c ? " " + c : "")}{(t.MinimumAltitude is { } q ? " " + q : "")}"));
        Assert.Equal(("LMT", 128.0, 14.0), (tratti[1].Navaid, tratti[1].Radial!.Value, tratti[1].DmeNm!.Value));
        Assert.Equal(14, Distanza(Stazioni["LMT"].Position, tratti[1].Position!.Value), 2);
        // Il punto c'è già (una STAR che lo dice): non si spezza, ma la MEA va sui tratti.
        var detto = ItalianAd2Prose.DecodeWithWarnings(star with { Description = "DELIK – TR 308° (RDL 128 LMT VOR) until 14 NM LMT DME, then LMT VOR/DME." },
            n => Punti.TryGetValue(n, out var x) ? x : null, n => Stazioni.GetValueOrDefault(n)).Legs;
        Assert.Equal([null, "6000FT", "5000FT"], detto.Select(t => t.MinimumAltitude));
    }

    // Segnalazioni 2611, B6: «on TR 073° until joining RDL …» — il punto dove la rotta prende la radiale (prima si andava dritti
    // al punto dopo). Non dopo una virata («then turn left to join RDL …»: lì la rotta di prima non vale più).
    [Fact]
    public void LaRadialeSiIntercetta_DaUnaRottaPresaDopoLaVirata()
    {
        var (tratti, problema) = Leggi(P(ProcedureKind.Star, "DELIK 1J",
            "DELIK – TR 310° (RDL 130 LMT VOR) until 14 NM LMT DME, then turn right on TR 300° until joining RDL 250 LMT VOR bound to SUGEP."));

        Assert.Null(problema);
        Assert.Equal(["IF DELIK", "FD RDL 130/14 NM LMT", "CR INT TR 300°/RDL 250 LMT", "DF SUGEP"], tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
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
        Assert.Equal(["CR INT TR 247°/RDL 278 OST", "DF NENIG"], tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
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
        Assert.Equal(["VD RWY heading/1 NM OST DME", "DF NENIG"], tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
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
        Assert.Equal(["IF DELIK", "FD RDL 130/14 NM LMT", "DF SUGEP"], tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
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
        Assert.Equal(["IF DELIK", "FD RDL 130/14 NM LMT", "DF SUGEP"], tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
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
        Assert.Equal(["IF DELIK", "FD RDL 130/14 NM LMT", "DF SUGEP"], tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
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
        Assert.Equal(["IF ARBUM", "FD RDL 312/17 NM LMT", "CD RDL 312/15 NM LMT", "AF RDL 283/15 NM LMT", "CF SUGEP"],
            tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
    }

    // Gruppo 4 e LIPZ: i verbi nuovi.
    [Theory]
    [InlineData("P roceed to LMT VOR/DME, to be left on RDL 130 LMT VOR until 14 NM LMT DME, then turn right bound to SUGEP.", "IF LMT|FD RDL 130/14 NM LMT|DF SUGEP")] // R3
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
    [InlineData(ProcedureKind.Star, "Leave DELIK on TR 310° (RDL 130 LMT VOR), then turn right bound to SUGEP.", "IF DELIK|TF SUGEP")]
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
    [InlineData("DELIK – TR 310\" (RDL 130 LMT VOR) until 14 NM LMT DME, then turn right bound to SUGEP.", "«TR 310\"»", "IF DELIK|FD RDL 130/14 NM LMT|DF SUGEP")]
    [InlineData("DELIK – 310° (RDL 130 LMT VOR) until 14 NM LMT DME, then turn right bound to SUGEP.", "«310°» senza «TR»", "IF DELIK|FD RDL 130/14 NM LMT|DF SUGEP")]
    [InlineData("DELIK – TR 310° … (RDL 130 LMT VOR) until 14 NM LMT DME, then turn right bound to SUGEP.", "«TR 310° …", "IF DELIK|FD RDL 130/14 NM LMT|DF SUGEP")]
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
        Assert.Equal("FD RDL 087/3 NM LMT", $"{tratti[1].PathTerminator} {tratti[1].Computed}");
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

    // Passo 2 della revisione col committente (carta §6, gruppo 5): «non si butta niente». La quota, la velocità e la nota
    // vanno nel tratto dove stanno; il percorso resta quello di prima.
    [Fact]
    public void QuoteVelocitaENote_NelTratto()
    {
        var (tratti, problema) = Leggi(P(ProcedureKind.Sid, "SUGEP 1A",
            "After take-off proceed on RDL 130 LMT VOR. At 14 NM LMT DME (to be crossed at 2200 ft or above) turn right (IAS MAX 220 kt during turn) bound to SUGEP, if cleared by ATC."));

        Assert.Null(problema);
        Assert.Equal(["IF RDL 130/14 NM LMT", "TF SUGEP"], tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
        Assert.Equal(("+2200", "-220", "IAS MAX 220 KT during turn"), (tratti[0].Altitude, tratti[0].Speed, tratti[0].Notes));
        Assert.Equal("if cleared by ATC", tratti[1].Notes);
    }

    [Theory]
    [InlineData("Cross SUGEP at FL 110 or above, as cleared.", "+FL110", "as cleared")]
    [InlineData("Cross SUGEP as cleared by ATC.", null, "as cleared by ATC")]
    [InlineData("Then climb in the holding pattern until reaching MEL for next AWY segment.", null, "climb in the holding pattern until reaching MEL for next AWY segment")]
    public void LeQuoteELeAutorizzazioniDopoIlPunto(string dopo, string? quota, string nota)
    {
        var (tratti, problema) = Leggi(P(ProcedureKind.Star, "DELIK 1T", "DELIK – TR 310° (RDL 130 LMT VOR) until 14 NM LMT DME, then turn right bound to SUGEP, " + dopo.ToLowerInvariant()[..1] + dopo[1..]));

        Assert.Null(problema);
        Assert.Equal("SUGEP", tratti[^1].Fix);
        Assert.Equal(quota, tratti[^1].Altitude);
        Assert.Contains(nota, tratti[^1].Notes, StringComparison.Ordinal);
    }

    // «not below 1500 ft» subito dopo un punto è la sua quota; senza un punto la virata è a una quota (passo 5) e si ferma.
    [Fact]
    public void NonSotto_DopoUnPunto_OppureSiFerma()
    {
        var (tratti, problema) = Leggi(P(ProcedureKind.Sid, "SUGEP 1A", "Proceed on RDL 130 LMT VOR, at 14 NM LMT DME, not below 1500 ft, turn right bound to SUGEP."));
        var (_, fermo) = Leggi(P(ProcedureKind.Sid, "SUGEP 1A", "Proceed on RDL 130 LMT VOR, not below 1500 ft, turn right bound to SUGEP."));

        Assert.Null(problema);
        Assert.Equal("+1500", tratti[0].Altitude);
        Assert.Contains("virata è a una quota", fermo, StringComparison.Ordinal);
    }

    // Q3: «suggested Track» si disegna come la rotta e si dice; Q4: «(in any case within 14NM LMT DME)» è il punto radiale/DME.
    [Fact]
    public void LaRottaSuggerita_EIlPuntoInOgniCaso()
    {
        var (tratti, problema, avvisi) = ItalianAd2Prose.DecodeWithWarnings(P(ProcedureKind.Star, "DELIK 1T",
                "DELIK – suggested Track 310° (RDL 130 LMT VOR). Crossing RDL 100 LMT VOR (in any case within 14NM LMT DME) turn right bound to SUGEP."),
            n => Punti.TryGetValue(n, out var x) ? x : null, n => Stazioni.GetValueOrDefault(n));

        Assert.Null(problema);
        Assert.Equal(["IF DELIK", "FD RDL 100/14 NM LMT", "DF SUGEP"], tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
        Assert.Contains("in any case within 14 NM LMT DME", tratti[1].Notes, StringComparison.Ordinal);
        Assert.Contains(avvisi, a => a.Contains("rotta suggerita", StringComparison.Ordinal));
    }

    // «then left turn» al contrario dà il verso (LICR FOSQA 2A); la nota sulla stessa riga chiude il percorso (LIBF URIPI 6A).
    [Fact]
    public void LaVirataAlContrario_ELaNotaInCoda()
    {
        var (tratti, problema) = Leggi(P(ProcedureKind.Star, "DELIK 1T",
            "DELIK – TR 310° (RDL 130 LMT VOR) until 14 NM LMT DME, then left turn bound to SUGEP. Minimum climb gradient: 6.8% (413 ft/NM) up to 5000 ft. FL 110"));

        Assert.Null(problema);
        Assert.Equal("L", tratti[^1].Turn);
    }

    // Una SID che non dice dove finisce finisce sul punto del suo nome (LIPB VIC 6B; committente, 23 ottobre).
    [Fact]
    public void UnaSidCheNonDiceDoveFinisce_FinisceSulPuntoDelSuoNome()
    {
        var (tratti, problema) = Leggi(P(ProcedureKind.Sid, "SUGEP 1A", "Proceed on RDL 130 LMT VOR. At 14 NM LMT DME turn left to join RDL 093 LMT VOR."));

        Assert.Null(problema);
        Assert.Equal(["IF RDL 130/14 NM LMT", "TF SUGEP"], tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
    }

    // Il percorso continua sulla riga che comincia con «Passing» (LIMF TOP 1B: prima finiva lì e la virata spariva, con un
    // punto solo); «Cross EKTOL» a fine riga è passare sul punto (LIBF EKTOL 6A).
    [Fact]
    public void LaRigaCheCominciaConPassing_EIlCrossDaSolo()
    {
        Assert.Contains("Passing 4000 ft", ItalianAd2Prose.Route(
            "After take-off join RDL 180 LMT VOR (TR 180°), crossing point RDL 180/6NM LMT VOR/DME at 2100 ft or above.\nPassing 4000 ft turn left bound to SUGEP."), StringComparison.Ordinal);
        var (tratti, problema) = Leggi(P(ProcedureKind.Sid, "SUGEP 1A", "Join and follow RDL 130 LMT VOR (TR 130°) bound to SUGEP. Cross SUGEP\nat FL 110 or above, as cleared."));

        Assert.Null(problema);
        Assert.Equal("SUGEP", tratti[^1].Fix);
    }

    // Passo 3 della revisione col committente (SC3): la rotta vera stampata «NIL» in AD 2.12 (LICG, LIDT, LIBD, LIPY): la
    // direzione della pista va dalla sua soglia a quella opposta.
    [Theory]
    [InlineData("07", "25")]
    [InlineData("25", "07")]
    [InlineData("18", "36")]
    [InlineData("36", "18")]
    [InlineData("22L", "04R")]
    [InlineData("16C", "34C")]
    public void LaPistaOpposta(string pista, string opposta) => Assert.Equal(opposta, ItalianAd2Prose.PistaOpposta(pista));

    [Fact]
    public void LaRottaDellaPistaStampataNil_DalleDueSoglie()
    {
        var scalo = new Aerodrome
        {
            Icao = "LIXX", Name = "Prova",
            Runways =
            [
                new() { Designator = "07", TrueBearing = "NIL", Threshold = new Position(39.0, 16.0) },
                new() { Designator = "25", TrueBearing = "NIL", Threshold = new Position(39.0, 16.03) },
            ],
        };

        var pista = ItalianAd2Prose.Pista(scalo, "07");

        Assert.NotNull(pista);
        Assert.Equal(90, pista.TrueBearing, 0);
    }

    // La declinazione quando il testo non nomina un VOR: dall'NDB nominato, se no dallo scalo (LIRS ILOWE 1W, LIEE KOVAS 2C).
    [Fact]
    public void UnaRottaSenzaVor_LaDeclinazioneDalloScalo()
    {
        var (tratti, problema, avvisi) = ItalianAd2Prose.DecodeWithWarnings(P(ProcedureKind.Star, "DELIK 1T", "DELIK – TR 199° (RDL 019 VOR) bound SUGEP."),
            n => Punti.TryGetValue(n, out var x) ? x : null, n => Stazioni.GetValueOrDefault(n), aerodromeVariation: 3);
        var (_, senza) = Leggi(P(ProcedureKind.Star, "DELIK 1T", "DELIK – TR 199° bound SUGEP."));

        Assert.Null(problema);
        Assert.Equal(["IF DELIK", "CF SUGEP"], tratti.Select(t => $"{t.PathTerminator} {t.Fix}"));
        Assert.Contains(avvisi, a => a.Contains("senza la stazione", StringComparison.Ordinal));
        Assert.Contains("declinazione", senza, StringComparison.Ordinal);
    }

    // Passo 4 della revisione col committente (SC1): la salita iniziale divisa in parte comune e rami per SID.
    [Fact]
    public void LaSalitaIniziale_LaParteComuneEIRami()
    {
        var divisa = ItalianAd2Prose.Dividi("After take-off proceed runway heading and passing 1100 ft turn right to join assigned SID as reported below: "
            + "a) on TR 262° if cleared via SID EKTOL 6A; b) on TR 010° if cleared via SID URIPI 6A. Turn and climb parameters: a) Departure turn limited to 210 KT; "
            + "b) Minimum climb gradient: 6.8%.");

        Assert.StartsWith("After take-off proceed runway heading and passing 1100 ft turn right", divisa.Comune, StringComparison.Ordinal);
        Assert.Equal(2, divisa.Rami.Count);
        Assert.Contains("URIPI 6A", divisa.Rami[1].Condizione, StringComparison.Ordinal);
        Assert.Contains("on TR 010°", divisa.Rami[1].Testo, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("OTRET 8G", "turn right on TR 043°")]
    [InlineData("INKIM9G", "turn right on TR 043°")]
    [InlineData("ASPIR 8G", "turn left on TR 160°")]   // «if cleared for other SID»
    public void LaSalitaIniziale_IlRamoDellaSid(string sid, string atteso)
    {
        var salita = new Procedure
        {
            Aerodrome = "LIXX", Kind = ProcedureKind.InitialClimb, Designator = "RWY 22", Runways = ["22"],
            Description = "After take-off, proceed on RDL 213 PES VOR until 4 NM PES DME, then: - if cleared for SID INKIM 9G or OTRET 8G: turn right on TR 043°; "
                + "- if cleared for other SID: turn left on TR 160°.",
        };

        var (testo, perche) = ItalianAd2Prose.SalitaPerLaSid(sid, [salita]);

        Assert.Null(perche);
        Assert.StartsWith("After take-off, proceed on RDL 213 PES VOR until 4 NM PES DME", testo, StringComparison.Ordinal);
        Assert.Contains(atteso, testo, StringComparison.Ordinal);
    }

    // Passo 8 (LICB): i rami col trattino e la condizione in coda («- direct to COM VOR … if cleared via OBAXU 5A, … e ROMSU 5A;»,
    // con la «e» italiana), non le righe delle pendenze; e la salita che dice a chi non vale («except NELDA 6E»).
    [Theory]
    [InlineData("OBAXU 5A", "RWY 05", "direct to COM VOR")]
    [InlineData("ROMSU 5A", "RWY 05", "direct to COM VOR")]
    [InlineData("OBAXU 5B", "RWY 05", "to join RDL 327 COM VOR")]
    [InlineData("OBAXU 5A", "RWY 23", "bound to COM VOR")]
    [InlineData("NELDA 6E", "RWY 23", null)]
    public void LaSalitaIniziale_RamiColTrattinoEdEccezioni(string sid, string pista, string? atteso)
    {
        var salita = pista == "RWY 05"
            ? "After take off at 1200ft turn left (IAS MAX 185kt):\n- direct to COM VOR to reach at 3500ft or above if cleared\nvia OBAXU 5A, ENEPA 5A, NELDA 6D e ROMSU 5A;\n"
                + "- to join RDL 327 COM VOR if cleared via OBAXU 5B e ENEPA 5B.\nRemarks 1. Minimum climb gradient: 8.5% (515ft/NM) till passing 2000ft"
            : "After take off , except NELDA 6E, turn right (IAS MAX 185KT) bound to COM VOR.\nMinimum climb gradient: - 300 ft/NM (4.9%) if cleared via SID KOVAS 6E.";
        var procedura = new Procedure { Aerodrome = "LIXX", Kind = ProcedureKind.InitialClimb, Designator = pista, Runways = [pista[4..]], Description = salita };

        var (testo, _) = ItalianAd2Prose.SalitaPerLaSid(sid, [procedura]);

        if (atteso is null)
        {
            Assert.Null(testo);
            Assert.False(ItalianAd2Prose.ValePer(salita, sid));
            return;
        }
        Assert.Contains(atteso, testo, StringComparison.Ordinal);
        Assert.DoesNotContain("except", testo, StringComparison.Ordinal);
        Assert.True(ItalianAd2Prose.ValePer(salita, sid));
        // Le righe delle pendenze non sono rami.
        Assert.DoesNotContain(ItalianAd2Prose.Dividi(salita).Rami, r => r.Condizione.Contains("KOVAS", StringComparison.Ordinal));
    }

    // Passo 8 (LICB RWY 05): «After take off at 1200ft turn left … direct to COM VOR to reach at 3500ft or above»: dritti dalla
    // pista fino alla quota (VA), poi diretti alla stazione (DF). E «RDL 231 (TR 231°) COM VOR»: COM è la stazione della radiale.
    [Fact]
    public void DopoIlDecolloAUnaQuota_ELaStazioneDopoLaRotta()
    {
        var soglia = Verso(Stazioni["LMT"].Position, 270, 3);
        var fine = Verso(soglia, 90, 1.5);
        var (tratti, problema, _) = LeggiDecollando("After take off at 1200ft turn left (IAS MAX 185kt) direct to LMT VOR to reach at 3500ft or above.",
            new ItalianAd2Prose.Runway(soglia, 90), new ItalianAd2Prose.Departure(fine, 200, 500, "500 ft/NM", false));

        Assert.Null(problema);
        Assert.Equal(["VA 1200 ft", "DF LMT"], tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
        Assert.Contains("to join RDL 231 LMT VOR (TR 231°) bound to SUGEP",
            ItalianAd2Prose.Normalizza("Initial climb procedure executed, turn right to join RDL 231 (TR 231°) LMT VOR bound to SUGEP."), StringComparison.Ordinal);
    }

    // Revisione 2 delle frasi non capite (committente, 24 ottobre): le forme nuove, coi testi del 2611.
    [Theory]
    [InlineData("LMT VOR - heading 040°. When at D3 LMT TACAN turn right bound to SUGEP.", "IF LMT|VD TR 040°/3 NM LMT DME|DF SUGEP")]                        // LIEE KOVAS 7A
    [InlineData("LMT VOR - TR 040° until 3 DME LMT (8.5 NM RCA DME), then turn right bound to SUGEP.", "IF LMT|FD TR 040°/3 NM LMT DME|DF SUGEP")]                  // LIBD
    [InlineData("LMT VOR - TR 040° until 3 NM LMT DME, to be reached not below 1000 ft, then turn left/right bound to SUGEP.", "IF LMT|FD TR 040°/3 NM LMT DME|DF SUGEP")] // LIEE, LIPX
    [InlineData("DELIK - TR 310° until joining 090 LMT VOR bound to SUGEP.", "IF DELIK|CR INT TR 310°/RDL 090 LMT|DF SUGEP")]                                     // LIPY KEGED 9H
    [InlineData("DELIK - TR 310° (RDL 130 LMT VOR) (IAS MAX 210) until 14 NM LMT DME, then turn right bound to SUGEP.", "IF DELIK|FD RDL 130/14 NM LMT|DF SUGEP")]                // LIPY LANLI 4C
    public void LeFrasiDellaRevisione2_SiLeggono(string prosa, string attesi)
    {
        var (tratti, problema, _) = ItalianAd2Prose.DecodeWithWarnings(P(ProcedureKind.Star, "DELIK 1R", prosa),
            n => Punti.TryGetValue(n, out var x) ? x : null, n => Stazioni.GetValueOrDefault(n));

        Assert.Null(problema);
        Assert.Equal(attesi.Split('|'), tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
    }

    // Revisione 3 (committente, 24 ottobre): «bound to BAR VOR. At 2 NM BAR DME turn left» si vira prima della stazione, sulla
    // radiale su cui si arriva (LIBD BANAV 8B, LIEO POZZO 7D); «RDL 273 (TR 093°) bound to BAR VOR» senza la stazione è di BAR;
    // «turn right immediately» è la virata, «passing RDL» è «crossing RDL» (LIRQ).
    [Theory]
    [InlineData("ARBUM - TR 132° (RDL 312 LMT VOR) bound to LMT VOR. At 3 NM LMT DME turn right bound to SUGEP.", "IF ARBUM|FD RDL 312/3 NM LMT|DF SUGEP", false)]
    [InlineData("ARBUM - RDL 312 (TR 132°) bound to LMT VOR. At 3 NM LMT DME turn right bound to SUGEP.", "IF ARBUM|FD RDL 312/3 NM LMT|DF SUGEP", true)]
    [InlineData("ARBUM - TR 132° (RDL 312 LMT VOR) until 17 NM LMT DME, then turn right immediately bound to SUGEP.", "IF ARBUM|FD RDL 312/17 NM LMT|DF SUGEP", false)]
    [InlineData("DELIK - TR 310° (RDL 130 LMT VOR), then passing RDL 020 RCA VOR turn right bound to SUGEP.", "IF DELIK|CR INT RDL 130 LMT/RDL 020 RCA|DF SUGEP", false)]
    public void LeFrasiDellaRevisione3_SiLeggono(string prosa, string attesi, bool erroreDiStampa)
    {
        var (tratti, problema, avvisi) = ItalianAd2Prose.DecodeWithWarnings(P(ProcedureKind.Star, "ARBUM 1R", prosa),
            n => Punti.TryGetValue(n, out var x) ? x : null, n => Stazioni.GetValueOrDefault(n));

        Assert.Null(problema);
        Assert.Equal(attesi.Split('|'), tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
        Assert.Equal(erroreDiStampa, avvisi.Any(a => a.Contains("errore di stampa", StringComparison.Ordinal)));
    }

    // Revisione 4 (LIDT): un numero grande non è una declinazione (il fax dell'ENAC nel campo dell'AD 2.2).
    [Theory]
    [InlineData("3° E-2010.0", 3.0)]
    [InlineData("1°W (2015)", -1.0)]
    [InlineData("TRENTINO TRASPORTI S.p.A Tel +39 0461 944355 fax +39 0461 946665 E-mail: airport@trentinotrasporti.it", null)]
    public void LaDeclinazioneDelloScalo(string stampata, double? attesa) => Assert.Equal(attesa, ItalianAd2Prose.Variation(stampata));

    // Revisione 4 (LIEE KOVAS 6R): «(TR Turn left … (TR QDR 298°)» è un errore di stampa, letto e detto.
    [Fact]
    public void LaRottaColQdrEUnaParentesiDiTroppo_ErroriDiStampa()
    {
        var (tratti, problema, avvisi) = ItalianAd2Prose.DecodeWithWarnings(P(ProcedureKind.Star, "DELIK 1Q",
            "DELIK - (TR Turn left on RDL 130 LMT VOR (TR QDR 310°) until 14 NM LMT DME, then turn right bound to SUGEP."),
            n => Punti.TryGetValue(n, out var x) ? x : null, n => Stazioni.GetValueOrDefault(n));

        Assert.Null(problema);
        Assert.Equal(["IF DELIK", "FD RDL 130/14 NM LMT", "DF SUGEP"], tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
        Assert.Contains("«(TR» davanti a «Turn» è un errore di stampa: tolto", avvisi);
        Assert.Contains(avvisi, a => a.Contains("«(TR QDR 310°)» è un errore di stampa", StringComparison.Ordinal));
    }

    // Revisione 3: la salita a vista della 05 di LIRQ comincia dopo «as follows:», senza «After take-off»; la prima rotta dopo il
    // decollo dice di che pista è una SID che comincia come una salita (LICR).
    [Fact]
    public void LaSalitaAVista_ELaPrimaRottaDopoIlDecollo()
    {
        var aVista = "Following aerodrome meteorological minima shall be applied. Due to ground elevation, take off and initial climb from "
            + "RWY 05 must be performed during daylight only and under visual conditions as follows: as soon as possible turn left on TR 237° "
            + "until intercepting and following RDL 267 PRT VOR. REMARKS 1. Turn must be completely performed within 3 NM PRT DME.";

        Assert.True(ItalianAd2Prose.HaUnPercorso(aVista));
        Assert.StartsWith("as soon as possible turn left on TR 237°", ItalianAd2Prose.Dividi(aVista).Comune, StringComparison.Ordinal);
        Assert.Equal("164", ItalianAd2Prose.RottaDopoIlDecollo("After takeoff proceed on track 164°, reaching 800ft turn right"));
        Assert.Equal("164", ItalianAd2Prose.RottaDopoIlDecollo("A fter take-off proceed on track 164°, reaching 800ft:"));
        Assert.Null(ItalianAd2Prose.RottaDopoIlDecollo("turn left as soon as possible after take-off (IAS MAX 200 kt during turn) on track 164°"));
    }

    // Revisione 2: «… (or 7 NM FAL TACAN) Remark: Minimum climb gradient: 425 ft/NM (7%).» (LIPY RWY 04): la nota chiude il percorso.
    [Fact]
    public void UnaSalitaConLaNotaInCoda_SenzaIlPunto()
    {
        var salita = new Procedure
        {
            Aerodrome = "LIXX", Kind = ProcedureKind.InitialClimb, Designator = "RWY 04", Runways = ["04"],
            Description = "After take-off proceed on TR 041° until 3 NM LMT DME (or 7 NM RCA TACAN)\nRemark: Minimum climb gradient: 425 ft/NM (7%).",
        };
        var soglia = Verso(Stazioni["LMT"].Position, 221, 6);

        var (tratti, problema, _) = ItalianAd2Prose.DecodeWithWarnings(salita, n => Punti.TryGetValue(n, out var x) ? x : null,
            n => Stazioni.GetValueOrDefault(n), new ItalianAd2Prose.Runway(soglia, 44), aerodromeVariation: 3);

        Assert.Null(problema);
        Assert.Equal(["CD TR 041°/3 NM LMT DME"], tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
    }

    [Theory]
    [InlineData("At 13NM PRU DME (12.4NM IPER DME) turn right until joining RDL 224 ANC VOR/DME (TR 224°) bound to GITOD.", true)]
    [InlineData("Turn left on TR 132°. Crossing RDL 166 BSM VOR turn left on RDL 133 BSM VOR.", true)]
    [InlineData("(ATC discretion)\nInitial climb procedure completed turn left until joining RDL 003 ALG VOR (TR 183°).", true)]
    [InlineData("Proceed on TR 041° until 7 NM IFA DME, then turn right to join RDL 039 ANC VOR (TR 219°).", false)]
    [InlineData("After take-off proceed on RWY heading until 2 NM TES DME.", false)]
    // Giro del 26 ottobre: anche dopo le note fra parentesi, col punto o senza (LIPX RWY 22, LIEA MINKA 9C).
    [InlineData("(ATC discretion)\nTurn left on TR 089° until joining RDL 137 VIL VOR bound to ITEFE.", true)]
    [InlineData("(On pilot’s request only).\nInitial climb procedure completed turn right on TR 115°.", true)]
    public void UnaSidCheNonPorteDallaPista(string testo, bool continua) => Assert.Equal(continua, ItalianAd2Prose.ContinuaLaSalita(testo));

    // Giro del 26 ottobre: la rotta con cui comincia una SID senza «After take-off».
    [Theory]
    [InlineData("(ATC discretion)\nProceed on TR 276° until intercepting and following RDL 306 BCS VOR (TR 306°) bound to DOLON.", 276d)]
    [InlineData("(4)\nProceed on track 108° to join and follow RDL 138 LIN VOR.", 108d)]
    [InlineData("Proceed on heading 160° until joining RDL 098 GRO VOR bound to GIKIN.", 160d)]
    [InlineData("After take-off proceed on TR 164°.", null)]
    [InlineData("Proceed on RDL 198 RCA VOR bound to BERMI.", null)]
    public void LaRottaDiPartenza(string testo, double? rotta) => Assert.Equal(rotta, ItalianAd2Prose.RottaDiPartenza(testo));

    // Giro del 26 ottobre (le «SID dalla soglia» delle segnalazioni del 2611, LIBR RWY 13): una SID che comincia con una rotta
    // lontana dalla pista, senza «After take-off», continua la salita iniziale della pista; la virata dopo il decollo è la sua.
    // «1500ft AMSL» è una quota, non un punto (LIMC RWY 17L/17R).
    [Fact]
    public void LaSidCheCominciaConUnaRottaLontana_ContinuaLaSalita()
    {
        var documento = new AipDocument
        {
            State = "LI", Airac = "2611", Airspaces = [],
            Points = [new SignificantPoint { Section = "ENR 4.4", Designator = "SUGEP", Position = Punti["SUGEP"] }],
            Aerodromes =
            [
                new Aerodrome
                {
                    Icao = "LIXX", Name = "Prova", MagneticVariation = "3° E (2020.0)",
                    NavigationAids = [new Navaid { Section = "AD 2.19", Designator = "LMT", Name = "LAMEZIA", Type = "DVOR/DME", MagneticVariation = "3° E-2010.0", Position = Stazioni["LMT"].Position, Components = [] }],
                    Runways = [new() { Designator = "13", TrueBearing = "133°", Threshold = new Position(38.95, 15.90) }],
                },
            ],
            Procedures =
            [
                new Procedure { Aerodrome = "LIXX", Kind = ProcedureKind.InitialClimb, Designator = "RWY 13", Runways = ["13"],
                    Description = "After take off turn right until joining RDL 130 LMT VOR (TR 130°) until 8NM LMT DME." },
                new Procedure { Aerodrome = "LIXX", Kind = ProcedureKind.Sid, Designator = "SUGEP 1G", Runways = ["13"],
                    Description = "(ATC discretion)\nProceed on TR 040° bound to SUGEP." },
            ],
        };

        var (letto, nonLette, avvisi) = ItalianAd2Prose.DecodeAll(documento);

        var sid = letto.Procedures!.Single(p => p.Designator == "SUGEP 1G");
        Assert.Equal("RDL 130/8 NM LMT", sid.Legs[0].Fix ?? sid.Legs[0].Computed);
        Assert.Equal("SUGEP", sid.Legs[^1].Fix);
        Assert.DoesNotContain(avvisi, a => a.Warning.Contains("soglia", StringComparison.Ordinal));
        Assert.Empty(nonLette);

        var conAmsl = new Procedure { Aerodrome = "LIXX", Kind = ProcedureKind.InitialClimb, Designator = "RWY 13", Runways = ["13"],
            Description = "After take-off proceed on TR 130° until passing 1500ft AMSL, then turn right to join the assigned SID." };
        // Senza il decollo (la pendenza) la virata a una quota non si disegna: qui conta che AMSL non sia preso per un punto.
        var (_, problema, _) = ItalianAd2Prose.DecodeWithWarnings(conAmsl, n => Punti.TryGetValue(n, out var x) ? x : null,
            n => Stazioni.GetValueOrDefault(n), new ItalianAd2Prose.Runway(new Position(38.95, 15.90), 133), aerodromeVariation: 3);
        Assert.StartsWith("«until passing 1500 ft»", problema, StringComparison.Ordinal);
    }

    // SC1 e SC2 dentro DecodeAll: la salita iniziale ha i suoi tratti; la SID che comincia «At 14NM LMT DME» la continua; una
    // SID per due piste con salite diverse si legge una volta per pista; le quote che nominano un punto fuori dal percorso
    // si dicono (N2, LIPH ADOSA 1A).
    [Fact]
    public void LaSidContinuaLaSalitaIniziale_UnaPerPista()
    {
        Procedure Salita(string pista, string testo) => new() { Aerodrome = "LIXX", Kind = ProcedureKind.InitialClimb, Designator = "RWY " + pista, Runways = [pista], Description = testo };
        Procedure Sid(string nome, string testo, params string[] piste) => new() { Aerodrome = "LIXX", Kind = ProcedureKind.Sid, Designator = nome, Runways = piste, Description = testo };
        var documento = new AipDocument
        {
            State = "LI", Airac = "2611", Airspaces = [],
            Points = [new SignificantPoint { Section = "ENR 4.4", Designator = "SUGEP", Position = Punti["SUGEP"] },
                new SignificantPoint { Section = "ENR 4.4", Designator = "DELIK", Position = Punti["DELIK"] }],
            Aerodromes =
            [
                new Aerodrome
                {
                    Icao = "LIXX", Name = "Prova", MagneticVariation = "3° E (2020.0)",
                    NavigationAids = [new Navaid { Section = "AD 2.19", Designator = "LMT", Name = "LAMEZIA", Type = "DVOR/DME", MagneticVariation = "3° E-2010.0", Position = Stazioni["LMT"].Position, Components = [] }],
                    Runways =
                    [
                        new() { Designator = "13", TrueBearing = "133°", Threshold = new Position(38.95, 15.90) },
                        new() { Designator = "31", TrueBearing = "313°", Threshold = new Position(38.90, 15.97) },
                    ],
                },
            ],
            Procedures =
            [
                Salita("13", "After take off turn right until joining RDL 130 LMT VOR (TR 130°) until 8NM LMT DME. REMARKS 1. Minimum climb gradient: 5.8%."),
                Salita("31", "After take-off proceed on RDL 093 LMT VOR (TR 093°) until 10 NM LMT DME, then turn right to join the assigned SID."),
                Sid("SUGEP 1A", "At 14NM LMT DME turn right bound to SUGEP.\nSUGEP: 5000 FT; DELIK – SUGEP: 4000 FT", "13"),
                Sid("SUGEP 1B", "At 10 NM LMT DME turn right bound to SUGEP.", "13", "31"),
            ],
        };

        var (letto, nonLette, avvisi) = ItalianAd2Prose.DecodeAll(documento);

        Assert.Equal(["RDL 130/8 NM LMT"], letto.Procedures![0].Legs.Select(t => t.Fix ?? t.Computed));
        Assert.Equal(["RDL 093/10 NM LMT"], letto.Procedures[1].Legs.Select(t => t.Fix ?? t.Computed));
        Assert.Equal(["RDL 130/8 NM LMT", "RDL 130/14 NM LMT", "SUGEP"], letto.Procedures[2].Legs.Select(t => t.Fix ?? t.Computed));
        Assert.Contains(avvisi, a => a.Procedure.Designator == "SUGEP 1A" && a.Warning.Contains("nominano DELIK", StringComparison.Ordinal));
        // SUGEP 1B vale per due piste con salite diverse: una per pista, ognuna con la sua.
        var unoB = letto.Procedures.Where(p => p.Designator == "SUGEP 1B").ToList();
        Assert.Equal([["13"], ["31"]], unoB.Select(p => p.Runways.ToArray()));
        Assert.Equal(["RDL 093/10 NM LMT", "SUGEP"], unoB[1].Legs.Select(t => t.Fix ?? t.Computed));
        Assert.Equal(["RDL 130/8 NM LMT", "RDL 130/10 NM LMT", "SUGEP"], unoB[0].Legs.Select(t => t.Fix ?? t.Computed));
        Assert.Empty(nonLette);
    }

    // Passo 5 della revisione col committente (gruppo 6): la virata «a una quota» si disegna dove l'aereo ci arriva salendo
    // dalla fine della pista con la pendenza della procedura; «not before … DME» la porta avanti, «not further than …» indietro.
    private static (IReadOnlyList<ProcedureLeg> Tratti, string? Problema, IReadOnlyList<string> Avvisi) LeggiDecollando(
        string prosa, ItalianAd2Prose.Runway pista, ItalianAd2Prose.Departure decollo, string? minimi = null) =>
        ItalianAd2Prose.DecodeWithWarnings(new Procedure
        {
            Aerodrome = "LIXX", Kind = ProcedureKind.Sid, Designator = "SUGEP 1A", Description = prosa, Minimums = minimi,
        }, n => Punti.TryGetValue(n, out var x) ? x : null, n => Stazioni.GetValueOrDefault(n), pista, departure: decollo);

    private static Position Verso(Position da, double rotta, double nm)
    {
        const double r = Math.PI / 180, raggio = 6371008.8 / 1852;
        var d = nm / raggio;
        var lat = Math.Asin(Math.Sin(da.Lat * r) * Math.Cos(d) + Math.Cos(da.Lat * r) * Math.Sin(d) * Math.Cos(rotta * r));
        var lon = da.Lon * r + Math.Atan2(Math.Sin(rotta * r) * Math.Sin(d) * Math.Cos(da.Lat * r), Math.Cos(d) - Math.Sin(da.Lat * r) * Math.Sin(lat));
        return new Position(lat / r, lon / r);
    }

    [Fact]
    public void LaVirataAUnaQuota_DallaFineDellaPista()
    {
        var soglia = Verso(Stazioni["LMT"].Position, 270, 20);
        var fine = Verso(soglia, 90, 1.6);
        var (tratti, problema, avvisi) = LeggiDecollando("After take-off proceed on RWY heading until leaving 1350 ft, then turn left bound to SUGEP.",
            new ItalianAd2Prose.Runway(soglia, 90), new ItalianAd2Prose.Departure(fine, 782, 500, "500 ft/NM (8.3%)", false));

        Assert.Null(problema);
        Assert.Equal(["VA 1350 ft", "DF SUGEP"], tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
        // (1350 − 782) / 500 = 1,14 NM dopo la fine della pista.
        Assert.Equal(1.6 + 568.0 / 500, Distanza(soglia, tratti[0].Position!.Value), 2);
        Assert.Contains(avvisi, a => a.Contains("virata a 1350 ft", StringComparison.Ordinal) && a.Contains("500 ft/NM", StringComparison.Ordinal));
    }

    [Fact]
    public void LaVirataAUnaQuota_NonPrimaDiUnaDistanzaDme()
    {
        // La fine della pista a 2 NM da LMT, verso LMT: a 1350 ft si arriva a 0,86 NM da LMT, ma «not before 0.8 NM LMT DME».
        var soglia = Verso(Stazioni["LMT"].Position, 270, 3);
        var fine = Verso(soglia, 90, 1);
        var (tratti, problema, avvisi) = LeggiDecollando("After take-off proceed on RWY heading until leaving 1350 ft, then turn left (not before 0.8 NM LMT DME) bound to SUGEP.",
            new ItalianAd2Prose.Runway(soglia, 90), new ItalianAd2Prose.Departure(fine, 782, 500, "500 ft/NM", false));

        Assert.Null(problema);
        Assert.Equal(0.8, Distanza(Stazioni["LMT"].Position, tratti[0].Position!.Value), 2);
        Assert.Contains(avvisi, a => a.Contains("spostata", StringComparison.Ordinal));
        Assert.Contains("not before 0.8 NM LMT DME", tratti[0].Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void LaVirataAUnaQuota_NonOltreUnPunto()
    {
        // A 4000 ft col 3,3% si arriva dopo 20 NM; «anyway not further than point RDL 090/5 NM LMT» la ferma lì.
        var soglia = Verso(Stazioni["LMT"].Position, 270, 3);
        var (tratti, problema, _) = LeggiDecollando("After take-off proceed on RWY heading. Passing 4000 ft, anyway not further than point RDL 090/5NM LMT VOR/DME (or RDL 288 RCA VOR) turn left bound to SUGEP.",
            new ItalianAd2Prose.Runway(soglia, 93), new ItalianAd2Prose.Departure(Verso(soglia, 93, 1), 0, 200, "il 3,3%", false));

        Assert.Null(problema);
        Assert.Equal(5, Distanza(Stazioni["LMT"].Position, tratti[0].Position!.Value), 1);
    }

    // ASPIR 8G (committente): fuori sulla radiale e di nuovo sulla stazione per salire alla quota delle quote minime, dalla quota
    // stimata al primo passaggio.
    [Fact]
    public void LAnelloPerSalire_LungoQuantoServe()
    {
        var lmt = Stazioni["LMT"].Position;
        var (tratti, problema, avvisi) = LeggiDecollando("Leave LMT VOR on RDL 083 (TR 083°), then turn left bound to LMT VOR to leave on RDL 230 (TR 230°) bound to SUGEP.",
            new ItalianAd2Prose.Runway(Verso(lmt, 270, 11), 90), new ItalianAd2Prose.Departure(Verso(lmt, 270, 10), 0, 200, "il 3,3%", false), "MCA/MCL: LMT VOR: 5000 FT");

        Assert.Null(problema);
        // A LMT si è a 10 NM × 200 = 2000 ft; per 5000 ne servono 15 NM: 7,5 fuori e 7,5 indietro.
        Assert.Equal(["IF LMT", "FD RDL 083/7.5 NM LMT", "DF LMT", "CF SUGEP"], tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
        Assert.Equal("L", tratti[2].Turn);
        Assert.Contains(avvisi, a => a.Contains("anello sulla RDL 083 LMT", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("611.3 FT / 611.3 FT", 611.3)]
    [InlineData("43 M AMSL", 141.08)]
    [InlineData("NIL / NIL", null)]
    public void LaQuotaInPiedi(string stampata, double? piedi)
    {
        var letta = ItalianAd2Prose.QuotaInPiedi(stampata);
        if (piedi is null) Assert.Null(letta);
        else Assert.Equal(piedi.Value, letta!.Value, 1);
    }

    // L'anello c'è solo se si esce dalla stazione su una radiale, si vira e ci si torna: passarci due volte di fila non è un
    // anello (LIBP ASPIR 8F, LIPE LOMED 6V).
    [Fact]
    public void PassareDueVolteSullaStazione_NonEUnAnello()
    {
        var lmt = Stazioni["LMT"].Position;
        var (tratti, problema, avvisi) = LeggiDecollando("Turn right bound to LMT VOR. Leave LMT VOR on RDL 230 (TR 230°) bound to SUGEP.",
            new ItalianAd2Prose.Runway(Verso(lmt, 270, 11), 90), new ItalianAd2Prose.Departure(Verso(lmt, 270, 10), 0, 200, "il 3,3%", false), "MCA/MCL: LMT VOR: 5000 FT");

        Assert.Null(problema);
        Assert.Equal(["IF LMT", "CF SUGEP"], tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
        Assert.DoesNotContain(avvisi, a => a.Contains("anello", StringComparison.Ordinal));
    }

    // Una SID senza la pista nel nome prende quella della salita iniziale stampata sulla sua stessa pagina (committente, 24
    // ottobre: LIBP ASPIR 8G sta sotto «INITIAL CLIMB PROCEDURE RWY 22»).
    [Fact]
    public void LaPistaDallaSalitaDellaStessaPagina()
    {
        var soglia13 = new Position(38.95, 15.90);
        var soglia31 = new Position(38.90, 15.97);
        Procedure Pagina(Procedure p, string pagina) => p with { Page = new AipPage(pagina) };
        var documento = new AipDocument
        {
            State = "LI", Airac = "2611", Airspaces = [],
            Points = [new SignificantPoint { Section = "ENR 4.4", Designator = "SUGEP", Position = Punti["SUGEP"] }],
            Aerodromes =
            [
                new Aerodrome
                {
                    Icao = "LIXX", Name = "Prova", MagneticVariation = "3° E (2020.0)",
                    NavigationAids = [new Navaid { Section = "AD 2.19", Designator = "LMT", Name = "LAMEZIA", Type = "DVOR/DME", MagneticVariation = "3° E-2010.0", Position = Stazioni["LMT"].Position, Components = [] }],
                    Runways = [new() { Designator = "13", TrueBearing = "133°", Threshold = soglia13 }, new() { Designator = "31", TrueBearing = "313°", Threshold = soglia31 }],
                },
            ],
            Procedures =
            [
                Pagina(new Procedure { Aerodrome = "LIXX", Kind = ProcedureKind.InitialClimb, Designator = "RWY 13", Runways = ["13"], Description = "After take-off proceed on RWY heading." }, "AD 2 LIXX 6-3"),
                Pagina(new Procedure { Aerodrome = "LIXX", Kind = ProcedureKind.InitialClimb, Designator = "RWY 31", Runways = ["31"], Description = "After take-off proceed on RWY heading." }, "AD 2 LIXX 6-7"),
                Pagina(P(ProcedureKind.Sid, "SUGEP 1C", "After take-off proceed on RWY heading until 8 NM LMT DME, then turn right bound to SUGEP."), "AD 2 LIXX 6-7"),
            ],
        };

        var (letto, _, _) = ItalianAd2Prose.DecodeAll(documento);

        var primo = letto.Procedures!.Single(p => p.Designator == "SUGEP 1C").Legs[0].Position!.Value;
        Assert.Equal(313, Rotta(soglia31, primo), 0);
    }

    // Passo 6 della revisione col committente: il localizzatore IBXY corre verso est sulla latitudine di SUGEP (38,95°N).
    private static (IReadOnlyList<ProcedureLeg> Tratti, string? Problema, IReadOnlyList<string> Avvisi) LeggiColLoc(string prosa) =>
        ItalianAd2Prose.DecodeWithWarnings(P(ProcedureKind.Star, "ARBUM 4F", prosa, "10"),
            n => Punti.TryGetValue(n, out var x) ? x : null, n => Stazioni.GetValueOrDefault(n),
            aerodromeVariation: 3, localizer: n => n == "IBXY" ? new ItalianAd2Prose.Localizer(new Position(38.95, 16.5), 90, 3) : null);

    private const string SullArco = "ARBUM – TR 132° (RDL 312 LMT VOR) until 17 NM LMT DME, then turn right to join ARC 15 NM LMT DME, ";

    // L'arco di una stazione che finisce sulla radiale di un'altra (LIPB ALESE 6A, VIC 6A): l'incrocio del cerchio con la radiale.
    [Fact]
    public void LArcoFinisceSullaRadialeDiUnAltraStazione()
    {
        var (tratti, problema, _) = LeggiColLoc(SullArco + "crossing RDL 354 RCA VOR turn left bound to SUGEP.");

        Assert.Null(problema);
        var fine = tratti.Single(t => t.PathTerminator == "AF").Position!.Value;
        Assert.Equal(15, Distanza(Stazioni["LMT"].Position, fine), 1);
        Assert.Equal(358, Rotta(Stazioni["RCA"].Position, fine), 0);
    }

    // L2: da una rotta, l'incrocio col localizzatore, come per una radiale; poi si è sul LOC.
    [Fact]
    public void IlLocalizzatoreDaUnaRotta_LIncrocio()
    {
        var (tratti, problema, _) = LeggiColLoc("DELIK – TR 010° until joining IBXY LOC (TR 087°) bound to SUGEP.");

        Assert.Null(problema);
        Assert.Equal(["IF DELIK", "CF INT TR 010°/IBXY LOC", "CF SUGEP"], tratti.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
        Assert.Equal(38.95, tratti[1].Position!.Value.Lat, 2);
        Assert.Equal("087°", tratti[2].Course);
    }

    // L3 (committente: «una curva, come sulle carte»): dopo la virata senza una rotta, l'arco tangente alla direzione in cui si
    // vola e al localizzatore, dalla parte della virata.
    [Fact]
    public void IlLocalizzatoreDopoUnaVirata_LaCurva()
    {
        var (tratti, problema, avvisi) = LeggiColLoc(SullArco + "crossing RDL 283 LMT VOR turn left to intercept and follow IBXY LOC bound to SUGEP.");

        Assert.Null(problema);
        var curva = tratti.Single(t => t.PathTerminator == "RF");
        Assert.Equal(("INT IBXY LOC", "L"), (curva.Computed, curva.Turn));
        Assert.NotNull(curva.ArcCenterPosition);
        // La curva tocca il LOC, e il centro sta a un raggio dal punto dove comincia e dal LOC.
        Assert.Equal(38.95, curva.Position!.Value.Lat, 3);
        var inizio = tratti[tratti.ToList().IndexOf(curva) - 1].Position!.Value;
        Assert.Equal(curva.ArcRadiusNm!.Value, Distanza(curva.ArcCenterPosition!.Value, inizio), 1);
        Assert.Equal(curva.ArcRadiusNm!.Value, Distanza(curva.ArcCenterPosition!.Value, curva.Position!.Value), 1);
        Assert.Equal("SUGEP", tratti[^1].Fix);
        Assert.Contains(avvisi, a => a.Contains("raggio", StringComparison.Ordinal));   // 5,6 NM: lontano da una virata vera
    }

    // U3 (LIBR DAMIC 2Y): un punto definito sul localizzatore dopo un arco: l'arco finisce dove incrocia il LOC, poi sul LOC.
    [Fact]
    public void UnPuntoSulLocalizzatore_DopoUnArco()
    {
        var (tratti, problema, avvisi) = LeggiColLoc(SullArco + "then turn right bound to SUGEP (IBXY LOC/12 NM LMT DME).");

        Assert.Null(problema);
        var arco = tratti.Single(t => t.PathTerminator == "AF");
        Assert.Equal(38.95, arco.Position!.Value.Lat, 2);
        Assert.Equal("SUGEP", tratti[^1].Fix);
        Assert.Contains(avvisi, a => a.Contains("raccordo fra l'arco e il localizzatore non è disegnato", StringComparison.Ordinal));
    }
}
