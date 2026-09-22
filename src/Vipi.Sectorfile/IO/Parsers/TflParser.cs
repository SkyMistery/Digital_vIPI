using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Shared block-parser for .tfl-family files (INTERFACE_CONTRACTS §6.4 / TEST_MATRIX §5–6). Each
/// sector is a header line plus N vertex lines; sectors are separated by blank lines:
///   Header: <c>SectorCode ; FillColor ; LineWeight ; StrokeColor ; Flags ;</c>
///   Vertex: <c>Lat ; Lon ;</c>   (the polygon is implicitly closed)
/// FillColor may be a palette name or a hex string (e.g. <c>#0C0C0C</c>), kept verbatim. Header vs
/// vertex is told apart by whether the first two fields parse as coordinates.
/// </summary>
public abstract class TflParserBase<T> : IFileParser<T>
    where T : TflSector, new()
{
    protected IWarningCollector Warnings { get; }

    protected TflParserBase(IWarningCollector warnings)
        => Warnings = warnings ?? throw new ArgumentNullException(nameof(warnings));

    public ParseResult<T> Parse(string filePath, ColorPalette palette)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(palette);
        return Assemble(SectorFileReader.Read(filePath), filePath);
    }

    /// <summary>Parses already-read lines (filesystem-free; used by tests).</summary>
    public ParseResult<T> Parse(FileReadResult read, string source) => Assemble(read, source);

    /// <summary>Hook called at the header, with the comments that immediately preceded the block.</summary>
    protected virtual void Configure(T sector, IReadOnlyList<string> leadingComments) { }

    /// <summary>Hook called for each comment that appears inside an open block (after the header).</summary>
    protected virtual void OnComment(T sector, string commentLine) { }

    private ParseResult<T> Assemble(FileReadResult read, string source)
    {
        var records = new List<T>();
        var chunks = new List<FileChunk<T>>();
        var raw = new List<string>();
        var comments = new List<string>();

        T? current = null;
        List<string> currentLines = new();
        List<string> currentLeading = new();

        void FlushRaw()
        {
            if (raw.Count > 0)
            {
                chunks.Add(new RawChunk<T>(raw.ToArray()));
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
            chunks.Add(new RecordChunk<T>(current, currentLines.ToArray(), hasMarkers: false, currentLeading.ToArray()));
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
                // A comment inside an open block (e.g. the //GARDA label that follows a FIC header)
                // stays in the block; otherwise it is a pending leading comment for the next block.
                if (current is not null)
                {
                    currentLines.Add(line);
                    OnComment(current, line);
                }
                else
                {
                    comments.Add(line);
                }

                continue;
            }

            if (TryParseVertex(line, out Coordinate vertex))
            {
                if (current is not null)
                {
                    current.Vertices.Add(vertex);
                    currentLines.Add(line);
                }
                else
                {
                    OrphanComments();
                    raw.Add(line);   // vertices before any header
                }

                continue;
            }

            if (TryParseHeader(line, out var sector))
            {
                FinalizeCurrent();
                FlushRaw();
                sector.Source = new SourceRef(source, lineNumber);
                current = sector;
                currentLeading = new List<string>(comments);
                comments.Clear();
                Configure(sector, currentLeading);
                currentLines = new List<string> { line };
                continue;
            }

            // Neither vertex nor header → malformed.
            FinalizeCurrent();
            Warnings.Add(WarningSeverity.Warning, WarningCategory.Parser, source, "Skipping malformed line", lineNumber, line);
            OrphanComments();
            raw.Add(line);
        }

        FinalizeCurrent();
        OrphanComments();
        FlushRaw();

        return new ParseResult<T>(records, chunks, read.Encoding, read.HasByteOrderMark)
        {
            NewLine = read.NewLine,
            HasFinalNewLine = read.HasFinalNewLine,
        };
    }

    private static bool TryParseVertex(string line, out Coordinate vertex)
    {
        vertex = default;
        string[] parts = line.Split(';');
        int n = parts.Length;
        if (n > 0 && parts[^1].Length == 0)
        {
            n--;
        }

        if (n < 2)
        {
            return false;
        }

        try
        {
            var lat = CoordinateConverter.Parse(parts[0].Trim());
            var lon = CoordinateConverter.Parse(parts[1].Trim());
            vertex = new Coordinate(lat.LatitudeDeg, lon.LongitudeDeg);
            return true;
        }
        catch (CoordinateParseException)
        {
            return false;
        }
    }

    private static bool TryParseHeader(string line, out T sector)
    {
        sector = new T();
        string[] parts = line.Split(';');
        int n = parts.Length;
        if (n > 0 && parts[^1].Length == 0)
        {
            n--;
        }

        if (n < 5)
        {
            return false;
        }

        sector.SectorCode = parts[0].Trim();                    // colon-separated multi-position kept verbatim
        sector.FillColor = parts[1].Trim();                     // palette name OR hex (#0C0C0C)
        sector.LineWeight = int.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int w) ? w : 0;
        sector.StrokeColor = parts[3].Trim();
        sector.Flags = int.TryParse(parts[4].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int f) ? f : 0;
        return true;
    }
}

/// <summary>Parses .tfl dynamic-sector files into <see cref="TflSector"/> records.</summary>
public sealed class TflParser : TflParserBase<TflSector>
{
    public TflParser(IWarningCollector warnings) : base(warnings) { }
}

/// <summary>
/// Parses *fic.tfl files into <see cref="FicSector"/> records, deriving <see cref="FicSector.ShapeLabel"/>
/// from the section comment immediately preceding the block. Selected by file name (the loader routes
/// <c>*fic.tfl</c> here instead of <see cref="TflParser"/>).
/// </summary>
public sealed class FicParser : TflParserBase<FicSector>
{
    public FicParser(IWarningCollector warnings) : base(warnings) { }

    // ShapeLabel = the section comment associated with the block. In real files it follows the header
    // (e.g. header then "//GARDA"); some files place it before the header (leading comment). Either
    // way the first such comment wins. A comment separated from the block by a blank line is orphaned
    // to a RawChunk and never reaches these hooks, so it correctly does not become a ShapeLabel (§6.6).
    protected override void Configure(FicSector sector, IReadOnlyList<string> leadingComments)
    {
        if (leadingComments.Count > 0)
        {
            SetShapeLabelIfEmpty(sector, leadingComments[^1]);
        }
    }

    protected override void OnComment(FicSector sector, string commentLine)
        => SetShapeLabelIfEmpty(sector, commentLine);

    private static void SetShapeLabelIfEmpty(FicSector sector, string commentLine)
    {
        if (sector.ShapeLabel is not null)
        {
            return;
        }

        string text = commentLine.Trim();
        if (text.StartsWith("//", StringComparison.Ordinal))
        {
            text = text[2..].Trim();
        }

        if (text.Length > 0)
        {
            sector.ShapeLabel = text;
        }
    }
}
