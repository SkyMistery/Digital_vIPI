using Bunit;
using Vipi.Application.Content;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>Viewer 3D dell'AoR (AccAor3d): markup + payload data-sectors3d. Il rendering WebGL è client-side
/// (vipi-aor3d.js), qui si verifica solo che il componente emetta stage, fallback e i dati dei settori corretti.</summary>
public class AccAor3dTests : TestContext
{
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
