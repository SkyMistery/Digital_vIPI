using System.Collections.Generic;
using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>Composizione della frase di coordinamento: mapping stato ↑/↓/exact, omissione codice per APP,
/// fallback nomi, livello speciale (niente «per FL»), aeroporto assente (nessuna frase).</summary>
public class CoordinationSentenceComposerTests
{
    private static readonly CoordinationSentenceTemplate Tpl = CoordinationSentenceTemplate.Default;

    private static readonly IReadOnlyDictionary<string, SectorType> Types =
        new Dictionary<string, SectorType>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["LIRR_NE_CTR"] = SectorType.Ctr,
            ["LIMM_WS2"] = SectorType.Ctr,
            ["LIRP_APP"] = SectorType.App,
        };
    // Sector.Name: i CTR proiettati = callsign (nome nice via AtcCallsign); gli APP hanno il nome IVAO.
    private static readonly IReadOnlyDictionary<string, string> Names =
        new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["LIRR_NE_CTR"] = "LIRR_NE_CTR",
            ["LIMM_WS2"] = "LIMM_WS2",
            ["LIRP_APP"] = "LIRP Approach",
        };
    private static readonly IReadOnlyDictionary<string, string> Codes =
        new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["LIRR_NE_CTR"] = "NE",
            ["LIMM_WS2"] = "WS2",
            ["LIRP_APP"] = "US0",
        };
    private static readonly IReadOnlyDictionary<string, string> Airports =
        new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["LIRF"] = "Fiumicino",
            ["LIRP"] = "Pisa - San Giusto",
        };
    // Sector.Name dei CTR = callsign (proiezione); il nome nice arriva da AtcCallsign + MiddleIdentifier.
    private static readonly IReadOnlyDictionary<string, string> Atc =
        new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["LIRR_NE_CTR"] = "Roma Radar",
            ["LIMM_WS2"] = "Milano Radar",
            ["LIRP_APP"] = "Pisa Approach",
        };

    private static string? Compose(string owner, string target, string? icao, LevelConstraint c, string level, string cop)
        => CoordinationSentences.Compose(Tpl, Types, Names, Codes, Airports, Atc, owner, target, icao, c, level, cop);

    [Fact]
    public void Ctr_target_includes_code_and_descent()
    {
        var s = Compose("LIRR_NE_CTR", "LIMM_WS2", "LIRF", LevelConstraint.AtOrBelow, "FL130↓", "VALMA");
        Assert.Equal("Roma Radar NE trasferisce a Milano Radar WS2 il traffico con destinazione Fiumicino LIRF in discesa per FL130 su VALMA.", s);
    }

    [Fact]
    public void App_target_omits_code()
    {
        var s = Compose("LIRR_NE_CTR", "LIRP_APP", "LIRP", LevelConstraint.AtOrBelow, "FL120↓", "MAREL");
        Assert.Equal("Roma Radar NE trasferisce a Pisa Approach il traffico con destinazione Pisa - San Giusto LIRP in discesa per FL120 su MAREL.", s);
        Assert.DoesNotContain("US0", s);
    }

    [Fact]
    public void Climb_and_level_state_words()
    {
        var up = Compose("LIRR_NE_CTR", "LIMM_WS2", "LIRF", LevelConstraint.AtOrAbove, "FL280↑", "VALMA");
        Assert.Contains("in salita per FL280", up);
        var lvl = Compose("LIRR_NE_CTR", "LIMM_WS2", "LIRF", LevelConstraint.Exact, "FL240", "VALMA");
        Assert.Contains("stabile per FL240", lvl);
    }

    [Fact]
    public void Special_level_has_no_per_fl()
    {
        var s = Compose("LIRR_NE_CTR", "LIMM_WS2", "LIRF", LevelConstraint.Special, "per aerovia", "ELB");
        Assert.DoesNotContain("per FL", s);
        Assert.Contains("destinazione Fiumicino LIRF per aerovia su ELB.", s);
    }

    [Fact]
    public void Missing_airport_returns_null()
    {
        Assert.Null(Compose("LIRR_NE_CTR", "LIMM_WS2", null, LevelConstraint.AtOrBelow, "FL130↓", "VALMA"));
        Assert.Null(Compose("LIRR_NE_CTR", "LIMM_WS2", "", LevelConstraint.AtOrBelow, "FL130↓", "VALMA"));
    }

    [Fact]
    public void Unknown_names_fall_back_to_callsign_and_icao()
    {
        var s = Compose("LFOO_CTR", "LFBB_XX", "LFPG", LevelConstraint.AtOrBelow, "FL130↓", "ABC");
        Assert.Contains("LFOO_CTR trasferisce a LFBB_XX", s);   // niente codice noto → solo callsign
        Assert.Contains("destinazione LFPG LFPG", s);            // ICAO come nome di fallback
    }
}
