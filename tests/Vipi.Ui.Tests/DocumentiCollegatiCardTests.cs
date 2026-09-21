using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui;
using Vipi.Ui.Components.Doc;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il riquadro «Documenti collegati» della colonna di destra (§A109): i link portano al DOCUMENTO, la vIPI ACC ha i
/// gruppi APP e Aeroporti chiusi, e senza link il riquadro non c'è.
/// </summary>
public class DocumentiCollegatiCardTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public DocumentiCollegatiCardTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<StringheDelSito>();
    }

    private static ResolvedDocLink L(DocLinkGroup g, string label, string href, int id = 1) =>
        new(g, new DocLinkTarget { Label = label, DocumentId = id, Type = ReleaseTargetType.Airport, Key = label }, href);

    [Fact]
    public void Senza_link_nessun_riquadro()
    {
        var cut = RenderComponent<DocumentiCollegatiCard>(p => p.Add(x => x.Link, Array.Empty<ResolvedDocLink>()));
        Assert.Empty(cut.FindAll(".rail-card").ToList());
    }

    [Fact]
    public void Un_documento_d_aeroporto_ha_un_elenco_solo_verso_i_documenti()
    {
        var cut = RenderComponent<DocumentiCollegatiCard>(p => p.Add(x => x.Link, new[]
        {
            L(DocLinkGroup.Acc, "LIBB vIPI", "/services/vsop/libb/vipi"),
            L(DocLinkGroup.App, "LIBN_APP", "/services/vsop/libb/apps/vipi?app=LIBN_APP", 2),
        }));

        Assert.Empty(cut.FindAll("details").ToList());
        Assert.Equal(new[] { "/services/vsop/libb/vipi", "/services/vsop/libb/apps/vipi?app=LIBN_APP" },
            cut.FindAll(".rail-collegati a").Select(a => a.GetAttribute("href")));
    }

    [Fact]
    public void La_vIPI_ACC_ha_APP_e_Aeroporti_chiusi()
    {
        var cut = RenderComponent<DocumentiCollegatiCard>(p => p
            .Add(x => x.PerAcc, true)
            .Add(x => x.Link, new[]
            {
                L(DocLinkGroup.App, "LIBV_APP", "/a"),
                L(DocLinkGroup.Airport, "LIBD vIPI", "/b", 2),
                L(DocLinkGroup.Airport, "LIBN vSOP", "/c", 3),
            }));

        var gruppi = cut.FindAll("details.rail-grp").ToList();
        Assert.Equal(2, gruppi.Count);
        Assert.All(gruppi, g => Assert.False(g.HasAttribute("open")));
        Assert.Equal(new[] { 1, 2 }, gruppi.Select(g => g.QuerySelectorAll("a").Length));
    }
}
