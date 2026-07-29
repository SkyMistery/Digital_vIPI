using System.Collections.Generic;
using Vipi.Application.Aor;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>Colori di default per tipo-ente (suffisso callsign) + risoluzione con override manuale.</summary>
public class AorColorSchemeTests
{
    [Theory]
    [InlineData("LIRP_TWR", "TWR")]
    [InlineData("LIRR_NE_CTR", "CTR")]
    [InlineData("LIRP_US0_APP", "APP")]
    [InlineData("LIML_GND", "GND")]
    [InlineData("bare", "BARE")]
    [InlineData("", "")]
    public void SuffixOf_Takes_Last_Token(string callsign, string expected) =>
        Assert.Equal(expected, AorColorScheme.SuffixOf(callsign));

    [Fact]
    public void DefaultForCallsign_Maps_Type_To_Color()
    {
        Assert.Equal(AorColorScheme.Defaults["TWR"], AorColorScheme.DefaultForCallsign("LIRP_TWR"));
        Assert.Equal(AorColorScheme.Defaults["CTR"], AorColorScheme.DefaultForCallsign("LIRR_NE_CTR"));
        // Tutte le torri condividono lo stesso colore, a prescindere dall'aeroporto.
        Assert.Equal(AorColorScheme.DefaultForCallsign("LIRF_TWR"), AorColorScheme.DefaultForCallsign("LIML_TWR"));
    }

    [Fact]
    public void DefaultForCallsign_Unknown_Suffix_Falls_Back()
    {
        Assert.Equal(AorColorScheme.Fallback, AorColorScheme.DefaultForCallsign("SOMETHING_WEIRD"));
    }

    [Fact]
    public void Resolve_Prefers_Override_Then_Default()
    {
        var overrides = new Dictionary<string, string> { ["LIRP_TWR"] = "#abcdef" };
        Assert.Equal("#abcdef", AorColorScheme.Resolve("LIRP_TWR", overrides));                 // override
        Assert.Equal(AorColorScheme.Defaults["APP"], AorColorScheme.Resolve("LIRP_APP", overrides)); // default
        Assert.Equal(AorColorScheme.Defaults["APP"], AorColorScheme.Resolve("LIRP_APP", null));      // no overrides
    }

    [Fact]
    public void Resolve_Ignores_Blank_Override()
    {
        var overrides = new Dictionary<string, string> { ["LIRP_TWR"] = "  " };
        Assert.Equal(AorColorScheme.Defaults["TWR"], AorColorScheme.Resolve("LIRP_TWR", overrides));
    }
}
