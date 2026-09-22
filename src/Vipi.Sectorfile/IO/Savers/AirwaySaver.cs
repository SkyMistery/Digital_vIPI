using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Serialises an <see cref="Airway"/> block: one <c>T ; Name ; Label ; Label ;</c> line per fix label
/// followed by one <c>L ; Name ; Lat ; Lon ;</c> line per coordinate. Since the source file stores
/// T-runs and L-runs as separate blocks, a parsed Airway is all-labels or all-coordinates and this
/// ordering reproduces it; mixed blocks serialise labels first, then coordinates.
/// </summary>
public sealed class AirwaySaver : IFileSaver<Airway>
{
    public IReadOnlyList<string> Serialize(Airway record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var lines = new List<string>();

        foreach (var label in record.FixLabels)
        {
            lines.Add($"T;{record.Name};{label};{label};");
        }

        foreach (var coord in record.Coordinates)
        {
            lines.Add($"L;{record.Name};"
                      + CoordinateConverter.LatitudeToDottedDms(coord.LatitudeDeg)
                      + ";"
                      + CoordinateConverter.LongitudeToDottedDms(coord.LongitudeDeg)
                      + ";");
        }

        return lines;
    }

    public string GetIdentifier(Airway record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Name;
    }
}
