using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Parses .str map/procedure/holding files (ARCHITECTURE §3.3 / TEST_MATRIX §7) — the most complex
/// format in the project. A record is a header line followed by its body, running until the NEXT
/// header line or EOF. Blank lines and comments do NOT end a record: they are cosmetic and preserved
/// verbatim in RawLines (so a non-dirty round-trip is byte-for-byte, NFR-04).
///
/// Header (8 semicolon fields, the last 2–3 usually absent in IT):
///   <c>Icao ; Runways ; ProcId ; LabelLat ; LabelLon ; Type ; Transition ; RNAV</c>
/// A line is a header iff it has ≥5 semicolon-fields (after dropping one trailing empty); body lines
/// have ≤3. The concrete subtype is inferred from body content (ARCHITECTURE §3.3):
///   all coordinate lines  → <see cref="GeometricStrRecord"/>
///   all fix-reference lines → <see cref="ProcedureStrRecord"/>
///   a mix of the two       → <see cref="HoldingStrRecord"/>
///
/// &lt;br&gt; semantics (official, ARCHITECTURE §3.3): when "&lt;br&gt;" is the 3rd field of a body
/// coordinate line, that point is the FIRST point of a NEW segment — it does NOT belong to the
/// previous segment. <see cref="GeometricStrRecord"/> therefore starts a new segment at every &lt;br&gt;.
/// 🔴 F3-bis slice 3: a named point can carry it too (<c>ODINA;ODINA;&lt;br&gt;</c>, the maps that gather
/// procedures), and so can a coordinate inside a mixed record: until then only the geometric records kept it,
/// and the other two dropped it silently (<see cref="ProcedureWaypoint.IniziaUnTratto"/>).
/// </summary>
public sealed class StrParser : IFileParser<StrRecord>
{
    private readonly IWarningCollector _warnings;

    public StrParser(IWarningCollector warnings)
        => _warnings = warnings ?? throw new ArgumentNullException(nameof(warnings));

