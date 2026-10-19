using System.Text.Json.Nodes;

namespace Aip.Tests;

/// <summary>
/// Le correzioni dell'AIP espanso, slice 1: errori dell'AIP e aggiunte da altre fonti, che durano da un ciclo all'altro
/// finché un PDF o un'altra correzione non le supera (committente, 19 ottobre). Dati inventati.
/// </summary>
public sealed class AipCorrectionsTests
{
    private static SignificantPoint Punto(string nome, double lat, double lon, string pagina = "ENR 4.4-1", int giorno = 9) => new()
    {
        Section = "ENR 4.4",
        Designator = nome,
        Position = new Position(lat, lon),
        Page = new AipPage(pagina, new DateOnly(2026, 7, giorno)),
    };

    private static Airspace Area(string id, string? nome, double raggio) => new()
    {
        Section = "ENR 5.1",
        Type = AirspaceType.Restricted,
        Designator = id,
        Name = nome,
        Upper = new VerticalLimit { Text = "FL 100" },
        Lower = new VerticalLimit { Text = "GND", Reference = VerticalReference.Ground },
        Geometry = new CircleGeometry(new Position(42.0, 13.0), new Distance(raggio, DistanceUnit.Nm)),
    };

    private static AipDocument Pdf(string ciclo, params SignificantPoint[] punti) => new()
    {
        State = "LI",
        Airac = ciclo,
        Airspaces = [Area("LI R901", "PROVA", 3.0)],
        Points = punti,
    };

    private static AipCorrection Correzione(string id, CorrectionKind tipo, string chiave, CorrectionOp op, JsonObject? dopo = null) => new()
    {
        Id = id,
        Kind = tipo,
        Key = chiave,
        Op = op,
        After = dopo,
        Reason = "coordinate sbagliate nel PDF",
        Source = "errore AIP, segnalato a ENAV",
    };

    private static AipCorrectionFile File(params AipCorrection[] correzioni) => new() { State = "LI", Corrections = correzioni };

    [Fact]
    public void UnaCorrezioneSiApplica_ELaPrimaVoltaScriveBeforeESince()
    {
        var pdf = Pdf("2611", Punto("ABRUS", 41.0, 12.0), Punto("BOLSA", 43.0, 11.0));
        var giusto = AipCorrections.ToNode(Punto("ABRUS", 41.5, 12.0));

        var esito = AipCorrections.Apply(pdf, File(Correzione("1", CorrectionKind.Point, "ABRUS", CorrectionOp.Change, giusto)));

        Assert.Equal(CorrectionOutcomeKind.Applied, Assert.Single(esito.Outcomes).Outcome);
        Assert.Equal(new Position(41.5, 12.0), esito.Document.Points![0].Position);
        Assert.Equal("BOLSA", esito.Document.Points[1].Designator);
        Assert.True(esito.FileChanged);
        var scritta = Assert.Single(esito.File.Corrections);
        Assert.Equal("2611", scritta.Since);
        Assert.Null(scritta.Superseded);
        Assert.Equal(41.0, (double)scritta.Before!["position"]!["lat"]!);

        // Al ciclo dopo, il PDF uguale: si applica ancora, e il file non cambia.
        var dopo = AipCorrections.Apply(Pdf("2612", Punto("ABRUS", 41.0, 12.0), Punto("BOLSA", 43.0, 11.0)), esito.File);
        Assert.Equal(CorrectionOutcomeKind.Applied, Assert.Single(dopo.Outcomes).Outcome);
        Assert.Equal(new Position(41.5, 12.0), dopo.Document.Points![0].Position);
        Assert.False(dopo.FileChanged);
    }

    [Fact]
    public void UnaPaginaRistampataConLaDataNuovaNonSuperaNiente()
    {
        var prima = AipCorrections.Apply(Pdf("2611", Punto("ABRUS", 41.0, 12.0)),
            File(Correzione("1", CorrectionKind.Point, "ABRUS", CorrectionOp.Change, AipCorrections.ToNode(Punto("ABRUS", 41.5, 12.0)))));

        var ristampata = AipCorrections.Apply(Pdf("2612", Punto("ABRUS", 41.0, 12.0, "ENR 4.4-2", giorno: 30)), prima.File);

        Assert.Equal(CorrectionOutcomeKind.Applied, Assert.Single(ristampata.Outcomes).Outcome);
    }

