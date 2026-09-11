using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Le colonne a chip della tabella piste (11 settembre 2026). Il campo salvato resta la stringa del vecchio testo
/// libero, quindi la prova che conta è quella sui <b>valori vecchi</b>: i documenti già scritti portano «RNAV»,
/// «N», una tabulazione in testa — misurati sul <c>vipi.db</c> di sviluppo, LIBD e LIBR — e nessun clic su un
/// altro chip deve farli sparire.
/// </summary>
public class RunwayChoicesTests
{
    private static readonly IReadOnlyList<string> App = RunwayChoices.App;

    [Fact]
    public void Un_valore_vecchio_si_divide_in_voci_note_e_fuori_elenco()
    {
        var p = RunwayChoices.Parse("\tILS, VOR, RNAV", App);

        Assert.Equal(new[] { "ILS", "VOR" }, p.Known);
        Assert.Equal(new[] { "RNAV" }, p.Legacy);
    }

    /// <summary>⚠️ «L JET» è UNA voce: non si divide sugli spazi. E maiuscole e spazi doppi non contano.</summary>
    [Fact]
    public void L_JET_e_una_voce_sola()
    {
        var p = RunwayChoices.Parse("l  jet, R", RunwayChoices.Patterns);

        Assert.Equal(new[] { "L JET", "R" }, p.Known);
        Assert.Empty(p.Legacy);
    }

    /// <summary>⚠️ Il caso che giustifica tutto: accendere ILS non butta l'RNAV scritto a mano.</summary>
    [Fact]
    public void Accendere_una_voce_non_butta_quelle_fuori_elenco()
    {
        Assert.Equal("ILS, VOR, RNAV", RunwayChoices.Toggle("VOR, RNAV", App, "ILS"));
    }

    [Fact]
    public void Le_voci_si_scrivono_nell_ordine_dell_elenco_non_del_clic()
    {
        var v = RunwayChoices.Toggle("SRA", App, "VOR");
        v = RunwayChoices.Toggle(v, App, "ILS");

        Assert.Equal("ILS, VOR, SRA", v);
    }

    [Fact]
    public void Spegnere_l_ultima_voce_lascia_il_campo_vuoto()
    {
        Assert.Null(RunwayChoices.Toggle("L", RunwayChoices.Circling, "L"));
    }

    /// <summary>Togliere la voce vecchia è l'unico modo in cui lascia il campo, e tocca solo lei.</summary>
    [Fact]
    public void La_voce_fuori_elenco_si_toglie_da_sola()
    {
        Assert.Equal("ILS, VOR", RunwayChoices.RemoveLegacy("ILS, VOR, RNAV", App, "rnav"));
        Assert.Null(RunwayChoices.RemoveLegacy("N", RunwayChoices.Circling, "N"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" , ; ")]
    public void Un_campo_vuoto_non_ha_voci(string? v)
    {
        var p = RunwayChoices.Parse(v, App);

        Assert.Empty(p.Known);
        Assert.Empty(p.Legacy);
    }
}
