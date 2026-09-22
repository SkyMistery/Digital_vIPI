using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>Serialises a <see cref="TaxiwayLabel"/> to a .txi line: <c>Name ; ICAO ; Lat ; Lon ;</c></summary>
public sealed class TxiSaver : IFileSaver<TaxiwayLabel>
{
    public IReadOnlyList<string> Serialize(TaxiwayLabel record)
    {
        ArgumentNullException.ThrowIfNull(record);

        string line = string.Join(
            ";",
            record.Name,
            record.IcaoCode,
            CoordinateConverter.LatitudeToDottedDms(record.Position.LatitudeDeg),
            CoordinateConverter.LongitudeToDottedDms(record.Position.LongitudeDeg)) + ";";

        return new[] { line };
    }

    public string GetIdentifier(TaxiwayLabel record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Name;
    }
}
