using System.Globalization;
using Vipi.Sectorfile.Models;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Serialises a <see cref="SidProcedure"/> to a .sid line:
///   <c>ICAO ; Runway ; Name ; Field4 ; Field5 ; [DefaultVisible] ; [RelatedFix] ;</c>
/// Field4/Field5 are written verbatim (literal space). Optional tail fields are emitted only when
/// present; an empty DefaultVisible placeholder is kept if RelatedFix follows (TEST_MATRIX §11.7).
/// </summary>
public sealed class SidSaver : IFileSaver<SidProcedure>
{
    public IReadOnlyList<string> Serialize(SidProcedure record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var fields = new List<string>
        {
            record.IcaoCode,
            record.Runway,
            record.Name,
            record.Field4,
            record.Field5,
        };

        bool hasRelatedFix = record.RelatedFix is not null;

        if (record.DefaultVisible is { } visible)
        {
            fields.Add(visible.ToString(CultureInfo.InvariantCulture));
        }
        else if (hasRelatedFix)
        {
            fields.Add(string.Empty);   // keep RelatedFix in its positional slot (field 7)
        }

        if (hasRelatedFix)
        {
            fields.Add(record.RelatedFix!);
        }

        return new[] { string.Join(";", fields) + ";" };
    }

    public string GetIdentifier(SidProcedure record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return $"{record.Runway}-{record.Name}";
    }
}
