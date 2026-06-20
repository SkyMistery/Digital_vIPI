using Vipi.Application.Aor;
using Vipi.Application.Content;
using Vipi.Domain;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>Tabella di verità della visibilità (SPEC_Logica_AoR §4) + scenario canonico S3 (WS2↔ANE).</summary>
public class ContentServiceTests
{
    private readonly ContentService _sut = new();

    private static AorResult Aor(string sector, SectorState state) => new()
    {
        Ownership = new Dictionary<string, string> { [sector] = "x" },
        State = new Dictionary<string, SectorState>(StringComparer.OrdinalIgnoreCase) { [sector] = state },
    };

    private static BlockInput Op(string sector) => new(1, BlockVisibility.Operational, sector, BlockTier.Extended);
    private static BlockInput Ho(string sector) => new(2, BlockVisibility.Handoff, sector, BlockTier.Extended);

    [Fact] // S3-A — ANE offline (Covered): operativo espanso, handoff compresso
    public void S3_AneOffline_OperationalExpanded_HandoffCollapsed()
    {
        var aor = Aor("ANE", SectorState.Covered);

        var op = _sut.BuildView(new[] { Op("ANE") }, aor, BlockTier.Extended, live: true)[0];
        var ho = _sut.BuildView(new[] { Ho("ANE") }, aor, BlockTier.Extended, live: true)[0];

        Assert.Equal(RenderState.Expanded, op.State);
        Assert.Equal(RenderState.Collapsed, ho.State);
    }

    [Fact] // S3-B — ANE online: operativo compresso, handoff espanso (inversione)
    public void S3_AneOnline_OperationalCollapsed_HandoffExpanded()
    {
        var aor = Aor("ANE", SectorState.Online);

        var op = _sut.BuildView(new[] { Op("ANE") }, aor, BlockTier.Extended, live: true)[0];
        var ho = _sut.BuildView(new[] { Ho("ANE") }, aor, BlockTier.Extended, live: true)[0];

        Assert.Equal(RenderState.Collapsed, op.State);
        Assert.Equal(RenderState.Expanded, ho.State);
    }

    [Fact] // S8 — Live OFF: tutto espanso a prescindere da O
    public void S8_LiveOff_AllExpanded()
    {
        var aor = Aor("ANE", SectorState.Online);

        var view = _sut.BuildView(new[] { Op("ANE"), Ho("ANE") }, aor, BlockTier.Extended, live: false);

        Assert.All(view, v => Assert.Equal(RenderState.Expanded, v.State));
    }

    [Fact] // S9 — Always sempre espanso
    public void S9_Always_AlwaysExpanded()
    {
        var aor = Aor("ANE", SectorState.Online);
        var always = new BlockInput(3, BlockVisibility.Always, "ANE", BlockTier.Extended);

        var v = _sut.BuildView(new[] { always }, aor, BlockTier.Extended, live: true)[0];

        Assert.Equal(RenderState.Expanded, v.State);
    }

    [Theory] // Invariante: esclusività operativo/handoff — mai entrambi espansi né entrambi compressi
    [InlineData(SectorState.Covered)]
    [InlineData(SectorState.Online)]
    public void Invariant_OperationalHandoffExclusivity(SectorState state)
    {
        var aor = Aor("ANE", state);

        var op = _sut.BuildView(new[] { Op("ANE") }, aor, BlockTier.Extended, live: true)[0];
        var ho = _sut.BuildView(new[] { Ho("ANE") }, aor, BlockTier.Extended, live: true)[0];

        Assert.NotEqual(op.State, ho.State);
    }

    [Fact] // Tier Ridotta: i blocchi Extended sono filtrati via
    public void ReducedTier_FiltersExtendedBlocks()
    {
        var aor = Aor("ANE", SectorState.Covered);
        var reduced = new BlockInput(4, BlockVisibility.Always, null, BlockTier.Reduced);
        var extended = new BlockInput(5, BlockVisibility.Always, null, BlockTier.Extended);

        var view = _sut.BuildView(new[] { reduced, extended }, aor, BlockTier.Reduced, live: false);

        Assert.Single(view);
        Assert.Equal(4, view[0].BlockId);
    }
}
