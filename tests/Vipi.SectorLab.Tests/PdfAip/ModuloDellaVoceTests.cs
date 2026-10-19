using Aip;
using Vipi.SectorLab.Core.PdfAip;
using Route = Aip.Route;

namespace Vipi.SectorLab.Tests.PdfAip;

/// <summary>
/// Le correzioni dell'AIP espanso, slice 3: il modulo generale (punti, radioassistenze, luci, pericoli, attese, rotte, scali)
/// e i gesti del nucleo su quei tipi. Dati inventati.
/// </summary>
public sealed class ModuloDellaVoceTests
{
    private static readonly DateOnly Oggi = new(2026, 10, 19);

    private static Navaid Vor() => new()
    {
        Section = "ENR 4.1",
        Designator = "PRV",
        Name = "PROVA",
        Type = "VOR/DME",
        Kind = NavaidKind.VorDme,
        Frequency = "112.30 MHz",
        Channel = "70X",
        // Una posizione che in secondi non torna esatta: se il campo non si tocca, deve restare questa.
        Position = new Position(41.123456789, 12.987654321),
        Components = [],
        Page = new AipPage("ENR 4.1-1", new DateOnly(2026, 7, 9)),
    };

    private static Holding Attesa() => new()
    {
        Section = "ENR 3.4.1",
        Fix = "ABRUS",
        Minimum = new VerticalLimit { Text = "FL 110" },
        Turn = TurnDirection.Right,
        InboundTrack = 250,
    };

    private static Dictionary<string, string?> Valori(CorrectionKind tipo, object? voce) =>
        ModuloDellaVoce.Valori(tipo, voce).ToDictionary(v => v.Key, v => (string?)v.Value, StringComparer.Ordinal);

    private static AipDocument Pdf() => new()
    {
        State = "LI",
        Airac = "2611",
        Navaids = [Vor()],
        Holdings = [Attesa()],
        Points = [new SignificantPoint { Section = "ENR 4.4", Designator = "ABRUS", Position = new Position(41.5, 12.25) }],
    };

    [Theory]
    [InlineData(41.8, 12.5, "414800N 0123000E")]
    [InlineData(45.835, 7.1533333333, "455006N 0070912E")]
    [InlineData(41.80013888888889, -0.5, "414800.5N 0003000W")]
    public void LaCoordinataNellaFormaDellAip(double lat, double lon, string attesa) =>
        Assert.Equal(attesa, ModuloDellaVoce.Coordinata(lat, lon));

    [Fact]
    public void UnCampoNonToccatoRestaComEra_QuelloToccatoCambia()
    {
        var valori = Valori(CorrectionKind.Navaid, Vor());
        Assert.Equal("410724.44N 0125915.56E", valori["position"]);
        valori["frequency"] = "112.35 MHz";

        var (voce, perche) = ModuloDellaVoce.Voce(CorrectionKind.Navaid, valori, Vor());

        Assert.Null(perche);
        var nuova = Assert.IsType<Navaid>(voce);
        Assert.Equal("112.35 MHz", nuova.Frequency);
        Assert.Equal(Vor().Position, nuova.Position);
        Assert.Equal(Vor().Page, nuova.Page);

        // Una posizione riscritta si legge come la scrive l'AIP.
        valori["position"] = "41°48'00\"N 012°30'00\"E";
        Assert.Equal(new Position(41.8, 12.5), ((Navaid)ModuloDellaVoce.Voce(CorrectionKind.Navaid, valori, Vor()).Voce!).Position);
    }

    [Theory]
    [InlineData("position", "dove capita", "coordinata")]
    [InlineData("position", "414800N 0123000E 414900N 0123000E", "una sola")]
    [InlineData("name", "", "Manca «Nome»")]
    [InlineData("kind", "astronave", "una di")]
    [InlineData("components", "[ non è json", "JSON non si legge")]
    public void IlModuloDiceCheCosaNonVa(string campo, string testo, string atteso)
    {
        var valori = Valori(CorrectionKind.Navaid, Vor());
        valori[campo] = testo;
        Assert.Contains(atteso, ModuloDellaVoce.Voce(CorrectionKind.Navaid, valori, Vor()).Perche, StringComparison.Ordinal);
    }

    [Fact]
    public void SenzaModificheNonCorregge_EUnaDistanzaSiScriveCollUnita()
    {
        Assert.Contains("Non è cambiato niente", ModuloDellaVoce.Voce(CorrectionKind.Navaid, Valori(CorrectionKind.Navaid, Vor()), Vor()).Perche, StringComparison.Ordinal);

        var valori = Valori(CorrectionKind.Hazard, null);
        Assert.Equal("balloon", valori["kind"]);
        valori["section"] = "ENR 5.3.1";
        valori["name"] = "PROVA";
        valori["position"] = "414800N 0123000E";
        valori["radius"] = "16 km";
        var pericolo = Assert.IsType<Hazard>(ModuloDellaVoce.Voce(CorrectionKind.Hazard, valori, null).Voce);
        Assert.Equal(new Distance(16, DistanceUnit.Km), pericolo.Radius);
        valori["radius"] = "sedici";
        Assert.Contains("16 KM", ModuloDellaVoce.Voce(CorrectionKind.Hazard, valori, null).Perche, StringComparison.Ordinal);
    }

