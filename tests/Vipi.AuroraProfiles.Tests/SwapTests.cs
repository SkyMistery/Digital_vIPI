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

    /// <summary>T-051: la destinazione finisce senza a-capo. La sezione accodata non deve incollarsi
    /// all'ultima riga («Color=12[MAPS]»), o l'intestazione si perde alla prossima lettura.</summary>
    [Fact]
    public void Appended_section_does_not_glue_to_a_last_line_without_terminator()
    {
        var dest = CprProfile.Parse("[COLORS]\r\nText=1\r\nColor=12");
        var src = CprProfile.Parse("[MAPS]\r\nMap=LIRF\r\n");

        var result = ProfileSwapper.SwapSection(dest, src, "MAPS");
        var reread = CprProfile.Parse(result.Serialize());

        Assert.Equal("[COLORS]\r\nText=1\r\nColor=12\r\n[MAPS]\r\nMap=LIRF\r\n", result.Serialize());
        Assert.Equal(new[] { "COLORS", "MAPS" }, reread.SectionNames);
    }

    /// <summary>T-051, l'altro verso: la sezione copiata era l'ultima della sorgente, senza a-capo, e
    /// sostituisce una sezione in mezzo alla destinazione. L'intestazione che la segue non deve incollarsi.</summary>
    [Fact]
    public void Replaced_section_without_terminator_does_not_swallow_the_next_header()
    {
        var dest = CprProfile.Parse("[MAPS]\nMap=OLD\n[LABELS]\nLabel=1\n");
        var src = CprProfile.Parse("[LABELS]\nLabel=9\n[MAPS]\nMap=NEW");

        var result = ProfileSwapper.SwapSection(dest, src, "MAPS");
        var reread = CprProfile.Parse(result.Serialize());

        Assert.Equal("[MAPS]\nMap=NEW\n[LABELS]\nLabel=1\n", result.Serialize());
        Assert.Equal(new[] { "MAPS", "LABELS" }, reread.SectionNames);
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
