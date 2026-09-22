using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Serialises a <see cref="StrRecord"/> back to .str form: a header line plus one body line per point.
///   Header: <c>Icao ; Runways ; ProcId ; LabelLat ; LabelLon ; Type ;</c>
///   Geometric body: <c>Lat ; Lon ;</c> — the first point of each segment after the first carries
///                   a trailing <c>&lt;br&gt;</c> (ARCHITECTURE §3.3: &lt;br&gt; marks the FIRST point
///                   of a new segment).
///   Procedure body: <c>Fix ; Display ; [Suffix ;]</c>
///   Holding  body:  fix → <c>Fix ; Display ; [Suffix ;]</c>; coord → <c>Lat ; Lon ; [Suffix ;]</c>
/// Only invoked for dirty records — non-dirty records round-trip via verbatim RawLines, so the exact
/// original spacing, inline comments and disabled lines are preserved regardless of this canonical form.
/// </summary>
public sealed class StrSaver : IFileSaver<StrRecord>
{
    public IReadOnlyList<string> Serialize(StrRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var lines = new List<string>
        {
            string.Join(
                ";",
                record.IcaoCode,
                record.RunwaySpec,
                record.ProcedureId,
                record.LabelLat ?? string.Empty,
                record.LabelLon ?? string.Empty,
                ((int)record.RecordType).ToString(CultureInfo.InvariantCulture)) + ";",
        };

        switch (record)
        {
            case GeometricStrRecord geometric:
                for (int s = 0; s < geometric.Segments.Count; s++)
                {
                    var points = geometric.Segments[s].Points;
                    for (int p = 0; p < points.Count; p++)
                    {
                        // The first point of a segment after the first marks a new segment with <br>.
                        bool isSegmentStart = s > 0 && p == 0;
                        lines.Add(Coord(points[p]) + (isSegmentStart ? "<br>" : string.Empty));
                    }
                }

                break;

            case ProcedureStrRecord procedure:
                foreach (var wp in procedure.Waypoints)
                {
                    lines.Add(FixLine(wp.FixName, wp.DisplayLabel, wp.SuffixCode));
                }

                break;

            case HoldingStrRecord holding:
                foreach (var point in holding.Points)
                {
                    lines.Add(point switch
                    {
                        HoldingFixPoint fix => FixLine(fix.FixName, fix.DisplayLabel, fix.SuffixCode),
                        HoldingCoordPoint coord => Coord(coord.Position) + Suffix(coord.SuffixCode),
                        _ => string.Empty,
                    });
                }

                break;
        }

        return lines;
    }

    public string GetIdentifier(StrRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.ProcedureId;
    }

    private static string Coord(Coordinate c)
        => CoordinateConverter.LatitudeToDottedDms(c.LatitudeDeg)
           + ";"
           + CoordinateConverter.LongitudeToDottedDms(c.LongitudeDeg)
           + ";";

    private static string FixLine(string fix, string display, string? suffix)
        => $"{fix};{display};{(suffix is null ? string.Empty : suffix + ";")}";

    private static string Suffix(string? suffix) => suffix is null ? string.Empty : suffix + ";";
}