    [Fact]
    public void UnAttesaCambiaQuota_ELaChiaveSegueIlVerso()
    {
        var valori = Valori(CorrectionKind.Holding, Attesa());
        Assert.Equal("right", valori["turn"]);
        valori["minimum"] = "FL 120";
        valori["turn"] = "left";

        var nuova = Assert.IsType<Holding>(ModuloDellaVoce.Voce(CorrectionKind.Holding, valori, Attesa()).Voce);

        Assert.Equal((120.0, VerticalReference.FlightLevel), (nuova.Minimum.Value!.Value, nuova.Minimum.Reference));
        var (fatta, _) = CorrezioniDellAip.Correggi(Pdf(), new AipCorrectionFile { State = "LI" }, CorrectionKind.Holding, "ABRUS 250 right", nuova,
            "quota sbagliata", "AIC 1/26", "2611", Oggi);
        Assert.Equal("ABRUS 250 left", AipCorrections.KeyOf(Assert.Single(fatta!.Modello.Holdings!)));
        ComeUnaLettura(fatta);
    }

    [Fact]
    public void IGestiSugliAltriTipi_ComeUnaLettura()
    {
        var vuoto = new AipCorrectionFile { State = "LI" };

        // Un punto nuovo, da un'altra fonte: aggiunto, poi corretto (un'altra aggiunta), poi tolto (l'aggiunta si ritira).
        var valori = Valori(CorrectionKind.Point, null);
        valori["section"] = "ENR 4.4";
        valori["designator"] = "NUOVO";
        valori["position"] = "420000N 0130000E";
        valori["routes"] = "L5, M726";
        var punto = Assert.IsType<SignificantPoint>(ModuloDellaVoce.Voce(CorrectionKind.Point, valori, null).Voce);
        Assert.Equal(["L5", "M726"], punto.Routes);
        var (aggiunto, _) = CorrezioniDellAip.Aggiungi(Pdf(), vuoto, CorrectionKind.Point, punto, "punto nuovo", "AIP SUP 7/26", "2611", Oggi);
        Assert.Equal(["ABRUS", "NUOVO"], aggiunto!.Modello.Points!.Select(p => p.Designator));
        ComeUnaLettura(aggiunto);
        var (corretto, _) = CorrezioniDellAip.Correggi(aggiunto.Modello, aggiunto.Correzioni, CorrectionKind.Point, "NUOVO", punto with { Fra = "I" },
            "FRA", "AIP SUP 7/26", "2611", Oggi);
        Assert.Equal([CorrectionOp.Add, CorrectionOp.Add], corretto!.Correzioni.Corrections.Select(c => c.Op));
        ComeUnaLettura(corretto);
        var (tolto, _) = CorrezioniDellAip.Togli(corretto.Modello, corretto.Correzioni, CorrectionKind.Point, "NUOVO", null, null, "2611", Oggi);
        Assert.Equal(["ABRUS"], tolto!.Modello.Points!.Select(p => p.Designator));
        ComeUnaLettura(tolto);

        // Una radioassistenza tolta, poi ritirata: torna quella del PDF; l'elenco vuoto non si scrive.
        var (senza, _) = CorrezioniDellAip.Togli(Pdf(), vuoto, CorrectionKind.Navaid, "PRV VOR/DME", "dismessa", "AIC 2/26", "2611", Oggi);
        Assert.Null(senza!.Modello.Navaids);
        ComeUnaLettura(senza);
        var (rimessa, _) = CorrezioniDellAip.Ritira(senza.Modello, senza.Correzioni, senza.Correzioni.Corrections[0].Id);
        Assert.Equal(Vor(), Assert.Single(rimessa!.Modello.Navaids!));

        // Due voci con la stessa chiave no.
        Assert.Contains("C'è già", CorrezioniDellAip.Aggiungi(Pdf(), vuoto, CorrectionKind.Navaid, Vor(), "x", "y", "2611", Oggi).Perche, StringComparison.Ordinal);
    }

    [Fact]
    public void UnaRottaSiCorreggeNelJsonDeiTratti()
    {
        var rotta = new Route
        {
            Section = "ENR 3.2",
            Designator = "L5",
            Points = [new RoutePoint("ABRUS", "ABRUS", new Position(41.5, 12.25), true), new RoutePoint("BOLSA", "BOLSA", new Position(42, 12.5), false)],
            Segments = [new RouteSegment { Track = "045", DistanceNm = 33.5 }],
        };
        var valori = Valori(CorrectionKind.Route, rotta);
        Assert.Contains("\"designator\": \"ABRUS\"", valori["points"], StringComparison.Ordinal);
        valori["segments"] = valori["segments"]!.Replace("33.5", "34", StringComparison.Ordinal);

        var nuova = Assert.IsType<Route>(ModuloDellaVoce.Voce(CorrectionKind.Route, valori, rotta).Voce);

        Assert.Equal(34, nuova.Segments[0].DistanceNm);
        Assert.Equal(rotta.Points, nuova.Points);
    }

    private static void ComeUnaLettura(CorrezioneFatta fatta)
    {
        var letto = AipCorrections.Apply(Pdf(), fatta.Correzioni);
        Assert.Equal(AipJson.Serialize(fatta.Modello), AipJson.Serialize(letto.Document));
        Assert.All(letto.Outcomes, o => Assert.NotEqual(CorrectionOutcomeKind.Invalid, o.Outcome));
    }
}
