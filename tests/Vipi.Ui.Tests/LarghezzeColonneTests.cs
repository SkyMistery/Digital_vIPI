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

    /// <summary>Nell'editor il colgroup c'e' sempre, anche senza larghezze: la maniglia allarga il suo `<col>`.
    /// Senza stile e senza `tab-larg`, cioe' la tabella non cambia aspetto.</summary>
    [Fact]
    public void Sempre_da_il_colgroup_anche_senza_larghezze()
    {
        var cut = RenderComponent<ColonneTabella>(p => p
            .Add(x => x.Larghezze, new int?[] { null, null })
            .Add(x => x.Sempre, true)
            .Add(x => x.Coda, "width:70px"));

        var cols = cut.FindAll("col").ToList();
        Assert.Equal(3, cols.Count);
        Assert.Null(cols[0].GetAttribute("style"));
        Assert.Equal("width:70px", cols[2].GetAttribute("style"));
    }

    /// <summary>
    /// ⚠️ La maniglia vive di tre pezzi in tre file: la `<th>` con `.col-grip` e il campo «%» nei DUE editor, il
    /// colgroup `Sempre`, e il gestore in vipi-editor.js. Se uno cade, la maniglia c'e' e non fa niente — senza
    /// errori. bUnit non esegue il JS: si presidia il sorgente.
    /// </summary>
    [Theory]
    [InlineData("Components/DocumentSectionsEditor.razor")]
    [InlineData("Components/DocumentBlocksEditor.razor")]
    public void Gli_editor_hanno_la_maniglia(string file)
    {
        var sorgente = Leggi(file);
        Assert.Contains("<span class=\"col-grip\"", sorgente, StringComparison.Ordinal);
        Assert.Contains("Sempre=\"true\"", sorgente, StringComparison.Ordinal);
        Assert.Contains("type=\"number\"", sorgente, StringComparison.Ordinal);
        Assert.Contains("<table class=\"cfg-table tab-larg\">", sorgente, StringComparison.Ordinal);
    }

    [Fact]
    public void Il_gestore_della_maniglia_c_e()
    {
        var js = Leggi("wwwroot/vipi-editor.js");
        Assert.Contains("closest('.col-grip')", js, StringComparison.Ordinal);
        Assert.Contains(":scope > colgroup > col", js, StringComparison.Ordinal);
        Assert.Contains("input[type=number]", js, StringComparison.Ordinal);
    }

    private static string Leggi(string relativo) =>
        File.ReadAllText(Path.Combine(Radice(), relativo.Replace('/', Path.DirectorySeparatorChar)));

    private static string Radice()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var c = Path.Combine(dir.FullName, "src", "Vipi.Ui");
            if (Directory.Exists(Path.Combine(c, "Pages"))) return c;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException($"src/Vipi.Ui non trovata risalendo da {AppContext.BaseDirectory}");
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