    public ParseResult<StrRecord> Parse(string filePath, ColorPalette palette)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(palette);
        return Assemble(SectorFileReader.Read(filePath), filePath);
    }

    /// <summary>Parses already-read lines (filesystem-free; used by tests).</summary>
    public ParseResult<StrRecord> Parse(FileReadResult read, string source) => Assemble(read, source);

    /// <summary>A classified body line: either a coordinate point (optionally a &lt;br&gt; segment start) or a fix reference.</summary>
    private readonly struct BodyToken
    {
        public BodyToken(Coordinate coord, bool hasBr, string? suffix)
        {
            IsCoord = true;
            Coord = coord;
            HasBr = hasBr;
            Suffix = suffix;
            Fix = string.Empty;
            Display = string.Empty;
        }

        public BodyToken(string fix, string display, bool hasBr, string? suffix)
        {
            IsCoord = false;
            Fix = fix;
            Display = display;
            HasBr = hasBr;
            Suffix = suffix;
        }

        public bool IsCoord { get; }
        public Coordinate Coord { get; }
        public bool HasBr { get; }
        public string Fix { get; }
        public string Display { get; }
        public string? Suffix { get; }
    }

    private ParseResult<StrRecord> Assemble(FileReadResult read, string source)
    {
        var records = new List<StrRecord>();
        var chunks = new List<FileChunk<StrRecord>>();
        var raw = new List<string>();
        var comments = new List<string>();

        string[]? headerParts = null;
        int headerLineNumber = 0;
        var body = new List<BodyToken>();
        List<string> currentLines = new();
        List<string> currentLeading = new();

        void FlushRaw()
        {
            if (raw.Count > 0)
            {
                chunks.Add(new RawChunk<StrRecord>(raw.ToArray()));
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
            if (headerParts is null)
            {
                return;
            }

            FlushRaw();
            var record = BuildRecord(headerParts, body, source, headerLineNumber);
            chunks.Add(new RecordChunk<StrRecord>(record, currentLines.ToArray(), hasMarkers: false, currentLeading.ToArray()));
            records.Add(record);

            headerParts = null;
            body = new List<BodyToken>();
            currentLines = new List<string>();
            currentLeading = new List<string>();
        }

        var lines = read.Lines;
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            int lineNumber = i + 1;
            string trimmed = line.TrimStart();

            // Blank lines and comments are cosmetic: kept in the open record's RawLines, otherwise
            // they are pending raw/leading content before the next record.
            if (trimmed.Length == 0)
            {
                if (headerParts is not null)
                {
                    currentLines.Add(line);
                }
                else
                {
                    OrphanComments();
                    raw.Add(line);
                }

                continue;
            }

            if (trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                // A //@ line (the Lab's tags, carta madre §8.2) is never body: it closes the open record, so that
                // `//@END NOME` and the next `//@NOME` stay out of it (F2 slice 7). The master has none.
                if (headerParts is not null && !Metadati.EUnTag(trimmed))
                {
                    currentLines.Add(line);   // in-block comment / disabled body line
                }
                else if (headerParts is not null)
                {
                    Finalize();
                    comments.Add(line);
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

            if (n >= 5)
            {
                // Header line — starts a new record.
                Finalize();
                FlushRaw();
                headerParts = parts;
                headerLineNumber = lineNumber;
                currentLeading = new List<string>(comments);
                comments.Clear();
                currentLines = new List<string> { line };
                continue;
            }

            // Body line (within an open record). A stray body line before any header is malformed.
            if (headerParts is null)
            {
                _warnings.Add(WarningSeverity.Warning, WarningCategory.Parser, source, "STR body line before any header", lineNumber, line);
                OrphanComments();
                raw.Add(line);
                continue;
            }

            currentLines.Add(line);
            var token = ClassifyBody(parts, n);
            if (!token.IsCoord && n >= 2 && !Punto.TryLeggi(parts[0], parts[1], out _))
            {
                // A body line that is neither a point by coordinates nor by name: a coordinate that does not read
                // (minutes 75, a lowercase hemisphere…). A turned it into a FIX named «N047.44.75.000» without a
                // word; it still is one, to keep the model, but now it is said (F2 slice 4).
                _warnings.Add(WarningSeverity.Warning, WarningCategory.Parser, source, "Unparseable STR point", lineNumber, line);
            }

            body.Add(token);
        }

        Finalize();
        OrphanComments();
        FlushRaw();

        return new ParseResult<StrRecord>(records, chunks, read.Encoding, read.HasByteOrderMark)
        {
            NewLine = read.NewLine,
            HasFinalNewLine = read.HasFinalNewLine,
        };
    }

    private static BodyToken ClassifyBody(string[] parts, int n)
    {
        string field3 = n >= 3 ? parts[2].Trim() : string.Empty;
        bool hasBr = string.Equals(field3, "<br>", StringComparison.Ordinal);
        bool field3IsComment = field3.StartsWith("//", StringComparison.Ordinal);
        string? suffix = field3.Length > 0 && !hasBr && !field3IsComment ? field3 : null;

        // Coordinate point if the first two fields parse as a coordinate; otherwise a fix reference.
        // Parsing (not a first-char test) keeps fixes that start with N/S/E/W (e.g. NILTO) as fixes.
        if (n >= 2)
        {
            try
            {
                return new BodyToken(CoordinateConverter.ParsePair(parts[0].Trim(), parts[1].Trim()), hasBr, suffix);
            }
            catch (CoordinateParseException)
            {
                // Fall through to fix reference.
            }
        }

        string fix = parts[0].Trim();
        string display = n >= 2 ? parts[1].Trim() : fix;
        return new BodyToken(fix, display, hasBr, suffix);
    }

    private static StrRecord BuildRecord(string[] header, List<BodyToken> body, string source, int lineNumber)
    {
        bool anyCoord = body.Exists(t => t.IsCoord);
        bool anyFix = body.Exists(t => !t.IsCoord);

        StrRecord record;
        if (anyCoord && anyFix)
        {
            record = BuildHolding(body);
        }
        else if (anyFix)
        {
            record = BuildProcedure(body);
        }
        else
        {
            record = BuildGeometric(body);   // all-coordinate, or empty body
        }

        SetCommon(record, header, source, lineNumber);
        return record;
    }

    private static GeometricStrRecord BuildGeometric(List<BodyToken> body)
    {
        var record = new GeometricStrRecord();
        GeometricSegment? segment = null;
        foreach (var token in body)
        {
            if (segment is null || token.HasBr)
            {
                segment = new GeometricSegment();
                record.Segments.Add(segment);
            }

            segment.Points.Add(token.Coord);
        }

        return record;
    }

    private static ProcedureStrRecord BuildProcedure(List<BodyToken> body)
    {
        var record = new ProcedureStrRecord();
        foreach (var token in body)
        {
            record.Waypoints.Add(new ProcedureWaypoint
            {
                FixName = token.Fix,
                DisplayLabel = token.Display,
                SuffixCode = token.Suffix,
                IniziaUnTratto = token.HasBr,
            });
        }

        return record;
    }

    private static HoldingStrRecord BuildHolding(List<BodyToken> body)
    {
        var record = new HoldingStrRecord();
        foreach (var token in body)
        {
            record.Points.Add(token.IsCoord
                ? new HoldingCoordPoint { Position = token.Coord, SuffixCode = token.Suffix, IniziaUnTratto = token.HasBr }
                : new HoldingFixPoint { FixName = token.Fix, DisplayLabel = token.Display, SuffixCode = token.Suffix, IniziaUnTratto = token.HasBr });
        }

        return record;
    }

    private static void SetCommon(StrRecord record, string[] p, string source, int lineNumber)
    {
        record.IcaoCode = p[0].Trim();
        record.RunwaySpec = p.Length > 1 ? p[1].Trim() : string.Empty;
        record.ProcedureId = p.Length > 2 ? p[2].Trim() : string.Empty;
        record.LabelLat = Field(p, 3);
        record.LabelLon = Field(p, 4);
        record.RecordType = p.Length > 5 && int.TryParse(p[5].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int t)
            ? (StrRecordType)t
            : StrRecordType.Star;
        record.Transition = Field(p, 6);
        string? rnav = Field(p, 7);
        record.IsRnav = rnav is null ? null : rnav is "1" or "true" or "True";
        record.Source = new SourceRef(source, lineNumber);
    }

    private static string? Field(string[] parts, int index)
    {
        if (index >= parts.Length)
        {
            return null;
        }

        string value = parts[index].Trim();
        return value.Length > 0 ? value : null;
    }
}
