using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Parses ACC/*.artcc files (ARCHITECTURE §5.2 / TEST_MATRIX §19), which mix two kinds of record
/// (<see cref="ElementoArtcc"/>):
///   <c>L ; FixName ; Lat ; Lon ; FontSize ;</c> → one <see cref="LabelPoint"/> per line. An empty FixName →
///   <see cref="LabelMode.None"/>; otherwise <see cref="LabelMode.FixName"/> (Custom is only produced by editing).
///   <c>T ; Name ; Lat ; Lon ;</c> → a boundary, read as in .hartcc/.lartcc (<see cref="StaticBoundaryParser"/>):
///   a maximal run of consecutive <c>T;</c> lines is one <see cref="StaticBoundaryGroup"/>, DUMMY lines separate
///   its polygons, comments inside the run stay in it.
/// ⚠️ TEST_MATRIX §19.4 of A (a <c>T;</c> line is not a record: warning + RawChunk) is overturned by F2 slice 5:
/// those were 5 041 lines, every boundary of FRA.artcc and FRA-gates.artcc. The loader skips
/// <c>ACC/test.artcc</c> (a dev artefact) — that is a GlobalLayerLoader concern (§28).
/// </summary>
public sealed class ArtccParser : IFileParser<ElementoArtcc>
{
    private readonly IWarningCollector _warnings;

    public ArtccParser(IWarningCollector warnings)
        => _warnings = warnings ?? throw new ArgumentNullException(nameof(warnings));

    public ParseResult<ElementoArtcc> Parse(string filePath, ColorPalette palette)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(palette);
        return Assemble(SectorFileReader.Read(filePath), filePath);
    }

    /// <summary>Parses already-read lines (filesystem-free; used by tests).</summary>
    public ParseResult<ElementoArtcc> Parse(FileReadResult read, string source, ColorPalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        return Assemble(read, source);
    }

    private ParseResult<ElementoArtcc> Assemble(FileReadResult read, string source)
    {
        var records = new List<ElementoArtcc>();
        var chunks = new List<FileChunk<ElementoArtcc>>();
        var raw = new List<string>();
        var comments = new List<string>();

        StaticBoundaryGroup? group = null;
        StaticBoundaryPolygon? polygon = null;
        List<string> groupLines = new();
        List<string> groupLeading = new();

        void FlushRaw()
        {
            if (raw.Count > 0)
            {
                chunks.Add(new RawChunk<ElementoArtcc>(raw.ToArray()));
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

        void Emit(ElementoArtcc record, IReadOnlyList<string> lines, IReadOnlyList<string> leading)
        {
            FlushRaw();
            chunks.Add(new RecordChunk<ElementoArtcc>(record, lines, hasMarkers: false, leading));
            records.Add(record);
        }

        void FinalizeGroup()
        {
            if (group is null)
            {
                return;
            }

            Emit(group, groupLines.ToArray(), groupLeading.ToArray());
            group = null;
            polygon = null;
            groupLines = new List<string>();
            groupLeading = new List<string>();
        }

        var lines = read.Lines;
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            int lineNumber = i + 1;
            string trimmed = line.TrimStart();

            if (trimmed.Length == 0)
            {
                FinalizeGroup();
                OrphanComments();
                raw.Add(line);
                continue;
            }

            // LabelPoint has no disabled concept; a // line is always a plain comment — inside a boundary run it
            // belongs to the run, otherwise it leads the next record.
            if (trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                if (group is not null)
                {
                    groupLines.Add(line);
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

            string kind = parts[0].Trim();
            if (string.Equals(kind, "T", StringComparison.Ordinal) && n >= 2)
            {
                string name = parts[1].Trim();
                if (group is null)
                {
                    FlushRaw();
                    group = new StaticBoundaryGroup { Name = name, Source = new SourceRef(source, lineNumber) };
                    groupLeading = new List<string>(comments);
                    comments.Clear();
                }

                groupLines.Add(line);

                if (string.Equals(name, "DUMMY", StringComparison.OrdinalIgnoreCase))
                {
                    polygon = null;   // polygon separator; its coordinates are never read
                    continue;
                }

                StaticBoundaryParser.NameFromFirstVertex(group, name);
                if (polygon is null)
                {
                    polygon = new StaticBoundaryPolygon();
                    group.Polygons.Add(polygon);
                }

                polygon.Vertices.Add(StaticBoundaryParser.ParseVertex(parts, n));
                if (StaticBoundaryParser.CoordinataCheNonSiLegge(parts, n))
                {
                    _warnings.Add(WarningSeverity.Warning, WarningCategory.Parser, source, "Unparseable T; vertex", lineNumber, line);
                }

                continue;
            }

            if (TryParseLabel(parts, n, source, lineNumber, out var label))
            {
                FinalizeGroup();
                Emit(label, new[] { line }, comments.ToArray());
                comments.Clear();
                continue;
            }

            FinalizeGroup();
            _warnings.Add(WarningSeverity.Warning, WarningCategory.Parser, source, "Skipping malformed line", lineNumber, line);
            OrphanComments();
            raw.Add(line);
        }

        FinalizeGroup();
        OrphanComments();
        FlushRaw();

        return new ParseResult<ElementoArtcc>(records, chunks, read.Encoding, read.HasByteOrderMark)
        {
            NewLine = read.NewLine,
            HasFinalNewLine = read.HasFinalNewLine,
        };
    }

    private static bool TryParseLabel(string[] parts, int n, string source, int lineNumber, out LabelPoint record)
    {
        record = default!;
        if (n < 4 || !string.Equals(parts[0].Trim(), "L", StringComparison.Ordinal))
        {
            return false;
        }

        Coordinate position;
        try
        {
            position = CoordinateConverter.ParsePair(parts[2].Trim(), parts[3].Trim());
        }
        catch (CoordinateParseException)
        {
            return false;
        }

        int fontSize = 0;
        if (n >= 5)
        {
            int.TryParse(parts[4].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out fontSize);
        }

        string fixName = parts[1];
        bool hasName = fixName.Trim().Length > 0;

        record = new LabelPoint
        {
            Mode = hasName ? LabelMode.FixName : LabelMode.None,
            FixRef = hasName ? fixName : null,
            Position = position,
            FontSize = fontSize,
            Source = new SourceRef(source, lineNumber),
        };
        return true;
    }
}
