using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Models;

/// <summary>Line geometry classified by palette colour. Sources: GEO/lixx.geo + RW_MARKINGS/xx_mark.geo.</summary>
public sealed class AirportLines
{
    public IList<Line> RunwayCenterlines { get; } = new List<Line>();   // RUNWAY
    public IList<Line> TaxiwayEdges { get; } = new List<Line>();        // TAXIWAY
    public IList<Line> TaxiwayCenterlines { get; } = new List<Line>();  // TAXI_CENTER (note: NOT "CENTERLINE")
    public IList<Line> ApronEdgeLines { get; } = new List<Line>();      // APRON (lines, distinct from APRON polygon fill)
    public IList<Line> CoastLines { get; } = new List<Line>();          // COAST
    public IList<Line> StopbarLines { get; } = new List<Line>();        // STOPBAR
    public IList<Line> StopLines { get; } = new List<Line>();           // STOPLINE
    public IList<Line> PierLines { get; } = new List<Line>();           // PIER
    public IList<Line> BuildingLines { get; } = new List<Line>();       // BUILDING
    public IList<Line> RunwayMarkings { get; } = new List<Line>();      // from RW_MARKINGS/*.geo (all colours)
    public IList<Line> Other { get; } = new List<Line>();              // all remaining classified lines
}

public sealed class Line
{
    public Coordinate Start { get; set; }
    public Coordinate End { get; set; }
    public string Color { get; set; } = string.Empty;   // palette name
    public SourceRef Source { get; set; } = null!;
}
