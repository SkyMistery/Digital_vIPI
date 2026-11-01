using Aip.Pdf.Italia;

namespace Aip.Pdf.Tests;

/// <summary>
/// Carta «Le curve delle procedure», §7 (committente, 1 novembre): R6, la velocità delle virate detta nel testo o nelle REMARKS;
/// R7, una salita iniziale coi rami per SID diventa una salita per ramo. Le frasi sono quelle dell'AIP; stazioni e punti inventati.
/// </summary>
public sealed class Ad2ProseVelocitaERamiTests
{
    private static readonly Position Lmt = new(39.0, 16.0), Ost = new(41.8, 12.24);
    private static readonly Position Sugep = new(38.95, 16.40), Delik = new(38.70, 16.45), Nenig = new(41.85, 11.90);

    private static Navaid Vor(string sigla, Position dove) =>
        new() { Section = "AD 2.19", Designator = sigla, Name = sigla, Type = "DVOR/DME", MagneticVariation = "3° E-2010.0", Position = dove, Components = [] };

    private static AipDocument Documento(Aerodrome scalo, params Procedure[] procedure) => new()
    {
        State = "LI", Airac = "2611", Airspaces = [],
        Points =
        [
            new SignificantPoint { Section = "ENR 4.4", Designator = "SUGEP", Position = Sugep },
            new SignificantPoint { Section = "ENR 4.4", Designator = "DELIK", Position = Delik },
            new SignificantPoint { Section = "ENR 4.4", Designator = "NENIG", Position = Nenig },
        ],
        Aerodromes = [scalo],
        Procedures = procedure,
    };

    private static Aerodrome Scalo(params Runway[] piste) =>
        new() { Icao = "LIXX", Name = "Prova", MagneticVariation = "3° E (2020.0)", NavigationAids = [Vor("LMT", Lmt), Vor("OST", Ost)], Runways = piste };

    private static Procedure Salita(string pista, string testo) =>
        new() { Aerodrome = "LIXX", Kind = ProcedureKind.InitialClimb, Designator = "RWY " + pista, Runways = [pista], Description = testo };

    private static Procedure Sid(string nome, string testo, string pista, string? note = null) =>
        new() { Aerodrome = "LIXX", Kind = ProcedureKind.Sid, Designator = nome, Runways = [pista], Description = testo, Remarks = note };

    private static double Raggio(double nodi) => Math.Pow(nodi * 1852 / 3600.0, 2) / (9.80665 * Math.Tan(25 * Math.PI / 180)) / 1852;

    // R6: una sola IAS MAX vale; una sola «during turn» vale anche fra altre; due diverse senza una «during turn» no.
    [Theory]
    [InlineData("2. During initial climb IAS MAX 140 KT until establishing on RDL 267 PRT VOR.", 140.0, null)]  // LIRQ RWY 05
    [InlineData("DELIK - TR 310° (RDL 130 LMT VOR) (IAS MAX 210KT) until 14 NM LMT DME.", 210.0, null)]
    [InlineData("Turns at IAS MAX 220 kt.", 220.0, null)]                                                         // LIEE RWY 32
    [InlineData("Turn and climb parameters: a) Departure turn limited to 210 KT;", 210.0, null)]
    [InlineData("IAS MAX 250 KT below FL100. Turn left (IAS MAX 200 kt during turn) on TR 164°.", 200.0, null)]
    [InlineData("IAS MAX 185 KT until 5 NM, then IAS MAX 240 KT.", null, "185/240")]                             // LIPQ
    [InlineData("SID ANC 8A: turn right (IAS MAX 210 kt during turn) … SID BOA 7V: turn left (IAS MAX 180 kt during turn)", null, "180/210")] // LIPR RWY 30
    [InlineData("After take-off proceed on TR 130°.", null, null)]
    public void LaVelocitaDelTesto(string testo, double? nodi, string? diverse) =>
        Assert.Equal(new ItalianAd2Prose.VelocitaDelTesto(nodi, diverse), ItalianAd2Prose.VelocitaNelTesto(testo));

