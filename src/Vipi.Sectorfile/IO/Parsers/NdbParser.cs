using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Parses .ndb files. One record per line: <c>Ident ; Frequency(kHz) ; Lat ; Lon ;</c>
/// <see cref="Ndb"/> has no disabled state, so a <c>//</c> line is always a comment.
/// </summary>
public sealed class NdbParser : LineRecordParser<Ndb>
{
    public NdbParser(IWarningCollector warnings) : base(warnings) { }

    protected override bool TryParseRecord(
        string content, bool isDisabled, string source, int lineNumber, ColorPalette palette, out Ndb record)
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
            var lat = CoordinateConverter.Parse(parts[2].Trim());
            var lon = CoordinateConverter.Parse(parts[3].Trim());
            position = new Coordinate(lat.LatitudeDeg, lon.LongitudeDeg);
        }
        catch (CoordinateParseException)
        {
            return false;
        }

        record = new Ndb
        {
            Ident = parts[0].Trim(),
            Frequency = freq,
            Position = position,
            Source = new SourceRef(source, lineNumber),
        };
        return true;
    }
}
