using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Components;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// La torre radar a schermo (18 ottobre 2026, committente): la sezione dice sì, no o «non indicato»; la figlia
/// porta la tecnica operativa e nasce chiusa. Dal 21 ottobre 2026 la tecnica è fatta di blocchi (committente: «non
/// ci deve essere un paragrafo fisso»): la disegna il motore comune delle sezioni, non la pagina.
/// </summary>
public class TorreRadarUiTests : TestContext
{
    /// <summary>Localizer che rende la chiave stessa (stesso stratagemma di EditorLvpTests).</summary>
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    public TorreRadarUiTests()
    {
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
        Services.AddSingleton<Vipi.Ui.StringheDelSito>();
    }

    // ---- La lettura ----------------------------------------------------------------------------------

    private IRenderedComponent<AirportRadarTower> Leggi(AirportRadarTowerView vista) =>
        RenderComponent<AirportRadarTower>(p => p.Add(x => x.View, vista));

    /// <summary>
    /// I tre stati coi colori dei callout del sito (committente, 18 ottobre 2026): verde col radar, rosso senza,
    /// giallo se nessuno l'ha detto — «non indicato» non è «no», e non deve averne il colore.
    /// </summary>
    [Theory]
    [InlineData(true, "success", "TorreRadar_Si")]
    [InlineData(false, "danger", "TorreRadar_No")]
    [InlineData(null, "warning", "TorreRadar_NonIndicato")]
    public void La_torre_dice_si_no_o_non_indicato_col_suo_colore(bool? dotata, string classe, string testo)
    {
        var stato = Leggi(new AirportRadarTowerView(dotata)).Find(".torre-radar-stato");

        Assert.True(stato.ClassList.Contains("callout"));
        Assert.True(stato.ClassList.Contains(classe), stato.ClassName);
        Assert.Equal(testo, stato.TextContent.Trim());
    }

    // ---- La sotto-sezione: blocchi, e chiusa ----------------------------------------------------------

    private static SectionView Figlia(string chiave, params BlockView[] blocchi) => new()
    {
        Id = "s-2", Title = chiave, Depth = 1, SectionKey = chiave,
        Blocks = blocchi, Children = Array.Empty<SectionView>(),
    };

    private IRenderedComponent<SectionNode> Nodo(string chiave, params BlockView[] blocchi) =>
        RenderComponent<SectionNode>(p => p
            .Add(x => x.Section, Figlia(chiave, blocchi))
            .Add(x => x.Profile, SectionProfile.Airport)
            .Add(x => x.DerivedContent, (RenderFragment<SectionView>)(_ => b => b.AddContent(0, "SCHEDA"))));

    /// <summary>
    /// Fino al 18 ottobre 2026 ogni sotto-sezione del documento nasceva aperta, scritto a mano nel markup: la
    /// tecnica operativa è la prima che il catalogo vuole chiusa (committente).
    /// </summary>
    [Fact]
    public void La_tecnica_nasce_chiusa_le_altre_sotto_sezioni_restano_aperte()
    {
        Assert.False(Nodo(SectionKeys.RadarTowerTechnique).Find("details").HasAttribute("open"));
        Assert.True(Nodo(SectionKeys.RunwayThresholds).Find("details").HasAttribute("open"));
    }

    /// <summary>
    /// La tecnica mostra i SUOI blocchi, come ogni sezione scritta, e la pagina non ci mette niente di suo: fino al
    /// 21 ottobre 2026 lì c'era un testo fisso dell'anagrafica, e i blocchi della sezione non uscivano.
    /// </summary>
    [Fact]
    public void La_tecnica_mostra_i_suoi_blocchi_e_non_una_scheda_della_pagina()
    {
        var cut = Nodo(SectionKeys.RadarTowerTechnique, new BlockView
        {
            Id = 1, Order = 1, Format = BlockFormat.Prose, State = RenderState.Expanded,
            Body = "Identificazione radar entro 5 NM.\nSeconda riga.",
        });

        var prosa = cut.Find(".prose");
        Assert.Contains("Identificazione radar entro 5 NM.", prosa.TextContent);
        Assert.Contains("Seconda riga.", prosa.TextContent);
        Assert.NotEmpty(prosa.QuerySelectorAll("br"));
        Assert.DoesNotContain("SCHEDA", cut.Markup);
    }

    // ---- L'editor ------------------------------------------------------------------------------------

    private IRenderedComponent<AirportRadarTowerEditor> Editor(RadarTowerEdit? riga, Action<RadarTowerEdit?>? cambiata = null) =>
        RenderComponent<AirportRadarTowerEditor>(p => p
            .Add(x => x.Row, riga)
            .Add(x => x.Editing, true)
            .Add(x => x.RowChanged, EventCallback.Factory.Create<RadarTowerEdit?>(this, r => cambiata?.Invoke(r))));

    /// <summary>Tre stati e non una casella: «non indicato» è una risposta diversa da «no».</summary>
    [Fact]
    public void Il_si_no_ha_tre_scelte_e_parte_da_non_indicato()
    {
        var cut = Editor(null);

        var radio = cut.FindAll("input[type=radio]");
        Assert.Equal(3, radio.Count);
        Assert.True(radio.ElementAt(2).HasAttribute("checked"));
    }

    [Fact]
    public void Scegliere_si_fa_nascere_la_riga()
    {
        RadarTowerEdit? arrivata = null;
        var cut = Editor(null, r => arrivata = r);

        cut.FindAll("input[type=radio]").ElementAt(0).Change(true);

        Assert.NotNull(arrivata);
        Assert.True(arrivata!.Equipped);
    }

    [Fact]
    public void Passare_a_no_cambia_la_stessa_riga()
    {
        var riga = new RadarTowerEdit { Equipped = true };
        RadarTowerEdit? arrivata = null;
        var cut = Editor(riga, r => arrivata = r);

        cut.FindAll("input[type=radio]").ElementAt(1).Change(true);

        Assert.Same(riga, arrivata);
        Assert.False(arrivata!.Equipped);
    }

    /// <summary>
    /// Tornare a «non indicato» toglie la riga SENZA chiedere: dal 21 ottobre 2026 non porta via niente, la tecnica
    /// sta nei blocchi del documento e torna col «sì».
    /// </summary>
    [Fact]
    public void Tornare_a_non_indicato_toglie_la_riga_senza_chiedere()
    {
        var chiamata = false;
        RadarTowerEdit? arrivata = new();
        var cut = Editor(new RadarTowerEdit { Equipped = true }, r => { chiamata = true; arrivata = r; });

        cut.FindAll("input[type=radio]").ElementAt(2).Change(true);

        Assert.True(chiamata);
        Assert.Null(arrivata);
    }

    /// <summary>Senza il «sì» i blocchi della tecnica, sotto, non escono nel documento: l'editor lo dice qui.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public void Senza_radar_l_editor_dice_che_la_tecnica_non_esce(bool? dotata)
    {
        var cut = Editor(dotata is bool d ? new RadarTowerEdit { Equipped = d } : null);

        Assert.Contains("TorreRadar_TecnicaSoloConRadar", cut.Markup);
        Assert.Empty(cut.FindAll("textarea"));
    }

    [Fact]
    public void Con_la_torre_radar_la_frase_non_c_e()
    {
        Assert.DoesNotContain("TorreRadar_TecnicaSoloConRadar", Editor(new RadarTowerEdit { Equipped = true }).Markup);
    }
}
