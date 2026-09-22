using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Models;

/// <summary>
/// A named boundary group from a .hartcc or .lartcc file. Within one group there may be
/// multiple polygon blocks separated by DUMMY lines. CONF* groups represent the aggregate
/// outer boundary formed when multiple sectors operate together; no programmatic link is
/// maintained to any TflSector.
/// </summary>
public sealed class StaticBoundaryGroup : ElementoArtcc
{
    /// <summary>Verbatim from field 2 of T; lines (e.g. "RR NE", "RR CONF1", "CNF1", "LIRR ES0").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>≥1 polygon blocks (DUMMY-separated).</summary>
    public IList<StaticBoundaryPolygon> Polygons { get; } = new List<StaticBoundaryPolygon>();

    public SourceRef Source { get; set; } = null!;
}

public sealed class StaticBoundaryPolygon
{
    public IList<StaticBoundaryVertex> Vertices { get; } = new List<StaticBoundaryVertex>();
}

/// <summary>
/// A vertex in a .hartcc / .lartcc polygon, in one of two representations:
///   - Coordinate vertex (field-3 starts with N/S/E/W): <see cref="Position"/> set, FixA/FixB null.
///   - Fix-pair vertex (field-3 is a fix name): Position null, <see cref="FixA"/>/<see cref="FixB"/> set;
///     resolved to a coordinate via NavaidSet at render time.
/// </summary>
public sealed class StaticBoundaryVertex
{
    public Coordinate? Position { get; set; }
    public string? FixA { get; set; }
    public string? FixB { get; set; }
}
