using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Parses .sid files. A record is a header line:
///   <c>ICAO ; Runway ; Name ; Field4 ; Field5 ; [DefaultVisible] ; [RelatedFix] ;</c>
/// optionally followed by a drawn track, one point per line: <c>LAT ; LON ; [label ;]</c>, the point by
/// coordinates or by name (F2 slice 4 — in A these lines were malformed, 84 in lied.sid).
/// Field4/Field5 are a literal " " (space) in Italian files and are preserved verbatim (TEST_MATRIX §11).
/// Blank lines separate runway groups and end a track (kept as RawChunk for round-trip).
/// <see cref="SidProcedure"/> has no disabled state, so a <c>//</c> line is always a comment: inside a track it
/// stays with the record, otherwise it leads the next record, as it did in A.
/// </summary>
public sealed class SidParser : IFileParser<SidProcedure>
{
    private readonly IWarningCollector _warnings;

    public SidParser(IWarningCollector warnings)
        => _warnings = warnings ?? throw new ArgumentNullException(nameof(warnings));

    public ParseResult<SidProcedure> Parse(string filePath, ColorPalette palette)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(palette);
        return Assemble(SectorFileReader.Read(filePath), filePath);
    }

    /// <summary>Parses already-read lines (filesystem-free; used by tests).</summary>
    public ParseResult<SidProcedure> Parse(FileReadResult read, string source, ColorPalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        return Assemble(read, source);
    }

    private ParseResult<SidProcedure> Assemble(FileReadResult read, string source)
    {
        var records = new List<SidProcedure>();
        var chunks = new List<FileChunk<SidProcedure>>();
        var raw = new List<string>();
        var comments = new List<string>();

        SidProcedure? current = null;
        List<string> currentLines = new();
        List<string> currentLeading = new();

        void FlushRaw()
        {
            if (raw.Count > 0)
            {
                chunks.Add(new RawChunk<SidProcedure>(raw.ToArray()));
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
            chunks.Add(new RecordChunk<SidProcedure>(current, currentLines.ToArray(), hasMarkers: false, currentLeading.ToArray()));
            records.Add(current);
            current = null;
            currentLines = new List<string>();
            currentLeading = new List<string>();
        }

        bool nuovoTratto = false;
        var lines = read.Lines;
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            int lineNumber = i + 1;
            string trimmed = line.TrimStart();

            if (trimmed.Length == 0)
            {
                // Blank lines between two points of a track break the line, they do not end the SID: the next
                // point starts a new stretch. Before anything else they separate records, as in A.
                if (current is { Track.Count: > 0 } && PuntoDopoLeRigheVuote(lines, i))
                {
                    currentLines.Add(line);
                    nuovoTratto = true;
                    continue;
                }

                FinalizeCurrent();
                OrphanComments();
                raw.Add(line);
                continue;
            }

            if (trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                // Inside a track the comment belongs to it; after a one-line SID it leads the next record. A //@ tag
                // is never part of a track: it closes it (F2 slice 7).
                if (current is { Track.Count: > 0 } && !Metadati.EUnTag(trimmed))
                {
                    currentLines.Add(line);
                }
                else
                {
                    FinalizeCurrent();
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
                FinalizeCurrent();
                current = Header(parts, n, source, lineNumber);
                currentLeading = new List<string>(comments);
                comments.Clear();
                currentLines = new List<string> { line };
                continue;
            }

            if (current is not null && TryPunto(parts, n, out var punto))
            {
                current.Track.Add(new PuntoDelTracciato { Punto = punto, Etichetta = n >= 3 ? parts[2] : null, NuovoTratto = nuovoTratto });
                currentLines.Add(line);
                nuovoTratto = false;
                continue;
            }

            FinalizeCurrent();
            _warnings.Add(WarningSeverity.Warning, WarningCategory.Parser, source, "Skipping malformed line", lineNumber, line);
            OrphanComments();
            raw.Add(line);
        }

        FinalizeCurrent();
        OrphanComments();
        FlushRaw();

        return new ParseResult<SidProcedure>(records, chunks, read.Encoding, read.HasByteOrderMark)
        {
            NewLine = read.NewLine,
            HasFinalNewLine = read.HasFinalNewLine,
        };
    }

    // A track line: fewer fields than a header, and the first two are a point.
    private static bool TryPunto(string[] parts, int n, out Punto punto)
    {
        punto = default;
        return n is >= 2 and < 5 && Punto.TryLeggi(parts[0], parts[1], out punto);
    }

    // True when the blank run starting at `from` is followed by a track point (not a header, comment or EOF).
    private static bool PuntoDopoLeRigheVuote(IReadOnlyList<string> lines, int from)
    {
        int j = from;
        while (j < lines.Count && lines[j].TrimStart().Length == 0)
        {
            j++;
        }

        if (j == lines.Count || lines[j].TrimStart().StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        string[] parts = lines[j].Split(';');
        int n = parts.Length;
        if (n > 0 && parts[^1].Length == 0)
        {
            n--;
        }

        return TryPunto(parts, n, out _);
    }

    private static SidProcedure Header(string[] parts, int n, string source, int lineNumber)
    {
        int? defaultVisible = null;
        if (n >= 6 && parts[5].Trim().Length > 0
            && int.TryParse(parts[5].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
        {
            defaultVisible = v;
        }

        string? relatedFix = n >= 7 && parts[6].Trim().Length > 0 ? parts[6] : null;

        return new SidProcedure
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
    }
}
