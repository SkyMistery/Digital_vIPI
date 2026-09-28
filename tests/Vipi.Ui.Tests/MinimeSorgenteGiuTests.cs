using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Ui;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// 🔴 U-039 (revisione totale 3): la sezione delle minime la cui carta non è arrivata lo dice — con l'avviso, non
/// col «nessuna carta» di quando il sectorfile il file non ce l'ha, che è un altro fatto.
/// </summary>
public class MinimeSorgenteGiuTests : TestContext
{
    private sealed class ChiaveComeValore : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public MinimeSorgenteGiuTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new ChiaveComeValore());
        Services.AddSingleton<StringheDelSito>();
    }

    [Fact]
    public void La_sorgente_giu_si_dice_e_non_si_confonde_con_nessuna_carta()
    {
        var cut = RenderComponent<MinimaSection>(p => p
            .Add(x => x.View, new MinimaView(Array.Empty<MinimaChart>(), SorgenteNonRaggiungibile: true)));

        Assert.Contains("Minima_SourceDownTitle", cut.Markup);
        Assert.DoesNotContain("Minima_NoneTitle", cut.Markup);
    }

    [Fact]
    public void Senza_file_resta_nessuna_carta_senza_avviso()
    {
        var cut = RenderComponent<MinimaSection>(p => p.Add(x => x.View, MinimaView.Empty));

        Assert.Contains("Minima_NoneTitle", cut.Markup);
        Assert.DoesNotContain("Minima_SourceDownTitle", cut.Markup);
    }
}
