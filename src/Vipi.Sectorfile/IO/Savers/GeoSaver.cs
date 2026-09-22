using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Serialises a <see cref="Line"/> back to a .geo segment (INTERFACE_CONTRACTS §6.2 / §2):
///   Lat1 ; Lon1 ; Lat2 ; Lon2 ; Color ;
/// Coordinates are always dotted DMS; the colour name is written verbatim.
/// </summary>
public sealed class GeoSaver : IFileSaver<Line>
{
    public IReadOnlyList<string> Serialize(Line record)
    {
        ArgumentNullException.ThrowIfNull(record);

        string line = string.Join(
            ";",
            CoordinateConverter.LatitudeToDottedDms(record.Start.LatitudeDeg),
            CoordinateConverter.LongitudeToDottedDms(record.Start.LongitudeDeg),
            CoordinateConverter.LatitudeToDottedDms(record.End.LatitudeDeg),
            CoordinateConverter.LongitudeToDottedDms(record.End.LongitudeDeg),
            record.Color) + ";";

        return new[] { line };
    }

    /// <summary>
    /// .geo files carry no natural key, so the start point identifies the segment. The orchestrator
    /// appends _2, _3… to disambiguate segments that happen to share a start point.
    /// </summary>
    public string GetIdentifier(Line record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return CoordinateConverter.LatitudeToDottedDms(record.Start.LatitudeDeg)
             + "/"
             + CoordinateConverter.LongitudeToDottedDms(record.Start.LongitudeDeg);
    }
}
