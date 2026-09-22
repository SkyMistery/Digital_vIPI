using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Serialises a <see cref="Fix"/> to a .fix line:
///   <c>Name ; Lat ; Lon ; DisplayType ; Field5 ;</c>
/// </summary>
public sealed class FixSaver : IFileSaver<Fix>
{
    public IReadOnlyList<string> Serialize(Fix record)
    {
        ArgumentNullException.ThrowIfNull(record);

        string line = string.Join(
            ";",
            record.Name,
            CoordinateConverter.LatitudeToDottedDms(record.Position.LatitudeDeg),
            CoordinateConverter.LongitudeToDottedDms(record.Position.LongitudeDeg),
            record.DisplayType.ToString(CultureInfo.InvariantCulture),
            record.ExtraField) + ";";

        return new[] { line };
    }

    public string GetIdentifier(Fix record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Name;
    }
}
