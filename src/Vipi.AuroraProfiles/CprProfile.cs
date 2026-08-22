using System.Text;

namespace Vipi.AuroraProfiles;

/// <summary>
/// An Aurora .cpr profile parsed as an ordered list of verbatim blocks
/// (<see cref="CprSection"/>). Parsing and serialization are byte-lossless:
/// <c>Serialize()</c> of a freshly parsed, unmodified profile reproduces the
/// original text exactly, and <c>ToBytes()</c> reproduces the original bytes exactly.
/// </summary>
public sealed class CprProfile
{
    /// <summary>
    /// Encoding used for byte-level load/save. Latin-1 maps every byte 0x00-0xFF to a
    /// single char and back, guaranteeing byte-identical round-trips regardless of the
    /// original ANSI/Windows-1252 content (we never reinterpret the bytes, only move them).
    /// </summary>
    public static readonly Encoding FileEncoding = Encoding.Latin1;

    /// <summary>Ordered blocks of the profile (preamble first, if any).</summary>
    public List<CprSection> Sections { get; }

    private CprProfile(List<CprSection> sections) => Sections = sections;

    /// <summary>Names of the real sections, in file order (excludes the preamble).</summary>
    public IEnumerable<string> SectionNames =>
        Sections.Where(s => s.Name is not null).Select(s => s.Name!);

    /// <summary>Find a section by name (case-insensitive), or null if absent.</summary>
    public CprSection? FindSection(string name) =>
        Sections.FirstOrDefault(s =>
            s.Name is not null && string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Parse from text.</summary>
    public static CprProfile Parse(string text)
    {
        var blocks = new List<CprSection>();
        string? curName = null;
        var curLines = new List<string>();
        bool started = false;

        void Flush()
        {
            if (started || curLines.Count > 0)
                blocks.Add(new CprSection(curName, curLines));
        }

        foreach (var line in SplitKeepEnds(text))
        {
            var name = HeaderName(line);
            if (name is not null)
            {
                Flush();
                curName = name;
                curLines = new List<string> { line };
                started = true;
            }
            else
            {
                curLines.Add(line);
            }
        }
        Flush();
        return new CprProfile(blocks);
    }

    /// <summary>Parse from raw bytes using <see cref="FileEncoding"/>.</summary>
    public static CprProfile Load(byte[] bytes) => Parse(FileEncoding.GetString(bytes));

    /// <summary>Serialize back to text, verbatim.</summary>
    public string Serialize()
    {
        var sb = new StringBuilder();
        foreach (var s in Sections)
            foreach (var l in s.Lines)
                sb.Append(l);
        return sb.ToString();
    }

    /// <summary>Serialize back to raw bytes using <see cref="FileEncoding"/>.</summary>
    public byte[] ToBytes() => FileEncoding.GetBytes(Serialize());

    /// <summary>Deep copy of the whole profile.</summary>
    public CprProfile Clone() => new(Sections.Select(s => s.Clone()).ToList());

    // --- helpers ---

    /// <summary>
    /// Split text into lines while preserving each line's terminator (\r\n, \n, or \r).
    /// A trailing line without a terminator is preserved as-is.
    /// </summary>
    internal static IEnumerable<string> SplitKeepEnds(string text)
    {
        int n = text.Length, start = 0, i = 0;
        while (i < n)
        {
            char c = text[i];
            if (c == '\n')
            {
                yield return text.Substring(start, i - start + 1);
                start = i + 1;
            }
            else if (c == '\r')
            {
                if (i + 1 < n && text[i + 1] == '\n')
                {
                    yield return text.Substring(start, i - start + 2);
                    i++;
                    start = i + 1;
                }
                else
                {
                    yield return text.Substring(start, i - start + 1);
                    start = i + 1;
                }
            }
            i++;
        }
        if (start < n)
            yield return text.Substring(start);
    }

    /// <summary>If the line is a section header "[NAME]" (ignoring surrounding whitespace
    /// and its line ending), return NAME; otherwise null.</summary>
    internal static string? HeaderName(string line)
    {
        var s = line.TrimEnd('\r', '\n').Trim();
        if (s.Length >= 2 && s[0] == '[' && s[^1] == ']')
            return s.Substring(1, s.Length - 2);
        return null;
    }
}
