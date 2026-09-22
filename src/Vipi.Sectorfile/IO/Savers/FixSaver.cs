using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Serialises a <see cref="Fix"/> to a .fix line:
///   <c>Name ; Lat ; Lon ; [DisplayType ;] [Field5 ;]</c>
/// </summary>
public sealed class FixSaver : IFileSaver<Fix>
{
    public IReadOnlyList<string> Serialize(Fix record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var fields = new List<string>
        {
            record.Name,
            CoordinateConverter.LatitudeToDottedDms(record.Position.LatitudeDeg),
            CoordinateConverter.LongitudeToDottedDms(record.Position.LongitudeDeg),
        };

        // The optional fields as far as they go: a Field5 without a DisplayType keeps its slot.
        if (record.DisplayType is not null || record.ExtraField is not null)
        {
            fields.Add(record.DisplayType?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        }

        if (record.ExtraField is not null)
        {
            fields.Add(record.ExtraField);
        }

        return new[] { string.Join(";", fields) + ";" };
    }

    public string GetIdentifier(Fix record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Name;
    }
}