    [Theory]
    [InlineData("SID BOA 7Z and EKTUK 7B:", "BOA 7Z, EKTUK 7B")]  // LIPR RWY 12
    [InlineData("if cleared via SID\nBEROK8A;", "BEROK 8A")]       // LIRP RWY 03R/03L
    [InlineData("if cleared for other SID:", "altre SID")]        // LIRN RWY 06
    public void IlNomeDelRamo(string condizione, string nome) => Assert.Equal(nome, ItalianAd2Prose.NomeDelRamo(condizione));

    // R7: una salita coi rami per SID è una salita per ramo, col nome delle sue SID e la parte comune davanti; il testo stampato
    // resta quello intero. I rami che dicono solo una pendenza non sono salite.
    [Fact]
    public void UnaSalitaCoiRami_UnaSalitaPerRamo()
    {
        var conRami = Salita("13", "After take off proceed on RDL 130 LMT VOR (TR 130°) until 8NM LMT DME, then\n"
            + "a) if cleared via SID SUGEP 1A: turn left bound to SUGEP;\n"
            + "b) if cleared via SID DELIK 1A: turn right bound to DELIK.");
        var conPendenze = Salita("31", "After take-off proceed on RDL 093 LMT VOR (TR 093°) until 10 NM LMT DME. Minimum climb gradient:\n"
            + "- if proceeding via SID SUGEP 1B: 395 ft/NM (6,5%);\n- if proceeding via SID DELIK 1B: 304 ft/NM (5,0%).");
        var documento = Documento(Scalo(), conRami, conPendenze);

        var (letto, nonLette, _) = ItalianAd2Prose.DecodeAll(documento);

        var salite = letto.Procedures!.Where(p => p.Kind == ProcedureKind.InitialClimb).ToList();
        Assert.Equal(["RWY 13 per SUGEP 1A", "RWY 13 per DELIK 1A", "RWY 31"], salite.Select(s => s.Designator));
        Assert.Equal(["RDL 130/8 NM LMT", "SUGEP"], salite[0].Legs.Select(t => t.Fix ?? t.Computed));
        Assert.Equal(["RDL 130/8 NM LMT", "DELIK"], salite[1].Legs.Select(t => t.Fix ?? t.Computed));
        Assert.Equal(["13"], salite[0].Runways);
        Assert.Equal(conRami.Description, salite[1].Description);
        Assert.Equal(["RDL 093/10 NM LMT"], salite[2].Legs.Select(t => t.Fix ?? t.Computed));
        Assert.Empty(nonLette);
    }

    // R7: un ramo che non si legge («then expect ATC instruction», LIRF RWY 07) tiene la parte comune, come prima, e lo dice.
    [Fact]
    public void UnRamoCheNonSiLegge_TieneLaParteComune()
    {
        var salita = Salita("13", "After take off proceed on RDL 130 LMT VOR (TR 130°) until 8NM LMT DME, then\n"
            + "a) if cleared via SID SUGEP 1A: turn left bound to SUGEP;\n"
            + "b) if cleared via SID DELIK 1A: turn right and then expect ATC instruction.");

        var (letto, nonLette, avvisi) = ItalianAd2Prose.DecodeAll(Documento(Scalo(), salita));

        var delik = letto.Procedures!.Single(p => p.Designator == "RWY 13 per DELIK 1A");
        Assert.Equal(["RDL 130/8 NM LMT"], delik.Legs.Select(t => t.Fix ?? t.Computed));
        Assert.Contains(avvisi, a => a.Procedure.Designator == delik.Designator && a.Warning.StartsWith("il ramo non si legge", StringComparison.Ordinal));
        Assert.Empty(nonLette);
    }

