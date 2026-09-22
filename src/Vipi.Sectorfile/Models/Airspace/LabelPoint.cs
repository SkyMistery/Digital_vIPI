using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Models;

/// <summary>
/// Display mode for ACC/*.artcc fix-label records. HARTCC/LARTCC T; lines do NOT produce
/// LabelPoint records.
/// </summary>
public enum LabelMode
{
    None,    // not displayed; serialised as empty FixName field
    FixName, // display the resolved fix/navaid name (default)
    Custom,  // display a user-supplied string
}

/// <summary>
/// An ACC fix-label record (from ACC/*.artcc). Serialised as:
/// FixRef (or CustomName, or "") ; Lat ; Lon ; FontSize ;
/// </summary>
public sealed class LabelPoint
{
    public LabelMode Mode { get; set; } = LabelMode.FixName;

    /// <summary>Fix identifier (used when Mode = FixName; also the anchor when Mode = Custom).</summary>
    public string? FixRef { get; set; }

    /// <summary>User text (used when Mode = Custom).</summary>
    public string? CustomName { get; set; }

    public Coordinate Position { get; set; }
    public int FontSize { get; set; }
    public SourceRef Source { get; set; } = null!;
}
