using Vipi.Domain;
using Xunit;

namespace Vipi.Domain.Tests;

/// <summary>Formattazione livello + suffisso parità (regola semicircolare).</summary>
public class LevelFormattingTests
{
    [Fact]
    public void Any_parity_has_no_suffix()
    {
        Assert.Equal("FL130↓",
            LevelFormatting.Format(130, LevelUnit.Fl, LevelConstraint.AtOrBelow, null, LevelParity.Any));
        // default del parametro = Any (compatibilità chiamate a 4 argomenti).
        Assert.Equal("FL130↓",
            LevelFormatting.Format(130, LevelUnit.Fl, LevelConstraint.AtOrBelow, null));
    }

    [Theory]
    [InlineData(LevelParity.Even, "FL290↑ (pari)")]
    [InlineData(LevelParity.Odd, "FL290↑ (dispari)")]
    public void Even_odd_append_suffix(LevelParity parity, string expected)
    {
        Assert.Equal(expected,
            LevelFormatting.Format(290, LevelUnit.Fl, LevelConstraint.AtOrAbove, null, parity));
    }

    [Fact]
    public void Special_text_keeps_parity_suffix()
    {
        Assert.Equal("per aerovia (pari)",
            LevelFormatting.Format(null, LevelUnit.Fl, LevelConstraint.Special, "per aerovia", LevelParity.Even));
    }

    [Theory]
    [InlineData(LevelParity.Any, "")]
    [InlineData(LevelParity.Even, "pari")]
    [InlineData(LevelParity.Odd, "dispari")]
    public void ParityLabel_maps_italian(LevelParity parity, string expected) =>
        Assert.Equal(expected, LevelFormatting.ParityLabel(parity));
}
