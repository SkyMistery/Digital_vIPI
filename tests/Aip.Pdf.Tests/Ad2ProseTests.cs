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

        var (letto, nonLette) = ItalianAd2Prose.DecodeAll(documento);

        var sugep = letto.Procedures![0];
        Assert.True(sugep.LegsFromDescription);
        Assert.Equal(["SUGEP", "LMT", "RDL 115/16 NM LMT"], sugep.Legs.Select(t => t.Fix ?? t.Computed));
        Assert.Same(documento.Procedures[2], letto.Procedures[2]);
        var (nonLetta, perche) = Assert.Single(nonLette);
        Assert.Equal("SUGEP 4G", nonLetta.Designator);
        Assert.Contains("Leaving 4000 FT", perche, StringComparison.Ordinal);
        Assert.False(letto.Procedures[1].LegsFromDescription);
    }
}
