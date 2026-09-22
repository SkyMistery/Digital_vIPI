using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Models;

/// <summary>SID procedure from ICAO.sid.</summary>
public sealed class SidProcedure
{
    public string IcaoCode { get; set; } = string.Empty;
    public string Runway { get; set; } = string.Empty;   // single designator, e.g. "07"
    public string Name { get; set; } = string.Empty;     // e.g. "OST1E", "SOS5A-ESI8H"

    /// <summary>Always " " (space) in Italian files; preserved verbatim.</summary>
    public string Field4 { get; set; } = string.Empty;

    /// <summary>Always " " (space) in Italian files; preserved verbatim.</summary>
    public string Field5 { get; set; } = string.Empty;

    /// <summary>6th optional field: 0 = hidden, 1 = visible by default.</summary>
    public int? DefaultVisible { get; set; }

    /// <summary>7th optional field: associated fix/navaid name.</summary>
    public string? RelatedFix { get; set; }

    public SourceRef Source { get; set; } = null!;
}