    [Fact]
    public void IlPdfCheCambiaQuelDatoSuperaLaCorrezione()
    {
        var prima = AipCorrections.Apply(Pdf("2611", Punto("ABRUS", 41.0, 12.0)),
            File(Correzione("1", CorrectionKind.Point, "ABRUS", CorrectionOp.Change, AipCorrections.ToNode(Punto("ABRUS", 41.5, 12.0)))));

        // ENAV ha corretto (anche in modo diverso dal nostro): vale il PDF.
        var esito = AipCorrections.Apply(Pdf("2612", Punto("ABRUS", 41.4, 12.0)), prima.File);

        var come = Assert.Single(esito.Outcomes);
        Assert.Equal(CorrectionOutcomeKind.SupersededByAip, come.Outcome);
        Assert.Equal("AIP 2612", come.SupersededBy);
        Assert.Equal(new Position(41.4, 12.0), esito.Document.Points![0].Position);
        Assert.Equal("AIP 2612", Assert.Single(esito.File.Corrections).Superseded);
        Assert.True(esito.FileChanged);

        // Superata resta superata: al ciclo dopo non si guarda più.
        var poi = AipCorrections.Apply(Pdf("2613", Punto("ABRUS", 41.0, 12.0)), esito.File);
        Assert.Empty(poi.Outcomes);
        Assert.Equal(new Position(41.0, 12.0), poi.Document.Points![0].Position);
        Assert.False(poi.FileChanged);
    }

    [Fact]
    public void UnAggiuntaSiAggiunge_FinchéIlPdfNonLaPorta()
    {
        var aggiunta = Correzione("1", CorrectionKind.Point, "NUOVO", CorrectionOp.Add, AipCorrections.ToNode(Punto("NUOVO", 40.0, 15.0))) with
        {
            Source = "AIP SUP 123/26",
        };

        var esito = AipCorrections.Apply(Pdf("2611", Punto("ABRUS", 41.0, 12.0)), File(aggiunta));
        Assert.Equal(CorrectionOutcomeKind.Applied, Assert.Single(esito.Outcomes).Outcome);
        Assert.Equal(["ABRUS", "NUOVO"], esito.Document.Points!.Select(p => p.Designator));

        var portata = AipCorrections.Apply(Pdf("2612", Punto("ABRUS", 41.0, 12.0), Punto("NUOVO", 40.0, 15.0)), esito.File);
        Assert.Equal(CorrectionOutcomeKind.SupersededByAip, Assert.Single(portata.Outcomes).Outcome);
        Assert.Equal(2, portata.Document.Points!.Count);
    }

    [Fact]
    public void UnaListaCheIlPdfNonHaNasceDallAggiunta()
    {
        var doc = Pdf("2611") with { Points = null };
        var esito = AipCorrections.Apply(doc, File(Correzione("1", CorrectionKind.Point, "NUOVO", CorrectionOp.Add, AipCorrections.ToNode(Punto("NUOVO", 40.0, 15.0)))));

        Assert.Equal("NUOVO", Assert.Single(esito.Document.Points!).Designator);
        Assert.Null(esito.Document.Routes);
    }

    [Fact]
    public void UnaRimozioneToglie_ESeLOggettoSparisceDalPdfESuperata()
    {
        var togli = Correzione("1", CorrectionKind.Airspace, "LI R901 PROVA", CorrectionOp.Remove);

        var esito = AipCorrections.Apply(Pdf("2611"), File(togli));
        Assert.Equal(CorrectionOutcomeKind.Applied, Assert.Single(esito.Outcomes).Outcome);
        Assert.Empty(esito.Document.Airspaces);

        var sparita = AipCorrections.Apply(Pdf("2612") with { Airspaces = [] }, esito.File);
        Assert.Equal(CorrectionOutcomeKind.SupersededByAip, Assert.Single(sparita.Outcomes).Outcome);
    }

    [Fact]
    public void UnAreaSiCorreggeConLaChiaveDiIdentificativoENome()
    {
        var esito = AipCorrections.Apply(Pdf("2611"),
            File(Correzione("1", CorrectionKind.Airspace, "LI R901 PROVA", CorrectionOp.Change, AipCorrections.ToNode(Area("LI R901", "PROVA", 5.0)))));

        Assert.Equal(CorrectionOutcomeKind.Applied, Assert.Single(esito.Outcomes).Outcome);
        Assert.Equal(new Distance(5.0, DistanceUnit.Nm), Assert.IsType<CircleGeometry>(Assert.Single(esito.Document.Airspaces).Geometry).Radius);
    }

