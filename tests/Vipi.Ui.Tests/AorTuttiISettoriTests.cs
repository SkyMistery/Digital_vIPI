using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// La scelta «Tutti i settori» della mappa AoR: la mappa se la dà da sola quando il gruppo non ha configurazioni.
///
/// <para><b>Il difetto che presidia.</b> Il nome era scritto in italiano nel servizio, e la pagina inglese diceva
/// «Tutti i settori». Ora il nome lo mette la pagina dalla chiave (<see cref="AccConfigSelection.TuttiISettori"/>):
/// vale anche per le sezioni AoR già congelate, che portano il nome di allora e non si riscrivono.</para>
/// </summary>
public class AorTuttiISettoriTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public AorTuttiISettoriTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
    }

    private static AppAorPolygon Poly() => new(
        "0 0 100 100", "M0 0L10 0L10 10Z",
        new[] { new[] { 45.0, 8.0 }, new[] { 45.0, 9.0 }, new[] { 46.0, 9.0 } },
        45.0, 8.0, 46.0, 9.0, 45.5, 8.5);

    private static AccAorView Vista(params AccConfigSelection[] configs) => new(
        new[] { new AccSectorAor("LIMC_ANE_APP", "Milano Radar", "#3C55AC", new[] { Poly() }) },
        configs);

    [Fact]
    public void La_scelta_della_mappa_prende_il_nome_dalla_pagina_anche_se_congelata_in_italiano()
    {
        var cut = RenderComponent<AccAor>(p => p.Add(x => x.View,
            Vista(new AccConfigSelection(AccConfigSelection.TuttiISettori, "Tutti i settori", new[] { "LIMC_ANE_APP" }))));

        Assert.Equal("Aor_AllSectors", cut.Find(".cfg-btn").TextContent.Trim());
    }

    [Fact]
    public void Una_configurazione_vera_tiene_il_suo_nome()
    {
        var cut = RenderComponent<AccAor>(p => p.Add(x => x.View,
            Vista(new AccConfigSelection("cfg:1", "Tutti i settori", new[] { "LIMC_ANE_APP" }))));

        // Chi chiama così una sua configurazione la vede chiamata così: conta la chiave, non il nome.
        Assert.Equal("Tutti i settori", cut.Find(".cfg-btn").TextContent.Trim());
    }
}
