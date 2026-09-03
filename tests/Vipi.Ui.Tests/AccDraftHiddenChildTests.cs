using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Ui.Components;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Anteprima BOZZA di una vIPI ACC: una SOTTO-sezione nascosta si vede, marcata; in pubblica no.
///
/// <para>⚠️ Fino al 4 settembre 2026 `AccVipiPage` e `AccSectionBody` non passavano MAI `IsDraft`. Le
/// sezioni di primo livello avevano il filtro consapevole (`VisibleSections`, che la bozza la conosce), ma
/// le sotto-sezioni scendevano in `SectionBody` col default `false` e sparivano <b>anche dalla bozza</b> —
/// cioè proprio dove si lavora. Il default prudente è giusto: mancava chi lo passa.</para>
/// </summary>
public class AccDraftHiddenChildTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public AccDraftHiddenChildTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
    }

    private static SectionView Figlia(string titolo, bool nascosta) => new()
    {
        Id = "s-2",
        Title = titolo,
        Depth = 1,
        SectionKey = SectionKeys.NewCustom(),
        IsHidden = nascosta,
        Blocks = Array.Empty<BlockView>(),
        Children = Array.Empty<SectionView>(),
    };

    /// <summary>Una sezione LIBERA del blocco (corpo editoriale) con una sola figlia nascosta.</summary>
    private static SectionView Sezione(bool figliaNascosta) => new()
    {
        Id = "s-1",
        Title = "Sezione libera",
        Depth = 0,
        SectionKey = SectionKeys.NewCustom(),
        Blocks = Array.Empty<BlockView>(),
        Children = new[] { Figlia("FIGLIA-NASCOSTA", figliaNascosta) },
    };

    private string Markup(bool bozza, bool figliaNascosta = true) =>
        RenderComponent<AccSectionBody>(p => p
            .Add(x => x.Block, new AccBlock { Key = "grp:test", Kind = AccBlockKind.AppGroup, Title = "Gruppo" })
            .Add(x => x.Key, SectionKeys.NewCustom())
            .Add(x => x.Editorial, Sezione(figliaNascosta))
            .Add(x => x.IsDraft, bozza)).Markup;

    [Fact]
    public void In_bozza_una_sotto_sezione_nascosta_si_vede_marcata()
    {
        var markup = Markup(bozza: true);

        Assert.Contains("FIGLIA-NASCOSTA", markup);
        Assert.Contains("Common_HiddenNotPublic", markup);   // la pill «nascosta», chiave resa dal localizer
    }

    [Fact]
    public void In_pubblica_una_sotto_sezione_nascosta_non_esce()
    {
        Assert.DoesNotContain("FIGLIA-NASCOSTA", Markup(bozza: false));
    }

    /// <summary>Il cancello vale per la sola nascosta: una figlia normale esce in tutt'e due le viste.</summary>
    [Fact]
    public void Una_sotto_sezione_normale_esce_sempre()
    {
        Assert.Contains("FIGLIA-NASCOSTA", Markup(bozza: false, figliaNascosta: false));
        Assert.Contains("FIGLIA-NASCOSTA", Markup(bozza: true, figliaNascosta: false));
    }
}
