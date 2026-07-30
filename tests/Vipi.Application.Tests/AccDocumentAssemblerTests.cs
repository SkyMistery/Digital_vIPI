using System.Text.Json;
using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>Caratterizzazione dell'assembler ACC (doc refactor 08e-acc): dall'albero DocumentSection ricostruisce i
/// blocchi con i campi che guidano le derivazioni (kind/membri/config/override/aree).</summary>
public class AccDocumentAssemblerTests
{
    private static EditableBlock Table(string? json) => new()
    {
        Id = 0, Order = 1, Format = BlockFormat.Table, Tier = BlockTier.Extended,
        Visibility = BlockVisibility.Always, BodyJson = json,
    };

    private static EditableSection Prose(int id, string key, string title, int order, string body) => new()
    {
        Id = id, Title = title, SectionKey = key, Depth = 1, Order = order,
        Blocks = new List<EditableBlock>
        {
            new() { Id = 0, Order = 1, Format = BlockFormat.Prose, Tier = BlockTier.Extended,
                    Visibility = BlockVisibility.Always, Body = body },
        },
        Children = new List<EditableSection>(),
    };

    private static EditableSection Sec(int id, string key, string title, int order,
        string? ownJson = null, IReadOnlyList<EditableSection>? children = null) => new()
    {
        Id = id, Title = title, SectionKey = key, Depth = children is null ? 1 : 0, Order = order,
        Blocks = ownJson is null ? new List<EditableBlock>() : new List<EditableBlock> { Table(ownJson) },
        Children = children ?? new List<EditableSection>(),
    };

    private static EditableDocument Doc(params EditableSection[] roots) => new()
    {
        DocumentId = 1, VersionId = 1, VersionNumber = 1, VersionStatus = DocumentStatus.Draft,
        Title = "vIPI ACC", Sections = roots,
    };

    [Fact]
    public void Assembles_Aerovia_And_AppGroup_With_Meta_Config_And_Regulated()
    {
        var meta = new AccBlockMeta
        {
            Key = "grp:abc", Kind = AccBlockKind.AppGroup,
            MemberCallsigns = new() { "LIRP_APP" },
            HiddenSections = new() { "vfr" },
            FreqOrder = new() { new AppFreqOrderOverride("LIRP_APP", 0) },
            FreqLinkCallsigns = new() { "LIRR_CTR" },
        };
        var configs = new List<AccConfiguration>
        {
            new() { Key = "cfg:1", Name = "Conf 1", Open = new() { new AccConfigOpen { Callsign = "LIRR_NE_CTR" } } },
        };
        var attached = new List<string> { "area-42" };

        var doc = Doc(
            // Blocco Aerovia: nessun blockmeta (default), con figlie keyed.
            Sec(10, "aerovia", "Settori di aerovia", 1, ownJson: null, children: new[]
            {
                Sec(11, "separations", "Separazioni radar", 1,
                    ownJson: JsonSerializer.Serialize(new[] { new AppSeparationRow("1000 ft", "5 NM") })),
                Sec(12, "configurations", "Configurazioni", 2, ownJson: JsonSerializer.Serialize(configs)),
                Sec(13, "aor", "AOR", 3),
                Sec(14, "regulated", "Aree", 4, ownJson: JsonSerializer.Serialize(attached)),
                Sec(15, "vfr", "VFR", 5, ownJson: JsonSerializer.Serialize(new AppVfrContent("intro", new List<AppVfrRow>()))),
                Prose(16, "operationaltechnique", "Procedure generali", 6, "testo procedura"),
            }),
            // Blocco gruppo APP: blockmeta valorizzato.
            Sec(20, "appgroup", "Gruppo APP", 2, ownJson: JsonSerializer.Serialize(meta), children: new[]
            {
                Sec(21, "frequencies", "Frequenze", 1),
            }));

        var blocks = AccDocumentAssembler.Assemble(doc);
        Assert.Equal(2, blocks.Count);

        // Aerovia: kind da chiave sezione (no meta); config e aree dai BodyJson figli.
        var aerovia = blocks[0];
        Assert.Equal(10, aerovia.BlockSectionId);
        Assert.Equal(AccBlockKind.Aerovia, aerovia.Block.Kind);
        Assert.Equal("aerovia", aerovia.Block.Key);
        Assert.Single(aerovia.Block.Configurations);
        Assert.Equal("Conf 1", aerovia.Block.Configurations[0].Name);
        // regulated array legacy ["area-42"] → manuale (OwnAuto=false) con quegli id (back-compat).
        Assert.False(aerovia.Block.Regulated.OwnAuto);
        Assert.Equal(new[] { "area-42" }, aerovia.Block.Regulated.OwnIds);
        Assert.Empty(aerovia.Block.Regulated.ExtraIds);
        // Ordine del documento, poi le sezioni-catalogo mancanti accodate al loro posto (doc 11 §3b).
        Assert.Equal(
            new[] { "separations", "configurations", "aor", "regulated", "vfr", "operationaltechnique", "frequencies", "minima", "coordination", "validity" },
            aerovia.Block.Sections.Select(s => s.Key).ToArray());
        Assert.Equal(12, aerovia.ChildSectionIdsByKey["configurations"]);
        Assert.Equal(14, aerovia.ChildSectionIdsByKey["regulated"]);
        Assert.Equal("1000 ft", Assert.Single(aerovia.Block.Separations).Vertical);
        Assert.Contains("intro", aerovia.Block.VfrJson);

        // Editoriale generico: la figlia porta la vista di resa condivisa (blocchi veri, non prosa appiattita).
        var custom = Assert.Single(aerovia.Block.Sections, s => s.Key == "operationaltechnique");
        Assert.Equal(16, custom.SectionId);
        Assert.Equal("Procedure generali", custom.Title);
        Assert.Equal("testo procedura", Assert.Single(custom.Editorial!.Blocks).Body);

        // Le sezioni-catalogo assenti dal documento sono accodate senza vista editoriale (nessuna sezione reale).
        Assert.Null(Assert.Single(aerovia.Block.Sections, s => s.Key == "minima").Editorial);

        // Gruppo APP: tutti i campi dal blockmeta.
        var grp = blocks[1];
        Assert.Equal(AccBlockKind.AppGroup, grp.Block.Kind);
        Assert.Equal("grp:abc", grp.Block.Key);
        Assert.Equal(new[] { "LIRP_APP" }, grp.Block.MemberCallsigns);
        Assert.Equal(new[] { "vfr" }, grp.Block.HiddenSections);
        Assert.Equal("LIRR_CTR", Assert.Single(grp.Block.FreqLinkCallsigns));
        Assert.Equal(0, Assert.Single(grp.Block.FreqOrder).Order);
    }

