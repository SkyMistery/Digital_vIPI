using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Serialises a <see cref="Vor"/> to a .vor line:
///   <c>Ident ; Frequency ; Lat ; Lon ; [Field5] ; [Field6] ; [TACAN] ; [Hold] ;</c>
/// The optional fields are emitted as far as the last one present, each in its positional slot.
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

        // Each optional field keeps its positional slot (the hold is the 8th: slice 10a).
        CampiFacoltativi.Aggiungi(fields, record.ExtraField5, record.ExtraField6, record.CanaleTacan, record.NomeDellAttesa);

        return new[] { string.Join(";", fields) + ";" };
    }

    public string GetIdentifier(Vor record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Ident;
    }
}
