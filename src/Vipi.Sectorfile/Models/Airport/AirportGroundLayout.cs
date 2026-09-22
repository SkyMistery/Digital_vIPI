using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Models;

/// <summary>
/// Ground polygons from GND_LAYOUT/xx_ad_gnd.pol, classified by FillColor.
/// NOTE: MARKING and STOPBAR do NOT appear as .pol FillColor in Italian sectorfiles
/// (STOPBAR is a .geo line colour; MARKING is absent entirely).
/// </summary>
public sealed class AirportGroundLayout
{
    public IList<Polygon> AirportBoundary { get; } = new List<Polygon>();    // GRASS fill
    public IList<Polygon> BoundaryHoles { get; } = new List<Polygon>();      // HOLE fill
    public IList<Polygon> RunwaySurfaces { get; } = new List<Polygon>();     // RUNWAY fill
    public IList<Polygon> TaxiwayAreas { get; } = new List<Polygon>();       // TAXIWAY fill
    public IList<Polygon> ApronAreas { get; } = new List<Polygon>();         // APRON fill
    public IList<Polygon> ConcreteAreas { get; } = new List<Polygon>();      // CONCRETE fill
    public IList<Polygon> BuildingFootprints { get; } = new List<Polygon>(); // BUILDING fill
    public IList<Polygon> Other { get; } = new List<Polygon>();              // unclassified fill colours
}

public sealed class Polygon
{
    public IList<Coordinate> Vertices { get; } = new List<Coordinate>();
    public string FillColor { get; set; } = string.Empty;   // palette name
    public string LineColor { get; set; } = string.Empty;
    public float LineWeight { get; set; }
    public SourceRef Source { get; set; } = null!;
}
