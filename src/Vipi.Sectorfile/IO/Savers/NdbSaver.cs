using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>Serialises an <see cref="Ndb"/> to a .ndb line: <c>Ident ; Frequency ; Lat ; Lon ;</c></summary>
public sealed class NdbSaver : IFileSaver<Ndb>
{
    public IReadOnlyList<string> Serialize(Ndb record)
    {
        ArgumentNullException.ThrowIfNull(record);

        string line = string.Join(
            ";",
            record.Ident,
            record.Frequency.ToString(CultureInfo.InvariantCulture),
            CoordinateConverter.LatitudeToDottedDms(record.Position.LatitudeDeg),
            CoordinateConverter.LongitudeToDottedDms(record.Position.LongitudeDeg)) + ";";

        return new[] { line };
    }

    public string GetIdentifier(Ndb record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Ident;
    }
}
