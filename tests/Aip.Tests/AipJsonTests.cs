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
            Remarks = "For continuation see AIP MALTA",
            Notes = [new RouteNote(1, "Il tratto UNIMI-NAXAV è utilizzabile solo in direzione nord.", "Route usable northbound only."), new RouteNote(2, "Seconda.", null)],
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
        var elenco = new NavaidProcedures
        {
            Section = "ENR 3.4.2", Designator = "AHO", Type = "TACAN", Name = "Alghero", Affected = "EA star\nEA sid",
            Page = new AipPage("ENR 3.4.2-1", new DateOnly(2026, 9, 3)),
        };
        var frequenze = new AtsUnitFrequencies
        {
            Section = "ENR 2.1.1.1", Station = "Milano ACC", Service = "Servizio di controllo di area/ Area control service",
            CallSign = "Milano Radar", Hours = "H24\n(1)", Remarks = "1. A discrezione ATC",
            Frequencies = [new AtsFrequency { Text = "CH 118.805", Megahertz = 118.805, Emission = "A3E", NoteRefs = "(7)" }],
        };
        var linea = new AtsUnitLine
        {
            Section = "ENR 2.1.1.1", Unit = "Milano ACC/FIC", Side = "nord-ovest", Text = "a nord-ovest della linea …",
            Points = [new LinePoint { Position = new Position(46.2875, 10.0081) }, new LinePoint { Designator = "LUSIL", Position = new Position(46.2, 10.1) }],
            Lower = new VerticalLimit { Text = "SFC", Reference = VerticalReference.Ground },
            Upper = new VerticalLimit { Text = "FL 195 (incluso)", Reference = VerticalReference.FlightLevel, Value = 195, Unit = VerticalUnit.Fl },
        };
        var doc = new AipDocument
        {
            State = "LI", Routes = [Rotta(RouteDirection.Backward)], Holdings = [attesa], NavaidProcedures = [elenco], UnitFrequencies = [frequenze],
            UnitLines = [linea],
        };

        var json = AipJson.Serialize(doc);
        var riletto = AipJson.Deserialize(json);

        Assert.Equal(doc, riletto);
        Assert.NotEqual(doc with { NavaidProcedures = [elenco with { Affected = "EA star" }] }, riletto);
        Assert.DoesNotContain("navaidProcedures", AipJson.Serialize(new AipDocument { State = "LI" }));
        Assert.NotEqual(doc with { UnitFrequencies = [frequenze with { Frequencies = [] }] }, riletto);
        Assert.DoesNotContain("unitFrequencies", AipJson.Serialize(new AipDocument { State = "LI" }));
        Assert.NotEqual(doc with { UnitLines = [linea with { Points = linea.Points.Take(1).ToList() }] }, riletto);
        Assert.NotEqual(Rotta(RouteDirection.None), riletto.Routes![0]);
        Assert.NotEqual(Rotta(RouteDirection.Backward) with { Remarks = null }, riletto.Routes![0]);
        Assert.NotEqual(Rotta(RouteDirection.Backward) with { Notes = null }, riletto.Routes![0]);
        Assert.NotEqual(Rotta(RouteDirection.Backward) with { Notes = [] }, riletto.Routes![0]);
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
            AipJson.Deserialize("""{ "schemaVersion": 3, "state": "LI", "airspaces": [ { "nuovo": true } ] }"""));

        Assert.Equal(AipJsonError.UnsupportedSchemaVersion, e.Error);
        Assert.Equal("3", e.Detail);
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

    [Fact]
    public void RadioassistenzePuntiELuci_VannoETornanoUguali()
    {
        var vor = new Navaid
        {
            Section = "ENR 4.1",
            Designator = "MPV",
            Name = "MONTE PROVA",
            Type = "VOR/DME",
            Kind = NavaidKind.VorDme,
            MagneticVariation = "3° E-2020.0",
            Frequency = "113.80 MHZ",
            Channel = "85X",
            Position = new Position(41.5, 12.25),
            Components = [new NavaidComponent("VOR", new Position(41.5, 12.25), "H24"), new NavaidComponent("DME", new Position(41.51, 12.25), "H24")],
            Elevation = "443 M AMSL",
            Remarks = "1) MAINT: first MON",
            Page = new AipPage("ENR 4.1.1-1", new DateOnly(2026, 6, 11)),
        };
        var tacan = new Navaid
        {
            Section = "ENR 4.1", Military = true, Designator = "MPT", Name = "PROVA", Type = "TACAN", Kind = NavaidKind.Tacan,
            Channel = "99Y", Position = new Position(41.6, 15.7), Components = [new NavaidComponent(null, new Position(41.6, 15.7), "H24")],
        };
        var punto = new SignificantPoint
        {
            Section = "ENR 4.4", Designator = "ABADI", Position = new Position(40.75, 18.6), Routes = ["P92", "L12"],
            Procedures = "BR star, BR sid", Fra = "FRA(I)",
        };
        var luce = new GroundLight { Section = "ENR 4.5", Name = "CAPO DI PROVA", Type = "Marittimo / Maritime", Intensity = "668,8" };
        var doc = new AipDocument { State = "LI", Navaids = [vor, tacan], Points = [punto], Lights = [luce] };

        var json = AipJson.Serialize(doc);
        var riletto = AipJson.Deserialize(json);

        Assert.Equal(doc, riletto);
        Assert.NotEqual(vor with { Components = [vor.Components[0]] }, riletto.Navaids![0]);
        Assert.NotEqual(punto with { Routes = ["P92"] }, riletto.Points![0]);
        Assert.NotEqual(doc with { Lights = null }, riletto);
        Assert.Contains("\"kind\": \"vorDme\"", json);
        Assert.Contains("\"military\": true", json);
        // Il militare si scrive solo dove c'è; la chiave non va nel file.
        Assert.Equal(1, json.Split("military").Length - 1);
        Assert.DoesNotContain("\"key\"", json);
        // Un documento senza ENR 4 non lo scrive: l'AIP espanso di prima non cambia.
        var vuoto = AipJson.Serialize(new AipDocument { State = "LI" });
        Assert.DoesNotContain("navaids", vuoto);
        Assert.DoesNotContain("points", vuoto);
        Assert.DoesNotContain("lights", vuoto);
    }

    [Fact]
    public void UnCorridoio_VaETornaUguale_ELoSchemaE2()
    {
        var corridoio = new CorridorGeometry(new Distance(10, DistanceUnit.Nm), [new Position(41, 18.5), new Position(40.9, 18.95), new Position(40.5, 19)]);
        var doc = new AipDocument
        {
            State = "LI",
            Airspaces =
            [
                new Airspace
                {
                    Section = "ENR 5.2", Type = AirspaceType.Military, Designator = "ALBA Corridor",
                    Upper = new VerticalLimit { Text = "FL200" }, Lower = new VerticalLimit { Text = "FL150" },
                    Hours = "H24", Unit = "Brindisi SCCAM", Geometry = corridoio,
                },
            ],
        };

        var json = AipJson.Serialize(doc);
        var riletto = AipJson.Deserialize(json);

        Assert.Equal(doc, riletto);
        Assert.Contains("\"schemaVersion\": 2", json);
        Assert.Contains("\"kind\": \"corridor\"", json);
        Assert.NotEqual(corridoio with { Width = new Distance(6, DistanceUnit.Nm) }, riletto.Airspaces[0].Geometry);
        // Un file dello schema 1 si legge ancora, e riscritto resta 1.
        var vecchio = AipJson.Deserialize("""{ "schemaVersion": 1, "state": "LI", "airspaces": [] }""");
        Assert.Equal(1, vecchio.SchemaVersion);
        Assert.Contains("\"schemaVersion\": 1", AipJson.Serialize(vecchio));
    }

    [Fact]
    public void UnoScalo_VaETornaUguale()
    {
        var scalo = new Aerodrome
        {
            Icao = "LICA", Name = "LAMEZIA TERME", Arp = new Position(38.9083, 16.2417), Elevation = "46 FT", ReferenceTemperature = "31.4° C",
            MagneticVariation = "4° E (2020.0)", AnnualChange = "6'E", Traffic = "IFR/VFR",
            Runways =
            [
                new Runway { Designator = "10", TrueBearing = "098.49°", Dimensions = "3015 x 45", Threshold = new Position(38.907, 16.2285), End = new Position(38.903, 16.2629), ThresholdElevation = "19.3 FT / NIL" },
                new Runway { Designator = "28", TrueBearing = "278.51°", Remarks = "1) DTHR 366 m" },
            ],
            DeclaredDistances = [new DeclaredDistance("10", null, "3015", "3075", "3015", "3015"), new DeclaredDistance("10", "INT TAKE-OFF B", "671", "731", "671", "-")],
            Communications = [new CommunicationFacility("TWR", "Lamezia TWR", "125.900", "H24")],
            Page = new AipPage("AD 2 LICA 1-1", new DateOnly(2026, 6, 11)),
        };
        var doc = new AipDocument { State = "LI", Aerodromes = [scalo] };

        var json = AipJson.Serialize(doc);
        var riletto = AipJson.Deserialize(json);

        Assert.Equal(doc, riletto);
        Assert.NotEqual(scalo with { Runways = [scalo.Runways[0]] }, riletto.Aerodromes![0]);
        Assert.DoesNotContain("aerodromes", AipJson.Serialize(new AipDocument { State = "LI" }));
    }

    [Fact]
    public void IPericoliELeAtteseSulleRadioassistenze_VannoETornanoUguali()
    {
        var doc = new AipDocument
        {
            State = "LI",
            Hazards =
            [
                new Hazard { Section = "ENR 5.3.1", Kind = HazardKind.Balloon, Name = "AREA ETNEA (A)", Position = new Position(37.75, 14.76), Radius = new Distance(16, DistanceUnit.Km), Vertical = "20000 m" },
                new Hazard { Section = "ENR 5.3.3", Kind = HazardKind.Volcano, Name = "ETNA 211060", Position = new Position(37.73, 15.0), Vertical = "3330m 10922ft", Remarks = "LI-P1" },
            ],
            Holdings =
            [
                new Holding
                {
                    Section = "ENR 3.4.2", Fix = "AEA", FixType = "VOR/DME", Minimum = new VerticalLimit { Text = "4000" }, Turn = TurnDirection.Left,
                    InboundTrack = 48, NoteRefs = "(1)", Affected = "EA star\nEA sid",
                },
            ],
            MinimumAltitudes =
            [
                new MinimumAltitudeArea
                {
                    Aerodrome = "LICA", Minimum = new VerticalLimit { Text = "5000FT" }, Name = "NORTH-EAST", NoteRefs = "(5)",
                    Geometry = new CircleGeometry(new Position(38.9, 16.2), new Distance(7, DistanceUnit.Nm)),
                },
            ],
        };

        var json = AipJson.Serialize(doc);
        var riletto = AipJson.Deserialize(json);

        Assert.Equal(doc, riletto);
        Assert.Contains("\"kind\": \"volcano\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("hazards", AipJson.Serialize(new AipDocument { State = "LI" }), StringComparison.Ordinal);
    }

    [Fact]
    public void LeRadioassistenzeEGliIlsDiUnoScalo_VannoETornanoUguali()
    {
        var lmt = new Navaid
        {
            Section = "AD 2.19", Designator = "LMT", Name = "LMT", Type = "DVOR/DME", Kind = NavaidKind.DvorDme, Frequency = "112.50 MHZ", Channel = "72X",
            Position = new Position(38.9044, 16.2656), Components = [new NavaidComponent("DVOR", new Position(38.9044, 16.2656), "H24"), new NavaidComponent("DME", new Position(38.9046, 16.2656), "H24")],
        };
        var ils = new Ils
        {
            Runway = "28", Category = "CAT I", Designator = "LAM", MagneticVariation = "3° E-2010.0", Slope = "3°", ReferenceDatumHeight = "17 M",
            Components = [new IlsComponent("LOC", new Position(38.9073, 16.226), "110.30 MHZ", Hours: "H24"), new IlsComponent("DME-P", new Position(38.9024, 16.2546), Channel: "40X", Elevation: "16 M AMSL")],
        };
        var doc = new AipDocument { State = "LI", Aerodromes = [new Aerodrome { Icao = "LICA", Name = "LAMEZIA TERME", NavigationAids = [lmt], Ils = [ils] }] };

        var riletto = AipJson.Deserialize(AipJson.Serialize(doc));

        Assert.Equal(doc, riletto);
        Assert.NotEqual(ils with { Components = [ils.Components[0]] }, riletto.Aerodromes![0].Ils[0]);
    }
    // Carta F7, slice 7a: le procedure di AD 2 vanno e tornano uguali, coi tratti (l'uguaglianza è scritta a mano per le liste).
    [Fact]
    public void LeProcedureVannoETornanoUguali()
    {
        var sid = new Procedure
        {
            Aerodrome = "LIXX",
            Kind = ProcedureKind.Sid,
            Designator = "PROVA 1A",
            Runways = ["16L", "16R"],
            Section = "SID RNAV1 RWY 16 L/R DESCRIPTION",
            Legs =
            [
                new ProcedureLeg { PathTerminator = "CA", Course = "159°", TrueCourse = "162.6", Altitude = "+410" },
                new ProcedureLeg { PathTerminator = "RF", Fix = "XX604", ArcCenter = "XX620", ArcRadiusNm = 2.1, DistanceNm = 2.5, Turn = "R", Speed = "-200", Navigation = "RNP1" },
                new ProcedureLeg { PathTerminator = "TF", Fix = "PROVA", FlyOver = true, Altitude = "+FL110", Notes = "(2)" },
            ],
            Description = "Proceed via …",
            Minimums = "MCA/MCL: PROVA FL110",
            Remarks = "(1) Nota.",
            Page = new AipPage("AD 2 LIXX 6-3", new DateOnly(2026, 6, 11)),
        };
        var doc = new AipDocument { State = "LI", Airspaces = [], Procedures = [sid] };

        var riletto = AipJson.Deserialize(AipJson.Serialize(doc));

        Assert.Equal(doc, riletto);
        Assert.Equal("LIXX Sid PROVA 1A RWY 16L/16R", riletto.Procedures![0].Key);
        Assert.NotEqual(sid with { Legs = [sid.Legs[0]] }, riletto.Procedures[0]);
        Assert.NotEqual(sid with { Runways = ["16L"] }, riletto.Procedures[0]);
    }

    // Slice 7b: i tratti ricavati dalla prosa, coi punti calcolati e l'arco DME, vanno e tornano uguali; il segno che vengono
    // dalla prosa conta nell'uguaglianza, e non si scrive quando è falso.
    [Fact]
    public void LeProcedureDallaProsaVannoETornanoUguali()
    {
        var star = new Procedure
        {
            Aerodrome = "LIXX", Kind = ProcedureKind.Star, Designator = "PROVA 1H", Runways = ["10"], LegsFromDescription = true,
            Description = "PROVA – TR 310° (RDL 130 LMT VOR) …",
            Legs =
            [
                new ProcedureLeg { PathTerminator = "IF", Fix = "PROVA" },
                new ProcedureLeg { PathTerminator = "CF", Computed = "RDL 312/15 NM LMT", Position = new Position(39.08, 16.04), Course = "132°" },
                new ProcedureLeg { PathTerminator = "AF", Computed = "RDL 283/15 NM LMT", Position = new Position(38.97, 15.96), ArcCenter = "LMT", ArcRadiusNm = 15, Turn = "L" },
            ],
        };
        var doc = new AipDocument { State = "LI", Airspaces = [], Procedures = [star] };

        var json = AipJson.Serialize(doc);
        var riletto = AipJson.Deserialize(json);

        Assert.Equal(doc, riletto);
        Assert.NotEqual(star with { LegsFromDescription = false }, riletto.Procedures![0]);
        Assert.Contains("\"legsFromDescription\": true", json, StringComparison.Ordinal);
        Assert.DoesNotContain("legsFromDescription", AipJson.Serialize(doc with { Procedures = [star with { LegsFromDescription = false }] }), StringComparison.Ordinal);
    }

    // Carta «L'AIP intero», passo 4: gli ostacoli di ENR 5.4 (con la fine dei lineari e i numeri decimali), le aree di ENR
    // 5.5 e 5.6 coi tipi nuovi e a chi si notifica, vanno e tornano uguali; un documento senza ostacoli non li scrive.
    [Fact]
    public void GliOstacoliELeAreeSportiveVannoETornanoUguali()
    {
        var puntuale = new Obstacle
        {
            Section = "ENR 5.4.1.13", Kind = ObstacleKind.Point, Designator = "T004963", Region = "PUGLIA", Province = "FOGGIA", Town = "ASCOLI",
            Site = "FONTANELLE", Type = "ANEMOMETRO", TypeEnglish = "ANEMOMETER", Position = new Position(41.232778, 15.663333),
            HeightM = 118.2, ElevationM = 321, ElevationFt = 1053, DayMarking = "YES", NightMarking = "NO",
            Page = new AipPage("ENR 5.4.1.13-18", new DateOnly(2026, 5, 14)),
        };
        var lineare = new Obstacle
        {
            Section = "ENR 5.4.3.1", Kind = ObstacleKind.Line, Designator = "L003543", Position = new Position(42.049167, 14.5725),
            End = new Position(42.052778, 14.568056), LengthM = 532, LowestM = 181, HighestFt = 741, HeightM = 55, HeightFt = 180,
            DayMarking = "Note RMKs", Remarks = "Ostacolo lineare sul fiume Alento/Linear obstacle on the Alento river",
        };
        var lancio = new Airspace
        {
            Section = "ENR 5.5", Type = AirspaceType.Parachute, Designator = "ACQUI TERME -CIV-", Upper = new VerticalLimit { Text = "FL80" },
            Lower = new VerticalLimit { Text = "SFC" }, Hours = "HJ DAILY", Notification = "MILANO SCCAM", Unit = "MILANO ACC/FIC",
            Geometry = new CircleGeometry(new Position(44.678889, 8.502778), new Distance(2, DistanceUnit.Nm)),
        };
        var parco = lancio with { Section = "ENR 5.6", Type = AirspaceType.NaturePark, Designator = "Parco Prova", Notification = null, Unit = null, Hours = null };
        var doc = new AipDocument { State = "LI", Airspaces = [lancio, parco], Obstacles = [puntuale, lineare] };

        var json = AipJson.Serialize(doc);
        var riletto = AipJson.Deserialize(json);

        Assert.Equal(doc, riletto);
        Assert.NotEqual(doc with { Obstacles = [puntuale, lineare with { End = null }] }, riletto);
        Assert.NotEqual(doc with { Airspaces = [lancio with { Notification = null }, parco] }, riletto);
        Assert.Contains("\"type\": \"naturePark\"", json, StringComparison.Ordinal);
        Assert.Contains("\"heightM\": 118.2", json, StringComparison.Ordinal);
        Assert.DoesNotContain("obstacles", AipJson.Serialize(new AipDocument { State = "LI" }), StringComparison.Ordinal);
    }
}
