using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Serialises a <see cref="TflSector"/> block: header + one line per vertex.
///   <c>SectorCode ; FillColor ; LineWeight ; StrokeColor ; Flags ;</c>
///   <c>Lat ; Lon ;</c> …
/// FillColor/StrokeColor are written verbatim (palette name or hex).
/// </summary>
public sealed class TflSaver : IFileSaver<TflSector>
{
    public IReadOnlyList<string> Serialize(TflSector record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return SerializeBlock(record);
    }

    public string GetIdentifier(TflSector record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.SectorCode;
    }

    /// <summary>Shared by <see cref="TflSaver"/> and <see cref="FicSaver"/> (identical header/geometry).</summary>
    internal static List<string> SerializeBlock(TflSector record)
    {
        var lines = new List<string>
        {
            string.Join(
                ";",
                record.SectorCode,
                record.FillColor,
                record.LineWeight.ToString(CultureInfo.InvariantCulture),
                record.StrokeColor,
                record.Flags.ToString(CultureInfo.InvariantCulture)) + ";",
        };

        foreach (var vertex in record.Vertices)
        {
            lines.Add(vertex.Riga());
        }

        return lines;
    }
}