    [Fact]
    public void Empty_Block_Defaults_To_Empty_Collections()
    {
        var doc = Doc(Sec(1, "aerovia", "Aerovia", 1, ownJson: null, children: new EditableSection[0]));
        var block = Assert.Single(AccDocumentAssembler.Assemble(doc)).Block;

        Assert.Equal(AccBlockKind.Aerovia, block.Kind);
        Assert.Empty(block.MemberCallsigns);
        Assert.Empty(block.Configurations);
        // Nessuna figlia regulated → aree del proprio ACC in automatico (default dinamico), nessuna esplicita.
        Assert.True(block.Regulated.OwnAuto);
        Assert.Empty(block.Regulated.OwnIds);
        Assert.Empty(block.Regulated.ExtraIds);
        Assert.Empty(block.ExtraAorCallsigns);
        // Nessuna figlia nel documento ⇒ solo le sezioni-catalogo del profilo, tutte senza vista editoriale.
        Assert.Equal(SectionCatalog.For(SectionProfile.AccAerovia).Count, block.Sections.Count);
        Assert.All(block.Sections, s => Assert.Null(s.Editorial));
    }

    [Fact]
    public void ExtraAorCallsigns_And_Colors_Read_From_Aor_Section_BodyJson()
    {
        var extras = new AorExtraShapes
        {
            Callsigns = new() { "LGKR_APP", "LIRP_TWR" },
            Colors = new() { ["LIRP_TWR"] = "#b0413e" },
        };
        var doc = Doc(Sec(1, "aerovia", "Aerovia", 1, ownJson: null, children: new[]
        {
            Sec(2, "aor", "AOR", 1, ownJson: JsonSerializer.Serialize(extras)),
        }));
        var block = Assert.Single(AccDocumentAssembler.Assemble(doc)).Block;

        Assert.Equal(new[] { "LGKR_APP", "LIRP_TWR" }, block.ExtraAorCallsigns);
        Assert.Equal("#b0413e", block.AorColorOverrides["LIRP_TWR"]);
    }

    [Fact]
    public void Frozen_AccAorView_In_Aor_Section_Does_Not_Leak_Into_ExtraAorCallsigns()
    {
        // Negli snapshot frozen il BodyJson di "aor" contiene l'AccAorView renderizzato, non AorExtraShapes:
        // la deserializzazione tollerante deve produrre una lista vuota (nessun campo "callsigns").
        var frozen = JsonSerializer.Serialize(AccAorView.Empty);
        var doc = Doc(Sec(1, "aerovia", "Aerovia", 1, ownJson: null, children: new[]
        {
            Sec(2, "aor", "AOR", 1, ownJson: frozen),
        }));
        var block = Assert.Single(AccDocumentAssembler.Assemble(doc)).Block;

        Assert.Empty(block.ExtraAorCallsigns);
    }

    [Fact]
    public void Regulated_Object_Schema_RoundTrips()
    {
        var sel = new RegulatedSelection { OwnAuto = false, OwnIds = new() { "a1" }, ExtraIds = new() { "x9" } };
        var doc = Doc(Sec(1, "aerovia", "Aerovia", 1, ownJson: null, children: new[]
        {
            Sec(2, "regulated", "Aree", 1, ownJson: JsonSerializer.Serialize(sel)),
        }));
        var block = Assert.Single(AccDocumentAssembler.Assemble(doc)).Block;

        Assert.False(block.Regulated.OwnAuto);
        Assert.Equal(new[] { "a1" }, block.Regulated.OwnIds);
        Assert.Equal(new[] { "x9" }, block.Regulated.ExtraIds);
    }
}
