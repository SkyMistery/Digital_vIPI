using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Serialises a <see cref="Runway"/> to a //PISTE line:
///   <c>ICAO ; Des1 ; Des2 ; Elev1 ; Elev2 ; Hdg1 ; Hdg2 ; Lat1 ; Lon1 ; Lat2 ; Lon2 ;</c>
/// Hdg2 is written empty when null. Coordinates are dotted DMS.
/// </summary>
public sealed class RwSaver : IFileSaver<Runway>
{
    public IReadOnlyList<string> Serialize(Runway record)
    {
        ArgumentNullException.ThrowIfNull(record);

        string line = string.Join(
            ";",
            record.IcaoCode,
            record.Designator1,
            record.Designator2,
            record.ElevThresh1Ft.ToString(CultureInfo.InvariantCulture),
            record.ElevThresh2Ft.ToString(CultureInfo.InvariantCulture),
            record.TrueHeading1.ToString(CultureInfo.InvariantCulture),
            record.TrueHeading2?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            CoordinateConverter.LatitudeToDottedDms(record.Threshold1.LatitudeDeg),
            CoordinateConverter.LongitudeToDottedDms(record.Threshold1.LongitudeDeg),
            CoordinateConverter.LatitudeToDottedDms(record.Threshold2.LatitudeDeg),
            CoordinateConverter.LongitudeToDottedDms(record.Threshold2.LongitudeDeg)) + ";";

        return new[] { line };
    }

    public string GetIdentifier(Runway record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return $"{record.IcaoCode}-{record.Designator1}/{record.Designator2}";
    }
}
