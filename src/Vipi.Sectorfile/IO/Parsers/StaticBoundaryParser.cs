using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Shared base for the two static-boundary parsers (.hartcc / .lartcc — ARCHITECTURE §3.4 /
/// TEST_MATRIX §17–18). The two formats are byte-identical; only the destination layer differs
/// (the loader routes by directory). A <b>group</b> is a maximal run of consecutive <c>T;</c> lines;
/// blank lines separate groups and go to RawChunks. Within a group:
///   <c>T ; Name ; Lat ; Lon ;</c>     → a coordinate vertex (<see cref="StaticBoundaryVertex.Position"/>)
///   <c>T ; Name ; FixA ; FixB ;</c>   → a fix-pair vertex (FixA/FixB; resolved via NavaidSet at render)
///   <c>T ; DUMMY ; … ;</c>            → a polygon separator: the next vertex opens a new polygon.
/// Coordinate vs fix-pair is told apart by whether field 3 parses as a coordinate (so a fix such as
/// NILTO, which starts with 'N', is still treated as a fix and not a malformed coordinate). DUMMY is
/// recognised by field 2 == "DUMMY" (case-sensitive, Ordinal); its coordinates are never inspected.
/// Round-trip is guaranteed by verbatim RawLines, so even non-canonical real files round-trip exactly.
/// </summary>
public abstract class StaticBoundaryParser : IFileParser<StaticBoundaryGroup>
{
    protected IWarningCollector Warnings { get; }

    protected StaticBoundaryParser(IWarningCollector warnings)
        => Warnings = warnings ?? throw new ArgumentNullException(nameof(warnings));

    public ParseResult<StaticBoundaryGroup> Parse(string filePath, ColorPalette palette)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(palette);
        return Assemble(SectorFileReader.Read(filePath), filePath);
    }

    /// <summary>Parses already-read lines (filesystem-free; used by tests).</summary>
    public ParseResult<StaticBoundaryGroup> Parse(FileReadResult read, string source) => Assemble(read, source);

    private ParseResult<StaticBoundaryGroup> Assemble(FileReadResult read, string source)
    {
        var records = new List<StaticBoundaryGroup>();
        var chunks = new List<FileChunk<StaticBoundaryGroup>>();
        var raw = new List<string>();
        var comments = new List<string>();

        StaticBoundaryGroup? current = null;
        StaticBoundaryPolygon? polygon = null;
        List<string> currentLines = new();
        List<string> currentLeading = new();

        void FlushRaw()
        {
            if (raw.Count > 0)
            {
                chunks.Add(new RawChunk<StaticBoundaryGroup>(raw.ToArray()));
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

        void Finalize()
        {
            if (current is null)
            {
                return;
            }

            FlushRaw();
            chunks.Add(new RecordChunk<StaticBoundaryGroup>(current, currentLines.ToArray(), hasMarkers: false, currentLeading.ToArray()));
            records.Add(current);
            current = null;
            polygon = null;
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
                Finalize();
                OrphanComments();
                raw.Add(line);
                continue;
            }

            if (trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                // A comment inside an open group stays in the block (e.g. "//RR CONF2 - TS+US+OV"
                // after a DUMMY); otherwise it is a pending leading comment for the next group.
                if (current is not null)
                {
                    currentLines.Add(line);
                }
                else
                {
                    comments.Add(line);
                }

                continue;
            }

            string[] parts = line.Split(';');
            int n = parts.Length;
            if (n > 0 && parts[^1].Length == 0)
            {
                n--;
            }

            if (n < 2 || !string.Equals(parts[0].Trim(), "T", StringComparison.Ordinal))
            {
                // Not a T; line — unexpected in a static-boundary file.
                Finalize();
                Warnings.Add(WarningSeverity.Warning, WarningCategory.Parser, source, "Skipping malformed line", lineNumber, line);
                OrphanComments();
                raw.Add(line);
                continue;
            }

            string name = parts[1].Trim();
            bool isDummy = string.Equals(name, "DUMMY", StringComparison.Ordinal);

            if (current is null)
            {
                FlushRaw();
                current = new StaticBoundaryGroup { Name = name, Source = new SourceRef(source, lineNumber) };
                currentLeading = new List<string>(comments);
                comments.Clear();
                currentLines = new List<string>();
                polygon = null;
            }

            currentLines.Add(line);

            if (isDummy)
            {
                // Polygon separator — close the current polygon; the next vertex opens a new one.
                // The DUMMY line is kept in RawLines and its coordinates are never read.
                polygon = null;
                continue;
            }

            if (polygon is null)
            {
                polygon = new StaticBoundaryPolygon();
                current.Polygons.Add(polygon);
            }

            polygon.Vertices.Add(ParseVertex(parts, n));
        }

        Finalize();
        OrphanComments();
        FlushRaw();

        return new ParseResult<StaticBoundaryGroup>(records, chunks, read.Encoding, read.HasByteOrderMark)
        {
            NewLine = read.NewLine,
            HasFinalNewLine = read.HasFinalNewLine,
        };
    }

    private static StaticBoundaryVertex ParseVertex(string[] parts, int n)
    {
        string field3 = n > 2 ? parts[2].Trim() : string.Empty;
        string field4 = n > 3 ? parts[3].Trim() : string.Empty;

        // Coordinate vertex if field 3 parses as a coordinate; otherwise a fix-pair. Parsing (rather
        // than a first-char N/S/E/W test) avoids mis-reading fixes like NILTO/VEGAN as coordinates.
        try
        {
            return new StaticBoundaryVertex { Position = CoordinateConverter.ParsePair(field3, field4) };
        }
        catch (CoordinateParseException)
        {
            return new StaticBoundaryVertex { FixA = field3, FixB = field4 };
        }
    }
}

/// <summary>Parses HI_AIRSPACE/*.hartcc files into <see cref="StaticBoundaryGroup"/> records.</summary>
public sealed class HartccParser : StaticBoundaryParser
{
    public HartccParser(IWarningCollector warnings) : base(warnings) { }
}

/// <summary>Parses LOW_AIRSPACE/*.lartcc files into <see cref="StaticBoundaryGroup"/> records.</summary>
public sealed class LartccParser : StaticBoundaryParser
{
    public LartccParser(IWarningCollector warnings) : base(warnings) { }
}
