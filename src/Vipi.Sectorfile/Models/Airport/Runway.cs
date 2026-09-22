using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Models;

/// <summary>
/// Runway pair, parsed from the //PISTE section of .rw files. The //MENU MAPPE and //ACC
/// sections are preserved as RawChunks for round-trip.
/// </summary>
public sealed class Runway
{
    public string IcaoCode { get; set; } = string.Empty;
    public string Designator1 { get; set; } = string.Empty;   // e.g. "16L"
    public string Designator2 { get; set; } = string.Empty;   // e.g. "34R"
    public int ElevThresh1Ft { get; set; }
    public int ElevThresh2Ft { get; set; }
    public float TrueHeading1 { get; set; }

    /// <summary>Nullable — the field may be empty in the file.</summary>
    public float? TrueHeading2 { get; set; }

    public Coordinate Threshold1 { get; set; }
    public Coordinate Threshold2 { get; set; }

    /// <summary>OTHER/itrw.rw + all FIR-specific .rw files that contain this runway pair.</summary>
    public IList<SourceRef> Sources { get; } = new List<SourceRef>();
}
