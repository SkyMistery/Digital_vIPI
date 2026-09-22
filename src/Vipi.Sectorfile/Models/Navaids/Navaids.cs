using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Models;

/// <summary>VOR navaid. Frequency in MHz (e.g. 111.65).</summary>
public sealed class Vor
{
    public string Ident { get; set; } = string.Empty;
    public decimal Frequency { get; set; }
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
    public int DisplayType { get; set; }

    /// <summary>Field5, always present in Italian .fix files; purpose unknown; preserved verbatim.</summary>
    public string ExtraField { get; set; } = string.Empty;

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
