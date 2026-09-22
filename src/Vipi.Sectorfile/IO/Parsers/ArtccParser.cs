using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Parses ACC/*.artcc label files (ARCHITECTURE §5.2 / TEST_MATRIX §19). A single-line-record format:
///   <c>L ; FixName ; Lat ; Lon ; FontSize ;</c>
/// Each <c>L;</c> line is one <see cref="LabelPoint"/>. An empty FixName field → <see cref="LabelMode.None"/>;
/// otherwise <see cref="LabelMode.FixName"/> (Custom is only produced by editing, never by parsing).
/// Any other active line — notably the boundary <c>T;</c> lines that appear in some real ACC files —
/// is not a label record: it yields a warning and is preserved verbatim in a RawChunk (§19.4). The
/// loader skips <c>ACC/test.artcc</c> (a dev artefact) — that is a GlobalLayerLoader concern (§28).
/// </summary>
public sealed class ArtccParser : LineRecordParser<LabelPoint>
{
    public ArtccParser(IWarningCollector warnings) : base(warnings) { }

    protected override bool TryParseRecord(
        string content, bool isDisabled, string source, int lineNumber, ColorPalette palette, out LabelPoint record)
    {
        record = default!;

        // LabelPoint has no disabled concept; a // line is always a plain comment.
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

        if (n < 4 || !string.Equals(parts[0].Trim(), "L", StringComparison.Ordinal))
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

        int fontSize = 0;
        if (n >= 5)
        {
            int.TryParse(parts[4].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out fontSize);
        }

        string fixName = parts[1];
        bool hasName = fixName.Trim().Length > 0;

        record = new LabelPoint
        {
            Mode = hasName ? LabelMode.FixName : LabelMode.None,
            FixRef = hasName ? fixName : null,
            Position = position,
            FontSize = fontSize,
            Source = new SourceRef(source, lineNumber),
        };
        return true;
    }
}
