using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Shared base for the two MVA parsers (SRS / ARCHITECTURE §3.3 / TEST_MATRIX §12–13). A block is a
/// maximal run of consecutive <c>L;</c>/<c>T;</c> lines (active or commented); blank lines and
/// plain-text lines (e.g. enroute "EX ETNA") separate blocks and go to RawChunks. Within a block:
///   <c>L ; f1 ; Lat ; Lon ; f4 ; Size ;</c> → a LabelAnchor (+ AltLabel/LabelSize from the first L)
///   <c>T ; ident ; Lat ; Lon ; [ident] ;</c> → a vertex (ExtraField = field 5 if present)
/// A <c>T ; DUMMY ; …</c> row (field 2 == "DUMMY", in any case: <c>T;dummy;</c> is one too, as Aurora reads it — F3 slice 10, 100 lowercase rows in 4 files) is a terminator: it is kept in
/// RawLines and its coordinates are never inspected. AltLabel comes from L field 2 (airport) or
/// L field 5 (enroute) — the only difference between the two contexts.
///
/// The loaders pick the airport vs enroute parser by directory (ENRMVA/). Round-trip is guaranteed by verbatim RawLines, so even
/// non-standard real files (named-zone airport .mva with no L; line) round-trip byte-for-byte.
/// </summary>
public abstract class MvaParser : IFileParser<MvaSector>
{
    private readonly IWarningCollector _warnings;
    private readonly bool _enroute;

    protected MvaParser(IWarningCollector warnings, bool enroute)
    {
        _warnings = warnings ?? throw new ArgumentNullException(nameof(warnings));
        _enroute = enroute;
    }

    public ParseResult<MvaSector> Parse(string filePath, ColorPalette palette)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(palette);
        return Assemble(SectorFileReader.Read(filePath), filePath);
    }

    /// <summary>Parses already-read lines (filesystem-free; used by tests).</summary>
    public ParseResult<MvaSector> Parse(FileReadResult read, string source) => Assemble(read, source);

    private ParseResult<MvaSector> Assemble(FileReadResult read, string source)
    {
        var records = new List<MvaSector>();
        var chunks = new List<FileChunk<MvaSector>>();
        var raw = new List<string>();
        var comments = new List<string>();

        MvaSector? current = null;
        List<string> currentLines = new();
        List<string> currentLeading = new();
        bool labelSet = false;

        void FlushRaw()
        {
            if (raw.Count > 0)
            {
                chunks.Add(new RawChunk<MvaSector>(raw.ToArray()));
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
            chunks.Add(new RecordChunk<MvaSector>(current, currentLines.ToArray(), hasMarkers: false, currentLeading.ToArray()));
            records.Add(current);
            current = null;
            currentLines = new List<string>();
            currentLeading = new List<string>();
            labelSet = false;
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

            bool commented = trimmed.StartsWith("//", StringComparison.Ordinal);
            string inner = commented ? trimmed[2..].TrimStart() : trimmed;
            bool isL = inner.StartsWith("L;", StringComparison.Ordinal);
            bool isT = inner.StartsWith("T;", StringComparison.Ordinal);

            if (isL || isT)
            {
                if (current is null)
                {
                    current = new MvaSector { Source = new SourceRef(source, lineNumber) };
                    currentLeading = new List<string>(comments);
                    comments.Clear();
                }

                currentLines.Add(line);

                if (!commented && isL)
                {
                    ParseActiveL(inner, source, lineNumber, current, ref labelSet);
                }
                else if (!commented)
                {
                    ParseActiveT(inner, source, lineNumber, current);
                }

                continue;
            }

            if (commented)
            {
                // Plain comment: part of an open block, otherwise a pending leading comment.
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

            // Plain-text line (not L;/T;/comment/blank) — silently separates blocks (§13.7).
            Finalize();
            OrphanComments();
            raw.Add(line);
        }

        Finalize();
        OrphanComments();
        FlushRaw();

        return new ParseResult<MvaSector>(records, chunks, read.Encoding, read.HasByteOrderMark)
        {
            NewLine = read.NewLine,
            HasFinalNewLine = read.HasFinalNewLine,
        };
    }

    private void ParseActiveL(string line, string source, int lineNumber, MvaSector sector, ref bool labelSet)
    {
        string[] parts = line.Split(';');
        int n = parts.Length;
        if (n > 0 && parts[^1].Length == 0)
        {
            n--;
        }

        if (n < 4)
        {
            _warnings.Add(WarningSeverity.Warning, WarningCategory.Parser, source, "Malformed L; line", lineNumber, line);
            return;
        }

        Punto anchor;
        try
        {
            anchor = Punto.Leggi(parts[2], parts[3]);
        }
        catch (CoordinateParseException)
        {
            _warnings.Add(WarningSeverity.Warning, WarningCategory.Parser, source, "Unparseable L; anchor", lineNumber, line);
            return;
        }

        sector.LabelAnchors.Add(anchor);

        if (!labelSet)
        {
            // Airport: AltLabel = field 2 (index 1). Enroute: AltLabel = field 5 (index 4).
            sector.AltLabel = _enroute ? (n >= 5 ? parts[4] : string.Empty) : parts[1];
            if (n >= 6 && int.TryParse(parts[5].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int size))
            {
                sector.LabelSize = size;
            }

            labelSet = true;
        }
    }

    private void ParseActiveT(string line, string source, int lineNumber, MvaSector sector)
    {
        string[] parts = line.Split(';');
        int n = parts.Length;
        if (n > 0 && parts[^1].Length == 0)
        {
            n--;
        }

        if (n < 4)
        {
            _warnings.Add(WarningSeverity.Warning, WarningCategory.Parser, source, "Malformed T; line", lineNumber, line);
            return;
        }

        // DUMMY terminator — case-sensitive; coordinates are never inspected.
        if (string.Equals(parts[1].Trim(), "DUMMY", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // A vertex may be given by name (T;LIRR;UTENO;UTENO;LIRR;, 18 lines of ENRMVA/lirr.mva): in A it was
        // «Unparseable T; vertex» and the polygon lost the point (F2 slice 4).
        Punto position;
        try
        {
            position = Punto.Leggi(parts[2], parts[3]);
        }
        catch (CoordinateParseException)
        {
            _warnings.Add(WarningSeverity.Warning, WarningCategory.Parser, source, "Unparseable T; vertex", lineNumber, line);
            return;
        }

        sector.Vertices.Add(new MvaVertex
        {
            Position = position,
            ExtraField = n >= 5 ? parts[4] : null,   // field 5 (repeated ident); absent in airport T lines
        });
    }
}

/// <summary>Airport .mva (ICAO.mva): AltLabel from L; field 2. Instantiated by the AirportLoader.</summary>
public sealed class MvaAirportParser : MvaParser
{
    public MvaAirportParser(IWarningCollector warnings) : base(warnings, enroute: false) { }
}

/// <summary>Enroute ENRMVA/&lt;fir&gt;.mva: AltLabel from L; field 5. Instantiated by the GlobalLayerLoader.</summary>
public sealed class MvaEnrouteParser : MvaParser
{
    public MvaEnrouteParser(IWarningCollector warnings) : base(warnings, enroute: true) { }
}
