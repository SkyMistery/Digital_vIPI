using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Serialises a <see cref="Stand"/> to a .gts line: <c>Number ; ICAO ; Lat ; Lon ; [Type] ; [Slot] ;</c> — tipo e slot
/// solo se ci sono (lotto «Subito» slice 12b, R2); uno slot senza tipo lascia il 5° campo vuoto al suo posto.
/// </summary>
public sealed class GtsSaver : IFileSaver<Stand>
{
    public IReadOnlyList<string> Serialize(Stand record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var campi = new List<string>
        {
            record.Number,
            record.IcaoCode,
            CoordinateConverter.LatitudeToDottedDms(record.Position.LatitudeDeg),
            CoordinateConverter.LongitudeToDottedDms(record.Position.LongitudeDeg),
        };
        CampiFacoltativi.Aggiungi(campi, Scritto(record.Type), Scritto(record.Slot));
        string line = string.Join(";", campi) + ";";

        return new[] { record.IsDisabled ? "//" + line : line };
    }

    public string GetIdentifier(Stand record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Number;
    }

    private static string? Scritto(string? campo) => string.IsNullOrWhiteSpace(campo) ? null : campo.Trim();
}
