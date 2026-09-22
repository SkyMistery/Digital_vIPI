using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Parses .fix files. One record per line: <c>Name ; Lat ; Lon ; DisplayType ; Field5 ;</c>
/// Field5 (purpose unknown) is always present in Italian files and preserved verbatim; a line
/// missing it is treated as malformed (warned + RawChunk) per TEST_MATRIX §22.3.
/// <see cref="Fix"/> has no disabled state, so a <c>//</c> line is always a comment.
/// </summary>
public sealed class FixParser : LineRecordParser<Fix>
{
    public FixParser(IWarningCollector warnings) : base(warnings) { }

    protected override bool TryParseRecord(
        string content, bool isDisabled, string source, int lineNumber, ColorPalette palette, out Fix record)
    {
        record = default!;
        if (isDisabled)
        {
            return false;
        }

        string[] parts = content.Split(';');
        int n = parts.Length;
        if (n > 0 && parts[^1].Length == 0)
        {
            n--;
        }

        // Name, Lat, Lon, DisplayType and Field5 are all required.
        if (n < 5)
        {
            return false;
        }

        Coordinate position;
        try
        {
            position = CoordinateConverter.ParsePair(parts[1].Trim(), parts[2].Trim());
        }
        catch (CoordinateParseException)
        {
            return false;
        }

        int displayType = int.TryParse(parts[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int dt) ? dt : 0;

        record = new Fix
        {
            Name = parts[0].Trim(),
            Position = position,
            DisplayType = displayType,
            ExtraField = parts[4],   // verbatim
            Source = new SourceRef(source, lineNumber),
        };
        return true;
    }
}
