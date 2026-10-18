using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Ui.Components;
using Vipi.Ui.Components.App;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// La torre radar a schermo (18 ottobre 2026, committente): la sezione dice sì, no o «non indicato»; la figlia
/// porta la tecnica operativa, nasce chiusa, e nell'editor si scrive solo con la torre radar.
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

    private IRenderedComponent<AirportRadarTower> Leggi(AirportRadarTowerView vista, bool tecnica = false) =>
        RenderComponent<AirportRadarTower>(p => p.Add(x => x.View, vista).Add(x => x.Tecnica, tecnica));

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
        var stato = Leggi(new AirportRadarTowerView(dotata, null)).Find(".torre-radar-stato");

        Assert.True(stato.ClassList.Contains("callout"));
        Assert.True(stato.ClassList.Contains(classe), stato.ClassName);
        Assert.Equal(testo, stato.TextContent.Trim());
    }

    /// <summary>La tecnica si scrive a righe, e gli a-capo sono la sua sola struttura: devono arrivare a schermo.</summary>
    [Fact]
    public void La_tecnica_tiene_gli_a_capo()
    {
        var cut = Leggi(new AirportRadarTowerView(true, "Prima riga.\nSeconda riga."), tecnica: true);

        var p = cut.Find("p.torre-radar-tecnica");
        Assert.Contains("pre-line", p.GetAttribute("style"));
        Assert.Equal("Prima riga.\nSeconda riga.", p.TextContent);
    }

    [Fact]
    public void Una_tecnica_non_scritta_lo_dice()
    {
        Assert.Contains("TorreRadar_TecnicaVuota", Leggi(new AirportRadarTowerView(true, null), tecnica: true).Markup);
    }

    // ---- La sotto-sezione nasce chiusa ---------------------------------------------------------------

    private static SectionView Figlia(string chiave) => new()
    {
        Id = "s-2", Title = chiave, Depth = 1, SectionKey = chiave,
        Blocks = Array.Empty<BlockView>(), Children = Array.Empty<SectionView>(),
    };

    private IRenderedComponent<SectionNode> Nodo(string chiave) =>
        RenderComponent<SectionNode>(p => p
            .Add(x => x.Section, Figlia(chiave))
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

    // ---- L'editor ------------------------------------------------------------------------------------

    private IRenderedComponent<AirportRadarTowerEditor> Editor(RadarTowerEdit? riga, bool tecnica,
        Action<RadarTowerEdit?>? cambiata = null, bool conferma = true) =>
        RenderComponent<AirportRadarTowerEditor>(p => p
            .Add(x => x.Row, riga)
            .Add(x => x.Editing, true)
            .Add(x => x.Tecnica, tecnica)
            .Add(x => x.RowChanged, EventCallback.Factory.Create<RadarTowerEdit?>(this, r => cambiata?.Invoke(r)))
            .Add(x => x.ConfirmRemove, () => Task.FromResult(conferma)));

    /// <summary>Tre stati e non una casella: «non indicato» è una risposta diversa da «no».</summary>
    [Fact]
    public void Il_si_no_ha_tre_scelte_e_parte_da_non_indicato()
    {
        var cut = Editor(null, tecnica: false);

        var radio = cut.FindAll("input[type=radio]");
        Assert.Equal(3, radio.Count);
        Assert.True(radio.ElementAt(2).HasAttribute("checked"));
    }

    [Fact]
    public void Scegliere_si_fa_nascere_la_riga()
    {
        RadarTowerEdit? arrivata = null;
        var cut = Editor(null, tecnica: false, r => arrivata = r);

        cut.FindAll("input[type=radio]").ElementAt(0).Change(true);

        Assert.NotNull(arrivata);
        Assert.True(arrivata!.Equipped);
    }

    /// <summary>Chi passa da «sì» a «no» non perde il testo: la stessa riga, col solo sì/no cambiato.</summary>
    [Fact]
    public void Passare_a_no_tiene_la_tecnica()
    {
        var riga = new RadarTowerEdit { Equipped = true, Technique = "testo" };
        RadarTowerEdit? arrivata = null;
        var cut = Editor(riga, tecnica: false, r => arrivata = r);

        cut.FindAll("input[type=radio]").ElementAt(1).Change(true);

        Assert.Same(riga, arrivata);
        Assert.False(arrivata!.Equipped);
        Assert.Equal("testo", arrivata.Technique);
    }

    /// <summary>«Non indicato» porta via la tecnica: con un testo scritto si chiede, e un no lascia tutto com'era.</summary>
    [Fact]
    public void Tornare_a_non_indicato_con_un_testo_chiede_e_un_no_non_cambia_niente()
    {
        var chiamata = false;
        var cut = Editor(new RadarTowerEdit { Equipped = true, Technique = "testo" }, tecnica: false,
                         _ => chiamata = true, conferma: false);

        cut.FindAll("input[type=radio]").ElementAt(2).Change(true);

        Assert.False(chiamata);
        Assert.True(cut.FindAll("input[type=radio]").ElementAt(0).HasAttribute("checked"));
    }

    [Fact]
    public void Tornare_a_non_indicato_confermato_toglie_la_riga()
    {
        var chiamata = false;
        RadarTowerEdit? arrivata = new();
        var cut = Editor(new RadarTowerEdit { Equipped = true, Technique = "testo" }, tecnica: false,
                         r => { chiamata = true; arrivata = r; });

        cut.FindAll("input[type=radio]").ElementAt(2).Change(true);

        Assert.True(chiamata);
        Assert.Null(arrivata);
    }

    /// <summary>La tecnica di una torre senza radar non va nel documento: niente campo, e la frase dice perché.</summary>
    [Fact]
    public void Senza_radar_la_tecnica_non_ha_un_campo()
    {
        var cut = Editor(new RadarTowerEdit { Equipped = false, Technique = "testo" }, tecnica: true);

        Assert.Empty(cut.FindAll("textarea"));
        Assert.Contains("TorreRadar_TecnicaSoloConRadar", cut.Markup);
    }

    /// <summary>Il campo ha il tetto del servizio, come la nota delle LVP: evita di scrivere per niente.</summary>
    [Fact]
    public void Con_la_torre_radar_la_tecnica_si_scrive_col_tetto_del_servizio()
    {
        var cut = Editor(new RadarTowerEdit { Equipped = true }, tecnica: true);

        Assert.Equal(Airport.TecnicaRadarMassima.ToString(), cut.Find("textarea").GetAttribute("maxlength"));
    }
}
