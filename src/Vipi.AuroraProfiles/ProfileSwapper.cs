namespace Vipi.AuroraProfiles;

/// <summary>Thrown when a section requested for swapping is not present in the source profile.</summary>
public sealed class SectionNotFoundException : Exception
{
    /// <summary>Name of the section (without brackets) that was missing in the source.</summary>
    public string SectionName { get; }

    /// <summary>Create the exception for the given missing section name.</summary>
    public SectionNotFoundException(string sectionName)
        : base($"Section [{sectionName}] not found in the source profile.")
        => SectionName = sectionName;
}

/// <summary>Result of a swap for a single section, useful for reporting to the user.</summary>
/// <param name="SectionName">The section that was copied.</param>
/// <param name="WasReplaced">True if the section already existed in the destination and was
/// replaced in place; false if it was appended at the end because it was missing.</param>
public readonly record struct SwapOutcome(string SectionName, bool WasReplaced);

/// <summary>
/// Copies whole sections from a source profile into a destination profile.
/// The destination passed in is never mutated; a new <see cref="CprProfile"/> is returned.
/// Everything outside the selected sections is preserved byte-for-byte.
/// </summary>
public static class ProfileSwapper
{
    /// <summary>
    /// Copy a single section from <paramref name="source"/> into a copy of
    /// <paramref name="destination"/>. If the section exists in the destination it is
    /// replaced in place; otherwise it is appended at the end.
    /// </summary>
    /// <exception cref="SectionNotFoundException">The section is absent from the source.</exception>
    public static CprProfile SwapSection(
        CprProfile destination, CprProfile source, string sectionName)
        => SwapSections(destination, source, new[] { sectionName }, out _);

    /// <summary>
    /// Copy several sections from <paramref name="source"/> into a copy of
    /// <paramref name="destination"/>. All requested sections must exist in the source,
    /// otherwise a <see cref="SectionNotFoundException"/> is thrown and nothing is changed.
    /// </summary>
    public static CprProfile SwapSections(
        CprProfile destination, CprProfile source, IEnumerable<string> sectionNames)
        => SwapSections(destination, source, sectionNames, out _);

    /// <summary>
    /// As <see cref="SwapSections(CprProfile, CprProfile, IEnumerable{string})"/>, also
    /// reporting per-section outcomes (replaced vs appended).
    /// </summary>
    public static CprProfile SwapSections(
        CprProfile destination, CprProfile source, IEnumerable<string> sectionNames,
        out IReadOnlyList<SwapOutcome> outcomes)
    {
        // Deduplicate case-insensitively, preserving order.
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in sectionNames)
            if (seen.Add(n)) names.Add(n);

        // Validate first: fail atomically before touching anything.
        foreach (var name in names)
            if (source.FindSection(name) is null)
                throw new SectionNotFoundException(name);

        var result = destination.Clone();
        var report = new List<SwapOutcome>(names.Count);

        foreach (var name in names)
        {
            var srcSection = source.FindSection(name)!.Clone();
            int idx = result.Sections.FindIndex(s =>
                s.Name is not null &&
                string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

            if (idx >= 0)
            {
                result.Sections[idx] = srcSection;
                report.Add(new SwapOutcome(name, WasReplaced: true));
            }
            else
            {
                result.Sections.Add(srcSection);
                report.Add(new SwapOutcome(name, WasReplaced: false));
            }
        }

        outcomes = report;
        return result;
    }
}
