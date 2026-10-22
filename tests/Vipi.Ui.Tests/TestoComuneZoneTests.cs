using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Components;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il testo comune delle torri radar nell'editor dello scalo (S127): in testa alla tecnica operativa, in un riquadro
/// suo, e modificabile solo dagli admin.
/// </summary>
public class TestoComuneZoneTests : TestContext
{
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    private sealed class Autore : IEditAuthorizationService
    {
        public VipiRole Role => VipiRole.Editor;
        public bool IsAdmin => false;
        public int? CurrentUserId => 704798;
        public string? CurrentName => "Chi scrive lo scalo";
    }

    private sealed class Deposito : ITestoComuneStore
    {
        public Task<IReadOnlyList<ExtraBlock>> LeggiAsync(string sezione, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ExtraBlock>>(new[]
            {
                new ExtraBlock { Format = BlockFormat.Callout, CalloutKind = CalloutKind.Danger, Text = "È fatto obbligo la visione del documento." },
                new ExtraBlock { Format = BlockFormat.Callout, CalloutKind = CalloutKind.Success, Text = "LA TORRE NON FORNISCE VETTORAMENTO" },
            });
        public Task SalvaAsync(string sezione, IReadOnlyList<ExtraBlock> blocchi, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    public TestoComuneZoneTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
        Services.AddSingleton<IEditAuthorizationService>(new Autore());
        Services.AddSingleton<ITestoComuneStore>(new Deposito());
    }

    [Fact]
    public void Chi_scrive_lo_scalo_vede_il_testo_comune_e_nessun_comando_per_cambiarlo()
    {
        var cut = RenderComponent<TestoComuneZone>(p => p.Add(x => x.Sezione, SectionKeys.RadarTowerTechnique));

        cut.WaitForAssertion(() => Assert.Contains("LA TORRE NON FORNISCE VETTORAMENTO", cut.Find(".testo-comune").TextContent));
        // Il testo visibile, non il markup: lì le lettere accentate escono come entità HTML.
        var testo = cut.Find(".testo-comune").TextContent;
        Assert.Contains("È fatto obbligo", testo);
        Assert.Contains("TestoComune_Title", testo);
        Assert.Empty(cut.FindAll(".lockbar"));
        Assert.Empty(cut.FindAll("button.primary"));
    }

    /// <summary>
    /// 🔴 Nella pagina il testo comune sta TUTTO sopra i blocchi dello scalo. Visto a schermo il 22 ottobre 2026 su LIRQ:
    /// il corpo della sezione leggeva gli ordini negativi dei blocchi comuni come «sconosciuti» e li rimetteva a 1 e 2, e
    /// il paragrafo dello scalo (ordine 1) finiva fra i due callout.
    /// </summary>
    [Fact]
    public void Nella_pagina_il_testo_comune_sta_sopra_il_paragrafo_dello_scalo()
    {
        var tecnica = new SectionView
        {
            Id = "s-6", Title = "Tecnica operativa", Depth = 1, SectionKey = SectionKeys.RadarTowerTechnique,
            Blocks = new[] { new BlockView { Id = 41, Order = 1, Format = BlockFormat.Prose, State = RenderState.Expanded, Body = "Dello scalo" } },
            Children = Array.Empty<SectionView>(),
        };
        var radar = new SectionView
        {
            Id = "s-5", Title = "Radar di torre", Depth = 0, SectionKey = SectionKeys.RadarTower,
            Blocks = Array.Empty<BlockView>(), Children = new[] { tecnica },
        };
        var comune = TestoComune.Vista(new[]
        {
            new ExtraBlock { Format = BlockFormat.Callout, CalloutKind = CalloutKind.Danger, Text = "Obbligo di leggere le linee guida." },
            new ExtraBlock { Format = BlockFormat.Callout, CalloutKind = CalloutKind.Success, Text = "LA TORRE NON FORNISCE VETTORAMENTO" },
        });
        var conComune = TorreRadar.ConTestoComune(new[] { radar }, new AirportRadarTowerView(true, null, comune))[0].Children[0];

        var testo = RenderComponent<SectionBody>(p => p.Add(x => x.Section, conComune)).Markup;

        var scalo = testo.IndexOf("Dello scalo", StringComparison.Ordinal);
        Assert.True(testo.IndexOf("Obbligo di leggere", StringComparison.Ordinal) is var a && a >= 0 && a < scalo);
        Assert.True(testo.IndexOf("VETTORAMENTO", StringComparison.Ordinal) is var b && b >= 0 && b < scalo, testo);
    }

    /// <summary>Il preludio c'è solo nella tecnica operativa: le altre sezioni dell'editor restano com'erano.</summary>
    [Fact]
    public void Il_preludio_c_e_solo_nella_tecnica_operativa()
    {
        EditableSection Sez(string chiave) => new()
        {
            Id = 1, Title = chiave, Depth = 1, Order = 1, SectionKey = chiave,
            Blocks = new List<EditableBlock>(), Children = new List<EditableSection>(),
        };

        Assert.NotNull(TestoComuneZone.Preludio(Sez(SectionKeys.RadarTowerTechnique)));
        Assert.Null(TestoComuneZone.Preludio(Sez(SectionKeys.RadarTower)));
        Assert.Null(TestoComuneZone.Preludio(Sez("runways")));
    }
}
