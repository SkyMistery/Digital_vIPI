using System.Text;
using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Aip.Aurora.Tests;

/// <summary>
/// La slice 0b della F6, «finita quando»: gli esempi scritti a mano diventano righe che il motore del sector rilegge,
/// con la forma che torna entro la densità.
/// </summary>
public sealed class RilettoDalMotoreTests
{
    private static AipDocument Esempio(string nome) =>
        AipJson.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Esempi", nome)));

    private static ParseResult<Line> Rileggi(IReadOnlyList<string> righe, ListaDiAvvisi avvisi, string nome = "li.restrict") =>
        new GeoParser(avvisi).Parse(
            SectorFileReader.Decode(Encoding.UTF8.GetBytes(string.Join("\r\n", righe) + "\r\n")), nome, new ColorPalette());

    // Mezzo millesimo di secondo d'arco: l'arrotondamento della scrittura (~1,5 cm).
    private const double MezzoMillesimo = 0.5 / 3_600_000 + 1e-12;

    private static void Vicino(Position atteso, Coordinate letto)
    {
        Assert.InRange(letto.LatitudeDeg, atteso.Lat - MezzoMillesimo, atteso.Lat + MezzoMillesimo);
        Assert.InRange(letto.LongitudeDeg, atteso.Lon - MezzoMillesimo, atteso.Lon + MezzoMillesimo);
    }

    private static Position P(Coordinate c) => new(c.LatitudeDeg, c.LongitudeDeg);

    [Fact]
    public void OgniAreaDegliEsempiTornaDalMotoreSenzaAvvisi()
    {
        var doc = Esempio("aree-di-ogni-forma.json");
        var avvisi = new ListaDiAvvisi();

        var scritto = AuroraGeo.Write(doc.Airspaces, doc.State, doc.Airac);
        var letto = Rileggi(scritto.Lines, avvisi);

        Assert.Empty(avvisi.Tutti);
        var metadati = Metadati.Leggi(letto);
        Assert.Empty(metadati.Problemi);
        Assert.Equal("AIRAC2609", metadati.DelFile["source"]);
        Assert.Equal(["P901", "R902", "PROVA CTR", "D903"], metadati.Record.Select(r => r.Nome));
        Assert.All(metadati.Record, r => Assert.True(r.Delimitato));

        // Ogni segmento sta nel blocco della sua area, e porta il suo nome nel 6° campo.
        foreach (var r in metadati.Record)
            Assert.All(r.Records, s => Assert.Equal(r.Nome, s.Nome));
        Assert.Equal(letto.Records.Count, metadati.Record.Sum(r => r.Records.Count));

        // Le quote e la classe come la carta §M regola 8.
        var chiavi = metadati.Record.ToDictionary(r => r.Nome, r => r.Chiavi);
        Assert.Equal("GND", chiavi["P901"]["lower"]);
        Assert.Equal("\"2000ft AMSL\"", chiavi["P901"]["upper"]);
        Assert.Equal("FL95", chiavi["R902"]["upper"]);
        Assert.Equal("SFC", chiavi["PROVA CTR"]["lower"]);
        Assert.Equal("D", chiavi["PROVA CTR"]["class"]);
        Assert.False(chiavi["P901"].ContainsKey("class"));
        Assert.Equal("FL285", chiavi["D903"]["lower"]);
        Assert.Equal("UNL", chiavi["D903"]["upper"]);

        // Il confine senza tracciato si dice, con la sua frase.
        Assert.Equal(
            [new AuroraIssue(AuroraIssueKind.BorderNotDrawn, "LI D903", "Italian northern geographical border"),
             new AuroraIssue(AuroraIssueKind.BorderNotDrawn, "LI D903", "line at 500 m from coast")],
            scritto.Issues);
    }

