using Bunit;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Components.Blocks;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// S4 (23 settembre 2026): la tabella pubblicata porta le larghezze scelte da chi scrive, in un
/// <c>&lt;colgroup&gt;</c>, e la classe che toglie alle colonne automatiche le percentuali di <c>cfg-table</c>.
/// </summary>
public class LarghezzeColonneTests : TestContext
{
    private IRenderedComponent<TableBlock> Tabella(string json) =>
        RenderComponent<TableBlock>(p => p.Add(x => x.Block, new BlockView
        {
            Id = 1, Format = BlockFormat.Table, State = RenderState.Expanded, BodyJson = json,
        }));

    [Fact]
    public void Con_le_larghezze_esce_il_colgroup()
    {
        var cut = Tabella(TabellaGenerica.Scrivi(new[] { "A", "B", "C" },
            new[] { new[] { "1", "2", "3" } }, new int?[] { 20, null, 50 }));

        var table = cut.Find("table");
        Assert.Contains("tab-larg", table.ClassList);
        Assert.Contains("cfg-table", table.ClassList);
        var cols = cut.FindAll("colgroup > col").ToList();
        Assert.Equal(3, cols.Count);
        Assert.Equal("width:20%", cols[0].GetAttribute("style"));
        Assert.Null(cols[1].GetAttribute("style"));
        Assert.Equal("width:50%", cols[2].GetAttribute("style"));
        // ⚠️ Il colgroup sta PRIMA dell'intestazione: dopo, il browser non lo considera.
        Assert.Equal("COLGROUP", table.FirstElementChild!.TagName);
    }

    /// <summary>Senza larghezze la tabella resta esattamente quella di prima.</summary>
    [Fact]
    public void Senza_larghezze_niente_colgroup_e_niente_classe()
    {
        var cut = Tabella("""{"columns":["A","B"],"rows":[{"cells":["1","2"]}]}""");

        Assert.Empty(cut.FindAll("colgroup"));
        Assert.DoesNotContain("tab-larg", cut.Find("table").ClassList);
    }

    /// <summary>L'avviso nell'editor: solo quando la pagina non potra' rispettare le larghezze alla lettera.</summary>
    [Theory]
    [InlineData(new[] { 0, 0 }, null)]          // tutte automatiche
    [InlineData(new[] { 25, 0 }, null)]         // una automatica prende il resto
    [InlineData(new[] { 40, 60 }, null)]        // tutte scritte, fanno 100
    [InlineData(new[] { 25, 30 }, 55)]          // tutte scritte, avanzano: si allargano
    [InlineData(new[] { 60, 70 }, 130)]         // tutte scritte, troppe: si stringono
    [InlineData(new[] { 60, 50, 0 }, 110)]      // l'automatica resterebbe senza spazio
    public void La_somma_che_non_torna(int[] valori, int? attesa)
    {
        var larghezze = valori.Select(v => v == 0 ? (int?)null : v).ToList();
        Assert.Equal(attesa, ColonneTabella.SommaCheNonTorna(larghezze));
    }

    /// <summary>⚠️ Nella tabella unificata la cella di gruppo e' una colonna in PIU' di <c>columns</c>: le
    /// larghezze finirebbero spostate di uno, quindi non si applicano.</summary>
    [Fact]
    public void Nella_tabella_unificata_le_larghezze_non_si_applicano()
    {
        var cut = Tabella("""{"columns":["G","A"],"widths":[30,70],"unified":true,"rows":[{"group":"X","cells":["1"]}]}""");

        Assert.Empty(cut.FindAll("colgroup"));
    }
}
