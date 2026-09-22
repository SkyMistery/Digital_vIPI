using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Serialises an ACC <see cref="LabelPoint"/> (ARCHITECTURE §5.2 / TEST_MATRIX §19.5–19.7):
///   <c>L ; FixName(or CustomName, or "") ; Lat ; Lon ; FontSize ;</c>
/// Field 2 depends on <see cref="LabelMode"/>: FixName → <see cref="LabelPoint.FixRef"/>,
/// Custom → <see cref="LabelPoint.CustomName"/>, None → empty (re-parses back to None).
/// Only invoked for dirty records — non-dirty labels round-trip via verbatim RawLines.
/// </summary>
public sealed class ArtccSaver : IFileSaver<LabelPoint>
{
    public IReadOnlyList<string> Serialize(LabelPoint record)
    {
        ArgumentNullException.ThrowIfNull(record);

        string field2 = record.Mode switch
        {
            LabelMode.FixName => record.FixRef ?? string.Empty,
            LabelMode.Custom => record.CustomName ?? string.Empty,
            _ => string.Empty,
        };

        string lat = CoordinateConverter.LatitudeToDottedDms(record.Position.LatitudeDeg);
        string lon = CoordinateConverter.LongitudeToDottedDms(record.Position.LongitudeDeg);
        string fontSize = record.FontSize.ToString(CultureInfo.InvariantCulture);

        return new[] { $"L;{field2};{lat};{lon};{fontSize};" };
    }

    public string GetIdentifier(LabelPoint record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.FixRef
            ?? record.CustomName
            ?? CoordinateConverter.LatitudeToDottedDms(record.Position.LatitudeDeg);
    }
}
