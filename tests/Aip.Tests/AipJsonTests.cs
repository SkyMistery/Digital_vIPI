using System.Text;
using System.Text.Json;

namespace Aip.Tests;

/// <summary>
/// La slice 0a della F6, «finita quando»: un'area di ogni forma (poligono, cerchio, arco, confine) va in JSON e
/// torna uguale. Gli esempi in <c>Esempi/</c> sono scritti a mano, con dati inventati.
/// </summary>
public sealed class AipJsonTests
{
    private static string Esempio(string nome) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Esempi", nome));

    [Fact]
    public void GliEsempiScrittiAManoSiLeggonoComeSonoScritti()
    {
        var doc = AipJson.Deserialize(Esempio("aree-di-ogni-forma.json"));

        Assert.Equal(1, doc.SchemaVersion);
        Assert.Equal("LI", doc.State);
        Assert.Equal("2609", doc.Airac);
        Assert.Equal(new DateOnly(2026, 9, 3), doc.Effective);
        Assert.Equal(4, doc.Airspaces.Count);

        var poligono = doc.Airspaces[0];
        Assert.Equal(AirspaceType.Prohibited, poligono.Type);
        Assert.Equal("LI P901 PROVA POLIGONO", poligono.Key);
        Assert.Equal(new AipPage("ENR 5.1.1-1", new DateOnly(2025, 7, 10)), poligono.Page);
        Assert.Equal("2000 FT AMSL", poligono.Upper.Text);
        Assert.Equal(VerticalReference.Msl, poligono.Upper.Reference);
        Assert.Equal(2000, poligono.Upper.Value);
        Assert.Equal(VerticalUnit.Ft, poligono.Upper.Unit);
        Assert.Equal(VerticalReference.Ground, poligono.Lower.Reference);
        Assert.Null(poligono.Lower.Value);
        Assert.Equal("Prova di attività", poligono.Activity);
        Assert.Equal("Nota di prova, prima riga.\nSeconda riga: H24.", poligono.Remarks);
        var anello = Assert.IsType<RingGeometry>(poligono.Geometry);
        Assert.All(anello.Segments, s => Assert.IsType<LineSegment>(s));
        Assert.Equal(new Position(41.5, 12.25), anello.Segments[0].From);

        var cerchio = Assert.IsType<CircleGeometry>(doc.Airspaces[1].Geometry);
        Assert.Equal(new Position(42.0125, 13.6), cerchio.Center);
        Assert.Equal(new Distance(300.0, DistanceUnit.M), cerchio.Radius);

        var ctr = doc.Airspaces[2];
        Assert.Equal("D", ctr.Class);
        Assert.Equal("Nota di prova del CTR.", ctr.Remarks);
        Assert.Null(ctr.Activity);
        Assert.Null(ctr.Name);
        Assert.Equal("PROVA CTR", ctr.Key);
        var archi = Assert.IsType<RingGeometry>(ctr.Geometry).Segments;
        var orario = Assert.IsType<ArcSegment>(archi[1]);
        Assert.Equal(ArcDirection.Clockwise, orario.Direction);
        Assert.Equal(new Distance(7.5, DistanceUnit.Nm), orario.Radius);
        var antiorario = Assert.IsType<ArcSegment>(archi[2]);
        Assert.Equal(ArcDirection.CounterClockwise, antiorario.Direction);
        Assert.Null(antiorario.Radius);

        var confine = Assert.IsType<RingGeometry>(doc.Airspaces[3].Geometry).Segments;
        var stato = Assert.IsType<BorderSegment>(confine[1]);
        Assert.Equal(BorderKind.State, stato.Border);
        Assert.Equal("Italian northern geographical border", stato.Text);
        Assert.Equal(BorderKind.Coast, Assert.IsType<BorderSegment>(confine[3]).Border);
        Assert.Equal(VerticalReference.Unlimited, doc.Airspaces[3].Upper.Reference);
    }

    [Theory]
    [InlineData("aree-di-ogni-forma.json")]
    [InlineData("altro-stato.json")]
    public void UnEsempioVaInJsonETornaUguale(string nome)
    {
        var letto = AipJson.Deserialize(Esempio(nome));

        var scritto = AipJson.Serialize(letto);
        var riletto = AipJson.Deserialize(scritto);

        Assert.Equal(letto, riletto);
        // E la scrittura è stabile: scrivere il riletto dà lo stesso testo (l'AIP espanso in git non balla).
        Assert.Equal(scritto, AipJson.Serialize(riletto));
    }

