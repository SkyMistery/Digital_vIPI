using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Parses .gts stand files. One record per line: <c>Number ; ICAO ; Lat ; Lon ;</c>
/// A leading <c>//</c> marks the stand disabled (TEST_MATRIX §10).
/// </summary>
public sealed class GtsParser : LineRecordParser<Stand>
{
    public GtsParser(IWarningCollector warnings) : base(warnings) { }

    protected override bool TryParseRecord(
        string content, bool isDisabled, string source, int lineNumber, ColorPalette palette, out Stand record)
    {
        record = default!;

        string[] parts = content.Split(';');
        int n = parts.Length;
        if (n > 0 && parts[^1].Length == 0)
        {
            n--;
        }

        if (n < 4)
        {
            return false;
        }

        Coordinate position;
        try
        {
            var lat = CoordinateConverter.Parse(parts[2].Trim());
            var lon = CoordinateConverter.Parse(parts[3].Trim());
            position = new Coordinate(lat.LatitudeDeg, lon.LongitudeDeg);
        }
        catch (CoordinateParseException)
        {
            return false;
        }

        record = new Stand
        {
            Number = parts[0].Trim(),
            IcaoCode = parts[1].Trim(),
            Position = position,
            IsDisabled = isDisabled,
            Source = new SourceRef(source, lineNumber),
        };
        return true;
    }
}