    [Fact]
    public void OgniAreaRilettaEUnAnelloChiusoCheTieneIVerticiDellAip()
    {
        var doc = Esempio("aree-di-ogni-forma.json");
        var letto = Rileggi(AuroraGeo.Write(doc.Airspaces, doc.State, doc.Airac).Lines, new ListaDiAvvisi());
        var perArea = letto.Records.GroupBy(s => s.Nome).ToDictionary(g => g.Key!, g => g.ToList());

        foreach (var (area, nome) in doc.Airspaces.Zip(["P901", "R902", "PROVA CTR", "D903"]))
        {
            var segmenti = perArea[nome];
            for (var i = 0; i < segmenti.Count; i++)
                Assert.Equal(segmenti[i].End, segmenti[(i + 1) % segmenti.Count].Start);

            if (area.Geometry is RingGeometry anello)
            {
                // I vertici dichiarati escono identici (al millesimo di secondo), nell'ordine del modello.
                var inizi = segmenti.Select(s => s.Start).ToList();
                var cerca = 0;
                foreach (var tratto in anello.Segments)
                {
                    cerca = inizi.FindIndex(cerca, c => Math.Abs(c.LatitudeDeg - tratto.From.Lat) <= MezzoMillesimo
                        && Math.Abs(c.LongitudeDeg - tratto.From.Lon) <= MezzoMillesimo);
                    Assert.True(cerca >= 0, $"{nome}: manca il vertice {tratto.From}");
                    Vicino(tratto.From, inizi[cerca]);
                }
            }
        }

        // Il poligono di tre rette: tre segmenti, né uno di più né uno di meno.
        Assert.Equal(3, perArea["P901"].Count);
        // I due tratti di confine senza tracciato: retta fra i capi, quindi quattro segmenti.
        Assert.Equal(4, perArea["D903"].Count);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(0.5)]
    [InlineData(4.0)]
    public void IlCerchioTornaSulRaggioConIPuntiDellaDensita(double densita)
    {
        var doc = Esempio("aree-di-ogni-forma.json");
        var cerchio = Assert.IsType<CircleGeometry>(doc.Airspaces[1].Geometry);
        var opzioni = new AuroraOptions { Outline = new OutlineOptions { PointsPerDegree = densita } };

        var segmenti = Rileggi(AuroraGeo.Write([doc.Airspaces[1]], "LI", null, opzioni).Lines, new ListaDiAvvisi()).Records;

        Assert.Equal((int)Math.Ceiling(360 * densita), segmenti.Count);
        var raggio = cerchio.Radius.ToNauticalMiles();
        // Ogni vertice sul raggio (300 m = 0,162 NM), entro l'arrotondamento al millesimo di secondo (~2 cm).
        Assert.All(segmenti, s => Assert.InRange(Sphere.DistanceNm(cerchio.Center, P(s.Start)) - raggio, -2e-5, 2e-5));
        // E fra due vertici la corda non si stacca dal cerchio più della freccia del passo.
        var freccia = raggio * (1 - Math.Cos(Math.PI / 180 / densita / 2));
        Assert.All(segmenti, s => Assert.InRange(
            raggio - Sphere.DistanceNm(cerchio.Center, Mezzo(P(s.Start), P(s.End))), -2e-5, freccia + 2e-5));
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(10.0)]
    public void GliArchiTornanoFraIRaggiDeiLoroEstremiEntroLaDensita(double densita)
    {
        var doc = Esempio("aree-di-ogni-forma.json");
        var ctr = doc.Airspaces[2];
        var tratti = Assert.IsType<RingGeometry>(ctr.Geometry).Segments;
        var opzioni = new AuroraOptions { Outline = new OutlineOptions { PointsPerDegree = densita } };

        var vertici = Rileggi(AuroraGeo.Write([ctr], "LI", null, opzioni).Lines, new ListaDiAvvisi())
            .Records.Select(s => P(s.Start)).ToList();

        // L'arco orario: da tratti[1].From a tratti[2].From attorno al centro; l'antiorario da tratti[2] a tratti[0].
        foreach (var (i, fine) in new[] { (1, tratti[2].From), (2, tratti[0].From) })
        {
            var arco = Assert.IsType<ArcSegment>(tratti[i]);
            var r0 = Sphere.DistanceNm(arco.Center, arco.From);
            var r1 = Sphere.DistanceNm(arco.Center, fine);
            var dal = vertici.FindIndex(v => Sphere.DistanceNm(v, arco.From) < 1e-4);
            var al = vertici.FindIndex(v => Sphere.DistanceNm(v, fine) < 1e-4);
            var pezzo = al > dal ? vertici[dal..(al + 1)] : [.. vertici[dal..], vertici[0]];

            Assert.True(pezzo.Count >= 3, "un arco disegnato ha punti in mezzo");
            var passo = 1.0 / densita;
            foreach (var (a, b) in pezzo.Zip(pezzo.Skip(1)))
            {
                // Il passo angolare non supera quello della densità, a meno dell'arrotondamento dei due vertici al
                // millesimo di secondo (~2 cm ciascuno: a 5 NM dal centro fanno ~0,0001°).
                var angolo = Math.Abs(((Sphere.BearingDeg(arco.Center, b) - Sphere.BearingDeg(arco.Center, a)) + 540) % 360 - 180);
                Assert.True(angolo <= passo + 3e-4, $"passo {angolo}° oltre {passo}°");
                // …e ogni vertice sta fra il raggio dell'inizio e quello della fine.
                var r = Sphere.DistanceNm(arco.Center, b);
                Assert.InRange(r, Math.Min(r0, r1) - 2e-5, Math.Max(r0, r1) + 2e-5);
            }
        }
    }