    [Fact]
    public void UnModelloFattoNelCodiceTornaUgualeAncheNeiDecimali()
    {
        // 45°30'12.34"N 009°15'20.56"E: i decimali di una conversione da gradi, primi e secondi.
        var p = new Position(45 + 30 / 60.0 + 12.34 / 3600.0, 9 + 15 / 60.0 + 20.56 / 3600.0);
        var doc = new AipDocument
        {
            State = "LI",
            Airspaces =
            [
                new Airspace
                {
                    Section = "ENR 5.1",
                    Type = AirspaceType.Tsa,
                    Designator = "LI TSA74",
                    Name = "UNO",
                    Upper = new VerticalLimit { Text = "FL 195" },
                    Lower = new VerticalLimit { Text = "GND" },
                    Geometry = new RingGeometry(
                    [
                        new ArcSegment(p, new Position(45.4, 9.1), new Distance(2.8, DistanceUnit.Nm), ArcDirection.Clockwise),
                    ]),
                },
                new Airspace
                {
                    Section = "ENR 5.1",
                    Type = AirspaceType.Tsa,
                    Designator = "LI TSA74",
                    Name = "DUE",
                    Upper = new VerticalLimit { Text = "FL 195" },
                    Lower = new VerticalLimit { Text = "GND" },
                    Geometry = new CircleGeometry(p, new Distance(5.0, DistanceUnit.Km)),
                },
            ],
        };

        var riletto = AipJson.Deserialize(AipJson.Serialize(doc));

        Assert.Equal(doc, riletto);
        Assert.Equal(p, Assert.IsType<CircleGeometry>(riletto.Airspaces[1].Geometry).Center);
    }

    // Slice 1a: ENR 2 porta le fasce (la UIR: da UNL a FL660 «G», da FL660 a FL195 «C»), l'ente, gli orari, l'altitudine di
    // transizione. L'uguaglianza di Airspace è scritta a mano per le fasce: se un campo nuovo non ci entra, qui si vede.
    [Fact]
    public void LeFasceELEnteVannoInJsonETornanoUguali()
    {
        static Airspace Uir(string classeAlta) => new()
        {
            Section = "ENR 2.1",
            Type = AirspaceType.Uir,
            Designator = "PROVA UIR",
            Upper = new VerticalLimit { Text = "UNL", Reference = VerticalReference.Unlimited },
            Lower = new VerticalLimit { Text = "FL195", Reference = VerticalReference.FlightLevel, Value = 195, Unit = VerticalUnit.Fl },
            Bands =
            [
                new VerticalBand(new VerticalLimit { Text = "UNL" }, new VerticalLimit { Text = "FL660" }, classeAlta),
                new VerticalBand(new VerticalLimit { Text = "FL660" }, new VerticalLimit { Text = "FL195" }, "C"),
            ],
            Unit = "PROVA ACC/FIC\nALTRA ACC/FIC",
            Hours = "H24",
            TransitionAltitude = "6000ft",
            Geometry = new CircleGeometry(new Position(42, 12), new Distance(10, DistanceUnit.Nm)),
        };
        var doc = new AipDocument { State = "LI", Airspaces = [Uir("G")] };

        var riletto = AipJson.Deserialize(AipJson.Serialize(doc));

        Assert.Equal(doc, riletto);
        Assert.Equal(Uir("G"), riletto.Airspaces[0]);
        Assert.NotEqual(Uir("A"), riletto.Airspaces[0]);
        Assert.NotEqual(Uir("G") with { Unit = "un altro" }, riletto.Airspaces[0]);
        Assert.NotEqual(Uir("G") with { Bands = null }, riletto.Airspaces[0]);
        // Il testo si scrive com'è: le virgolette e le lettere accentate, non i loro codici.
        var leggibile = AipJson.Serialize(new AipDocument { State = "LI", Airspaces = [Uir("G") with { Designator = "Zona/Zone \"2\"", Remarks = "attività" }] });
        Assert.Contains("Zona/Zone \\\"2\\\"", leggibile);
        Assert.Contains("attività", leggibile);
        Assert.DoesNotContain("\\u00", leggibile);
        // Senza fasce il campo non si scrive.
        Assert.DoesNotContain("bands", AipJson.Serialize(new AipDocument { State = "LI", Airspaces = [Uir("G") with { Bands = null }] }));
    }

