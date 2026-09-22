using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Models;

/// <summary>
/// The <c>[INFO]</c> block of an Entry ISC: map centre, two display ranges, magnetic variation and
/// the include-folder name. All <c>F;</c> paths in the ISC resolve under
/// <c>&lt;iscDir&gt;/Include/&lt;IncludeFolder&gt;/</c> (ARCHITECTURE §7.1).
/// </summary>
public sealed class IscInfo
{
    public Coordinate Center { get; set; }
    public int Range1 { get; set; }
    public int Range2 { get; set; }

    /// <summary>Magnetic variation, kept verbatim from the file (e.g. "+4.0").</summary>
    public string MagneticVariation { get; set; } = string.Empty;

    /// <summary>Include folder name from line 6 of [INFO] (e.g. "IT").</summary>
    public string IncludeFolder { get; set; } = string.Empty;
}
