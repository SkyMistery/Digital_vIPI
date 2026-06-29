using Vipi.Application.Aor;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Cerchio di fallback per le TWR: poligono nel formato RegionMappolygon ([lng,lat]) parsabile dal proiettore,
/// chiuso, con raggio ≈ richiesto alle latitudini italiane.
/// </summary>
public class CircleShapeBuilderTests
{
    [Fact]
    public void Build_Produces_Polygon_Parsable_By_Projector()
    {
        var json = CircleShapeBuilder.Build(40.886, 14.291, radiusNm: 5, points: 36);
        var poly = AorPolygonProjector.Project(json);
        Assert.NotNull(poly);
        // 36 vertici + chiusura = 37 punti.
        Assert.Equal(37, poly!.Points.Count);
    }

    [Fact]
    public void Build_Radius_Is_About_Five_Nm()
    {
        const double lat = 41.8, lon = 12.5;
        var json = CircleShapeBuilder.Build(lat, lon, radiusNm: 5, points: 72);
        var poly = AorPolygonProjector.Project(json);
        Assert.NotNull(poly);

        // Punto più a nord ≈ 5 NM dal centro lungo il meridiano. 1 NM = 1852 m, 1° lat = 111320 m.
        var maxLat = poly!.MaxLat;
        var distNm = (maxLat - lat) * 111320.0 / 1852.0;
        Assert.InRange(distNm, 4.9, 5.1);
    }

    [Fact]
    public void Build_Is_Deterministic()
    {
        Assert.Equal(
            CircleShapeBuilder.Build(45.0, 9.0),
            CircleShapeBuilder.Build(45.0, 9.0));
    }
}
