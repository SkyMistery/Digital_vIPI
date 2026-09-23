using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Models;

/// <summary>
/// STR record type (.str header field 6).
///   0 = Star (STAR procedure zone / map overlay)
///   1 = Transition (CTR sector boundary, IT usage)
///   2 = Holding (holding pattern)
///   3 = Iap (Instrument Approach Procedure)
///   4 = Fap (Final Approach Path)
///   5 = GoAround (go-around / ATZ boundary, IT usage)
/// </summary>
public enum StrRecordType { Star = 0, Transition = 1, Holding = 2, Iap = 3, Fap = 4, GoAround = 5 }

/// <summary>
/// Base for .str records (Maps, Procedures, Holdings). The concrete subtype is inferred by the
/// parser from body-line content:
///   all coordinate lines                → <see cref="GeometricStrRecord"/>
///   all fix-reference lines             → <see cref="ProcedureStrRecord"/>
///   mix of fix-reference and coordinate → <see cref="HoldingStrRecord"/>
/// </summary>
public abstract class StrRecord
{
    public string IcaoCode { get; set; } = string.Empty;
    public string RunwaySpec { get; set; } = string.Empty;   // "MAPS", "16L:16R", "07:16L:16R" …
    public string ProcedureId { get; set; } = string.Empty;  // procedure name or zone label
    public string? LabelLat { get; set; }                    // on-map label anchor latitude (nullable)
    public string? LabelLon { get; set; }                    // on-map label anchor longitude (nullable)
    public StrRecordType RecordType { get; set; }
    public string? Transition { get; set; }                  // absent in Italian files
    public bool? IsRnav { get; set; }                        // absent in Italian files
    public SourceRef Source { get; set; } = null!;
}

/// <summary>
/// Type A — geometric zone / map overlay. Body: coordinate lines + optional &lt;br&gt;
/// segment-start markers. A new segment begins at each point carrying &lt;br&gt; (ARCHITECTURE §3.3).
/// </summary>
public sealed class GeometricStrRecord : StrRecord
{
    public IList<GeometricSegment> Segments { get; } = new List<GeometricSegment>();
}

public sealed class GeometricSegment
{
    public IList<Coordinate> Points { get; } = new List<Coordinate>();
}

/// <summary>Type B — procedure route (SID/STAR/IAP/FAP/GoAround). Body: fix references only.</summary>
public sealed class ProcedureStrRecord : StrRecord
{
    public IList<ProcedureWaypoint> Waypoints { get; } = new List<ProcedureWaypoint>();
}

public sealed class ProcedureWaypoint
{
    public string FixName { get; set; } = string.Empty;      // e.g. "ELKAP"
    public string DisplayLabel { get; set; } = string.Empty; // usually = FixName
    public string? SuffixCode { get; set; }                  // optional 3rd field, e.g. "3A"

    /// <summary>
    /// Vero se la riga porta <c>&lt;br&gt;</c> al terzo campo: il punto comincia un tratto nuovo (F3-bis slice 3). Le mappe
    /// che raccolgono procedure (<c>ODINA;ODINA;&lt;br&gt;</c> in <c>lime.str</c>) separano così una procedura dall'altra.
    /// </summary>
    public bool IniziaUnTratto { get; set; }
}

/// <summary>Type C — holding pattern (mix of fix references and bare coordinates).</summary>
public sealed class HoldingStrRecord : StrRecord
{
    public IList<HoldingPoint> Points { get; } = new List<HoldingPoint>();
}

/// <summary>Discriminated union for a single body point in a <see cref="HoldingStrRecord"/>.</summary>
public abstract class HoldingPoint;

public sealed class HoldingFixPoint : HoldingPoint
{
    public string FixName { get; set; } = string.Empty;
    public string DisplayLabel { get; set; } = string.Empty;
    public string? SuffixCode { get; set; }

    /// <summary>
    /// Vero se la riga porta <c>&lt;br&gt;</c> al terzo campo: il punto comincia un tratto nuovo (F3-bis slice 3). Le mappe
    /// che raccolgono procedure (<c>ODINA;ODINA;&lt;br&gt;</c> in <c>lime.str</c>) separano così una procedura dall'altra.
    /// </summary>
    public bool IniziaUnTratto { get; set; }
}

public sealed class HoldingCoordPoint : HoldingPoint
{
    public Coordinate Position { get; set; }
    public string? SuffixCode { get; set; }   // optional, e.g. "3T"

    /// <summary>
    /// Vero se la riga porta <c>&lt;br&gt;</c> al terzo campo: il punto comincia un tratto nuovo (F3-bis slice 3). Le mappe
    /// che raccolgono procedure (<c>ODINA;ODINA;&lt;br&gt;</c> in <c>lime.str</c>) separano così una procedura dall'altra.
    /// </summary>
    public bool IniziaUnTratto { get; set; }
}
