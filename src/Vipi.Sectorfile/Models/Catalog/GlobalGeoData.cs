using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Models;

/// <summary>Global geographic layers (country outline, restricted areas, ACC labels).</summary>
public sealed class GlobalGeoData
{
    public IList<Line> CountryOutline { get; } = new List<Line>();              // GEO/itgeo.geo
    public IList<RestrictedZone> RestrictedAreas { get; } = new List<RestrictedZone>(); // GEO/italy.restrict
    public IList<RestrictedZone> ProhibitedAreas { get; } = new List<RestrictedZone>(); // GEO/italy.prohibit
    public IList<RestrictedZone> DangerAreas { get; } = new List<RestrictedZone>();      // GEO/italy.danger

    /// <summary>From ACC/*.artcc (all files except ACC/test.artcc, which is skipped).</summary>
    public IList<LabelPoint> ArtccLabels { get; } = new List<LabelPoint>();
}

/// <summary>
/// A named airspace restriction zone from italy.restrict / .prohibit / .danger.
/// Segments are grouped by ZoneName and chained into a closed Polygon by the parser.
/// ZoneType values: "RESTRICT", "PROHIBIT", "DANGER".
/// </summary>
public sealed class RestrictedZone
{
    public string ZoneType { get; set; } = string.Empty;   // "RESTRICT", "PROHIBIT", or "DANGER"
    public string ZoneName { get; set; } = string.Empty;   // e.g. "R4", "R10A", "P1", "D5A"
    public Polygon Boundary { get; set; } = new();         // closed polygon from chained segments
    public SourceRef Source { get; set; } = null!;
}
