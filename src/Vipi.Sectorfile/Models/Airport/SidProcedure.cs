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

    /// <summary>
    /// The drawn track under the header, one point per line — empty for the usual one-line SID (F2 slice 4).
    /// Only the visual departure blocks of lied.sid have one today (<c>QUIRRA DEP34</c>, <c>FRASCA DEP34</c>: 84
    /// lines that A skipped as malformed), with points by coordinates or by name and an optional label.
    /// </summary>
    public IList<PuntoDelTracciato> Track { get; } = new List<PuntoDelTracciato>();

    public SourceRef Source { get; set; } = null!;
}

/// <summary>A point of a drawn track: <c>LAT;LON;[label;]</c>, the point by coordinates or by name.</summary>
public sealed class PuntoDelTracciato
{
    public Punto Punto { get; set; }

    /// <summary>The optional third field, a text drawn at the point (e.g. <c>GOLF</c>); null when absent.</summary>
    public string? Etichetta { get; set; }

    /// <summary>
    /// True when a blank line precedes the point inside the track: Aurora breaks the line there and the point
    /// starts a new stretch (lied.sid, <c>NORTH DEP16</c>: the last two points are a separate stretch).
    /// </summary>
    public bool NuovoTratto { get; set; }
}
