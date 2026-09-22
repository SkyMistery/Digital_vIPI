using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Parses .txi taxiway-label files. One record per line: <c>Name ; ICAO ; Lat ; Lon ;</c>
/// <see cref="TaxiwayLabel"/> has no disabled state, so a <c>//</c> line is always a plain comment.
/// </summary>
public sealed class TxiParser : LineRecordParser<TaxiwayLabel>
{
    public TxiParser(IWarningCollector warnings) : base(warnings) { }

    protected override bool TryParseRecord(
        string content, bool isDisabled, string source, int lineNumber, ColorPalette palette, out TaxiwayLabel record)
    {
        record = default!;

        // No disabled-record concept: a // line is a comment, never a record.
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

        record = new TaxiwayLabel
        {
            Name = parts[0].Trim(),
            IcaoCode = parts[1].Trim(),
            Position = position,
            Source = new SourceRef(source, lineNumber),
        };
        return true;
    }
}
