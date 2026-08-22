namespace Vipi.AuroraProfiles.Tests;

public class SwapTests
{
    private static CprProfile Load(string fileName) =>
        CprProfile.Load(File.ReadAllBytes(TestProfiles.Path(fileName)));

    [Fact]
    public void Swap_changes_only_the_selected_section()
    {
        var dest = Load("BASIC_TWR.cpr");
        var src = Load("LIRN_TWR.cpr");

        var result = ProfileSwapper.SwapSection(dest, src, "TRAFFICLISTS");

        foreach (var name in dest.SectionNames)
        {
            var expected = name.Equals("TRAFFICLISTS", StringComparison.OrdinalIgnoreCase)
                ? src.FindSection(name)!.RawText
                : dest.FindSection(name)!.RawText;
            Assert.Equal(expected, result.FindSection(name)!.RawText);
        }
    }

    [Fact]
    public void Swapped_section_equals_source_exactly()
    {
        var dest = Load("BASIC_TWR.cpr");
        var src = Load("LIRN_TWR.cpr");

        var result = ProfileSwapper.SwapSection(dest, src, "TRAFFICLISTS");

        Assert.Equal(
            src.FindSection("TRAFFICLISTS")!.RawText,
            result.FindSection("TRAFFICLISTS")!.RawText);
    }

    [Fact]
    public void Swap_does_not_mutate_the_destination()
    {
        var dest = Load("BASIC_TWR.cpr");
        var src = Load("LIRN_TWR.cpr");
        var destBefore = dest.ToBytes();

        _ = ProfileSwapper.SwapSection(dest, src, "TRAFFICLISTS");

        Assert.Equal(destBefore, dest.ToBytes());
    }

    [Fact]
    public void Swap_is_case_insensitive_on_section_name()
    {
        var dest = Load("BASIC_TWR.cpr");
        var src = Load("LIRN_TWR.cpr");

        var result = ProfileSwapper.SwapSections(
            dest, src, new[] { "trafficlists" }, out var outcomes);

        Assert.True(outcomes.Single().WasReplaced);
        Assert.Equal(
            src.FindSection("TRAFFICLISTS")!.RawText,
            result.FindSection("TRAFFICLISTS")!.RawText);
    }

    [Fact]
    public void Missing_section_in_destination_is_appended_at_end()
    {
        // BASIC_TWR has no [GC]; LICC_TWR has one.
        var dest = Load("BASIC_TWR.cpr");
        var src = Load("LICC_TWR.cpr");
        Assert.Null(dest.FindSection("GC"));
        Assert.NotNull(src.FindSection("GC"));

        var result = ProfileSwapper.SwapSections(dest, src, new[] { "GC" }, out var outcomes);

        Assert.False(outcomes.Single().WasReplaced); // appended, not replaced
        Assert.NotNull(result.FindSection("GC"));
        Assert.Equal("GC", result.Sections[^1].Name); // last block
    }

    [Fact]
    public void Missing_section_in_source_throws_and_changes_nothing()
    {
        var dest = Load("BASIC_TWR.cpr");
        var src = Load("LIRN_GND.cpr"); // has no [GC]
        Assert.Null(src.FindSection("GC"));

        var ex = Assert.Throws<SectionNotFoundException>(
            () => ProfileSwapper.SwapSection(dest, src, "GC"));
        Assert.Equal("GC", ex.SectionName);
    }

    [Fact]
    public void Multi_swap_is_atomic_when_one_section_is_missing_in_source()
    {
        var dest = Load("BASIC_TWR.cpr");
        var src = Load("LIRN_GND.cpr"); // has no [GC]

        // "GC" is missing in src -> whole operation must throw, nothing swapped.
        Assert.Throws<SectionNotFoundException>(
            () => ProfileSwapper.SwapSections(dest, src, new[] { "TRAFFICLISTS", "GC" }));
    }

    [Fact]
    public void Multi_swap_copies_all_requested_sections()
    {
        var dest = Load("BASIC_TWR.cpr");
        var src = Load("LIRN_TWR.cpr");
        var names = new[] { "TRAFFICLISTS", "LABELS", "STCA" };

        var result = ProfileSwapper.SwapSections(dest, src, names);

        foreach (var n in names)
            Assert.Equal(src.FindSection(n)!.RawText, result.FindSection(n)!.RawText);
    }

    [Fact]
    public void Result_stays_byte_identical_outside_the_swapped_section()
    {
        var dest = Load("BASIC_TWR.cpr");
        var src = Load("LIRN_TWR.cpr");

        var result = ProfileSwapper.SwapSection(dest, src, "TRAFFICLISTS");

        // Rebuild expected bytes: dest with only the TRAFFICLISTS block substituted.
        var expected = dest.Clone();
        int idx = expected.Sections.FindIndex(s =>
            string.Equals(s.Name, "TRAFFICLISTS", StringComparison.OrdinalIgnoreCase));
        expected.Sections[idx] = src.FindSection("TRAFFICLISTS")!.Clone();

        Assert.Equal(expected.ToBytes(), result.ToBytes());
    }
}
