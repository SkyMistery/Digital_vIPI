using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Parses .vfi VFR-point files. One record per line:
///   <c>Name ; Code ; Lat ; Lon ; [Type] ;</c>
/// The optional 5th field (Type: 0=mandatory, 1=VFR, 2=VFR HELI, 3=VFR AREA) is usually absent in
/// Italian files (TEST_MATRIX §9). Used for airport .vfi; the enroute ENRVFI/*.vfi share this format.
/// <see cref="VfrPoint"/> has no disabled state, so a <c>//</c> line is always a comment.
/// </summary>
public sealed class VfiParser : LineRecordParser<VfrPoint>
{
    public VfiParser(IWarningCollector warnings) : base(warnings) { }

    protected override bool TryParseRecord(
        string content, bool isDisabled, string source, int lineNumber, ColorPalette palette, out VfrPoint record)
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

        int? type = null;
        if (n >= 5 && parts[4].Trim().Length > 0
            && int.TryParse(parts[4].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int t))
        {
            type = t;
        }

        record = new VfrPoint
        {
            Name = parts[0],          // verbatim — may contain spaces (e.g. "PONTE GALERIA")
            Code = parts[1].Trim(),
            Position = position,
            Type = type,
            Source = new SourceRef(source, lineNumber),
        };
        return true;
    }
}
