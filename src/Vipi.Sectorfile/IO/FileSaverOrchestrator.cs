using System.Text;
using Vipi.Sectorfile.Models;

namespace Vipi.Sectorfile.IO;

/// <summary>
/// Writes a file back to disk preserving every unmodified byte verbatim and re-serialising only
/// the dirty records. Dirty records (and records that already had //Start//End markers) are wrapped
/// in markers; an empty dirty set yields a byte-for-byte copy of the original (NFR-04).
/// </summary>
public sealed class FileSaverOrchestrator
{
    private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };

    /// <summary>
    /// Serialises <paramref name="parseResult"/> to <paramref name="filePath"/>, replacing the
    /// records in <paramref name="dirtyRecords"/> via <paramref name="saver"/>. Dirty membership is
    /// by reference identity. The write is atomic (.tmp + Flush(true) + File.Replace).
    /// </summary>
    /// <exception cref="IOException">Propagated if the file cannot be written; the original is left intact.</exception>
    public void Save<T>(
        ParseResult<T> parseResult,
        ISet<T> dirtyRecords,
        IFileSaver<T> saver,
        string filePath)
    {
        ArgumentNullException.ThrowIfNull(parseResult);
        ArgumentNullException.ThrowIfNull(dirtyRecords);
        ArgumentNullException.ThrowIfNull(saver);
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        var lines = BuildLines(parseResult, dirtyRecords, saver);
        string text = string.Join(parseResult.NewLine, lines);
        if (parseResult.HasFinalNewLine)
        {
            text += parseResult.NewLine;
        }

        WriteAtomic(filePath, text, parseResult.Encoding, parseResult.HasByteOrderMark);
    }

    private static List<string> BuildLines<T>(ParseResult<T> parseResult, ISet<T> dirtyRecords, IFileSaver<T> saver)
    {
        var uniqueIds = AssignUniqueIdentifiers(parseResult, saver);
        var output = new List<string>();

        foreach (var chunk in parseResult.Chunks)
        {
            switch (chunk)
            {
                case RawChunk<T> raw:
                    output.AddRange(raw.Lines);
                    break;

                case RecordChunk<T> record:
                    output.AddRange(record.LeadingComments);

                    bool isDirty = dirtyRecords.Contains(record.Record);
                    bool emitMarkers = isDirty || record.HasMarkers;
                    string id = uniqueIds[record];

                    if (emitMarkers)
                    {
                        output.Add($"//Start {id}");
                    }

                    output.AddRange(isDirty ? saver.Serialize(record.Record) : record.RawLines);

                    if (emitMarkers)
                    {
                        output.Add($"//End {id}");
                    }

                    break;
            }
        }

        return output;
    }

    /// <summary>
    /// Computes the marker identifier for every RecordChunk in file order, appending _2, _3… to
    /// later records that share a base identifier (INTERFACE_CONTRACTS §2 uniqueness rule).
    /// </summary>
    private static Dictionary<RecordChunk<T>, string> AssignUniqueIdentifiers<T>(ParseResult<T> parseResult, IFileSaver<T> saver)
    {
        var ids = new Dictionary<RecordChunk<T>, string>();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var chunk in parseResult.Chunks)
        {
            if (chunk is not RecordChunk<T> record)
            {
                continue;
            }

            string baseId = saver.GetIdentifier(record.Record);
            if (counts.TryGetValue(baseId, out int seen))
            {
                seen++;
                counts[baseId] = seen;
                ids[record] = $"{baseId}_{seen}";
            }
            else
            {
                counts[baseId] = 1;
                ids[record] = baseId;
            }
        }

        return ids;
    }

    private static void WriteAtomic(string filePath, string text, Encoding encoding, bool hasByteOrderMark)
    {
        // GetBytes never emits a BOM; we prepend it explicitly when required. UTF-8 is normalised to
        // a non-BOM-emitting instance; legacy encodings (e.g. Windows-1252) are used as-is.
        Encoding contentEncoding = encoding.CodePage == Encoding.UTF8.CodePage
            ? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            : encoding;

        string tmpPath = filePath + ".tmp";
        try
        {
            using (var stream = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                if (hasByteOrderMark)
                {
                    stream.Write(Utf8Bom, 0, Utf8Bom.Length);
                }

                byte[] content = contentEncoding.GetBytes(text);
                stream.Write(content, 0, content.Length);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(filePath))
            {
                File.Replace(tmpPath, filePath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tmpPath, filePath);
            }
        }
        catch
        {
            TryDelete(tmpPath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup of the partial .tmp; ignore secondary failures.
        }
    }
}
