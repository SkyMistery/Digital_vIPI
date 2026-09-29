using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>Serialises an <see cref="Ndb"/> to a .ndb line: <c>Ident ; Frequency ; Lat ; Lon ; [Visibility ;] [; ;] [Hold ;]</c></summary>
public sealed class NdbSaver : IFileSaver<Ndb>
{
    public IReadOnlyList<string> Serialize(Ndb record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var fields = new List<string>
        {
            record.Ident,
            record.Frequency.ToString(CultureInfo.InvariantCulture),
            CoordinateConverter.LatitudeToDottedDms(record.Position.LatitudeDeg),
            CoordinateConverter.LongitudeToDottedDms(record.Position.LongitudeDeg),
        };

        // Visibility and hold (5th and 8th, slice 10a): each keeps its slot.
        CampiFacoltativi.Aggiungi(fields, record.Visibilita, record.ExtraField6, record.ExtraField7, record.NomeDellAttesa);

        return new[] { string.Join(";", fields) + ";" };
    }

    public string GetIdentifier(Ndb record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Ident;
    }
}
