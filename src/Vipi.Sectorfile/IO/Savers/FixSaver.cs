using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Serialises a <see cref="Fix"/> to a .fix line:
///   <c>Name ; Lat ; Lon ; [DisplayType ;] [Field5 ;] [Hold ;]</c>
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

        // The optional fields as far as they go: a Field5 without a DisplayType keeps its slot, and so does the hold.
        CampiFacoltativi.Aggiungi(fields,
            record.DisplayType?.ToString(CultureInfo.InvariantCulture), record.ExtraField, record.NomeDellAttesa);

        return new[] { string.Join(";", fields) + ";" };
    }

    public string GetIdentifier(Fix record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Name;
    }
}
