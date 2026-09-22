using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Models;

/// <summary>
/// Single-line ATIS text template from ICAO.atis, with named placeholders in square brackets
/// (e.g. [ATIS_LETTER], [METAR], [QFE]). The Aurora client substitutes tokens at runtime;
/// the model stores the raw template verbatim.
/// </summary>
public sealed class AtisData
{
    public string Template { get; set; } = string.Empty;   // raw template line, placeholders intact
    public SourceRef Source { get; set; } = null!;
}
