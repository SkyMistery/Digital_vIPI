using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Parses .frq ATC-position files. One record per line (SRS §5.14 / TEST_MATRIX §16):
///   <c>Code ; FreqMHz ; TransferList ; Profile ; AtisFile ; BlockCpdlc ; Loa ; DatisFile ;</c>
/// TransferList is space-separated; a leading <c>-</c> marks a negative transfer. Field 7 is the .loa file
/// (slice 11a; before, an always-empty placeholder). <see cref="AtcPosition"/> has no disabled state, so a
/// <c>//</c> line is a comment.
/// </summary>
public sealed class FrqParser : LineRecordParser<AtcPosition>
{
    public FrqParser(IWarningCollector warnings) : base(warnings) { }

    protected override bool TryParseRecord(
        string content, bool isDisabled, string source, int lineNumber, ColorPalette palette, out AtcPosition record)
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

        // Code, Frequency and TransferList are mandatory. The profile is not (F2 slice 5): 35 real positions stop
        // after a long transfer list (`LIZZ_AEW_CTR;136.400;LIMM LIRR … LIRA`), and in A they were «malformed».
        if (n < 3 || !decimal.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out decimal freq))
        {
            return false;
        }

        var position = new AtcPosition
        {
            Code = parts[0].Trim(),
            FrequencyMhz = freq,
            Profile = n >= 4 ? parts[3] : null,   // verbatim (e.g. "PREFS\CTR.cpr")
        };

        foreach (string token in parts[2].Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            bool negative = token[0] == '-';
            position.TransferList.Add(new Transfer
            {
                IsNegative = negative,
                PositionCode = negative ? token[1..] : token,
            });
        }

        if (n >= 5)
        {
            position.AtisFile = parts[4].Length > 0 ? parts[4] : null;   // verbatim
        }

        if (n >= 6)
        {
            position.BlockCpdlc = parts[5].Trim() == "1";
        }

        // Field 7 is the .loa file (IVAO manual «LOA & XFL», lotto «Subito» slice 11a); empty on the whole fork.
        if (n >= 7)
        {
            position.Loa = parts[6].Length > 0 ? parts[6] : null;   // verbatim
        }

        if (n >= 8)
        {
            position.DatisFile = parts[7].Length > 0 ? parts[7] : null;   // verbatim
        }

        position.Sources.Add(new SourceRef(source, lineNumber));
        record = position;
        return true;
    }
}
