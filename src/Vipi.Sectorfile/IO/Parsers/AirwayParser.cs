using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Parses .hairway / .lairway files. Two line kinds share an airway name (field 2):
///   <c>T ; Name ; FixLabel ; FixLabel ;</c>   (fix reference; field 4 repeats field 3)
///   <c>L ; Name ; Lat ; Lon ;</c>             (coordinate)
/// One <see cref="Airway"/> is produced per maximal run of consecutive same-name lines (the file
/// stores T-runs and L-runs separately, so a name appearing in both yields two Airway records — a
/// block format, NOT <see cref="LineRecordParser{T}"/>). A fully commented file yields no records
/// (TEST_MATRIX §23).
/// </summary>
public sealed class AirwayParser : IFileParser<Airway>
{
    private readonly IWarningCollector _warnings;

    public AirwayParser(IWarningCollector warnings)
        => _warnings = warnings ?? throw new ArgumentNullException(nameof(warnings));

    public ParseResult<Airway> Parse(string filePath, ColorPalette palette)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(palette);
        return Assemble(SectorFileReader.Read(filePath), filePath);
    }

    /// <summary>Parses already-read lines (filesystem-free; used by tests).</summary>
    public ParseResult<Airway> Parse(FileReadResult read, string source) => Assemble(read, source);

    private ParseResult<Airway> Assemble(FileReadResult read, string source)
    {
        var records = new List<Airway>();
        var chunks = new List<FileChunk<Airway>>();
        var raw = new List<string>();
        var comments = new List<string>();

        Airway? current = null;
        List<string> currentLines = new();
        List<string> currentLeading = new();

        void FlushRaw()
        {
            if (raw.Count > 0)
            {
                chunks.Add(new RawChunk<Airway>(raw.ToArray()));
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

        void FinalizeCurrent()
        {
            if (current is null)
            {
                return;
            }

            FlushRaw();
            chunks.Add(new RecordChunk<Airway>(current, currentLines.ToArray(), hasMarkers: false, currentLeading.ToArray()));
            records.Add(current);
            current = null;
            currentLines = new List<string>();
            currentLeading = new List<string>();
        }

        var lines = read.Lines;
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            int lineNumber = i + 1;
            string trimmed = line.TrimStart();

            if (trimmed.Length == 0)
            {
                FinalizeCurrent();
                OrphanComments();
                raw.Add(line);
                continue;
            }

            if (trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                FinalizeCurrent();
                comments.Add(line);
                continue;
            }

            if (TryParseLine(trimmed, out bool isT, out string name, out string label, out Coordinate coord))
            {
                if (current is null || !string.Equals(name, current.Name, StringComparison.Ordinal))
                {
                    FinalizeCurrent();
                    FlushRaw();
                    current = new Airway { Name = name, Source = new SourceRef(source, lineNumber) };
                    currentLines = new List<string>();
                    currentLeading = new List<string>(comments);
                    comments.Clear();
                }

                if (isT)
                {
                    current.FixLabels.Add(label);
                }
                else
                {
                    current.Coordinates.Add(coord);
                }

                currentLines.Add(line);
                continue;
            }

            // Neither a valid T; nor L; line.
            FinalizeCurrent();
            _warnings.Add(WarningSeverity.Warning, WarningCategory.Parser, source,
                "Skipping malformed airway line", lineNumber, line);
            OrphanComments();
            raw.Add(line);
        }

        FinalizeCurrent();
        OrphanComments();
        FlushRaw();

        return new ParseResult<Airway>(records, chunks, read.Encoding, read.HasByteOrderMark)
        {
            NewLine = read.NewLine,
            HasFinalNewLine = read.HasFinalNewLine,
        };
    }

    private static bool TryParseLine(string trimmed, out bool isT, out string name, out string label, out Coordinate coord)
    {
        isT = false;
        name = string.Empty;
        label = string.Empty;
        coord = default;

        string[] parts = trimmed.Split(';');
        int n = parts.Length;
        if (n > 0 && parts[^1].Length == 0)
        {
            n--;
        }

        string kind = parts[0].Trim();
        if (kind == "T")
        {
            if (n < 3)
            {
                return false;
            }

            isT = true;
            name = parts[1].Trim();
            label = parts[2].Trim();
            return true;
        }

        if (kind == "L")
        {
            if (n < 4)
            {
                return false;
            }

            try
            {
                coord = CoordinateConverter.ParsePair(parts[2].Trim(), parts[3].Trim());
            }
            catch (CoordinateParseException)
            {
                return false;
            }

            name = parts[1].Trim();
            return true;
        }

        return false;
    }
}