    // R7 (LIEE RWY 32): il testo italiano mescolato all'inglese, «on su track 300°» e «track 105°. 300° until», si legge e si dice.
    [Theory]
    [InlineData("After take off proceed on\nsu track 300° until 8NM LMT DME, then turn right bound to SUGEP.")]
    [InlineData("After take off proceed on track\n105°. 300° until 8NM LMT DME, then turn right bound to SUGEP.")]
    public void IlTestoItalianoMescolato_SiLegge(string testo)
    {
        var (normale, _) = ItalianAd2Prose.Ricuci("After take off proceed on track 300° until 8NM LMT DME, then turn right bound to SUGEP.", _ => true, _ => true, null);
        var (ricucito, avvisi) = ItalianAd2Prose.Ricuci(testo, _ => true, _ => true, null);

        Assert.Equal(normale!.Replace("\n", " ", StringComparison.Ordinal), ricucito!.Replace("\n", " ", StringComparison.Ordinal));
        Assert.Contains(avvisi, a => a.Contains("errore di stampa", StringComparison.Ordinal));
    }

    // R6 (committente: le REMARKS della salita valgono anche per le SID della pista): la SID che vira dopo il decollo vira con la
    // velocità della salita (140 kt), anche se le sue REMARKS ne dicono un'altra per le virate dopo (210 kt, la sua TurnSpeed).
    [Fact]
    public void LaSidViraDopoIlDecollo_ConLaVelocitaDellaSalita()
    {
        var soglia = new Position(41.90, 12.40);
        var fine = Sphere.Destination(soglia, 200, 1.6);
        var scalo = Scalo(
            new Runway { Designator = "20", TrueBearing = "200°", Threshold = soglia, ThresholdElevation = "30 FT" },
            new Runway { Designator = "02", TrueBearing = "020°", Threshold = fine, ThresholdElevation = "30 FT" }) with { Elevation = "30 FT", Arp = soglia };
        var documento = Documento(scalo,
            Salita("20", "REMARKS\n1. During initial climb IAS MAX 140 KT until establishing on RDL 278 OST VOR.\n2. Minimum climb gradient: 5%."),
            Sid("NENIG 6Z", "Proceed on TR 247° until joining RDL 278 OST VOR bound to NENIG.", "20", note: "IAS MAX 210 KT."),
            Sid("NENIG 6Y", "Proceed on TR 247° until joining RDL 278 OST VOR bound to NENIG.", "20"));

        var (letto, nonLette, _) = ItalianAd2Prose.DecodeAll(documento);

        Assert.Empty(nonLette);
        var conLaSua = letto.Procedures!.Single(p => p.Designator == "NENIG 6Z");
        Assert.Equal(["CA fine della pista", "RF fine della virata su TR 247°", "CR INT TR 247°/RDL 278 OST", "DF NENIG"],
            conLaSua.Legs.Select(t => $"{t.PathTerminator} {t.Fix ?? t.Computed}"));
        Assert.Equal(Math.Round(Raggio(140), 2), conLaSua.Legs[1].ArcRadiusNm);
        Assert.Equal(210, conLaSua.TurnSpeed);
        // Senza una sua: quella della salita, anche per le virate dopo.
        Assert.Equal(140, letto.Procedures!.Single(p => p.Designator == "NENIG 6Y").TurnSpeed);
    }

    // R6: due velocità e nessuna «during turn»: si resta a quella di base, e si dice.
    [Fact]
    public void DueVelocitaDiverse_SiRestaAQuellaDiBase_ESiDice()
    {
        var documento = Documento(Scalo(new Runway { Designator = "13", TrueBearing = "133°", Threshold = new Position(38.95, 15.90) }),
            Salita("13", "After take off turn right until joining RDL 130 LMT VOR (TR 130°) until 8NM LMT DME."),
            Sid("SUGEP 1A", "At 14NM LMT DME turn right bound to SUGEP.", "13", note: "IAS MAX 185 KT until 14 NM LMT DME, then IAS MAX 240 KT."));

        var (letto, _, avvisi) = ItalianAd2Prose.DecodeAll(documento);

        var sid = letto.Procedures!.Single(p => p.Designator == "SUGEP 1A");
        Assert.Null(sid.TurnSpeed);
        Assert.Contains(avvisi, a => a.Procedure.Designator == "SUGEP 1A"
            && a.Warning == "la IAS MAX non è una sola (185/240 kt) e nessuna è «during turn»: le virate sono disegnate a 210 kt");
    }
}
