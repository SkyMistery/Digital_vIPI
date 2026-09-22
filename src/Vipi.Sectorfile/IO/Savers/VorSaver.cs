using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Serialises a <see cref="Vor"/> to a .vor line:
///   <c>Ident ; Frequency ; Lat ; Lon ; [Field5] ; [Field6] ;</c>
/// Field5/Field6 are emitted only when present (a placeholder keeps Field6 positional if needed).
/// </summary>
public sealed class VorSaver : IFileSaver<Vor>
{
    public IReadOnlyList<string> Serialize(Vor record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var fields = new List<string>
        {
            record.Ident,
            record.Frequency?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,   // empty: a TACAN
            CoordinateConverter.LatitudeToDottedDms(record.Position.LatitudeDeg),
            CoordinateConverter.LongitudeToDottedDms(record.Position.LongitudeDeg),
        };

        if (record.ExtraField5 is not null)
        {
            fields.Add(record.ExtraField5);
        }
        else if (record.ExtraField6 is not null)
        {
            fields.Add(string.Empty);   // keep Field6 in its positional slot
        }

        if (record.ExtraField6 is not null)
        {
            fields.Add(record.ExtraField6);
        }

        return new[] { string.Join(";", fields) + ";" };
    }

    public string GetIdentifier(Vor record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Ident;
    }
}
