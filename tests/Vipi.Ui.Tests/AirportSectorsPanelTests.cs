using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il pannello «Settori ATC», montato per davvero. Estratto dall'editor d'aeroporto l'11 settembre 2026 perché
/// serviva anche all'editor del vSOP su un campo solo militare senza vIPI civile.
/// </summary>
public class AirportSectorsPanelTests : TestContext
{
    private sealed class ChiaveComeValore : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + ":" + string.Join("|", arguments), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public AirportSectorsPanelTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new ChiaveComeValore());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddLogging();
    }

    private static AirportSectorRow Twr(int id) => new(id, "LIMS_TWR", "LIMS", "LIMM", "TWR", null, "118.700",
        LowerLimit: 0, UpperLimit: 2500, IsHidden: false, HasPolygon: false, IsPrimary: true, IsAccApp: false);

    private IRenderedComponent<AirportSectorsPanel> Rendi(IReadOnlyList<AirportSectorRow> righe, bool editing,
        List<(int, int?, int?)>? limiti = null, Action? suImport = null) =>
        RenderComponent<AirportSectorsPanel>(p => p
            .Add(x => x.Sectors, righe)
            .Add(x => x.Editing, editing)
            .Add(x => x.OnImport, () => suImport?.Invoke())
            .Add(x => x.OnSetLimits, (id, lo, up) => { limiti?.Add((id, lo, up)); return Task.CompletedTask; })
            .Add(x => x.OnSetHidden, (_, _) => Task.CompletedTask)
            .Add(x => x.OnSetPrimary, _ => Task.CompletedTask)
            .Add(x => x.OnSetAccApp, (_, _) => Task.CompletedTask));

    /// <summary>Senza posizioni lo dice, e il tasto per importarle c'è — è il gesto che a LIMS mancava.</summary>
    [Fact]
    public void Senza_posizioni_offre_l_import()
    {
        var importati = 0;
        var c = Rendi(Array.Empty<AirportSectorRow>(), editing: true, suImport: () => importati++);

        Assert.Contains("Ape_NoSectors", c.Markup);
        c.Find("button.btn.primary").Click();
        Assert.Equal(1, importati);
    }

    /// <summary>Senza il lock i comandi sono spenti: il pannello si legge e basta.</summary>
    [Fact]
    public void Senza_lock_i_comandi_sono_spenti()
    {
        var c = Rendi(new[] { Twr(1) }, editing: false);

        Assert.All(c.FindAll("button, input"), e => Assert.True(e.HasAttribute("disabled")));
    }

    /// <summary>Un limite digitato arriva all'ospite con tutt'e due le quote della riga: «UNL» è illimitato.</summary>
    [Fact]
    public void Un_limite_digitato_arriva_all_ospite()
    {
        var limiti = new List<(int, int?, int?)>();
        var c = Rendi(new[] { Twr(7) }, editing: true, limiti: limiti);

        c.FindAll("tbody input[style*='70px']").ToList()[1].Change("UNL");

        Assert.Equal((7, (int?)0, (int?)null), Assert.Single(limiti));
    }
}
