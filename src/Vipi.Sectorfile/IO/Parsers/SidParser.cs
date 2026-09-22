using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Parses .sid files. One record per line:
///   <c>ICAO ; Runway ; Name ; Field4 ; Field5 ; [DefaultVisible] ; [RelatedFix] ;</c>
/// Field4/Field5 are a literal " " (space) in Italian files and are preserved verbatim
/// (TEST_MATRIX §11). Blank lines separate runway groups (kept as RawChunk for round-trip).
/// <see cref="SidProcedure"/> has no disabled state, so a <c>//</c> line is always a comment.
/// </summary>
public sealed class SidParser : LineRecordParser<SidProcedure>
{
    public SidParser(IWarningCollector warnings) : base(warnings) { }

    protected override bool TryParseRecord(
        string content, bool isDisabled, string source, int lineNumber, ColorPalette palette, out SidProcedure record)
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

        // ICAO, Runway, Name, Field4, Field5 are mandatory.
        if (n < 5)
        {
            return false;
        }

        int? defaultVisible = null;
        if (n >= 6 && parts[5].Trim().Length > 0
            && int.TryParse(parts[5].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
        {
            defaultVisible = v;
        }

        string? relatedFix = n >= 7 && parts[6].Trim().Length > 0 ? parts[6] : null;

        record = new SidProcedure
        {
            IcaoCode = parts[0].Trim(),
            Runway = parts[1].Trim(),
            Name = parts[2].Trim(),
            Field4 = parts[3],   // verbatim (literal space)
            Field5 = parts[4],   // verbatim (literal space)
            DefaultVisible = defaultVisible,
            RelatedFix = relatedFix,
            Source = new SourceRef(source, lineNumber),
        };
        return true;
    }
}
