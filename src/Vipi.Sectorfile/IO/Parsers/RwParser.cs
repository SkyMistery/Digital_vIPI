using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Parses .rw runway files (SRS §5.15 / TEST_MATRIX §15). The file has three sections introduced by
/// comment headers; only <c>//PISTE</c> rows become <see cref="Runway"/> records:
///   <c>ICAO ; Des1 ; Des2 ; Elev1 ; Elev2 ; Hdg1 ; Hdg2 ; Lat1 ; Lon1 ; Lat2 ; Lon2 ;</c>
/// <c>//MENU MAPPE</c> and <c>//ACC</c> are preserved verbatim in RawChunks. A sectioned format, so
/// it does NOT use <see cref="LineRecordParser{T}"/>.
/// </summary>
public sealed class RwParser : IFileParser<Runway>
{
    private readonly IWarningCollector _warnings;

    public RwParser(IWarningCollector warnings)
        => _warnings = warnings ?? throw new ArgumentNullException(nameof(warnings));

    public ParseResult<Runway> Parse(string filePath, ColorPalette palette)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(palette);
        return Assemble(SectorFileReader.Read(filePath), filePath);
    }

    /// <summary>Parses already-read lines (filesystem-free; used by tests).</summary>
    public ParseResult<Runway> Parse(FileReadResult read, string source) => Assemble(read, source);

    private ParseResult<Runway> Assemble(FileReadResult read, string source)
    {
        var records = new List<Runway>();
        var chunks = new List<FileChunk<Runway>>();
        var raw = new List<string>();
        var comments = new List<string>();   // pending leading comments (only inside //PISTE)
        bool inPiste = false;

        void FlushRaw()
        {
            if (raw.Count > 0)
            {
                chunks.Add(new RawChunk<Runway>(raw.ToArray()));
                raw.Clear();
            }
        }

        void OrphanComments()
        {
            if (comments.Count > 0)
            {
                raw.AddRange(comments);
                comments.Clear();
            }
        }

        var lines = read.Lines;
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            int lineNumber = i + 1;
            string trimmed = line.TrimStart();

            if (trimmed.Length == 0)
            {
                OrphanComments();
                raw.Add(line);
                continue;
            }

            if (trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                string header = trimmed[2..].Trim().ToUpperInvariant();
                if (header is "MENU MAPPE" or "PISTE" or "ACC")
                {
                    OrphanComments();
                    raw.Add(line);                 // section header kept verbatim
                    inPiste = header == "PISTE";
                    continue;
                }

                // Ordinary comment: a leading comment inside //PISTE, otherwise raw.
                if (inPiste)
                {
                    comments.Add(line);
                }
                else
                {
                    raw.Add(line);
                }

                continue;
            }

            // Data line.
            if (inPiste && TryParseRunway(line, source, lineNumber, out Runway runway))
            {
                FlushRaw();
                chunks.Add(new RecordChunk<Runway>(runway, new[] { line }, hasMarkers: false, comments.ToArray()));
                comments.Clear();
                records.Add(runway);
            }
            else
            {
                if (inPiste)
                {
                    _warnings.Add(WarningSeverity.Warning, WarningCategory.Parser, source,
                        "Skipping malformed runway line", lineNumber, line);
                }

                OrphanComments();
                raw.Add(line);
            }
        }

        OrphanComments();
        FlushRaw();

        return new ParseResult<Runway>(records, chunks, read.Encoding, read.HasByteOrderMark)
        {
            NewLine = read.NewLine,
            HasFinalNewLine = read.HasFinalNewLine,
        };
    }

    private static bool TryParseRunway(string line, string source, int lineNumber, out Runway runway)
    {
        runway = default!;

        string[] parts = line.Split(';');
        int n = parts.Length;
        if (n > 0 && parts[^1].Length == 0)
        {
            n--;
        }

        if (n < 11)
        {
            return false;
        }

        if (!float.TryParse(parts[5].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float heading1))
        {
            return false;
        }

        float? heading2;
        string h2 = parts[6].Trim();
        if (h2.Length == 0)
        {
            heading2 = null;
        }
        else if (float.TryParse(h2, NumberStyles.Float, CultureInfo.InvariantCulture, out float h2Value))
        {
            heading2 = h2Value;
        }
        else
        {
            return false;
        }

        Coordinate threshold1, threshold2;
        try
        {
            threshold1 = CoordinateConverter.ParsePair(parts[7].Trim(), parts[8].Trim());
            threshold2 = CoordinateConverter.ParsePair(parts[9].Trim(), parts[10].Trim());
        }
        catch (CoordinateParseException)
        {
            return false;
        }

        runway = new Runway
        {
            IcaoCode = parts[0].Trim(),
            Designator1 = parts[1].Trim(),
            Designator2 = parts[2].Trim(),
            ElevThresh1Ft = ParseIntOrDefault(parts[3]),
            ElevThresh2Ft = ParseIntOrDefault(parts[4]),
            TrueHeading1 = heading1,
            TrueHeading2 = heading2,
            Threshold1 = threshold1,
            Threshold2 = threshold2,
        };
        runway.Sources.Add(new SourceRef(source, lineNumber));
        return true;
    }

    private static int ParseIntOrDefault(string token)
        => int.TryParse(token.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
}
