using System.Text;

namespace Vipi.Sectorfile.IO;

/// <summary>Outcome of reading a sector file: its lines plus the fidelity metadata needed for round-trip.</summary>
public readonly record struct FileReadResult(
    IReadOnlyList<string> Lines,
    Encoding Encoding,
    bool HasByteOrderMark,
    string NewLine,
    bool HasFinalNewLine);

/// <summary>
/// Reads a sector file into lines while detecting encoding, BOM and line-ending style.
/// Shared foundation for all parsers (Phase 4+). Strategy: strict UTF-8 first, falling back to
/// Windows-1252 (Latin-1) on invalid bytes (INTERFACE_CONTRACTS §1.1 / §3 E1–E2).
/// </summary>
public static class SectorFileReader
{
    private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };

    static SectorFileReader()
        // Windows-1252 is not built into .NET Core; register the provider before any GetEncoding(1252).
        => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static FileReadResult Read(string filePath)
    {
        byte[] raw = File.ReadAllBytes(filePath);
        return Decode(raw);
    }

    /// <summary>Decodes raw bytes (exposed for testing without touching the filesystem).</summary>
    public static FileReadResult Decode(byte[] raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        bool hasBom = raw.Length >= 3 && raw[0] == Utf8Bom[0] && raw[1] == Utf8Bom[1] && raw[2] == Utf8Bom[2];
        int start = hasBom ? 3 : 0;

        Encoding encoding;
        string text;
        try
        {
            // Strict UTF-8: throws on invalid byte sequences so we can fall back.
            var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            text = strictUtf8.GetString(raw, start, raw.Length - start);

            // Store a non-throwing UTF-8 (Equals Encoding.UTF8 by codepage + default fallbacks).
            encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }
        catch (DecoderFallbackException)
        {
            encoding = Encoding.GetEncoding(1252);
            text = encoding.GetString(raw);   // Windows-1252 never throws; no BOM concept
            hasBom = false;
        }

        string newLine = DetectNewLine(text);
        bool hasFinalNewLine = text.EndsWith(newLine, StringComparison.Ordinal);

        var lines = SplitLines(text, newLine, hasFinalNewLine);
        return new FileReadResult(lines, encoding, hasBom, newLine, hasFinalNewLine);
    }

    private static string DetectNewLine(string text)
    {
        int crlf = text.IndexOf("\r\n", StringComparison.Ordinal);
        if (crlf >= 0)
        {
            return "\r\n";
        }

        return text.Contains('\n') ? "\n" : "\r\n";
    }

    private static IReadOnlyList<string> SplitLines(string text, string newLine, bool hasFinalNewLine)
    {
        if (text.Length == 0)
        {
            return new[] { string.Empty };
        }

        var parts = text.Split(newLine);
        // A trailing terminator yields a final empty element that is not a real line.
        if (hasFinalNewLine && parts.Length > 0 && parts[^1].Length == 0)
        {
            Array.Resize(ref parts, parts.Length - 1);
        }

        return parts;
    }
}
