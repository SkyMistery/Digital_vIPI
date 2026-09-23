using System.Collections.Generic;
using Vipi.Application.Content;
using Vipi.Application.Translation;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// S4 (23 settembre 2026): le larghezze delle colonne di una tabella generica, scelte da chi scrive. Stanno nel
/// JSON del blocco (<c>widths</c>), una per colonna, in percento; <c>null</c> = automatica.
/// </summary>
public class TabellaGenericaLarghezzeTests
{
    private static readonly string[] DueColonne = { "A", "B" };
    private static readonly IReadOnlyList<IReadOnlyList<string>> UnaRiga = new[] { new[] { "1", "2" } };

    [Fact]
    public void Senza_larghezze_il_json_resta_quello_di_prima()
    {
        var json = TabellaGenerica.Scrivi(DueColonne, UnaRiga, new int?[] { null, null });

        Assert.Equal("""{"columns":["A","B"],"rows":[{"cells":["1","2"]}]}""", json);
        Assert.Equal(new int?[] { null, null }, TabellaGenerica.Larghezze(json));
    }

    [Fact]
    public void Le_larghezze_fanno_il_giro_e_colonne_e_righe_restano()
    {
        var json = TabellaGenerica.Scrivi(DueColonne, UnaRiga, new int?[] { 30, null });

        Assert.Equal(new int?[] { 30, null }, TabellaGenerica.Larghezze(json));
        var (colonne, righe) = TabellaGenerica.Leggi(json);
        Assert.Equal(DueColonne, colonne);
        Assert.Equal(new[] { "1", "2" }, righe[0]);
    }

    /// <summary>Una per colonna, sempre: una colonna aggiunta nasce automatica, una tolta si porta via la sua.</summary>
    [Fact]
    public void Si_allineano_alle_colonne()
    {
        Assert.Equal(new int?[] { 40, null, null },
            TabellaGenerica.Larghezze(TabellaGenerica.Scrivi(new[] { "A", "B", "C" }, UnaRiga, new int?[] { 40 })));
        Assert.Equal(new int?[] { 40 },
            TabellaGenerica.Larghezze("""{"columns":["A"],"widths":[40,60,70],"rows":[]}"""));
    }

    [Theory]
    [InlineData("""{"columns":["A","B"],"widths":[0,101],"rows":[]}""")]
    [InlineData("""{"columns":["A","B"],"widths":["30",true],"rows":[]}""")]
    [InlineData("""{"columns":["A","B"],"widths":{"a":1},"rows":[]}""")]
    [InlineData("""{"columns":["A","B"],"widths":[12.5,null],"rows":[]}""")]
    public void Un_valore_che_non_va_torna_automatico(string json) =>
        Assert.Equal(new int?[] { null, null }, TabellaGenerica.Larghezze(json));

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("  ", null)]
    [InlineData("abc", null)]
    [InlineData("0", null)]
    [InlineData("150", null)]
    [InlineData("25", 25)]
    [InlineData(" 25 % ", 25)]
    [InlineData("100", 100)]
    public void Quel_che_si_scrive_nel_campo(string? testo, int? atteso) =>
        Assert.Equal(atteso, TabellaGenerica.LarghezzaDa(testo));

    [Fact]
    public void Json_rotto_o_senza_colonne_non_ha_larghezze()
    {
        Assert.Empty(TabellaGenerica.Larghezze(null));
        Assert.Empty(TabellaGenerica.Larghezze("{rotto"));
        Assert.Empty(TabellaGenerica.Larghezze("""[{"widths":[10]}]"""));
    }

    /// <summary>⚠️ La traduzione riscrive il JSON: le larghezze sono numeri, e devono restare dove sono.</summary>
    [Fact]
    public void La_traduzione_non_tocca_le_larghezze()
    {
        var json = TabellaGenerica.Scrivi(DueColonne, UnaRiga, new int?[] { 30, 70 });

        var tradotto = TextSegmenter.MapJson(json, s => s + "!");

        Assert.Equal(new int?[] { 30, 70 }, TabellaGenerica.Larghezze(tradotto));
        Assert.Equal(new[] { "A!", "B!" }, TabellaGenerica.Leggi(tradotto).Colonne);
    }
}
