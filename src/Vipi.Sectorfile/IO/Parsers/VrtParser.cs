using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Legge il <c>.vrt</c> (<c>[VFRROUTE]</c>, carta F2 slice 6) in <see cref="RottaVfr"/>: ogni riga è
/// <c>Numero ; Lat ; Lon ;</c> (di solito per nome, <c>1;MNL;MNL;</c>), e le righe consecutive con lo stesso
/// numero sono una rotta. Senza lettore in A.
/// </summary>
/// <remarks>
/// La rotta si chiude quando cambia il numero, a una riga vuota, a un commento o a una riga che non si legge.
/// I commenti subito prima di una rotta sono i suoi commenti di testa; separati da una riga vuota, restano righe
/// grezze (come in <see cref="LineRecordParser{T}"/>).
/// </remarks>
public sealed class VrtParser : IFileParser<RottaVfr>
{
    private readonly IWarningCollector _warnings;

    public VrtParser(IWarningCollector warnings)
        => _warnings = warnings ?? throw new ArgumentNullException(nameof(warnings));

    public ParseResult<RottaVfr> Parse(string filePath, ColorPalette palette)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(palette);
        return Parse(SectorFileReader.Read(filePath), filePath);
    }

    /// <summary>Legge righe già lette (senza disco; per i test).</summary>
    public ParseResult<RottaVfr> Parse(FileReadResult read, string source)
    {
        var records = new List<RottaVfr>();
        var chunks = new List<FileChunk<RottaVfr>>();
        var raw = new List<string>();
        var comments = new List<string>();

        RottaVfr? current = null;
        var currentLines = new List<string>();
        var currentLeading = new List<string>();

        void FlushRaw()
        {
            if (raw.Count > 0)
            {
                chunks.Add(new RawChunk<RottaVfr>(raw.ToArray()));
                raw.Clear();
            }
        }

        void OrphanComments()
        {
            raw.AddRange(comments);
            comments.Clear();
        }

        void FinalizeCurrent()
        {
            if (current is null)
            {
                return;
            }

            FlushRaw();
            chunks.Add(new RecordChunk<RottaVfr>(current, currentLines.ToArray(), hasMarkers: false, currentLeading.ToArray()));
            records.Add(current);
            current = null;
            currentLines = new List<string>();
            currentLeading = new List<string>();
        }

        var lines = read.Lines;
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
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

            if (!TryParseLine(line, out string numero, out Punto punto))
            {
                FinalizeCurrent();
                _warnings.Add(WarningSeverity.Warning, WarningCategory.Parser, source, "Skipping malformed line", i + 1, line);
                OrphanComments();
                raw.Add(line);
                continue;
            }

            if (current is not null && current.Numero != numero)
            {
                FinalizeCurrent();
            }

            if (current is null)
            {
                FlushRaw();
                current = new RottaVfr { Numero = numero, Source = new SourceRef(source, i + 1) };
                currentLeading = new List<string>(comments);
                comments.Clear();
            }

            current.Punti.Add(punto);
            currentLines.Add(line);
        }

        FinalizeCurrent();
        OrphanComments();
        FlushRaw();

        return new ParseResult<RottaVfr>(records, chunks, read.Encoding, read.HasByteOrderMark)
        {
            NewLine = read.NewLine,
            HasFinalNewLine = read.HasFinalNewLine,
        };
    }

    private static bool TryParseLine(string line, out string numero, out Punto punto)
    {
        numero = string.Empty;
        punto = default;
        string[] parts = line.Split(';');
        int n = parts.Length;
        if (n > 0 && parts[^1].Length == 0)
        {
            n--;
        }

        if (n < 3)
        {
            return false;
        }

        numero = parts[0].Trim();
        return numero.Length > 0 && numero.All(char.IsAsciiDigit) && Punto.TryLeggi(parts[1], parts[2], out punto);
    }
}
