using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Models;

/// <summary>VOR navaid. Frequency in MHz (e.g. 111.65).</summary>
public sealed class Vor
{
    public string Ident { get; set; } = string.Empty;

    /// <summary>
    /// Null when the field is empty: a TACAN has a channel and no frequency (<c>GRO;;N042.45.37.200;…;0;3;35Y</c>,
    /// the TACAN of Grosseto next to its VOR). In A it was mandatory and the line was malformed; vIPI's import
    /// reads it on purpose, and the concordance with vIPI found the gap (F2 slice 9).
    /// </summary>
    public decimal? Frequency { get; set; }

    public Coordinate Position { get; set; }

    /// <summary>Optional Field5 from the .vor file; purpose unknown; preserved verbatim.</summary>
    public string? ExtraField5 { get; set; }

    /// <summary>Optional Field6 from the .vor file; purpose unknown; preserved verbatim.</summary>
    public string? ExtraField6 { get; set; }

    public SourceRef Source { get; set; } = null!;
}

/// <summary>NDB navaid. Frequency in kHz (e.g. 340).</summary>
public sealed class Ndb
{
    public string Ident { get; set; } = string.Empty;
    public decimal Frequency { get; set; }
    public Coordinate Position { get; set; }
    public SourceRef Source { get; set; } = null!;
}

/// <summary>Fix / waypoint.</summary>
public sealed class Fix
{
    public string Name { get; set; } = string.Empty;
    public Coordinate Position { get; set; }

    /// <summary>Field 4, 0=enroute 1=terminal 2=both 3=hidden; null when the line stops at the longitude.</summary>
    public int? DisplayType { get; set; }

    /// <summary>
    /// Field 5 (boundary, 0/1), preserved verbatim; null when absent. In A it was mandatory, and the 2 032 fixes
    /// written with 4 fields (<c>BC404;N039.05.11.290;E017.03.27.750;3;</c>, hidden) and the 9 with 3 were
    /// «malformed» (F2 slice 5).
    /// </summary>
    public string? ExtraField { get; set; }

    public SourceRef Source { get; set; } = null!;
}

/// <summary>Airway built from T; (fix labels) and L; (coordinates) records.</summary>
public sealed class Airway
{
    public string Name { get; set; } = string.Empty;
    public IList<string> FixLabels { get; } = new List<string>();
    public IList<Coordinate> Coordinates { get; } = new List<Coordinate>();
    public SourceRef Source { get; set; } = null!;
}
