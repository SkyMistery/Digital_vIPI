using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Parses .fix files. One record per line: <c>Name ; Lat ; Lon ; [DisplayType ;] [Field5 ;]</c>
/// Field5 (boundary) is preserved verbatim. ⚠️ TEST_MATRIX §22.3 of A (a line without Field5 is malformed) is
/// overturned by F2 slice 5: 2 041 real fixes stop at field 4 or 3.
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

        // Name, Lat and Lon are required; DisplayType and Field5 are optional (F2 slice 5: 4-field hidden fixes
        // `BC404;…;3;` and 3-field ones `POE1;…;` are real, 2 041 lines on the master of 22 September 2026).
        if (n < 3)
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

        int? displayType = n < 4 ? null
            : int.TryParse(parts[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int dt) ? dt : 0;

        record = new Fix
        {
            Name = parts[0].Trim(),
            Position = position,
            DisplayType = displayType,
            ExtraField = n >= 5 ? parts[4] : null,   // verbatim
            Source = new SourceRef(source, lineNumber),
        };
        return true;
    }
}
