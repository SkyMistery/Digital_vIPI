using System.Collections.Generic;
using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

public class ExtraBlocksTests
{
    [Fact]
    public void Empty_Body_Yields_No_Blocks()
    {
        Assert.Empty(ExtraBlocks.Parse(null));
        Assert.Empty(ExtraBlocks.Parse("   "));
    }

    [Fact]
    public void Legacy_Markdown_Body_Becomes_Single_Prose_Block()
    {
        var blocks = ExtraBlocks.Parse("Testo **legacy** senza JSON.");
        var b = Assert.Single(blocks);
        Assert.Equal(BlockFormat.Prose, b.Format);
        Assert.Equal("Testo **legacy** senza JSON.", b.Text);
    }

    [Fact]
    public void RoundTrips_Prose_Callout_Table()
    {
        var blocks = new List<ExtraBlock>
        {
            new() { Format = BlockFormat.Prose, Text = "Intro" },
            new() { Format = BlockFormat.Callout, CalloutKind = CalloutKind.Warning, Text = "Attenzione" },
            new() { Format = BlockFormat.Table, TableJson = """{"columns":["A","B"],"rows":[{"cells":["1","2"]}]}""" },
        };

        var json = ExtraBlocks.Serialize(blocks);
        Assert.NotNull(json);

        var back = ExtraBlocks.Parse(json);
        Assert.Equal(3, back.Count);
        Assert.Equal(BlockFormat.Prose, back[0].Format);
        Assert.Equal("Intro", back[0].Text);
        Assert.Equal(BlockFormat.Callout, back[1].Format);
        Assert.Equal(CalloutKind.Warning, back[1].CalloutKind);
        Assert.Equal(BlockFormat.Table, back[2].Format);
        Assert.Contains("\"columns\"", back[2].TableJson);
    }

    [Fact]
    public void Serialize_Drops_Empty_Blocks_And_Returns_Null_When_All_Empty()
    {
        var blocks = new List<ExtraBlock>
        {
            new() { Format = BlockFormat.Prose, Text = "   " },
            new() { Format = BlockFormat.Table, TableJson = null },
        };
        Assert.Null(ExtraBlocks.Serialize(blocks));
    }

    [Fact]
    public void PlainText_Concatenates_Prose_And_Callout_Text_Only()
    {
        var json = ExtraBlocks.Serialize(new List<ExtraBlock>
        {
            new() { Format = BlockFormat.Prose, Text = "uno" },
            new() { Format = BlockFormat.Callout, Text = "due" },
            new() { Format = BlockFormat.Table, TableJson = """{"columns":["x"],"rows":[]}""" },
        });
        Assert.Equal("uno due", ExtraBlocks.PlainText(json));
    }
}
