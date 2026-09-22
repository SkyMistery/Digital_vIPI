using System.Globalization;
using Vipi.Sectorfile.Models;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Serialises an <see cref="AtcPosition"/> to a .frq line:
///   <c>Code ; Freq ; TransferList ; Profile ; AtisFile ; BlockCpdlc ; [empty] ; DatisFile ;</c>
/// Always writes through BlockCpdlc; the empty placeholder + DatisFile are appended only when a
/// DatisFile is present (SRS §5.14).
/// </summary>
public sealed class FrqSaver : IFileSaver<AtcPosition>
{
    public IReadOnlyList<string> Serialize(AtcPosition record)
    {
        ArgumentNullException.ThrowIfNull(record);

        string transfers = string.Join(
            " ",
            record.TransferList.Select(t => (t.IsNegative ? "-" : string.Empty) + t.PositionCode));

        var fields = new List<string>
        {
            record.Code,
            record.FrequencyMhz.ToString(CultureInfo.InvariantCulture),
            transfers,
            record.Profile,
            record.AtisFile ?? string.Empty,
            record.BlockCpdlc ? "1" : "0",
        };

        if (record.DatisFile is not null)
        {
            fields.Add(string.Empty);   // field 7 placeholder
            fields.Add(record.DatisFile);
        }

        return new[] { string.Join(";", fields) + ";" };
    }

    public string GetIdentifier(AtcPosition record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.Code;
    }
}
