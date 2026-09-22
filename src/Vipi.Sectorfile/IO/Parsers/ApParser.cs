using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Parses .ap airport-info files. One record per line (SRS §5.2 / INTERFACE_CONTRACTS §6.1):
///   ICAO ; Elevation(ft) ; TransitionAlt(ft) ; Lat ; Lon ; Name ; [HideTag] ; [InstallationType] ;
/// A leading <c>//</c> marks the record disabled. Coordinates may be dotted or compact DMS.
/// </summary>
public sealed class ApParser : LineRecordParser<AirportInfo>
{
    public ApParser(IWarningCollector warnings) : base(warnings) { }

    protected override bool TryParseRecord(
        string content, bool isDisabled, string source, int lineNumber, ColorPalette palette, out AirportInfo record)
    {
        record = default!;

        string[] parts = content.Split(';');
        int n = parts.Length;
        if (n > 0 && parts[^1].Length == 0)
        {
            n--;   // ignore the empty field produced by the trailing ';'
        }

        // A record needs at least ICAO, Elevation, TransitionAlt, Lat, Lon.
        if (n < 5)
        {
            return false;
        }

        string icao = parts[0].Trim();
        if (icao.Length == 0)
        {
            return false;
        }

        Coordinate centre;
        try
        {
            centre = CoordinateConverter.ParsePair(parts[3].Trim(), parts[4].Trim());
        }
        catch (CoordinateParseException)
        {
            return false;
        }

        HideTag? hideTag = null;
        if (n >= 7 && parts[6].Trim().Length > 0
            && int.TryParse(parts[6].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int hideValue)
            && Enum.IsDefined(typeof(HideTag), hideValue))
        {
            hideTag = (HideTag)hideValue;
        }

        InstallationType installation = InstallationType.Airport;
        string? customText = null;
        if (n >= 8 && parts[7].Trim().Length > 0)
        {
            string token = parts[7].Trim();
            if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int instValue)
                && instValue is >= 0 and <= 4)
            {
                installation = (InstallationType)instValue;
            }
            else
            {
                installation = InstallationType.Custom;
                customText = parts[7];   // verbatim (not trimmed) for round-trip fidelity
            }
        }

        record = new AirportInfo
        {
            IcaoCode = icao,
            ElevationFt = ParseIntOrDefault(parts[1]),
            TransitionAltFt = ParseIntOrDefault(parts[2]),
            Centre = centre,
            Name = n >= 6 ? parts[5] : string.Empty,   // verbatim — may carry a leading space
            HideTag = hideTag,
            InstallationType = installation,
            CustomInstallationTypeText = customText,
            IsDisabled = isDisabled,
        };
        record.Sources.Add(new SourceRef(source, lineNumber));
        return true;
    }

    private static int ParseIntOrDefault(string token)
        => int.TryParse(token.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
}
