using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Serialises a <see cref="Polygon"/> to a .pol block: a header line plus one line per vertex.
///   <c>STATIC ; FillColor ; LineWeight ; LineColor ;</c>
///   <c>Lat ; Lon ;</c> …
/// DisplayMode is always STATIC in Italian files (1751/1751 headers), so it is written as a constant;
/// the model does not carry it (ARCHITECTURE §3.3).
/// </summary>
public sealed class PolSaver : IFileSaver<Polygon>
{
    public IReadOnlyList<string> Serialize(Polygon record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var lines = new List<string>
        {
            $"STATIC;{record.FillColor};{record.LineWeight.ToString(CultureInfo.InvariantCulture)};{record.LineColor};",
        };

        foreach (var vertex in record.Vertices)
        {
            lines.Add(CoordinateConverter.LatitudeToDottedDms(vertex.LatitudeDeg)
                      + ";"
                      + CoordinateConverter.LongitudeToDottedDms(vertex.LongitudeDeg)
                      + ";");
        }

        return lines;
    }

    /// <summary>
    /// .pol blocks have no natural key; the fill colour plus the first vertex locate the block.
    /// The orchestrator appends _2, _3… to disambiguate identical starts.
    /// </summary>
    public string GetIdentifier(Polygon record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (record.Vertices.Count == 0)
        {
            return record.FillColor;
        }

        var v0 = record.Vertices[0];
        return $"{record.FillColor}-{CoordinateConverter.LatitudeToDottedDms(v0.LatitudeDeg)}/{CoordinateConverter.LongitudeToDottedDms(v0.LongitudeDeg)}";
    }
}
