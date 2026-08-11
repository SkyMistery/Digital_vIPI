using Bunit;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// I filtri di stato in cima a mezze pagine dell'app erano <c>&lt;span&gt;</c> con un gestore di click:
/// comandi che esistevano solo per il mouse. Fuori dal giro del tabulatore, sordi a Invio e Spazio, e per
/// uno screen reader indistinguibili dal testo attorno. Non è rifinitura: è il modo in cui si restringe un
/// elenco lungo.
/// </summary>
public class ChipTests : TestContext
{
    [Fact]
    public void Entra_nel_giro_del_tabulatore_e_si_annuncia_come_pulsante()
    {
        var cut = RenderComponent<Chip>(p => p
            .Add(x => x.Class, "ch on")
            .Add(x => x.Active, true)
            .AddChildContent("Tutti"));

        var span = cut.Find("span");
        Assert.Equal("button", span.GetAttribute("role"));
        Assert.Equal("0", span.GetAttribute("tabindex"));
        Assert.Equal("true", span.GetAttribute("aria-pressed"));
        Assert.Contains("ch on", span.GetAttribute("class"));
    }

    [Fact]
    public void Una_chip_non_selezionata_lo_dice()
    {
        var cut = RenderComponent<Chip>(p => p.Add(x => x.Active, false).AddChildContent("vLOA"));
        Assert.Equal("false", cut.Find("span").GetAttribute("aria-pressed"));
    }

    [Theory]
    [InlineData("Enter")]
    [InlineData(" ")]
    public void Si_attiva_da_tastiera(string tasto)
    {
        var attivazioni = 0;
        var cut = RenderComponent<Chip>(p => p
            .Add(x => x.OnActivate, () => attivazioni++)
            .AddChildContent("Tutti"));

        cut.Find("span").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = tasto });

        Assert.Equal(1, attivazioni);
    }

    [Fact]
    public void Gli_altri_tasti_non_la_attivano()
    {
        var attivazioni = 0;
        var cut = RenderComponent<Chip>(p => p
            .Add(x => x.OnActivate, () => attivazioni++)
            .AddChildContent("Tutti"));

        cut.Find("span").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "a" });
        cut.Find("span").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Tab" });

        Assert.Equal(0, attivazioni);
    }

    [Fact]
    public void Il_click_continua_a_funzionare()
    {
        var attivazioni = 0;
        var cut = RenderComponent<Chip>(p => p
            .Add(x => x.OnActivate, () => attivazioni++)
            .AddChildContent("Tutti"));

        cut.Find("span").Click();

        Assert.Equal(1, attivazioni);
    }

    /// <summary>Gli attributi non dichiarati passano: chi la usa non deve perdere `title` o `data-*`.</summary>
    [Fact]
    public void Gli_attributi_extra_arrivano_al_markup()
    {
        var cut = RenderComponent<Chip>(p => p
            .AddUnmatched("title", "filtra per ACC")
            .AddUnmatched("data-acc", "LIRR")
            .AddChildContent("LIRR"));

        var span = cut.Find("span");
        Assert.Equal("filtra per ACC", span.GetAttribute("title"));
        Assert.Equal("LIRR", span.GetAttribute("data-acc"));
    }
}
