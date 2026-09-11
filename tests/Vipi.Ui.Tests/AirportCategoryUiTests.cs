using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Domain;
using Vipi.Ui.Components;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// L'etichetta della categoria nei documenti e il comando della pagina Aeroporti (carta
/// <c>docs/feature/2026-09-11-categorie-aeroporto.md</c>).
/// </summary>
public class AirportCategoryUiTests : TestContext
{
    private sealed class ChiaveComeValore : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + ":" + string.Join("|", arguments), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public AirportCategoryUiTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new ChiaveComeValore());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddLogging();
    }

    /// <summary>Quattro categorie, quattro etichette: anche «Civile», che fino all'11 settembre non ne aveva.</summary>
    [Theory]
    [InlineData(AirportCategory.Civil, "Apt_Cat_Civil")]
    [InlineData(AirportCategory.MilitaryOnly, "Apt_Cat_MilitaryOnly")]
    [InlineData(AirportCategory.CivilWithMilitaryPresence, "Apt_Cat_CivilWithMilitaryPresence")]
    [InlineData(AirportCategory.MilitaryWithCivilPresence, "Apt_Cat_MilitaryWithCivilPresence")]
    public void L_etichetta_dice_la_categoria(AirportCategory c, string chiave)
    {
        var tag = RenderComponent<MilitaryTag>(p => p.Add(x => x.Category, c));

        Assert.Contains(chiave, tag.Find(".mil-tag").TextContent);
    }

    /// <summary>⚠️ Scalo sconosciuto (un APP la cui testa non è in anagrafica): nessuna etichetta, invece di
    /// dire «Civile» di qualcosa che non sappiamo.</summary>
    [Fact]
    public void Senza_categoria_non_si_scrive_niente()
    {
        var tag = RenderComponent<MilitaryTag>(p => p.Add(x => x.Category, (AirportCategory?)null));

        Assert.Empty(tag.FindAll(".mil-tag"));
    }

    // ---- Quando il comando chiede conferma -----------------------------------------------------------

    [Theory]
    // Verso «solo militare» con una vIPI: la vIPI resta fuori categoria.
    [InlineData(AirportCategory.MilitaryWithCivilPresence, AirportCategory.MilitaryOnly, true, false, DocumentEdition.Civil)]
    // Verso «civile con presenza militare» con un vSOP: il vSOP resta fuori categoria.
    [InlineData(AirportCategory.MilitaryWithCivilPresence, AirportCategory.CivilWithMilitaryPresence, false, true, DocumentEdition.Military)]
    [InlineData(AirportCategory.MilitaryOnly, AirportCategory.CivilWithMilitaryPresence, false, true, DocumentEdition.Military)]
    public void Chiede_conferma_quando_un_documento_esistente_resta_fuori(
        AirportCategory da, AirportCategory a, bool civile, bool militare, DocumentEdition fuori)
    {
        Assert.Equal(fuori, AirportCategoryPicker.Conseguenza(da, a, civile, militare));
    }

    /// <summary>🔴 Negli altri casi la scelta scrive senza chiedere: una conferma che chiede sempre è una
    /// conferma che nessuno legge.</summary>
    [Theory]
    [InlineData(AirportCategory.CivilWithMilitaryPresence, AirportCategory.MilitaryOnly, false, false)]  // niente documenti
    [InlineData(AirportCategory.CivilWithMilitaryPresence, AirportCategory.MilitaryWithCivilPresence, true, false)]  // allarga
    [InlineData(AirportCategory.MilitaryOnly, AirportCategory.MilitaryWithCivilPresence, false, true)]  // allarga
    // ⚠️ Un documento GIÀ fuori categoria non è una conseguenza di questo gesto: niente conferma.
    [InlineData(AirportCategory.CivilWithMilitaryPresence, AirportCategory.MilitaryWithCivilPresence, true, true)]
    public void Non_chiede_conferma_se_nessun_documento_resta_fuori_per_questo_gesto(
        AirportCategory da, AirportCategory a, bool civile, bool militare)
    {
        Assert.Null(AirportCategoryPicker.Conseguenza(da, a, civile, militare));
    }

    /// <summary>
    /// Il comando montato: una scelta senza conseguenze arriva alla pagina subito; una con conseguenze apre la
    /// conferma e NON arriva finché non si conferma.
    /// </summary>
    [Fact]
    public void La_scelta_arriva_subito_o_dopo_la_conferma()
    {
        AirportCategory? scritta = null;
        var c = RenderComponent<AirportCategoryPicker>(p => p
            .Add(x => x.Icao, "LIRP")
            .Add(x => x.Category, AirportCategory.MilitaryWithCivilPresence)
            .Add(x => x.HasMilitaryDocument, true)
            .Add(x => x.OnChange, (AirportCategory v) => scritta = v));

        // Offre le tre voci, e «Civile» no.
        Assert.Equal(3, c.FindAll("option").Count);

        c.Find("select").Change(nameof(AirportCategory.CivilWithMilitaryPresence));
        Assert.Null(scritta);
        Assert.Contains("Apt_CatConMil", c.Markup);

        c.Find(".apt-cat-confirm .btn.primary").Click();
        Assert.Equal(AirportCategory.CivilWithMilitaryPresence, scritta);
    }
}
