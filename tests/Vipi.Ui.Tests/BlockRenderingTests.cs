using Bunit;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Components;
using Vipi.Ui.Components.Blocks;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Rete bUnit sui componenti di rendering dei blocchi: cattura le regressioni Blazor "silenziose coi test verdi"
/// (dispatch del BlockRenderer sbagliato; attributo dinamico reso come letterale perché scritto senza @).
/// </summary>
public class BlockRenderingTests : TestContext
{
    private static BlockView Block(BlockFormat format, string? body = null, string? bodyJson = null,
        CalloutKind? callout = null) => new()
    {
        Id = 1,
        Format = format,
        State = RenderState.Expanded,
        Body = body,
        BodyJson = bodyJson,
        CalloutKind = callout,
    };

    [Fact]
    public void BlockRenderer_routes_prose_to_prose_block()
    {
        var cut = RenderComponent<BlockRenderer>(p => p.Add(x => x.Block, Block(BlockFormat.Prose, body: "ciao")));
        Assert.Contains("ciao", cut.Markup);
        Assert.DoesNotContain("callout", cut.Markup);   // non deve finire sul ramo sbagliato
    }

    [Fact]
    public void BlockRenderer_routes_tip_variant_to_tip_block()
    {
        var json = "{\"variant\":\"tip\",\"title\":\"Suggerimento\",\"lines\":[\"riga uno\"]}";
        var cut = RenderComponent<BlockRenderer>(p => p.Add(x => x.Block, Block(BlockFormat.Prose, bodyJson: json)));
        Assert.Contains("tip", cut.Markup);
        Assert.Contains("Suggerimento", cut.Markup);
        Assert.Contains("riga uno", cut.Markup);
    }

    [Fact]
    public void BlockRenderer_routes_callout_to_callout_block()
    {
        var cut = RenderComponent<BlockRenderer>(p => p.Add(x => x.Block,
            Block(BlockFormat.Callout, body: "occhio", callout: CalloutKind.Warning)));
        Assert.Contains("callout", cut.Markup);
        Assert.Contains("occhio", cut.Markup);
    }

    // Trappola nota (memoria dev-process-gates): un attributo dinamico scritto come class="Kind" invece di
    // class="@Kind" renderebbe la stringa letterale "Kind". Questo test morde se qualcuno rompe l'interpolazione.
    [Theory]
    [InlineData(CalloutKind.Danger, "danger", "octagon")]
    [InlineData(CalloutKind.Success, "success", "check-circle")]
    [InlineData(CalloutKind.Info, "info", "info")]
    public void CalloutBlock_renders_dynamic_kind_class_and_icon(CalloutKind kind, string cssClass, string icon)
    {
        var cut = RenderComponent<CalloutBlock>(p => p.Add(x => x.Block, Block(BlockFormat.Callout, callout: kind)));
        var callout = cut.Find("div.callout");
        Assert.Contains(cssClass, callout.ClassList);           // classe = valore dinamico, non "Kind" letterale
        Assert.Contains($"data-icon=\"{icon}\"", cut.Markup);   // icona SVG per tipo (Icon.razor, U2)
    }

    [Fact]
    public void CalloutBlock_html_encodes_body_no_xss()
    {
        var cut = RenderComponent<CalloutBlock>(p => p.Add(x => x.Block,
            Block(BlockFormat.Callout, body: "<script>alert(1)</script>", callout: CalloutKind.Info)));
        Assert.DoesNotContain("<script>", cut.Markup);          // MarkdownLite encoda
        Assert.Contains("&lt;script&gt;", cut.Markup);
    }
}
