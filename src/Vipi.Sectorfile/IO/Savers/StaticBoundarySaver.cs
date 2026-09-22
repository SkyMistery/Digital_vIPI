using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Serialises a <see cref="StaticBoundaryGroup"/> back to .hartcc / .lartcc form (the two formats are
/// identical). One <c>T;</c> line per vertex; polygons are separated by a canonical DUMMY terminator:
///   <c>T ; Name ; Lat ; Lon ;</c>        coordinate vertex
///   <c>T ; Name ; FixA ; FixB ;</c>      fix-pair vertex
///   <c>T ; DUMMY ; N000.00.00.000 ; E000.00.00.000 ;</c>   between polygons
/// Only invoked for dirty records — non-dirty groups round-trip via verbatim RawLines, so the exact
/// original spacing/precision is preserved regardless of this canonical form.
/// </summary>
public abstract class StaticBoundarySaver : IFileSaver<StaticBoundaryGroup>
{
    private const string DummyLine = "T;DUMMY;N000.00.00.000;E000.00.00.000;";

    public IReadOnlyList<string> Serialize(StaticBoundaryGroup record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var lines = new List<string>();
        for (int p = 0; p < record.Polygons.Count; p++)
        {
            if (p > 0)
            {
                lines.Add(DummyLine);
            }

            foreach (var vertex in record.Polygons[p].Vertices)
            {
                string field3, field4;
                if (vertex.Position is { } position)
                {
                    field3 = CoordinateConverter.LatitudeToDottedDms(position.LatitudeDeg);
                    field4 = CoordinateConverter.LongitudeToDottedDms(position.LongitudeDeg);
                }
                else
                {
                    field3 = vertex.FixA ?? string.Empty;
                    field4 = vertex.FixB ?? string.Empty;
                }

                lines.Add($"T;{record.Name};{field3};{field4};");
            }
        }

        return lines;
    }

    public string GetIdentifier(StaticBoundaryGroup record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Name;
    }
}

/// <summary>Serialises HI_AIRSPACE/*.hartcc boundary groups.</summary>
public sealed class HartccSaver : StaticBoundarySaver;

/// <summary>Serialises LOW_AIRSPACE/*.lartcc boundary groups.</summary>
public sealed class LartccSaver : StaticBoundarySaver;