    [Fact]
    public void DueCorrezioniSulloStessoOggetto_ValeLUltima()
    {
        var esito = AipCorrections.Apply(Pdf("2611", Punto("ABRUS", 41.0, 12.0)), File(
            Correzione("1", CorrectionKind.Point, "ABRUS", CorrectionOp.Change, AipCorrections.ToNode(Punto("ABRUS", 41.5, 12.0))),
            Correzione("2", CorrectionKind.Point, "ABRUS", CorrectionOp.Change, AipCorrections.ToNode(Punto("ABRUS", 41.6, 12.0)))));

        Assert.Equal([(CorrectionOutcomeKind.SupersededByCorrection, "2"), (CorrectionOutcomeKind.Applied, null)],
            esito.Outcomes.Select(o => (o.Outcome, o.SupersededBy)));
        Assert.Equal(new Position(41.6, 12.0), esito.Document.Points![0].Position);
        Assert.Equal("2", esito.File.Corrections[0].Superseded);
    }

    [Fact]
    public void QuelCheNonVaNonSiApplica_ESiDicePerché()
    {
        var giusto = AipCorrections.ToNode(Punto("ABRUS", 41.5, 12.0));
        var esito = AipCorrections.Apply(Pdf("2611", Punto("ABRUS", 41.0, 12.0), Punto("DOPPIO", 1, 1), Punto("DOPPIO", 2, 2)), File(
            Correzione("1", CorrectionKind.Point, "ABRUS", CorrectionOp.Change, giusto) with { Source = " " },
            Correzione("2", CorrectionKind.Point, "BOLSA", CorrectionOp.Change, giusto),
            Correzione("3", CorrectionKind.Point, "DOPPIO", CorrectionOp.Remove),
            Correzione("4", CorrectionKind.Point, "ZETA", CorrectionOp.Add, new JsonObject { ["designator"] = "ZETA" }),
            Correzione("5", CorrectionKind.Hazard, "ETNA", CorrectionOp.Add),
            Correzione("4", CorrectionKind.Light, "FARO", CorrectionOp.Remove)));

        Assert.Equal(
            [CorrectionProblem.MissingSource, CorrectionProblem.NotInAip, CorrectionProblem.DuplicateKey, CorrectionProblem.UnreadableAfter,
             CorrectionProblem.MissingAfter, CorrectionProblem.DuplicateId],
            esito.Outcomes.Select(o => o.Problem!.Value));
        Assert.All(esito.Outcomes, o => Assert.Equal(CorrectionOutcomeKind.Invalid, o.Outcome));
        Assert.Equal(new Position(41.0, 12.0), esito.Document.Points![0].Position);
        Assert.Equal(3, esito.Document.Points.Count);
        Assert.All(esito.File.Corrections, c => Assert.Null(c.Superseded));
    }

    [Fact]
    public void IlFileVaEToraUguale_ELaVersioneSiLeggePrima()
    {
        var esito = AipCorrections.Apply(Pdf("2611", Punto("ABRUS", 41.0, 12.0)),
            File(Correzione("1", CorrectionKind.Point, "ABRUS", CorrectionOp.Change, AipCorrections.ToNode(Punto("ABRUS", 41.5, 12.0)))));

        var testo = AipCorrections.Serialize(esito.File);
        Assert.Contains("\"kind\": \"point\"", testo, StringComparison.Ordinal);
        Assert.Contains("\"op\": \"change\"", testo, StringComparison.Ordinal);
        var riletto = AipCorrections.Deserialize(testo);
        Assert.Equal(testo, AipCorrections.Serialize(riletto));

        // Riletto dal file, si applica come prima.
        Assert.Equal(CorrectionOutcomeKind.Applied, Assert.Single(AipCorrections.Apply(Pdf("2612", Punto("ABRUS", 41.0, 12.0)), riletto).Outcomes).Outcome);

        Assert.Equal(AipJsonError.UnsupportedSchemaVersion,
            Assert.Throws<AipJsonException>(() => AipCorrections.Deserialize("""{"schemaVersion": 2, "state": "LI"}""")).Error);
        Assert.Equal(AipJsonError.MissingSchemaVersion,
            Assert.Throws<AipJsonException>(() => AipCorrections.Deserialize("""{"state": "LI"}""")).Error);
    }

    [Fact]
    public void LaChiaveDiUnAttesa()
    {
        var attesa = new Holding
        {
            Section = "ENR 3.4.2",
            Fix = "PIS",
            FixType = "VOR/DME",
            Minimum = new VerticalLimit { Text = "FL110" },
            Turn = TurnDirection.Right,
            InboundTrack = 27,
        };

        Assert.Equal("PIS VOR/DME 027 right", AipCorrections.KeyOf(attesa));
        Assert.Equal("ABRUS 250 left", AipCorrections.KeyOf(attesa with { Fix = "ABRUS", FixType = null, InboundTrack = 250, Turn = TurnDirection.Left }));
    }
}
