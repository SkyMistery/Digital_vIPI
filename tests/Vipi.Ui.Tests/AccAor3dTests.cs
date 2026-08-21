using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Ui.Components.App;

using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>Viewer 3D dell'AoR (AccAor3d): markup + payload data-sectors3d. Il rendering WebGL è client-side
/// (vipi-aor3d.js), qui si verifica solo che il componente emetta stage, fallback e i dati dei settori corretti.</summary>
public class AccAor3dTests : TestContext
{
    /// <summary>Localizer che rende la chiave stessa: il fallback «3D non disponibile» ora e' localizzato.</summary>
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public AccAor3dTests() =>
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());

    private static AppAorPolygon Poly() => new(
        "0 0 100 100", "M0 0L10 0L10 10Z",
        new[] { new[] { 43.0, 10.0 }, new[] { 43.0, 11.0 }, new[] { 44.0, 11.0 } },
        43.0, 10.0, 44.0, 11.0, 43.5, 10.5);

    [Fact]
    public void Empty_view_renders_nothing()
    {
        var cut = RenderComponent<AccAor3d>(p => p.Add(x => x.View, AccAorView.Empty));
        Assert.DoesNotContain("aor3d-stage", cut.Markup);
    }

    [Fact]
    public void Sector_emits_stage_fallback_and_band()
    {
        var view = new AccAorView(
            new[] { new AccSectorAor("LIRR_NE_CTR", "NE", "#0D2C99", new[] { Poly() }, 245, 355) },
            System.Array.Empty<AccConfigSelection>());

        var cut = RenderComponent<AccAor3d>(p => p.Add(x => x.View, view));

        Assert.Contains("aor3d-fallback", cut.Markup);        // resa robusta senza WebGL
        var payload = cut.Find(".aor3d-stage").GetAttribute("data-sectors3d")!;   // JSON decodificato
        Assert.Contains("LIRR_NE_CTR", payload);              // callsign nel payload
        Assert.Contains("\"fl\":[245,355]", payload);         // banda FL estrusione
    }

    /// <summary>Le chip settore del 3D sono le stesse del 2D (le pilota onAorClick in vipi-aor.js): servono il
    /// contenitore .aor-block, le .aor-chip col callsign e le azioni Tutti/Nessuno. Niente chip configurazione.</summary>
    [Fact]
    public void Renders_sector_chips_like_2d_without_config_chips()
    {
        var view = new AccAorView(
            new[] { new AccSectorAor("LIBB_ES_CTR", "Brindisi Radar", "#0D2C99", new[] { Poly() }, 245, 355) },
            new[] { new AccConfigSelection("cfg1", "Configurazione 1", new[] { "LIBB_ES_CTR" }) });

        var cut = RenderComponent<AccAor3d>(p => p.Add(x => x.View, view));

        Assert.NotNull(cut.Find(".aor-block .aor3d-stage"));            // stage raggiungibile dalle chip del blocco
        Assert.Equal("LIBB_ES_CTR", cut.Find(".aor-chip").GetAttribute("data-sec"));
        Assert.Equal(2, cut.FindAll(".aor-chip-actions .aor-all").Count);   // Tutti / Nessuno
        Assert.Empty(cut.FindAll(".cfg-btn"));                          // le configurazioni restano al 2D
    }

    /// <summary>Selettore «Altezza»: 6 fattori, ×0.5 acceso alla prima resa (allineato a ZDEF in vipi-aor3d.js).</summary>
    [Fact]
    public void Height_selector_defaults_to_half_scale()
    {
        var view = new AccAorView(
            new[] { new AccSectorAor("LIRR_NE_CTR", "NE", "#0D2C99", new[] { Poly() }, 245, 355) },
            System.Array.Empty<AccConfigSelection>());

        var cut = RenderComponent<AccAor3d>(p => p.Add(x => x.View, view));

        Assert.Equal(6, cut.FindAll(".aor3d-z").Count);
        Assert.Equal("0.5", cut.Find(".aor3d-z.on").GetAttribute("data-z"));
        Assert.DoesNotContain("aor3d/", cut.Markup);                    // link «Apri pagina» rimosso (rotta senza ingresso)
    }

    [Fact]
    public void Null_band_defaults_to_ground_and_unlimited()
    {
        var view = new AccAorView(
            new[] { new AccSectorAor("LIRP_APP", "Pisa", "#3C55AC", new[] { Poly() }) },
            System.Array.Empty<AccConfigSelection>());

        var cut = RenderComponent<AccAor3d>(p => p.Add(x => x.View, view));

        var payload = cut.Find(".aor3d-stage").GetAttribute("data-sectors3d")!;
        Assert.Contains("\"fl\":[0,660]", payload);           // GND / UNL di default
    }
}
