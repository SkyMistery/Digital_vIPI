namespace Vipi.AuroraProfiles;

/// <summary>
/// A single block of a .cpr profile: either a named section (e.g. "TRAFFICLISTS")
/// or the preamble before the first section (<see cref="Name"/> is null).
/// All lines are kept verbatim, including their original line endings. For a named
/// section, <c>Lines[0]</c> is the header line (e.g. "[TRAFFICLISTS]\r\n").
/// </summary>
public sealed class CprSection
{
    /// <summary>Section name without brackets, or null for the file preamble.</summary>
    public string? Name { get; }

    /// <summary>Raw lines of this block, verbatim (line endings preserved).</summary>
    public List<string> Lines { get; }

    /// <summary>Create a block from a section name (null for the preamble) and its verbatim lines.</summary>
    public CprSection(string? name, IEnumerable<string> lines)
    {
        Name = name;
        Lines = new List<string>(lines);
    }

    /// <summary>The exact text of this block (all lines joined verbatim).</summary>
    public string RawText => string.Concat(Lines);

    /// <summary>Deep copy, so swaps never share mutable state between profiles.</summary>
    public CprSection Clone() => new(Name, Lines);
}