    // Slice 2: le rotte di ENR 3 (punti e tratti) e le attese di ENR 3.4.1. Le uguaglianze sono scritte a mano per le liste.
    [Fact]
    public void RotteEAtteseVannoInJsonETornanoUguali()
    {
        static Route Rotta(RouteDirection pari) => new()
        {
            Section = "ENR 3.2",
            Designator = "L99",
            Navigation = "RNAV 5",
            Points =
            [
                new RoutePoint("UNIMI", "UNIMI", new Position(46.860833, 11.065278), true, "For continuation see AIP AUSTRIA"),
                new RoutePoint("VIC", "VICENZA VOR/DME 'VIC'", new Position(45.637222, 11.676389), false),
                new RoutePoint("KOPER", "KOPER", new Position(45.153889, 11.641389), true),
            ],
            Segments =
            [
                new RouteSegment
                {
                    Track = "153° / 333°", DistanceNm = 26.1, LateralLimits = "5",
                    Upper = new VerticalLimit { Text = "FL195" }, Lower = new VerticalLimit { Text = "FL155" },
                    Odd = RouteDirection.Forward, Even = pari, MinimumOdd = "150", MinimumEven = "160", Remarks = "CDR1",
                },
                new RouteSegment { Discontinuity = true },
            ],
            Page = new AipPage("ENR 3.2 L99-1", new DateOnly(2024, 3, 21)),
        };
        var attesa = new Holding
        {
            Section = "ENR 3.4.1",
            Fix = "ABRUS",
            Bearing = "RDL070/10NM PAN",
            Minimum = new VerticalLimit { Text = "3000" },
            Turn = TurnDirection.Left,
            InboundTrack = 250,
            NoteRefs = "(1)",
            Remarks = "IAS MAX 190kt",
        };
        var doc = new AipDocument { State = "LI", Routes = [Rotta(RouteDirection.Backward)], Holdings = [attesa] };

        var json = AipJson.Serialize(doc);
        var riletto = AipJson.Deserialize(json);

        Assert.Equal(doc, riletto);
        Assert.NotEqual(Rotta(RouteDirection.None), riletto.Routes![0]);
        Assert.NotEqual(attesa with { Turn = TurnDirection.Right }, riletto.Holdings![0]);
        Assert.NotEqual(doc with { Holdings = null }, riletto);
        Assert.Contains("\"even\": \"backward\"", json);
        Assert.DoesNotContain("\"forward\": ", json);
        // Un documento senza rotte né attese non le scrive: l'AIP espanso di prima non cambia.
        Assert.DoesNotContain("routes", AipJson.Serialize(new AipDocument { State = "LI" }));
    }

    [Fact]
    public void DueAnelliConGliStessiTrattiSonoUguali_UnTrattoDiversoNo()
    {
        RingGeometry Anello(double lon) => new(
        [
            new LineSegment(new Position(41, 12)),
            new LineSegment(new Position(41, lon)),
            new LineSegment(new Position(40, 12)),
        ]);

        Assert.Equal(Anello(13), Anello(13));
        Assert.NotEqual(Anello(13), Anello(13.0001));
    }

    [Fact]
    public void IlJsonScriveLeParoleEIlKindPerPrimo()
    {
        var json = AipJson.Serialize(AipJson.Deserialize(Esempio("aree-di-ogni-forma.json")));

        Assert.Contains("\"type\": \"prohibited\"", json);
        Assert.Contains("\"direction\": \"counterClockwise\"", json);
        Assert.Contains("\"reference\": \"flightLevel\"", json);
        // Il discriminatore apre l'oggetto: su net8 il lettore lo pretende lì.
        using (var letto = JsonDocument.Parse(json))
        {
            var conKind = OggettiConKind(letto.RootElement).ToList();
            Assert.Equal(4 + 3 + 3 + 4, conKind.Count); // le 4 geometrie, poi i tratti dei tre anelli
            Assert.All(conKind, o => Assert.Equal("kind", o.EnumerateObject().First().Name));
        }
        // I vuoti non si scrivono: il CTR non ha nome, l'arco antiorario non ha raggio.
        Assert.DoesNotContain("\"name\": null", json);
        Assert.DoesNotContain("null", json);
        // La chiave è calcolata, non scritta.
        Assert.DoesNotContain("\"key\"", json);
    }

