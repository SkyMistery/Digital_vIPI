using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Domain.Entities;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// U-194 (revisione totale 3): la nota delle LVP ha il tetto della colonna anche nel campo, come le altre note
/// dell'editor. Il servizio lo dice comunque con una frase; il campo evita di scriverla per niente.
/// </summary>
public class EditorLvpTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public EditorLvpTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
    }

    [Fact]
    public void La_nota_ha_il_tetto_della_colonna()
    {
        var cut = RenderComponent<AirportLvpEditor>(p => p
            .Add(x => x.Row, new LvpEdit())
            .Add(x => x.Editing, true));

        var nota = cut.Find("input[placeholder='Lvp_NotePlaceholder']");
        Assert.Equal(AirportLvpMinima.NotaMassima.ToString(), nota.GetAttribute("maxlength"));
    }
}
