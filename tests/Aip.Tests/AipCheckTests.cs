namespace Aip.Tests;

public sealed class AipCheckTests
{
    private static Airspace Area(string designator, Geometry geometria, string? nome = null, string sopra = "FL 195") => new()
    {
        Section = "ENR 5.1",
        Type = AirspaceType.Danger,
        Designator = designator,
        Name = nome,
        Upper = new VerticalLimit { Text = sopra },
        Lower = new VerticalLimit { Text = "GND" },
        Geometry = geometria,
    };

    private static readonly Geometry Cerchio = new CircleGeometry(new Position(42, 12), new Distance(1, DistanceUnit.Nm));

    [Fact]
    public void GliEsempiNonHannoProblemi()
    {
        var doc = AipJson.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Esempi", "aree-di-ogni-forma.json")));
        Assert.Empty(AipCheck.Run(doc));
    }

    [Fact]
    public void StessoIdentificativoConNomiDiversiVa_StessaChiaveNo()
    {
        var doc = new AipDocument
        {
            State = "LI",
            Airspaces = [Area("LI TSA74", Cerchio, "UNO"), Area("LI TSA74", Cerchio, "DUE"), Area("LI TSA74", Cerchio, "DUE")],
        };

        var problema = Assert.Single(AipCheck.Run(doc));
        Assert.Equal(AipProblemKind.DuplicateKey, problema.Kind);
        Assert.Equal("LI TSA74 DUE", problema.Key);
    }

    [Fact]
    public void UnAnelloDiDueRetteNonSiChiude_UnArcoDaSoloSi()
    {
        var p = new Position(42, 12);
        var dueRette = new RingGeometry([new LineSegment(p), new LineSegment(new Position(42, 13))]);
        var unArco = new RingGeometry([new ArcSegment(p, new Position(42.1, 12), null, ArcDirection.Clockwise)]);

        var doc = new AipDocument { State = "LI", Airspaces = [Area("A", dueRette), Area("B", unArco), Area("C", new RingGeometry([]))] };

        var problemi = AipCheck.Run(doc);
        Assert.Equal(
            [new AipProblem(AipProblemKind.TooFewSegments, "A", "2"), new AipProblem(AipProblemKind.TooFewSegments, "C", "0")],
            problemi);
    }

    [Fact]
    public void FuoriIntervalloRaggioNulloTestoVuotoStatoVuoto()
    {
        var doc = new AipDocument
        {
            State = " ",
            Airspaces =
            [
                Area("A", new CircleGeometry(new Position(91, 12), new Distance(0, DistanceUnit.Nm))),
                Area("B", new RingGeometry(
                [
                    new ArcSegment(new Position(42, 12), new Position(42, 181), new Distance(-1, DistanceUnit.Nm), ArcDirection.Clockwise),
                ])),
                Area("C", Cerchio, sopra: ""),
                Area("", Cerchio),
            ],
        };

        var tipi = AipCheck.Run(doc).Select(p => (p.Kind, p.Key)).ToList();

        Assert.Equal(
            [
                (AipProblemKind.MissingState, null),
                (AipProblemKind.OutOfRange, "A"),
                (AipProblemKind.NonPositiveRadius, "A"),
                (AipProblemKind.OutOfRange, "B"),
                (AipProblemKind.NonPositiveRadius, "B"),
                (AipProblemKind.MissingLimitText, "C"),
                (AipProblemKind.MissingDesignator, ""),
            ],
            tipi);
    }

    [Theory]
    [InlineData(1.0, DistanceUnit.Nm, 1.0)]
    [InlineData(1.852, DistanceUnit.Km, 1.0)]
    [InlineData(926.0, DistanceUnit.M, 0.5)]
    public void LeDistanzeInMiglia(double valore, DistanceUnit unita, double miglia) =>
        Assert.Equal(miglia, new Distance(valore, unita).ToNauticalMiles(), 12);
}