    [Fact]
    public void UnArcoDaSoloFaIlGiroIntero()
    {
        var centro = new Position(42, 12);
        var inizio = Sphere.Destination(centro, 90, 2.8);
        var area = Area("LI R1", new RingGeometry([new ArcSegment(inizio, centro, new Distance(2.8, DistanceUnit.Nm), ArcDirection.Clockwise)]));

        var scritto = AuroraGeo.Write([area], "LI", null);
        var segmenti = Rileggi(scritto.Lines, new ListaDiAvvisi()).Records;

        Assert.Empty(scritto.Issues);
        Assert.Equal(360, segmenti.Count);
        Vicino(inizio, segmenti[0].Start);
        Vicino(inizio, segmenti[^1].End);
    }

    [Fact]
    public void UnRaggioStampatoCheNonTornaSiDice()
    {
        var centro = new Position(42, 12);
        var area = Area("LI R2", new RingGeometry(
        [
            new ArcSegment(Sphere.Destination(centro, 0, 5), centro, new Distance(4.5, DistanceUnit.Nm), ArcDirection.Clockwise),
            new LineSegment(Sphere.Destination(centro, 90, 5)),
        ]));

        var problema = Assert.Single(AuroraGeo.Write([area], "LI", null).Issues);

        Assert.Equal(AuroraIssueKind.RadiusMismatch, problema.Kind);
        Assert.Equal("0.5", problema.Detail);
    }

    [Fact]
    public void IlConfineSiDisegnaColTracciatoDatoDaFuori()
    {
        var doc = Esempio("aree-di-ogni-forma.json");
        var d903 = doc.Airspaces[3];
        Position[] inMezzo = [new(46.0, 7.3), new(46.1, 7.5)];
        var opzioni = new AuroraOptions
        {
            Outline = new OutlineOptions
            {
                Border = (tratto, _, _) => tratto.Border == BorderKind.State ? inMezzo : null,
            },
        };

        var scritto = AuroraGeo.Write([d903], "LI", null, opzioni);
        var segmenti = Rileggi(scritto.Lines, new ListaDiAvvisi()).Records;

        Assert.Equal(6, segmenti.Count);
        Vicino(inMezzo[0], segmenti[2].Start);
        Vicino(inMezzo[1], segmenti[3].Start);
        // La costa, che il tracciatore non sa fare, resta una retta e si dice.
        Assert.Equal(AuroraIssueKind.BorderNotDrawn, Assert.Single(scritto.Issues).Kind);
    }

