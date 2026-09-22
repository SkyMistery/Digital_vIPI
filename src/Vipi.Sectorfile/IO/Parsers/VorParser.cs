using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Parses .vor files. One record per line: <c>Ident ; Frequency(MHz) ; Lat ; Lon ; [Field5] ; [Field6] ;</c>
/// Field5/Field6 (purpose unknown) are preserved verbatim when present (TEST_MATRIX §20).
/// <see cref="Vor"/> has no disabled state, so a <c>//</c> line is always a comment.
/// </summary>
public sealed class VorParser : LineRecordParser<Vor>
{
    public VorParser(IWarningCollector warnings) : base(warnings) { }

    protected override bool TryParseRecord(
        string content, bool isDisabled, string source, int lineNumber, ColorPalette palette, out Vor record)
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

        if (n < 4 || !decimal.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out decimal freq))
        {
            return false;
        }

        Coordinate position;
        try
        {
            position = CoordinateConverter.ParsePair(parts[2].Trim(), parts[3].Trim());
        }
        catch (CoordinateParseException)
        {
            return false;
        }

        record = new Vor
        {
            Ident = parts[0].Trim(),
            Frequency = freq,
            Position = position,
            ExtraField5 = n >= 5 ? parts[4] : null,   // verbatim
            ExtraField6 = n >= 6 ? parts[5] : null,   // verbatim
            Source = new SourceRef(source, lineNumber),
        };
        return true;
    }
}
