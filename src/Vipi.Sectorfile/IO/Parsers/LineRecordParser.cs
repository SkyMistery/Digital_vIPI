using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Reusable skeleton for single-line-record text formats (.ap, .geo, .vfi, .gts, .txi …).
/// Handles the structure shared by every Aurora line file: comment classification
/// (leading vs orphan), blank lines, disabled (//-prefixed) records, and //Start / //End
/// marker recognition. Concrete parsers implement only <see cref="TryParseRecord"/> for one line.
///
/// Block / multi-line formats (.tfl, .str, .mva, .hartcc) do NOT use this base — they group lines
/// into records differently and are implemented directly (Phases 8–11).
///
/// Postconditions Q1–Q4 of INTERFACE_CONTRACTS §1 are guaranteed here: every line lands in exactly
/// one chunk, in file order, and a parse → save with an empty dirty set is byte-for-byte identical.
/// </summary>
public abstract class LineRecordParser<T> : IFileParser<T>
{
    protected IWarningCollector Warnings { get; }

    protected LineRecordParser(IWarningCollector warnings)
        => Warnings = warnings ?? throw new ArgumentNullException(nameof(warnings));

    public ParseResult<T> Parse(string filePath, ColorPalette palette)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(palette);
        return Assemble(SectorFileReader.Read(filePath), filePath, palette);
    }

    /// <summary>Parses already-read lines (filesystem-free; used by tests).</summary>
    public ParseResult<T> Parse(FileReadResult read, string source, ColorPalette palette)
        => Assemble(read, source, palette);

    /// <summary>
    /// Attempts to parse one logical content line into a record. Must be pure and must NOT emit
    /// warnings: it is called speculatively to tell disabled records from plain comments. Return
    /// false for any line that is not a well-formed record of this type.
    /// </summary>
    /// <param name="content">Record text with any leading <c>//</c> already stripped.</param>
    /// <param name="isDisabled">True when the source line began with <c>//</c> (a disabled record).</param>
    /// <param name="source">File path, for <see cref="SourceRef"/>.</param>
    /// <param name="lineNumber">1-based line number, for <see cref="SourceRef"/>.</param>
    protected abstract bool TryParseRecord(
        string content, bool isDisabled, string source, int lineNumber, ColorPalette palette, out T record);

    private ParseResult<T> Assemble(FileReadResult read, string source, ColorPalette palette)
    {
        var records = new List<T>();
        var chunks = new List<FileChunk<T>>();
        var raw = new List<string>();        // pending RawChunk content
        var comments = new List<string>();   // pending leading comments (contiguous, no blanks)

        void FlushRaw()
        {
            if (raw.Count > 0)
            {
                chunks.Add(new RawChunk<T>(raw.ToArray()));
                raw.Clear();
            }
        }

        // The pending comments precede no record (a blank or malformed line broke adjacency).
        void OrphanComments()
        {
            if (comments.Count > 0)
            {
                raw.AddRange(comments);
                comments.Clear();
            }
        }

        void EmitRecord(T record, IReadOnlyList<string> recordLines, bool hasMarkers)
        {
            FlushRaw();   // earlier raw/blank content keeps its place before the record
            chunks.Add(new RecordChunk<T>(record, recordLines, hasMarkers, comments.ToArray()));
            comments.Clear();
            records.Add(record);
        }

        var lines = read.Lines;
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            int lineNumber = i + 1;
            string trimmed = line.TrimStart();

            // Blank line — never part of a leading-comment run; flush pending comments as orphans.
            if (trimmed.Length == 0)
            {
                OrphanComments();
                raw.Add(line);
                continue;
            }

            // //Start <id> … //End <id> marker block (file was previously saved by this app).
            if (IsStartMarker(trimmed))
            {
                int endIndex = FindEndMarker(lines, i);
                if (endIndex > i
                    && TryParseBody(lines, i + 1, endIndex, source, lineNumber, palette, out var marked))
                {
                    EmitRecord(marked, Slice(lines, i + 1, endIndex), hasMarkers: true);
                    i = endIndex;
                    continue;
                }

                // Malformed / unrecognised marker block → keep the //Start line as raw.
                OrphanComments();
                raw.Add(line);
                continue;
            }

            if (trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                // A disabled record is a // line whose body parses as a record; otherwise a comment.
                string content = trimmed[2..];
                if (TryParseRecord(content, isDisabled: true, source, lineNumber, palette, out var disabled))
                {
                    EmitRecord(disabled, new[] { line }, hasMarkers: false);
                }
                else
                {
                    comments.Add(line);
                }
                continue;
            }

            // Active record candidate.
            if (TryParseRecord(line, isDisabled: false, source, lineNumber, palette, out var record))
            {
                EmitRecord(record, new[] { line }, hasMarkers: false);
            }
            else
            {
                Warnings.Add(WarningSeverity.Warning, WarningCategory.Parser, source,
                    "Skipping malformed line", lineNumber, line);
                OrphanComments();
                raw.Add(line);
            }
        }

        OrphanComments();
        FlushRaw();

        return new ParseResult<T>(records, chunks, read.Encoding, read.HasByteOrderMark)
        {
            NewLine = read.NewLine,
            HasFinalNewLine = read.HasFinalNewLine,
        };
    }

    private bool TryParseBody(
        IReadOnlyList<string> lines, int from, int toExclusive,
        string source, int lineNumber, ColorPalette palette, out T record)
    {
        record = default!;

        // Single-line record formats: exactly one content line between the markers.
        if (toExclusive - from != 1)
        {
            return false;
        }

        string only = lines[from];
        string t = only.TrimStart();
        bool disabled = t.StartsWith("//", StringComparison.Ordinal);
        string content = disabled ? t[2..] : only;
        return TryParseRecord(content, disabled, source, lineNumber, palette, out record);
    }

    private static string[] Slice(IReadOnlyList<string> lines, int from, int toExclusive)
    {
        var result = new string[toExclusive - from];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = lines[from + i];
        }

        return result;
    }

    private static bool IsStartMarker(string trimmed)
        => trimmed.StartsWith("//Start ", StringComparison.Ordinal)
        || trimmed.Equals("//Start", StringComparison.Ordinal);

    private static int FindEndMarker(IReadOnlyList<string> lines, int startIndex)
    {
        for (int j = startIndex + 1; j < lines.Count; j++)
        {
            if (lines[j].TrimStart().StartsWith("//End", StringComparison.Ordinal))
            {
                return j;
            }
        }

        return -1;
    }
}