    private static IEnumerable<JsonElement> OggettiConKind(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Object)
        {
            if (e.TryGetProperty("kind", out _))
                yield return e;
            foreach (var p in e.EnumerateObject())
                foreach (var o in OggettiConKind(p.Value))
                    yield return o;
        }
        else if (e.ValueKind == JsonValueKind.Array)
        {
            foreach (var v in e.EnumerateArray())
                foreach (var o in OggettiConKind(v))
                    yield return o;
        }
    }

    [Fact]
    public void SiLeggeAncheDaUnoStream()
    {
        var doc = AipJson.Deserialize(Esempio("altro-stato.json"));
        using var flusso = new MemoryStream();

        AipJson.Serialize(flusso, doc);
        flusso.Position = 0;

        Assert.Equal(doc, AipJson.Deserialize(flusso));
        Assert.Equal("LF", doc.State);
        Assert.Equal(AirspaceType.Tma, doc.Airspaces[0].Type);
        Assert.Equal(VerticalReference.Unknown, doc.Airspaces[0].Lower.Reference);
    }

    [Fact]
    public void UnoSchemaPiuNuovoSiDiceComeTale()
    {
        var e = Assert.Throws<AipJsonException>(() =>
            AipJson.Deserialize("""{ "schemaVersion": 2, "state": "LI", "airspaces": [ { "nuovo": true } ] }"""));

        Assert.Equal(AipJsonError.UnsupportedSchemaVersion, e.Error);
        Assert.Equal("2", e.Detail);
    }

    [Theory]
    [InlineData("""{ "state": "LI" }""")]
    [InlineData("""{ "schemaVersion": "1", "state": "LI" }""")]
    [InlineData("""[ 1 ]""")]
    public void SenzaVersioneNonSiLegge(string json)
    {
        var e = Assert.Throws<AipJsonException>(() => AipJson.Deserialize(json));
        Assert.Equal(AipJsonError.MissingSchemaVersion, e.Error);
    }

    [Fact]
    public void NonJson()
    {
        var e = Assert.Throws<AipJsonException>(() => AipJson.Deserialize("non è json"));
        Assert.Equal(AipJsonError.NotJson, e.Error);

        using var flusso = new MemoryStream(Encoding.UTF8.GetBytes("{ rotto"));
        Assert.Equal(AipJsonError.NotJson, Assert.Throws<AipJsonException>(() => AipJson.Deserialize(flusso)).Error);
    }

    [Theory]
    // manca lo Stato (required)
    [InlineData("""{ "schemaVersion": 1, "airspaces": [] }""")]
    // un kind che non esiste
    [InlineData("""{ "schemaVersion": 1, "state": "LI", "airspaces": [ { "section": "ENR 5.1", "type": "danger", "designator": "X", "upper": { "text": "UNL" }, "lower": { "text": "GND" }, "geometry": { "kind": "triangle" } } ] }""")]
    // una geometria senza kind
    [InlineData("""{ "schemaVersion": 1, "state": "LI", "airspaces": [ { "section": "ENR 5.1", "type": "danger", "designator": "X", "upper": { "text": "UNL" }, "lower": { "text": "GND" }, "geometry": { "center": { "lat": 1, "lon": 1 } } } ] }""")]
    // un tipo che non esiste
    [InlineData("""{ "schemaVersion": 1, "state": "LI", "airspaces": [ { "section": "ENR 5.1", "type": "dangerous", "designator": "X", "upper": { "text": "UNL" }, "lower": { "text": "GND" }, "geometry": { "kind": "circle", "center": { "lat": 1, "lon": 1 }, "radius": { "value": 1, "unit": "nm" } } } ] }""")]
    // un limite senza il testo stampato
    [InlineData("""{ "schemaVersion": 1, "state": "LI", "airspaces": [ { "section": "ENR 5.1", "type": "danger", "designator": "X", "upper": { "value": 1 }, "lower": { "text": "GND" }, "geometry": { "kind": "circle", "center": { "lat": 1, "lon": 1 }, "radius": { "value": 1, "unit": "nm" } } } ] }""")]
    // un numero fra virgolette
    [InlineData("""{ "schemaVersion": 1, "state": "LI", "airspaces": [ { "section": "ENR 5.1", "type": "danger", "designator": "X", "upper": { "text": "UNL" }, "lower": { "text": "GND" }, "geometry": { "kind": "circle", "center": { "lat": "1", "lon": 1 }, "radius": { "value": 1, "unit": "nm" } } } ] }""")]
    // un enum come numero
    [InlineData("""{ "schemaVersion": 1, "state": "LI", "airspaces": [ { "section": "ENR 5.1", "type": 3, "designator": "X", "upper": { "text": "UNL" }, "lower": { "text": "GND" }, "geometry": { "kind": "circle", "center": { "lat": 1, "lon": 1 }, "radius": { "value": 1, "unit": "nm" } } } ] }""")]
    public void UnContenutoCheNonTornaSiDiceInvalid(string json)
    {
        var e = Assert.Throws<AipJsonException>(() => AipJson.Deserialize(json));
        Assert.Equal(AipJsonError.Invalid, e.Error);
    }
}
