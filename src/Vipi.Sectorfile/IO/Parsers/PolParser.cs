using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Parses .pol ground-layout files (GND_LAYOUT/xx_ad_gnd.pol). A block-structured format, so it does
/// NOT use <see cref="LineRecordParser{T}"/>: each polygon is a header line followed by N vertex
/// lines, blocks separated by a new header or a blank line (INTERFACE_CONTRACTS §6.3 / TEST_MATRIX §4).
///
///   Header:  DisplayMode ; FillColor ; LineWeight ; LineColor ;   (DisplayMode is always STATIC)
///   Vertex:  Lat ; Lon ;
///
/// A polygon with fewer than 3 vertices is kept (with a warning). Vertices appearing before any
/// header are accumulated verbatim into a RawChunk until the next header.
/// </summary>
public sealed class PolParser : IFileParser<Polygon>
{
    private readonly IWarningCollector _warnings;

    public PolParser(IWarningCollector warnings)
        => _warnings = warnings ?? throw new ArgumentNullException(nameof(warnings));

    public ParseResult<Polygon> Parse(string filePath, ColorPalette palette)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(palette);
        return Assemble(SectorFileReader.Read(filePath), filePath);
    }

    /// <summary>Parses already-read lines (filesystem-free; used by tests).</summary>
    public ParseResult<Polygon> Parse(FileReadResult read, string source) => Assemble(read, source);

    private ParseResult<Polygon> Assemble(FileReadResult read, string source)
    {
        var records = new List<Polygon>();
        var chunks = new List<FileChunk<Polygon>>();
        var raw = new List<string>();
        var comments = new List<string>();

        Polygon? current = null;
        List<string> currentLines = new();
        List<string> currentLeading = new();

        void FlushRaw()
        {
            if (raw.Count > 0)
            {
                chunks.Add(new RawChunk<Polygon>(raw.ToArray()));
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

            if (current.Vertices.Count < 3)
            {
                _warnings.Add(WarningSeverity.Warning, WarningCategory.Parser, source,
                    $"Polygon with {current.Vertices.Count} vertices", current.Source.LineNumber);
            }

            FlushRaw();
            chunks.Add(new RecordChunk<Polygon>(current, currentLines.ToArray(), hasMarkers: false, currentLeading.ToArray()));
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

            if (TryParseVertex(line, out Coordinate vertex))
            {
                if (current is not null)
                {
                    current.Vertices.Add(vertex);
                    currentLines.Add(line);
                }
                else
                {
                    // Vertex before any header (§4.9) — keep verbatim until a header appears.
                    OrphanComments();
                    raw.Add(line);
                }

                continue;
            }

            if (TryParseHeader(line, out string fill, out float weight, out string lineColor))
            {
                FinalizeCurrent();
                FlushRaw();
                current = new Polygon
                {
                    FillColor = fill,
                    LineWeight = weight,
                    LineColor = lineColor,
                    Source = new SourceRef(source, lineNumber),
                };
                currentLines = new List<string> { line };
                currentLeading = new List<string>(comments);
                comments.Clear();
                continue;
            }

            // Neither vertex nor header → malformed.
            FinalizeCurrent();
            _warnings.Add(WarningSeverity.Warning, WarningCategory.Parser, source,
                "Skipping malformed line", lineNumber, line);
            OrphanComments();
            raw.Add(line);
        }

        FinalizeCurrent();
        OrphanComments();
        FlushRaw();

        return new ParseResult<Polygon>(records, chunks, read.Encoding, read.HasByteOrderMark)
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
            vertex = CoordinateConverter.ParsePair(parts[0].Trim(), parts[1].Trim());
            return true;
        }
        catch (CoordinateParseException)
        {
            return false;
        }
    }

    private static bool TryParseHeader(string line, out string fill, out float weight, out string lineColor)
    {
        fill = string.Empty;
        lineColor = string.Empty;
        weight = 0f;

        string[] parts = line.Split(';');
        int n = parts.Length;
        if (n > 0 && parts[^1].Length == 0)
        {
            n--;
        }

        if (n < 4)
        {
            return false;
        }

        fill = parts[1].Trim();
        float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out weight);
        lineColor = parts[3].Trim();
        return true;
    }
}
