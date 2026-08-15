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

    // ---- Parse: l'inverso di Format, per l'editing in cella --------------------------------------------
    // La correttezza qui è una PROPRIETÀ, non un elenco di casi: qualunque testo Format sappia produrre deve
    // tornare identico dopo essere stato riletto. Un elenco di casi coprirebbe quelli a cui si è pensato; il
    // round-trip copre anche quelli a cui non si è pensato, ed è dove stanno i difetti.

    public static TheoryData<int?, LevelUnit, LevelConstraint, string?, LevelParity, TransferVerticalState> RoundTripCases()
    {
        var data = new TheoryData<int?, LevelUnit, LevelConstraint, string?, LevelParity, TransferVerticalState>();
        var parities = new[] { LevelParity.Any, LevelParity.Even, LevelParity.Odd };
        var states = new[] { TransferVerticalState.Unspecified, TransferVerticalState.Climbing, TransferVerticalState.Descending };

        foreach (var parity in parities)
            foreach (var state in states)
            {
                foreach (var constraint in new[] { LevelConstraint.Exact, LevelConstraint.AtOrAbove, LevelConstraint.AtOrBelow })
                {
                    // FL190 e 2500 ft: il primo sta sotto la soglia dei livelli, il secondo sopra — ed è la
                    // coppia che mette alla prova la lettura del numero nudo.
                    data.Add(190, LevelUnit.Fl, constraint, null, parity, state);
                    data.Add(2500, LevelUnit.Feet, constraint, null, parity, state);
                }
                // Il testo libero non ha né unità né vincolo: sta FUORI dai due cicli, altrimenti lo stesso caso
                // si ripeterebbe sei volte sotto sei nomi diversi.
                data.Add(null, LevelUnit.Fl, LevelConstraint.Special, "per aerovia", parity, state);
                // Cella vuota: Format scrive «—», e va riletto come «nessun livello», non come frase.
                data.Add(null, LevelUnit.Fl, LevelConstraint.Exact, null, parity, state);
            }
        return data;
    }

    [Theory]
    [MemberData(nameof(RoundTripCases))]
    public void Parse_reads_back_what_Format_wrote(int? value, LevelUnit unit, LevelConstraint constraint,
        string? special, LevelParity parity, TransferVerticalState state)
    {
        var text = LevelFormatting.Format(value, unit, constraint, special, parity, state);
        Assert.Equal(text, LevelFormatting.Format(LevelFormatting.Parse(text)));
    }

    [Theory]
    [InlineData("FL190", 190, LevelUnit.Fl, LevelConstraint.Exact)]
    [InlineData("fl190", 190, LevelUnit.Fl, LevelConstraint.Exact)]      // maiuscole indifferenti
    [InlineData("FL 190", 190, LevelUnit.Fl, LevelConstraint.Exact)]     // spazio dopo FL
    [InlineData("FL130-", 130, LevelUnit.Fl, LevelConstraint.AtOrBelow)]
    [InlineData("FL290+", 290, LevelUnit.Fl, LevelConstraint.AtOrAbove)]
    [InlineData("2500 ft", 2500, LevelUnit.Feet, LevelConstraint.Exact)]
    [InlineData("2500ft", 2500, LevelUnit.Feet, LevelConstraint.Exact)]  // senza spazio
    [InlineData("  FL190  ", 190, LevelUnit.Fl, LevelConstraint.Exact)]  // spazi attorno
    public void Parse_reads_a_written_level(string text, int value, LevelUnit unit, LevelConstraint constraint)
    {
        var p = LevelFormatting.Parse(text);
        Assert.Equal(value, p.Value);
        Assert.Equal(unit, p.Unit);
        Assert.Equal(constraint, p.Constraint);
        Assert.Null(p.Special);
    }

    [Theory]
    [InlineData("190", 190, LevelUnit.Fl)]     // sotto FL660: è un livello
    [InlineData("660", 660, LevelUnit.Fl)]     // il confine appartiene ai livelli
    [InlineData("3000", 3000, LevelUnit.Feet)] // sopra: è un'altitudine, e nessuno la scrive «3000 ft» di fretta
    public void Parse_decides_the_unit_of_a_bare_number_by_magnitude(string text, int value, LevelUnit unit)
    {
        var p = LevelFormatting.Parse(text);
        Assert.Equal(value, p.Value);
        Assert.Equal(unit, p.Unit);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("—")]
    public void Parse_reads_an_empty_cell_as_no_level(string text)
    {
        var p = LevelFormatting.Parse(text);
        Assert.Null(p.Value);
        Assert.Null(p.Special);
        Assert.NotEqual(LevelConstraint.Special, p.Constraint);
    }

    [Theory]
    [InlineData("per aerovia")]
    [InlineData("come da lettera")]
    [InlineData("FL100/FL200-")]   // finisce con «-» ma NON è un vincolo: il segno fa parte della frase
    public void Parse_keeps_free_text_as_a_special_level(string text)
    {
        var p = LevelFormatting.Parse(text);
        Assert.Equal(LevelConstraint.Special, p.Constraint);
        Assert.Equal(text, p.Special);
        Assert.Null(p.Value);
    }

    [Fact]
    public void Parse_reads_arrow_and_parity_together()
    {
        var p = LevelFormatting.Parse("FL280+ ↑ (dispari)");
        Assert.Equal(280, p.Value);
        Assert.Equal(LevelConstraint.AtOrAbove, p.Constraint);
        Assert.Equal(TransferVerticalState.Climbing, p.VerticalState);
        Assert.Equal(LevelParity.Odd, p.Parity);
    }

    [Fact]
    public void Level_state_is_invisible_in_the_text_and_cannot_survive_a_round_trip()
    {
        // Documenta il LIMITE, non un difetto: «in volo livellato» non lascia segno nel testo (nessuna freccia),
        // quindi rileggendolo torna Unspecified. È la ragione per cui chi salva una cella deve conservare lo
        // stato verticale che la cella non mostra — una casella non può cambiare ciò che non fa vedere.
        var text = LevelFormatting.Format(130, LevelUnit.Fl, LevelConstraint.Exact, null,
            LevelParity.Any, TransferVerticalState.Level);
        Assert.Equal("FL130", text);
        Assert.Equal(TransferVerticalState.Unspecified, LevelFormatting.Parse(text).VerticalState);
    }
}
