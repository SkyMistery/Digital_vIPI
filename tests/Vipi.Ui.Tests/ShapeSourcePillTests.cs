using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Airspace;
using Vipi.Domain;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// La pastiglia che dice <b>da dove viene</b> la forma di un settore (carta
/// <c>docs/refactor/15-shape-del-settore-una-porta-sola.md</c>, S8).
///
/// <para>⚠️ Vive in un componente solo perché le pagine che mostrano i limiti verticali sono <b>due</b> —
/// Struttura ACC ed editor aeroporto — e due copie diventerebbero due comportamenti diversi il giorno che una
/// delle due cambia.</para>
/// </summary>
public class ShapeSourcePillTests : TestContext
{
    /// <summary>Localizer che rende la chiave stessa: le asserzioni parlano di chiavi, non di traduzioni.</summary>
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name + ":" + string.Join(",", arguments), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public ShapeSourcePillTests() =>
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());

    private static ShapePart Pezzo(int? baseFt = 0, int? topFt = 3_000) =>
        new("[[15.0,37.0],[15.4,37.0],[15.4,37.4],[15.0,37.4]]",
            baseFt, topFt, AirspaceDatum.Gnd, AirspaceDatum.Amsl, "GND", "3000 FT AMSL");

    private static SectorShape Forma(ShapeSource fonte, int pezzi = 1) =>
        new("LICC_APP", fonte, Enumerable.Range(0, pezzi).Select(_ => Pezzo()).ToList(), Array.Empty<string>());

    private IRenderedComponent<ShapeSourcePill> Render(SectorShape? forma) =>
        RenderComponent<ShapeSourcePill>(p => p.Add(x => x.Shape, forma));

    [Fact]
    public void Dall_aip_dice_anche_QUANTI_pezzi()
    {
        // ⚠️ Il numero è l'unica cosa a schermo che distingue un CTR di due zone da uno solo — ed è il numero
        // da cui è cominciata tutta la storia.
        var testo = Render(Forma(ShapeSource.Aip, pezzi: 2)).Find("span").TextContent;

        Assert.Contains("Shape_FromAip", testo);
        Assert.Contains("2", testo);
    }

    [Fact]
    public void Le_altre_fonti_hanno_ognuna_la_sua_voce()
    {
        Assert.Contains("Shape_FromSectorfile", Render(Forma(ShapeSource.Sectorfile)).Find("span").TextContent);
        Assert.Contains("Shape_FromSource", Render(Forma(ShapeSource.Source)).Find("span").TextContent);
        Assert.Contains("Shape_Synthetic", Render(Forma(ShapeSource.Synthetic)).Find("span").TextContent);
    }

    [Fact]
    public void Senza_nessuna_forma_lo_dice_invece_di_restare_vuota()
    {
        // DEL e GND non hanno un'area: una cella vuota si legge come «non lo so», che è un'altra cosa.
        var pill = Render(null).Find("span");

        Assert.Contains("Shape_None", pill.TextContent);
        Assert.Equal("Shape_NoneTitle", pill.GetAttribute("title"));
    }

    [Fact]
    public void Il_titolo_dell_aip_porta_il_numero_dei_pezzi()
    {
        // Il testo lungo spiega perché le caselle dei limiti accanto sono sbiadite: quelle quote non le usa
        // nessuno finché l'aggancio dura.
        var title = Render(Forma(ShapeSource.Aip, pezzi: 7)).Find("span").GetAttribute("title");

        Assert.Equal("Shape_FromAipTitle:7", title);
    }
}
