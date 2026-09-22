using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Serialises a record of an ACC/*.artcc file (<see cref="ElementoArtcc"/>).
/// A <see cref="LabelPoint"/> (ARCHITECTURE §5.2 / TEST_MATRIX §19.5–19.7):
///   <c>L ; FixName(or CustomName, or "") ; Lat ; Lon ; FontSize ;</c>
/// Field 2 depends on <see cref="LabelMode"/>: FixName → <see cref="LabelPoint.FixRef"/>,
/// Custom → <see cref="LabelPoint.CustomName"/>, None → empty (re-parses back to None).
/// A <see cref="StaticBoundaryGroup"/> (the <c>T;</c> lines, F2 slice 5) exactly as .hartcc/.lartcc.
/// Only invoked for dirty records — non-dirty records round-trip via verbatim RawLines.
/// </summary>
public sealed class ArtccSaver : IFileSaver<ElementoArtcc>
{
    private static readonly HartccSaver Bordi = new();

    public IReadOnlyList<string> Serialize(ElementoArtcc record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record switch
        {
            LabelPoint label => new[] { Label(label) },
            StaticBoundaryGroup group => Bordi.Serialize(group),
            _ => throw new ArgumentException($"Not an .artcc record: {record.GetType().Name}", nameof(record)),
        };
    }

    public string GetIdentifier(ElementoArtcc record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record switch
        {
            LabelPoint label => label.FixRef
                ?? label.CustomName
                ?? CoordinateConverter.LatitudeToDottedDms(label.Position.LatitudeDeg),
            StaticBoundaryGroup group => "T:" + Bordi.GetIdentifier(group),
            _ => throw new ArgumentException($"Not an .artcc record: {record.GetType().Name}", nameof(record)),
        };
    }

    private static string Label(LabelPoint record)
    {
        string field2 = record.Mode switch
        {
            LabelMode.FixName => record.FixRef ?? string.Empty,
            LabelMode.Custom => record.CustomName ?? string.Empty,
            _ => string.Empty,
        };

        string lat = CoordinateConverter.LatitudeToDottedDms(record.Position.LatitudeDeg);
        string lon = CoordinateConverter.LongitudeToDottedDms(record.Position.LongitudeDeg);
        string fontSize = record.FontSize.ToString(CultureInfo.InvariantCulture);

        return $"L;{field2};{lat};{lon};{fontSize};";
    }
}
