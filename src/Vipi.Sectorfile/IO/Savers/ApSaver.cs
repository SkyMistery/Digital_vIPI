using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Serialises <see cref="AirportInfo"/> back to a .ap line (INTERFACE_CONTRACTS §6.1 / §2).
/// Coordinates are always written in dotted DMS; a disabled record is prefixed with <c>//</c>;
/// optional tail fields (HideTag, InstallationType) are emitted only when present.
/// </summary>
public sealed class ApSaver : IFileSaver<AirportInfo>
{
    public IReadOnlyList<string> Serialize(AirportInfo record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var fields = new List<string>
        {
            record.IcaoCode,
            record.ElevationFt.ToString(CultureInfo.InvariantCulture),
            record.TransitionAltFt.ToString(CultureInfo.InvariantCulture),
            CoordinateConverter.LatitudeToDottedDms(record.Centre.LatitudeDeg),
            CoordinateConverter.LongitudeToDottedDms(record.Centre.LongitudeDeg),
            record.Name,
        };

        bool hasInstallation = record.InstallationType != InstallationType.Airport
                               || record.CustomInstallationTypeText is not null;

        if (record.HideTag is { } tag)
        {
            fields.Add(((int)tag).ToString(CultureInfo.InvariantCulture));
        }
        else if (hasInstallation)
        {
            fields.Add(string.Empty);   // keep InstallationType in its positional slot (field 8)
        }

        if (hasInstallation)
        {
            fields.Add(record.InstallationType == InstallationType.Custom
                ? record.CustomInstallationTypeText ?? string.Empty
                : ((int)record.InstallationType).ToString(CultureInfo.InvariantCulture));
        }

        string line = string.Join(";", fields) + ";";
        return new[] { record.IsDisabled ? "//" + line : line };
    }

    public string GetIdentifier(AirportInfo record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.IcaoCode;
    }
}
