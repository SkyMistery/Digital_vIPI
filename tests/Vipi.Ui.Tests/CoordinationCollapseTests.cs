using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Components;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Stato iniziale dei coordinamenti: all'apertura del documento è espanso il SOLO livello di primo grado
/// (il settore nella vIPI ACC/vLOA, il gruppo nell'APP); tutto ciò che sta dentro nasce compresso. Sono
/// `open` scritti a mano nel markup, quindi una regressione qui non la vedrebbe nessun altro test.
/// </summary>
public class CoordinationCollapseTests : TestContext
{
    /// <summary>Localizer che rende la chiave stessa (stesso stratagemma di PrintMetaTests).</summary>
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public CoordinationCollapseTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
    }

    private static AppCoordRow Row(string cop) => new(cop, "FL200", "LIRR_CTR", TransferFlowKind.Arrival);

    private static AccCoordination AccTree() => new()
    {
        Sectors = new[]
        {
            new AccSectorApps("ES", new[]
            {
                new AccAccAirports("Roma",
                    new[] { new AccAirportFlows("LIRF", new[] { Row("ELKAP") }, Array.Empty<AppCoordRow>()) },
                    Array.Empty<AccExtraFlows>()),
            }),
        },
    };

    [Fact]
    public void Acc_coordination_opens_only_the_sector_level()
    {
        var cut = RenderComponent<AccCoordinationView>(p => p.Add(x => x.Coord, AccTree()));

        // Il settore è aperto…
        Assert.Contains("<details class=\"coord-sub\" open", cut.Markup);
        // …e nessun livello interno lo è.
        Assert.Contains("coord-sub2", cut.Markup);
        Assert.DoesNotContain("<details class=\"coord-sub2\" open", cut.Markup);
    }

    [Fact]
    public void App_coordination_opens_only_the_group_level()
    {
        var coord = new AppCoordination
        {
            TowardAcc = new[] { new AppCoordGroup("LIRR_CTR", new[] { Row("ELKAP") }) },
            TowardTowers = Array.Empty<AppCoordGroup>(),
        };

        var cut = RenderComponent<AppCoordinationView>(p => p.Add(x => x.Coord, coord));

        Assert.Contains("<details class=\"coord-sub\" open", cut.Markup);
        Assert.DoesNotContain("<details class=\"coord-sub2\" open", cut.Markup);
    }

    private static SectionView Section(string key, string title) => new()
    {
        Id = "s-1", Title = title, Depth = 0, SectionKey = key,
        Blocks = Array.Empty<BlockView>(), Children = Array.Empty<SectionView>(),
    };

    [Fact]
    public void Regulated_section_renders_collapsed_others_open()
    {
        // doc 11 §3i: la card «Aree regolamentate» nasce chiusa; le altre no. È l'attributo `open` del <details>,
        // che nessun altro test guarda.
        var regolamentate = RenderComponent<SectionNode>(p => p.Add(x => x.Section, Section("regulated", "Aree regolamentate")));
        var aor = RenderComponent<SectionNode>(p => p.Add(x => x.Section, Section("aor", "AOR")));

        Assert.DoesNotContain("open", regolamentate.Find("details.block").OuterHtml.Split('>')[0]);
        Assert.Contains("open", aor.Find("details.block").OuterHtml.Split('>')[0]);
    }
}
