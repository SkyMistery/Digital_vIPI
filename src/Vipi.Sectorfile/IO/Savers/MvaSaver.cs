using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Serialises an <see cref="MvaSector"/> back to its L;/T; block. One <c>L;</c> line per anchor,
/// then one <c>T;</c> line per vertex. The <paramref name="enroute"/> flag selects the L; layout:
///   airport:  <c>L ; AltLabel ; Lat ; Lon ; AltLabel ; Size ;</c>  and 4-field <c>T ; AltLabel ; Lat ; Lon ;</c>
///   enroute:  <c>L ; FIR ; Lat ; Lon ; AltLabel ; Size ;</c>       and 5-field <c>T ; FIR ; Lat ; Lon ; FIR ;</c>
/// where the enroute FIR code is recovered from a vertex's ExtraField.
///
/// LIMITATION: the model does not store the per-T-line identifier (e.g. a named airport zone like
/// "CERCHIO-BA") nor the enroute FIR when there are no vertices, so a *dirty* re-serialisation of
/// those non-standard blocks is approximate. Unmodified records always round-trip verbatim via
/// RawLines, so this only affects records the user actually edits.
/// </summary>
public sealed class MvaSaver : IFileSaver<MvaSector>
{
    private readonly bool _enroute;

    public MvaSaver(bool enroute) => _enroute = enroute;

    public IReadOnlyList<string> Serialize(MvaSector record)
    {
        ArgumentNullException.ThrowIfNull(record);

        string fir = record.Vertices.FirstOrDefault(v => v.ExtraField is not null)?.ExtraField ?? record.AltLabel;
        string labelField1 = _enroute ? fir : record.AltLabel;
        string size = record.LabelSize.ToString(CultureInfo.InvariantCulture);

        var lines = new List<string>();

        foreach (var anchor in record.LabelAnchors)
        {
            var (anchorLat, anchorLon) = anchor.Campi();
            lines.Add(string.Join(
                ";",
                "L",
                labelField1,
                anchorLat,
                anchorLon,
                record.AltLabel,
                size) + ";");
        }

        foreach (var vertex in record.Vertices)
        {
            var (lat, lon) = vertex.Position.Campi();
            string ident = vertex.ExtraField ?? record.AltLabel;

            lines.Add(vertex.ExtraField is not null
                ? $"T;{ident};{lat};{lon};{vertex.ExtraField};"
                : $"T;{ident};{lat};{lon};");
        }

        return lines;
    }

    public string GetIdentifier(MvaSector record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.AltLabel.Length > 0 ? record.AltLabel : "MVA";
    }
}
