using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Parses .geo line files (SRS §5.3 / INTERFACE_CONTRACTS §6.2). Each non-comment line is a
/// directed segment:
///   Lat1 ; Lon1 ; Lat2 ; Lon2 ; ColorName ;
/// The same parser handles airport GEO, RW_MARKINGS and the global itgeo.geo; routing of the
/// resulting <see cref="Line"/> records into the right collection is the loaders' job.
/// </summary>
public sealed class GeoParser : LineRecordParser<Line>
{
    public GeoParser(IWarningCollector warnings) : base(warnings) { }

    protected override bool TryParseRecord(
        string content, bool isDisabled, string source, int lineNumber, ColorPalette palette, out Line record)
    {
        record = default!;

        string[] parts = content.Split(';');
        int n = parts.Length;
        if (n > 0 && parts[^1].Length == 0)
        {
            n--;   // ignore the empty field produced by the trailing ';'
        }

        // A segment needs Lat1, Lon1, Lat2, Lon2 and a colour name.
        if (n < 5 || parts[4].Trim().Length == 0)
        {
            return false;
        }

        Coordinate start, end;
        try
        {
            start = CoordinateConverter.ParsePair(parts[0].Trim(), parts[1].Trim());
            end = CoordinateConverter.ParsePair(parts[2].Trim(), parts[3].Trim());
        }
        catch (CoordinateParseException)
        {
            return false;
        }

        record = new Line
        {
            Start = start,
            End = end,
            Color = parts[4].Trim(),
            Source = new SourceRef(source, lineNumber),
        };
        return true;
    }
}