    [Fact]
    public void DueAreeConLoStessoNomeNonSiSaldano()
    {
        var cerchio = new CircleGeometry(new Position(42, 12), new Distance(1, DistanceUnit.Nm));
        Airspace[] aree = [Area("LI TSA74", cerchio, "UNO"), Area("LI TSA74", cerchio, "DUE"), Area("LI TSA74", cerchio)];

        var scritto = AuroraGeo.Write(aree, "LI", null);
        var metadati = Metadati.Leggi(Rileggi(scritto.Lines, new ListaDiAvvisi()));

        Assert.Empty(metadati.Problemi);
        Assert.Equal(["TSA74", "TSA74 DUE", "TSA74 (2)"], metadati.Record.Select(r => r.Nome));
        Assert.Equal(
            [AuroraIssueKind.DuplicateName, AuroraIssueKind.DuplicateName],
            scritto.Issues.Select(p => p.Kind));
    }

    [Fact]
    public void UnNomeColPuntoEVirgolaSiCambiaESiDice()
    {
        var area = Area("LI R3;A", new CircleGeometry(new Position(42, 12), new Distance(1, DistanceUnit.Nm)));

        var scritto = AuroraGeo.Write([area], "LI", null, new AuroraOptions { StripState = false });
        var letto = Rileggi(scritto.Lines, new ListaDiAvvisi());

        Assert.Equal("LI R3,A", Assert.Single(Metadati.Leggi(letto).Record).Nome);
        Assert.Equal(new AuroraIssue(AuroraIssueKind.NameChanged, "LI R3;A", "LI R3,A"), Assert.Single(scritto.Issues));
    }

    [Fact]
    public void FileUnoPerTipoCheSiRileggonoDalDisco()
    {
        var doc = Esempio("aree-di-ogni-forma.json");
        var cartella = Path.Combine(Path.GetTempPath(), "aip-aurora-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (file, _) = AuroraFiles.Convert(doc);
            AuroraFiles.Write(file, cartella);

            Assert.Equal(["li-ctr.geo", "li.danger", "li.prohibit", "li.restrict"], Directory.GetFiles(cartella).Select(Path.GetFileName).Order(StringComparer.Ordinal));
            var letto = SectorFileReader.Read(Path.Combine(cartella, "li.prohibit"));
            Assert.Equal("\r\n", letto.NewLine);
            Assert.False(letto.HasByteOrderMark);
            var aree = new GeoParser(new ListaDiAvvisi()).Parse(Path.Combine(cartella, "li.restrict"), new ColorPalette());
            Assert.All(aree.Records, s => Assert.Equal("RESTRICT", s.Color));
            Assert.Equal("CTR", new GeoParser(new ListaDiAvvisi()).Parse(Path.Combine(cartella, "li-ctr.geo"), new ColorPalette()).Records[0].Color);
        }
        finally
        {
            if (Directory.Exists(cartella))
                Directory.Delete(cartella, recursive: true);
        }
    }

    private static Position Mezzo(Position a, Position b) =>
        Sphere.Destination(a, Sphere.BearingDeg(a, b), Sphere.DistanceNm(a, b) / 2);

    private static Airspace Area(string designator, Geometry geometria, string? nome = null) => new()
    {
        Section = "ENR 5.1",
        Type = AirspaceType.Restricted,
        Designator = designator,
        Name = nome,
        Upper = new VerticalLimit { Text = "FL 195", Reference = VerticalReference.FlightLevel, Value = 195, Unit = VerticalUnit.Fl },
        Lower = new VerticalLimit { Text = "GND", Reference = VerticalReference.Ground },
        Geometry = geometria,
    };
}

internal sealed class ListaDiAvvisi : IWarningCollector
{
    private readonly List<LoadWarning> _avvisi = [];

    public IReadOnlyList<LoadWarning> Tutti => _avvisi;

    public void Add(LoadWarning warning) => _avvisi.Add(warning);

    public void Add(WarningSeverity severity, WarningCategory category, string source, string message,
        int? lineNumber = null, string? rawSnippet = null)
        => Add(new LoadWarning(severity, category, source, message, lineNumber, rawSnippet));

    public IReadOnlyList<LoadWarning> Snapshot() => _avvisi.ToArray();

#pragma warning disable CS0067 // Il motore lo dichiara; qui nessuno ascolta.
    public event EventHandler<LoadWarning>? WarningAdded;
#pragma warning restore CS0067

    public void Clear() => _avvisi.Clear();

    public int Count => _avvisi.Count;
}
