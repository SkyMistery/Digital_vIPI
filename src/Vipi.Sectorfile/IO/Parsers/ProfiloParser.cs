using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Legge i profili <c>.cpr</c> (lotto «Subito» slice 11d, N1): un INI come i profili di Aurora. Ogni riga
/// <c>Chiave=Valore</c> è un <see cref="ImpostazioneDelProfilo"/> con la sezione che la precede; le intestazioni
/// (<c>[INSET1]</c>), i commenti, le righe vuote e quelle senza <c>=</c> restano righe del file, come sono.
/// </summary>
public sealed class ProfiloParser : IFileParser<ImpostazioneDelProfilo>
{
    private readonly IWarningCollector _warnings;

    public ProfiloParser(IWarningCollector warnings)
        => _warnings = warnings ?? throw new ArgumentNullException(nameof(warnings));

    public ParseResult<ImpostazioneDelProfilo> Parse(string filePath, ColorPalette palette)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(palette);
        return Parse(SectorFileReader.Read(filePath), filePath);
    }

    /// <summary>Le righe già lette (senza disco: per i test).</summary>
    public ParseResult<ImpostazioneDelProfilo> Parse(FileReadResult read, string source)
    {
        var records = new List<ImpostazioneDelProfilo>();
        var chunks = new List<FileChunk<ImpostazioneDelProfilo>>();
        var raw = new List<string>();
        string sezione = string.Empty;

        void FlushRaw()
        {
            if (raw.Count > 0)
            {
                chunks.Add(new RawChunk<ImpostazioneDelProfilo>(raw.ToArray()));
                raw.Clear();
            }
        }

        for (int i = 0; i < read.Lines.Count; i++)
        {
            string line = read.Lines[i];
            string t = line.Trim();
            if (t.StartsWith('[') && t.EndsWith(']'))
            {
                sezione = t[1..^1].Trim();
                raw.Add(line);
                continue;
            }

            int uguale = line.IndexOf('=', StringComparison.Ordinal);
            if (t.Length == 0 || t.StartsWith("//", StringComparison.Ordinal) || t.StartsWith(';') || uguale <= 0)
            {
                if (t.Length > 0 && !t.StartsWith("//", StringComparison.Ordinal) && !t.StartsWith(';'))
                {
                    _warnings.Add(WarningSeverity.Warning, WarningCategory.Parser, source, "Riga di profilo senza «=»", i + 1, line);
                }

                raw.Add(line);
                continue;
            }

            var impostazione = new ImpostazioneDelProfilo
            {
                Sezione = sezione,
                Chiave = line[..uguale],
                Valore = line[(uguale + 1)..],
                Source = new SourceRef(source, i + 1, sezione.Length > 0 ? sezione : null),
            };
            FlushRaw();
            chunks.Add(new RecordChunk<ImpostazioneDelProfilo>(impostazione, new[] { line }, hasMarkers: false));
            records.Add(impostazione);
        }

        FlushRaw();
        return new ParseResult<ImpostazioneDelProfilo>(records, chunks, read.Encoding, read.HasByteOrderMark)
        {
            NewLine = read.NewLine,
            HasFinalNewLine = read.HasFinalNewLine,
        };
    }
}
