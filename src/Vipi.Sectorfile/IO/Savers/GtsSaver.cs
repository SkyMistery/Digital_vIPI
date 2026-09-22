using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>Serialises a <see cref="Stand"/> to a .gts line: <c>Number ; ICAO ; Lat ; Lon ;</c></summary>
public sealed class GtsSaver : IFileSaver<Stand>
{
    public IReadOnlyList<string> Serialize(Stand record)
    {
        ArgumentNullException.ThrowIfNull(record);

        string line = string.Join(
            ";",
            record.Number,
            record.IcaoCode,
            CoordinateConverter.LatitudeToDottedDms(record.Position.LatitudeDeg),
            CoordinateConverter.LongitudeToDottedDms(record.Position.LongitudeDeg)) + ";";

        return new[] { record.IsDisabled ? "//" + line : line };
    }

    public string GetIdentifier(Stand record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Number;
    }
}
