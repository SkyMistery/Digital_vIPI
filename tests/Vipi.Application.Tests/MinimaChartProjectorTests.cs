using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// <see cref="MinimaChartProjector"/>: proiezione pura di una carta MRVA in SVG. Le due proprietà che contano sono
/// che tutto stia nello STESSO viewBox (tracciati ed etichette sono elementi indipendenti) e che un tracciato
/// aperto resti aperto.
/// </summary>
public class MinimaChartProjectorTests
{
    private static MvaChart Chart(IEnumerable<MvaShape>? shapes = null, IEnumerable<MvaLabel>? labels = null) =>
        new((shapes ?? Array.Empty<MvaShape>()).ToList(), (labels ?? Array.Empty<MvaLabel>()).ToList());

    private static MvaShape Square(string name = "A", bool closed = true) => new(name, closed, new[]
    {
        new MvaPoint(41.0, 12.0), new MvaPoint(41.0, 13.0),
        new MvaPoint(42.0, 13.0), new MvaPoint(42.0, 12.0),
        new MvaPoint(41.0, 12.0),
    });

    [Fact]
    public void Closed_Shape_Path_Ends_With_Z()
    {
        var svg = MinimaChartProjector.Project(Chart(new[] { Square() }))!;
        Assert.EndsWith("Z", Assert.Single(svg.Paths).Path);
        Assert.True(svg.Paths[0].IsClosed);
    }

    [Fact]
    public void Open_Shape_Is_Not_Closed_By_The_Projection()
    {
        // Una polilinea aperta è un arco o una linea di confine: la "Z" disegnerebbe un lato che non esiste.
        var line = new MvaShape("LINEA", false, new[] { new MvaPoint(41.0, 12.0), new MvaPoint(42.0, 13.0) });
        var svg = MinimaChartProjector.Project(Chart(new[] { line }))!;

        Assert.DoesNotContain("Z", Assert.Single(svg.Paths).Path);
        Assert.False(svg.Paths[0].IsClosed);
    }

    [Fact]
    public void Labels_Outside_The_Shapes_Stay_Inside_The_ViewBox()
    {
        // 13 etichette su 345 non cadono dentro nessuna area. Se il viewBox si calcolasse sui soli poligoni,
        // finirebbero fuori dalla cornice e sparirebbero in silenzio.
        var far = new MvaLabel("TRL", 43.5, 14.5, "8");
        var svg = MinimaChartProjector.Project(Chart(new[] { Square() }, new[] { far }))!;

        var parts = svg.ViewBox.Split(' ');
        var w = double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
        var h = double.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture);
        var label = Assert.Single(svg.Labels);

        Assert.InRange(label.X, 0, w);
        Assert.InRange(label.Y, 0, h);
        Assert.Equal("TRL", label.Text);
    }

    [Fact]
    public void North_Is_Up()
    {
        var north = new MvaLabel("N", 42.0, 12.5, "8");
        var south = new MvaLabel("S", 41.0, 12.5, "8");
        var svg = MinimaChartProjector.Project(Chart(new[] { Square() }, new[] { north, south }))!;

        // Y cresce verso il basso in SVG: il punto più a nord deve avere Y minore.
        Assert.True(svg.Labels[0].Y < svg.Labels[1].Y);
    }

    [Fact]
    public void Label_Only_Chart_Still_Projects()
    {
        // Esistono file con etichette e nessun tracciato utile: la carta è comunque qualcosa da mostrare.
        var svg = MinimaChartProjector.Project(Chart(labels: new[]
        {
            new MvaLabel("110", 41.0, 12.0, "8"), new MvaLabel("90", 42.0, 13.0, "8"),
        }));

        Assert.NotNull(svg);
        Assert.Equal(2, svg!.Labels.Count);
        Assert.Empty(svg.Paths);
    }

    [Theory]
    [InlineData(null)]
    public void Nothing_To_Draw_Gives_Null(MvaChart? chart) => Assert.Null(MinimaChartProjector.Project(chart));

    [Fact]
    public void Empty_Chart_Gives_Null() => Assert.Null(MinimaChartProjector.Project(MvaChart.Empty));

    [Fact]
    public void Single_Coincident_Point_Gives_Null()
    {
        // Tutti i punti nello stesso posto: non c'è estensione, quindi non c'è una carta.
        var svg = MinimaChartProjector.Project(Chart(labels: new[] { new MvaLabel("X", 41.0, 12.0, "8") }));
        Assert.Null(svg);
    }
}
