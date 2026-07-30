using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Fedeltà del contenuto editoriale della vIPI ACC (doc 11 §3b). Prima l'assembler appiattiva le sezioni libere a
/// sola prosa: tabelle e callout perdevano formato/dati e le sotto-sezioni sparivano; due sezioni libere collidevano
/// sulla chiave "custom" e ne restava una sola.
/// </summary>
public class AccEditorialFidelityTests
{
    private static EditableBlock Block(int id, int order, BlockFormat format, string? body = null, string? json = null, CalloutKind? kind = null) =>
        new() { Id = id, Order = order, Format = format, Tier = BlockTier.Extended, Visibility = BlockVisibility.Always, Body = body, BodyJson = json, CalloutKind = kind };

    private static EditableSection Sec(int id, string key, string title, int order, int depth = 1,
        IEnumerable<EditableBlock>? blocks = null, IEnumerable<EditableSection>? children = null,
        bool beforeParentBody = false) =>
        new()
        {
            Id = id, SectionKey = key, Title = title, Order = order, Depth = depth,
            BeforeParentBody = beforeParentBody,
            Blocks = (blocks ?? Array.Empty<EditableBlock>()).ToList(),
            Children = (children ?? Array.Empty<EditableSection>()).ToList(),
        };

    private static IReadOnlyList<AccAssembledBlock> AssembleAerovia(params EditableSection[] children) =>
        AccDocumentAssembler.Assemble(new List<EditableSection>
        {
            Sec(1, "aerovia", "Settori di aerovia", 1, depth: 0, children: children),
        });

    [Fact]
    public void Free_Section_Keeps_Table_Callout_And_Subsections()
    {
        var libera = Sec(10, "custom:aaaa1111", "Note operative", 1,
            blocks: new[]
            {
                Block(100, 1, BlockFormat.Table, json: """{"columns":["A"],"rows":[{"cells":["CELLA"]}]}"""),
                Block(101, 2, BlockFormat.Callout, body: "attenzione", kind: CalloutKind.Warning),
                Block(102, 3, BlockFormat.Prose, body: "testo"),
            },
            children: new[] { Sec(11, "custom:bbbb2222", "Dettaglio", 1, depth: 2, blocks: new[] { Block(103, 1, BlockFormat.Prose, body: "sotto") }) });

        var sezione = Assert.Single(AssembleAerovia(libera).Single().Block.Sections, s => s.SectionId == 10);
        var view = sezione.Editorial;
        Assert.NotNull(view);

        // Tre blocchi, con i formati originali (prima: solo i Prose non vuoti, tutti marcati prosa).
        Assert.Equal(new[] { BlockFormat.Table, BlockFormat.Callout, BlockFormat.Prose }, view.Blocks.Select(b => b.Format).ToArray());
        Assert.Contains("CELLA", view.Blocks[0].BodyJson);
        Assert.Equal(CalloutKind.Warning, view.Blocks[1].CalloutKind);

        // Sotto-sezione preservata col suo contenuto (prima veniva scartata).
        var sub = Assert.Single(view.Children);
        Assert.Equal("Dettaglio", sub.Title);
        Assert.Equal("sotto", Assert.Single(sub.Blocks).Body);
    }

    [Fact]
    public void Two_Free_Sections_Stay_Distinct()
    {
        var blocks = AssembleAerovia(
            Sec(10, "custom:aaaa1111", "Prima", 1, blocks: new[] { Block(100, 1, BlockFormat.Prose, body: "uno") }),
            Sec(20, "custom:bbbb2222", "Seconda", 2, blocks: new[] { Block(200, 1, BlockFormat.Prose, body: "due") }));

        var free = blocks.Single().Block.Sections.Where(s => s.SectionId is 10 or 20).ToList();

        Assert.Equal(2, free.Count);
        Assert.Equal(new[] { "Prima", "Seconda" }, free.Select(s => s.Title).ToArray());
        Assert.Equal(new[] { "uno", "due" }, free.Select(s => s.Editorial!.Blocks.Single().Body).ToArray());
    }

    [Fact]
    public void Structured_Section_Keeps_Its_Subsections()
    {
        // Le sotto-sezioni di una sezione derivata sono contenuto del documento: l'editor le sa creare, il viewer
        // le deve trovare (il corpo derivato lo produce la pagina).
        var separazioni = Sec(10, "separations", "Separazioni", 1,
            children: new[] { Sec(11, "custom:cccc3333", "Eccezioni", 1, depth: 2, blocks: new[] { Block(100, 1, BlockFormat.Prose, body: "nota") }) });

        var sezione = Assert.Single(AssembleAerovia(separazioni).Single().Block.Sections, s => s.SectionId == 10);

        var sub = Assert.Single(sezione.Editorial!.Children);
        Assert.Equal("Eccezioni", sub.Title);
        Assert.Equal("nota", Assert.Single(sub.Blocks).Body);
    }

    [Fact]
    public void Document_Order_Wins_Over_Catalog_Order()
    {
        var blocks = AssembleAerovia(
            Sec(10, "coordination", "Coordinamenti", 1),
            Sec(20, "custom:aaaa1111", "Libera", 2),
            Sec(30, "aor", "AOR", 3));

        var keys = blocks.Single().Block.Sections.Select(s => s.Key).ToList();

        Assert.Equal(new[] { "coordination", "custom:aaaa1111", "aor" }, keys.Take(3).ToArray());
    }

    [Fact]
    public void Subsection_Position_Relative_To_The_Body_Survives_Assembly()
    {
        // doc 11 §3g: la sotto-sezione può precedere il corpo della sezione (es. una premessa sopra le mappe
        // delle aree regolamentate). Il flag deve arrivare fino alla vista di resa.
        var regolamentate = Sec(10, "regulated", "Aree regolamentate", 1, children: new[]
        {
            Sec(11, "custom:aaaa1111", "Premessa", 1, depth: 2, beforeParentBody: true),
            Sec(12, "custom:bbbb2222", "Note finali", 2, depth: 2),
        });

        var sezione = Assert.Single(AssembleAerovia(regolamentate).Single().Block.Sections, s => s.SectionId == 10);
        var figlie = sezione.Editorial!.Children;

        Assert.True(figlie.Single(c => c.Title == "Premessa").BeforeParentBody);
        Assert.False(figlie.Single(c => c.Title == "Note finali").BeforeParentBody);
    }
}
