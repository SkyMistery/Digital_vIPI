namespace Vipi.AuroraProfiles.Tests;

public class RoundTripTests
{
    /// <summary>
    /// The anti-corruption test: parsing then serializing an unmodified profile must
    /// reproduce the original bytes exactly, for every real profile.
    /// </summary>
    [Theory]
    [ClassData(typeof(AllProfilesData))]
    public void Load_then_ToBytes_is_byte_identical(string path)
    {
        var original = File.ReadAllBytes(path);
        var profile = CprProfile.Load(original);

        var roundTripped = profile.ToBytes();

        Assert.Equal(original, roundTripped);
    }

    [Theory]
    [ClassData(typeof(AllProfilesData))]
    public void Every_profile_has_expected_core_sections(string path)
    {
        var profile = CprProfile.Load(File.ReadAllBytes(path));
        var names = profile.SectionNames.ToList();

        Assert.Contains("TRAFFICLISTS", names);
        Assert.Contains("Connection", names);
        Assert.NotEmpty(names);
    }

    [Fact]
    public void SplitKeepEnds_preserves_all_terminators_and_trailing_line()
    {
        var text = "a\r\nb\nc\rd"; // CRLF, LF, CR, then no terminator
        var lines = CprProfile.SplitKeepEnds(text).ToArray();

        Assert.Equal(new[] { "a\r\n", "b\n", "c\r", "d" }, lines);
        Assert.Equal(text, string.Concat(lines));
    }

    [Fact]
    public void HeaderName_detects_sections_and_ignores_key_values()
    {
        Assert.Equal("TRAFFICLISTS", CprProfile.HeaderName("[TRAFFICLISTS]\r\n"));
        Assert.Null(CprProfile.HeaderName("LST0XPos=640\r\n"));
        Assert.Null(CprProfile.HeaderName("\r\n"));
    }
}
