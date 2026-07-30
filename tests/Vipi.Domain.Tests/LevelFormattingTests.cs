using Vipi.Domain;
using Xunit;

namespace Vipi.Domain.Tests;

/// <summary>Formattazione livello + suffisso parità (regola semicircolare).</summary>
public class LevelFormattingTests
{
    [Fact]
    public void Any_parity_has_no_suffix()
    {
        // Il vincolo ≤ è reso col segno «-» (NON con una freccia: la freccia ora indica lo stato verticale).
        Assert.Equal("FL130-",
            LevelFormatting.Format(130, LevelUnit.Fl, LevelConstraint.AtOrBelow, null, LevelParity.Any));
        // default dei parametri = Any/Unspecified (compatibilità chiamate a 4 argomenti).
        Assert.Equal("FL130-",
            LevelFormatting.Format(130, LevelUnit.Fl, LevelConstraint.AtOrBelow, null));
    }

    [Theory]
    [InlineData(LevelParity.Even, "FL290+ (pari)")]
    [InlineData(LevelParity.Odd, "FL290+ (dispari)")]
    public void Even_odd_append_suffix(LevelParity parity, string expected)
    {
        // ≥ → segno «+».
        Assert.Equal(expected,
            LevelFormatting.Format(290, LevelUnit.Fl, LevelConstraint.AtOrAbove, null, parity));
    }

    [Theory]
    [InlineData(TransferVerticalState.Descending, "FL130- ↓")]
    [InlineData(TransferVerticalState.Climbing, "FL130- ↑")]
    [InlineData(TransferVerticalState.Level, "FL130-")]        // stabile: nessuna freccia
    [InlineData(TransferVerticalState.Unspecified, "FL130-")]  // non specificato: nessuna freccia
    public void Vertical_state_appends_arrow_independent_of_constraint(TransferVerticalState state, string expected)
    {
        // Vincolo ≤ (→ «-») + stato verticale (→ freccia): due dimensioni indipendenti, entrambe nel testo.
        Assert.Equal(expected,
            LevelFormatting.Format(130, LevelUnit.Fl, LevelConstraint.AtOrBelow, null, LevelParity.Any, state));
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
