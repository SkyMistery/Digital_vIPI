using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Picker delle aree regolamentate. Un ACC ne ha decine e l'insieme «altri ACC» centinaia: quel che conta è che
/// l'elenco tagliato dica quanto sta nascondendo, che si possa restringere per ente, e che un'area selezionata ma
/// non più esistente si veda invece di comparire come un id nudo.
/// </summary>
public class RegulatedAreasEditorTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + string.Concat(arguments.Select(a => " " + a)), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public RegulatedAreasEditorTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
    }

    private static IReadOnlyList<SpecialAreaPick> Areas(string acc, int count, string prefix) =>
        Enumerable.Range(1, count)
            .Select(i => new SpecialAreaPick($"{acc}-{i}", $"{prefix} {i:000}", "R", 0, 5000, new[] { acc }))
            .ToList();

    [Fact]
    public void Own_list_says_how_many_it_is_hiding()
    {
        var cut = RenderComponent<RegulatedAreasEditor>(p => p
            .Add(x => x.Selection, new RegulatedSelection { OwnAuto = false })
            .Add(x => x.OwnPicks, Areas("LIRR", 99, "LI R"))
            .Add(x => x.Editing, true)
            .Add(x => x.AllowAuto, false)
            .Add(x => x.ShowExtra, false));

        // 20 mostrate su 99: il conteggio è l'unica cosa che distingue «tagliato» da «non c'è».
        Assert.Contains("Acc_AreaCountTruncated 20 99", cut.Markup);
    }

    [Fact]
    public void Other_acc_list_appears_when_an_acc_is_picked_without_typing()
    {
        var picks = Areas("LIMM", 5, "LOMBARDIA").Concat(Areas("LIBB", 3, "PUGLIA")).ToList();
        var cut = RenderComponent<RegulatedAreasEditor>(p => p
            .Add(x => x.Selection, new RegulatedSelection { OwnAuto = false })
            .Add(x => x.OtherPicks, picks)
            .Add(x => x.Editing, true)
            .Add(x => x.AllowAuto, false)
            .Add(x => x.ShowExtra, true));

        // Prima di scegliere: nessun elenco (centinaia di voci non servono a nessuno).
        Assert.DoesNotContain("LOMBARDIA 001", cut.Markup);

        cut.Find("select").Change("LIMM");

        Assert.Contains("LOMBARDIA 001", cut.Markup);
        Assert.DoesNotContain("PUGLIA 001", cut.Markup);   // filtro per ente, non solo ricerca testuale
        Assert.Contains("Acc_AreaCount 5", cut.Markup);

        cut.Find("select").Change("LIBB");
        // Il campo delle aree extra (col localizzatore di test il placeholder è la chiave stessa).
        cut.Find("input[placeholder='Acc_SearchOtherArea']").Input("PUGLIA 001");
        Assert.Contains("Acc_AreaCountOne", cut.Markup);
        Assert.DoesNotContain("Acc_AreaCount 1", cut.Markup);
    }

    [Fact]
    public void Selected_area_that_no_longer_exists_is_marked()
    {
        var cut = RenderComponent<RegulatedAreasEditor>(p => p
            .Add(x => x.Selection, new RegulatedSelection { OwnAuto = false, OwnIds = { "8963" } })
            .Add(x => x.OwnPicks, Areas("LIRR", 2, "LI R"))   // 8963 non c'è più: potata da un import
            .Add(x => x.Editing, true)
            .Add(x => x.AllowAuto, false)
            .Add(x => x.ShowExtra, false));

        Assert.Contains("Acc_AreaGone", cut.Markup);
        Assert.Contains("8963", cut.Markup);
    }
}
