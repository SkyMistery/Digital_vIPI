using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Serialises a <see cref="VfrPoint"/> to a .vfi line: <c>Name ; Code ; Lat ; Lon ; [Type] ;</c>
/// The Type field is emitted only when present (TEST_MATRIX §9.6).
/// </summary>
public sealed class VfiSaver : IFileSaver<VfrPoint>
{
    public IReadOnlyList<string> Serialize(VfrPoint record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var fields = new List<string>
        {
            record.Name,
            record.Code,
            CoordinateConverter.LatitudeToDottedDms(record.Position.LatitudeDeg),
            CoordinateConverter.LongitudeToDottedDms(record.Position.LongitudeDeg),
        };

        if (record.Type is { } type)
        {
            fields.Add(type.ToString(CultureInfo.InvariantCulture));
        }

        return new[] { string.Join(";", fields) + ";" };
    }

    public string GetIdentifier(VfrPoint record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Code;
    }
}
